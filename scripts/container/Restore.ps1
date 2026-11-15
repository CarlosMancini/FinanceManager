param(
    [Parameter(Mandatory)][string] $BackupPath,
    [string] $ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.env')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ContainerOperations.ps1')

try {
    Import-ContainerConfiguration -Path $ConfigurationPath
    Assert-ContainerEnvironment
    if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath "$BackupPath.json" -PathType Leaf)) {
        throw 'O backup e seus metadados são necessários.'
    }
    $backupPath = (Resolve-Path -LiteralPath $BackupPath).Path
    $metadata = Get-Content -LiteralPath "$backupPath.json" -Raw | ConvertFrom-Json
    if ($metadata.database -ne 'FinanceManager' -or $metadata.imageId -notmatch '^sha256:[a-f0-9]{64}$' -or
        $metadata.sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -ne $metadata.sha256) {
        throw 'O backup ou seus metadados são inválidos.'
    }

    Invoke-ContainerExclusive {
        $compose = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'compose.yaml'
        $imageId = & docker image inspect --format '{{.Id}}' $env:APP_IMAGE 2>$null
        if ($LASTEXITCODE -ne 0 -or $imageId -ne $metadata.imageId) {
            throw 'Configure a imagem anterior indicada nos metadados do backup antes de restaurar.'
        }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --wait --wait-timeout 180 sql
        if ($LASTEXITCODE -ne 0) { throw 'O SQL Server não ficou disponível.' }

        $name = [Guid]::NewGuid().ToString('N') + '.bak'
        $containerPath = "/var/opt/mssql/backup/$name"
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose exec -T sql mkdir -p /var/opt/mssql/backup
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível preparar a restauração.' }
        try {
            & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose cp $backupPath "sql:$containerPath"
            if ($LASTEXITCODE -ne 0) { throw 'Não foi possível copiar o backup.' }
            Invoke-ContainerSql -ComposePath $compose -Query "RESTORE VERIFYONLY FROM DISK = N'$containerPath' WITH CHECKSUM;"
            & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose stop app
            if ($LASTEXITCODE -ne 0) { throw 'Não foi possível parar a aplicação.' }
            try {
                Invoke-ContainerSql -ComposePath $compose -Query 'ALTER DATABASE [FinanceManager] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;'
                Invoke-ContainerSql -ComposePath $compose -Query "RESTORE DATABASE [FinanceManager] FROM DISK = N'$containerPath' WITH REPLACE, RECOVERY;"
            }
            finally {
                Invoke-ContainerSql -ComposePath $compose -Query 'IF DB_ID(N''FinanceManager'') IS NOT NULL ALTER DATABASE [FinanceManager] SET MULTI_USER;'
            }
            Write-Output 'Backup restaurado. Verifique o banco antes de iniciar a aplicação anterior.'
        }
        finally {
            & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose exec -T sql rm -f $containerPath | Out-Null
        }
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
