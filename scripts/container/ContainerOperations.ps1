Set-StrictMode -Version Latest

function Assert-ContainerEnvironment {
    $required = @(
        'COMPOSE_PROJECT_NAME', 'APP_IMAGE', 'SQL_IMAGE', 'SQL_ADMIN_PASSWORD',
        'SQL_MIGRATION_CONNECTION_STRING', 'SQL_APP_CONNECTION_STRING',
        'HTTPS_CERTIFICATE_PATH', 'HTTPS_CERTIFICATE_PASSWORD',
        'DATA_PROTECTION_CERTIFICATE_PATH', 'DATA_PROTECTION_CERTIFICATE_PASSWORD',
        'BACKUP_DIRECTORY'
    )
    foreach ($name in $required) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Configure a variável $name."
        }
    }

    foreach ($name in @('HTTPS_CERTIFICATE_PATH', 'DATA_PROTECTION_CERTIFICATE_PATH')) {
        $path = [Environment]::GetEnvironmentVariable($name)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "O arquivo indicado por $name não foi encontrado."
        }
    }

    $server = & docker version --format '{{.Server.Version}} {{.Server.Os}}' 2>$null
    if ($LASTEXITCODE -ne 0 -or $server -notmatch '^(\d+)\.\d+\.\d+\S* linux$') {
        throw 'Inicie o Docker Desktop com contêineres Linux.'
    }
    if ([int]$Matches[1] -lt 28) {
        throw 'O Docker Engine deve estar na versão 28 ou superior.'
    }
}

function Invoke-ContainerExclusive {
    param(
        [Parameter(Mandatory)]
        [scriptblock] $Action
    )

    $project = [Environment]::GetEnvironmentVariable('COMPOSE_PROJECT_NAME')
    if ($project -notmatch '^[a-z0-9][a-z0-9_-]*$') {
        throw 'Configure um nome válido para COMPOSE_PROJECT_NAME.'
    }
    $lockName = "Local\FinanceManager-Compose-$project"
    $mutex = [System.Threading.Mutex]::new($false, $lockName)
    $acquired = $false
    try {
        try {
            $acquired = $mutex.WaitOne(0)
        }
        catch [System.Threading.AbandonedMutexException] {
            $acquired = $true
        }
        if (-not $acquired) {
            throw 'Outra operação desta instalação já está em andamento.'
        }
        & $Action
    }
    finally {
        if ($acquired) {
            $mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
}

function Receive-ContainerImages {
    foreach ($name in @('APP_IMAGE', 'SQL_IMAGE')) {
        $reference = [Environment]::GetEnvironmentVariable($name)
        if ([string]::IsNullOrWhiteSpace($reference) -or
            $reference -notmatch '(@sha256:[0-9a-fA-F]{64}|:[^/:]+)$' -or
            $reference -match ':latest$') {
            throw "Configure uma imagem identificada para $name."
        }
        & docker pull $reference
        if ($LASTEXITCODE -ne 0) {
            throw "Não foi possível obter a imagem configurada em $name."
        }
    }
}
