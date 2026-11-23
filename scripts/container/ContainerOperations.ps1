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
    param([switch] $LocalAppImage)
    foreach ($name in @('APP_IMAGE', 'SQL_IMAGE')) {
        $reference = [Environment]::GetEnvironmentVariable($name)
        if ([string]::IsNullOrWhiteSpace($reference) -or
            $reference -notmatch '(@sha256:[0-9a-fA-F]{64}|:[^/:]+)$' -or
            $reference -match ':latest$') {
            throw "Configure uma imagem identificada para $name."
        }
        if ($name -eq 'APP_IMAGE' -and $LocalAppImage) {
            & docker image inspect $reference *> $null
            if ($LASTEXITCODE -ne 0) { throw 'A imagem local da aplicação não foi encontrada.' }
            continue
        }
        & docker pull $reference
        if ($LASTEXITCODE -ne 0) {
            throw "Não foi possível obter a imagem configurada em $name."
        }
    }
}

function Import-ContainerConfiguration {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'Arquivo de configuração não encontrado.'
    }
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*(?:#|$)') { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') {
            throw 'O arquivo de configuração contém uma linha inválida.'
        }
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}

function Invoke-ContainerSql {
    param(
        [Parameter(Mandatory)][string] $ComposePath,
        [Parameter(Mandatory)][string] $Query
    )

    $env:SQLCMDPASSWORD = $env:SQL_ADMIN_PASSWORD
    try {
        $Query | & docker compose -p $env:COMPOSE_PROJECT_NAME -f $ComposePath exec -T -e SQLCMDPASSWORD sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'A operação no SQL Server falhou.'
        }
    }
    finally {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
}

function Protect-ContainerBackupFile {
    param([Parameter(Mandatory)][string] $Path)

    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $system = [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $file = [System.IO.FileInfo]::new($Path)
    $acl = [System.IO.FileSystemAclExtensions]::GetAccessControl($file, [System.Security.AccessControl.AccessControlSections]::Access)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($rule in @($acl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier]))) {
        [void] $acl.RemoveAccessRuleSpecific($rule)
    }
    foreach ($principal in @($identity, $system)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $principal, 'FullControl', 'Allow')
        $acl.AddAccessRule($rule)
    }
    [System.IO.FileSystemAclExtensions]::SetAccessControl($file, $acl)
    $actual = [System.IO.FileSystemAclExtensions]::GetAccessControl($file, [System.Security.AccessControl.AccessControlSections]::Access)
    $rules = $actual.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier])
    if (-not $actual.AreAccessRulesProtected -or
        @($rules | Where-Object { $_.IdentityReference -ne $identity -and $_.IdentityReference -ne $system }).Count -gt 0) {
        throw 'Não foi possível restringir o acesso ao backup.'
    }
}
