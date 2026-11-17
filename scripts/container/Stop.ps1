param(
    [string] $ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.env')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ContainerOperations.ps1')

try {
    if (-not (Test-Path -LiteralPath $ConfigurationPath -PathType Leaf)) {
        throw 'Arquivo de configuração não encontrado.'
    }
    foreach ($line in Get-Content -LiteralPath $ConfigurationPath) {
        if ($line -match '^\s*(?:#|$)') { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') {
            throw 'O arquivo de configuração contém uma linha inválida.'
        }
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
    Invoke-ContainerExclusive {
        $compose = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'compose.yaml'
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose stop
        if ($LASTEXITCODE -ne 0) {
            throw 'Não foi possível parar os serviços.'
        }
        Write-Output 'Serviços parados.'
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
