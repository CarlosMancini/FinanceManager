param(
    [string] $ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.env')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ContainerOperations.ps1')

try {
    if ($PSVersionTable.PSVersion -lt [version]'7.5') {
        throw 'Instale o PowerShell 7.5 ou superior.'
    }
    Import-ContainerConfiguration -Path $ConfigurationPath
    Invoke-ContainerExclusive {
        $compose = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'compose.yaml'
        & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $desktopPaths = @(
                (Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/Docker Desktop.exe'),
                (Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe')
            )
            $desktop = $desktopPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
            if (-not $desktop) {
                throw 'Instale e inicie o Docker Desktop.'
            }
            Start-Process -FilePath $desktop -WindowStyle Hidden | Out-Null
            $ready = $false
            for ($attempt = 0; $attempt -lt 60; $attempt++) {
                & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    $ready = $true
                    break
                }
                Start-Sleep -Seconds 2
            }
            if (-not $ready) {
                throw 'O Docker Desktop não ficou disponível. Abra-o e confira sua configuração.'
            }
        }

        Assert-ContainerEnvironment
        $migrationId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet migrate
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($migrationId)) {
            throw 'Não foi encontrada uma migração concluída. Execute scripts/container/Install.ps1 uma vez.'
        }
        $migrationState = & docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' $migrationId
        if ($LASTEXITCODE -ne 0 -or $migrationState -ne 'exited 0') {
            throw 'A última migração não foi concluída. Verifique a instalação antes de iniciar.'
        }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose start sql
        if ($LASTEXITCODE -ne 0) {
            throw 'A instalação ainda não está pronta. Execute scripts/container/Install.ps1 uma vez.'
        }
        $sqlId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet sql
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sqlId)) {
            throw 'Não foi possível localizar o banco da instalação.'
        }
        $ready = $false
        for ($attempt = 0; $attempt -lt 90; $attempt++) {
            $health = & docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{end}}' $sqlId
            if ($LASTEXITCODE -ne 0 -or $health -eq 'unhealthy') {
                throw 'O banco de dados não ficou disponível.'
            }
            if ($health -eq 'healthy') {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) {
            throw 'O banco de dados não respondeu a tempo.'
        }
        $appId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet app
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appId)) {
            throw 'A aplicação não foi encontrada. Execute scripts/container/Install.ps1 uma vez.'
        }
        $installedImage = & docker inspect --format '{{.Image}}' $appId
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar a versão instalada.' }
        $configuredImage = & docker image inspect --format '{{.Id}}' $env:APP_IMAGE 2>$null
        if ($LASTEXITCODE -ne 0 -or $installedImage -ne $configuredImage) {
            throw 'A versão configurada não corresponde à instalada. Execute a atualização antes de iniciar.'
        }
        & docker start $appId | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Não foi possível iniciar a aplicação.'
        }
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            $state = & docker inspect --format '{{.State.Running}}' $appId
            if ($LASTEXITCODE -ne 0 -or $state -ne 'true') {
                throw 'A aplicação encerrou durante a inicialização.'
            }
            $client = [System.Net.Sockets.TcpClient]::new()
            try {
                $connection = $client.ConnectAsync('127.0.0.1', 7043)
                if ($connection.Wait(1000) -and $client.Connected) {
                    $ready = $true
                    break
                }
            }
            catch {
                # A aplicação ainda está iniciando.
            }
            finally {
                $client.Dispose()
            }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) {
            throw 'A aplicação não respondeu a tempo.'
        }
        Start-Process 'https://localhost:7043'
        Write-Output 'FinanceManager iniciado em https://localhost:7043.'
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
