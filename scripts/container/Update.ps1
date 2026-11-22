param(
    [string] $ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.env'),
    [string] $LocalAppImage
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ContainerOperations.ps1')

try {
    Import-ContainerConfiguration -Path $ConfigurationPath
    if ($LocalAppImage) { $env:APP_IMAGE = $LocalAppImage }
    Assert-ContainerEnvironment
    Invoke-ContainerExclusive {
        $compose = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'compose.yaml'
        Receive-ContainerImages -LocalAppImage:([bool]$LocalAppImage)
        $appId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet app
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appId)) {
            throw 'Não foi encontrada uma instalação anterior para atualizar.'
        }
        $previousImage = & docker inspect --format '{{.Config.Image}}' $appId
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($previousImage)) {
            throw 'Não foi possível identificar a versão anterior.'
        }
        $previousImageId = & docker inspect --format '{{.Image}}' $appId
        if ($LASTEXITCODE -ne 0 -or $previousImageId -notmatch '^sha256:[a-f0-9]{64}$') {
            throw 'Não foi possível identificar a imagem anterior.'
        }
        $sqlId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet sql
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sqlId)) {
            throw 'Não foi encontrado o SQL Server da instalação anterior.'
        }
        $currentSqlImage = & docker inspect --format '{{.Image}}' $sqlId
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar a imagem atual do SQL Server.' }
        $configuredSqlImage = & docker image inspect --format '{{.Id}}' $env:SQL_IMAGE
        if ($LASTEXITCODE -ne 0 -or $currentSqlImage -ne $configuredSqlImage) {
            throw 'A imagem configurada do SQL Server difere da instalação existente.'
        }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --wait --wait-timeout 180 sql
        if ($LASTEXITCODE -ne 0) { throw 'O SQL Server não ficou disponível.' }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose stop app
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível parar a aplicação.' }

        $backupDirectory = [IO.Path]::GetFullPath($env:BACKUP_DIRECTORY)
        [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
        $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
        $suffix = [Guid]::NewGuid().ToString('N')
        $name = "financemanager-$stamp-$suffix.bak"
        $containerPath = "/var/opt/mssql/backup/$name"
        $backupPath = Join-Path $backupDirectory $name

        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose exec -T sql mkdir -p /var/opt/mssql/backup
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível preparar o backup.' }
        try {
            Invoke-ContainerSql -ComposePath $compose -Query "BACKUP DATABASE [FinanceManager] TO DISK = N'$containerPath' WITH COPY_ONLY, CHECKSUM;"
            Invoke-ContainerSql -ComposePath $compose -Query "RESTORE VERIFYONLY FROM DISK = N'$containerPath' WITH CHECKSUM;"
            & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose cp "sql:$containerPath" $backupPath
            if ($LASTEXITCODE -ne 0) { throw 'Não foi possível copiar o backup.' }
            try {
                Protect-ContainerBackupFile -Path $backupPath
            }
            catch {
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
                throw
            }
            $hash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
            $metadata = [ordered]@{
                database = 'FinanceManager'
                image = $previousImage
                imageId = $previousImageId
                sha256 = $hash
                createdUtc = [DateTime]::UtcNow.ToString('o')
            }
            $metadata | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$backupPath.json"
            Protect-ContainerBackupFile -Path "$backupPath.json"
        }
        finally {
            & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose exec -T sql rm -f $containerPath | Out-Null
        }

        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --force-recreate migrate
        if ($LASTEXITCODE -ne 0) { throw "Não foi possível iniciar a migração. Backup: $backupPath" }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose wait migrate
        if ($LASTEXITCODE -ne 0) { throw "Não foi possível verificar a migração. Backup: $backupPath" }
        $migrationId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet migrate
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($migrationId)) { throw "Migração não encontrada. Backup: $backupPath" }
        $migrationExit = & docker inspect --format '{{.State.ExitCode}}' $migrationId
        if ($LASTEXITCODE -ne 0 -or $migrationExit.Trim() -ne '0') { throw "A migração falhou. Backup: $backupPath" }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --wait --wait-timeout 60 --no-deps app
        if ($LASTEXITCODE -ne 0) { throw "Não foi possível iniciar a aplicação. Backup: $backupPath" }
        Write-Output "Atualização concluída. Backup: $backupPath"
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
