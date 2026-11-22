$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$config = Join-Path $root '.env'
$template = Join-Path $root '.env.example'
. (Join-Path $PSScriptRoot 'ContainerOperations.ps1')

try {
    if ($PSVersionTable.PSVersion -lt [version]'7.5') { throw 'Instale o PowerShell 7.5 ou superior.' }

    & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $desktop = @(
            (Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/Docker Desktop.exe'),
            (Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
        if (-not $desktop) { throw 'Instale e inicie o Docker Desktop com contêineres Linux.' }
        Start-Process -FilePath $desktop -WindowStyle Hidden | Out-Null
        for ($i = 0; $i -lt 60; $i++) {
            & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { break }
            Start-Sleep -Seconds 2
        }
        if ($LASTEXITCODE -ne 0) { throw 'O Docker Desktop não ficou disponível.' }
    }

    $configExists = Test-Path -LiteralPath $config -PathType Leaf
    $initialHash = if ($configExists) { (Get-FileHash -LiteralPath $config).Hash } else { $null }
    $isTemplate = $configExists -and (Test-Path -LiteralPath $template -PathType Leaf) -and
        ($initialHash -eq (Get-FileHash -LiteralPath $template).Hash)
    $isEmpty = $configExists -and @(
        Get-Content -LiteralPath $config | Where-Object {
            $_ -notmatch '^\s*(?:#.*)?$' -and
            $_ -notmatch '^\s*[A-Z][A-Z0-9_]*=\s*$' -and
            $_ -notmatch '^\s*COMPOSE_PROJECT_NAME=financemanager\s*$'
        }
    ).Count -eq 0
    if (-not $configExists -or $isTemplate -or $isEmpty) {
        $volumes = @(docker volume ls -q --filter 'label=com.docker.compose.project=financemanager')
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível verificar os volumes existentes.' }
        $containers = @(docker ps -aq --filter 'label=com.docker.compose.project=financemanager')
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível verificar a instalação existente.' }
        if ($volumes.Count -gt 0 -or $containers.Count -gt 0) {
            throw 'Há dados de uma instalação existente. Restaure o .env e os certificados originais antes de continuar.'
        }

        $directory = Join-Path $env:LOCALAPPDATA ('FinanceManager\' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
        $system = [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18')
        $directoryInfo = [System.IO.DirectoryInfo]::new($directory)
        $acl = [System.IO.FileSystemAclExtensions]::GetAccessControl($directoryInfo, [System.Security.AccessControl.AccessControlSections]::Access)
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($rule in @($acl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier]))) {
            [void] $acl.RemoveAccessRuleSpecific($rule)
        }
        foreach ($principal in @($identity, $system)) {
            $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($principal, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
            $acl.AddAccessRule($rule)
        }
        [System.IO.FileSystemAclExtensions]::SetAccessControl($directoryInfo, $acl)

        function New-InstallationPassword { 'Aa1!' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
        $adminPassword = New-InstallationPassword
        $migrationPassword = New-InstallationPassword
        $appPassword = New-InstallationPassword
        $certificatePassword = New-InstallationPassword
        $securePassword = ConvertTo-SecureString $certificatePassword -AsPlainText -Force
        $certificatePath = Join-Path $directory 'localhost.pfx'
        $certificate = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec KeyExchange
        Export-PfxCertificate -Cert $certificate -FilePath $certificatePath -Password $securePassword | Out-Null
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
        try { $store.Open('ReadWrite'); $store.Add($certificate) } finally { $store.Close() }
        $backupDirectory = Join-Path $directory 'backups'
        New-Item -ItemType Directory -Path $backupDirectory | Out-Null
        $content = @(
            'COMPOSE_PROJECT_NAME=financemanager'
            'APP_IMAGE=ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d'
            'SQL_IMAGE=mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9'
            'SQL_ADMIN_USER=sa'
            "SQL_ADMIN_PASSWORD=$adminPassword"
            'SQL_MIGRATION_USER=fm_migration'
            "SQL_MIGRATION_PASSWORD=$migrationPassword"
            'SQL_APP_USER=fm_app'
            "SQL_APP_PASSWORD=$appPassword"
            "HTTPS_CERTIFICATE_PATH=$certificatePath"
            "HTTPS_CERTIFICATE_PASSWORD=$certificatePassword"
            "DATA_PROTECTION_CERTIFICATE_PATH=$certificatePath"
            "DATA_PROTECTION_CERTIFICATE_PASSWORD=$certificatePassword"
            "BACKUP_DIRECTORY=$backupDirectory"
            "SQL_MIGRATION_CONNECTION_STRING=Server=sql;Database=FinanceManager;User ID=fm_migration;Password=$migrationPassword;Encrypt=True;TrustServerCertificate=True"
            "SQL_APP_CONNECTION_STRING=Server=sql;Database=FinanceManager;User ID=fm_app;Password=$appPassword;Encrypt=True;TrustServerCertificate=True"
        )
        $temporaryConfig = Join-Path $root ('.env.' + [guid]::NewGuid().ToString('N') + '.tmp')
        try {
            $file = [IO.File]::Open($temporaryConfig, [IO.FileMode]::CreateNew)
            $file.Dispose()
            Protect-ContainerBackupFile -Path $temporaryConfig
            [IO.File]::WriteAllLines($temporaryConfig, $content)
            if ($configExists) {
                if ((Get-FileHash -LiteralPath $config).Hash -ne $initialHash) { throw 'A configuração foi alterada durante a preparação.' }
                [IO.File]::Move($temporaryConfig, $config, $true)
            } else {
                [IO.File]::Move($temporaryConfig, $config)
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporaryConfig) { Remove-Item -LiteralPath $temporaryConfig -Force }
        }
        Protect-ContainerBackupFile -Path $config
        Write-Output 'Configuração inicial criada. A imagem privada exige acesso ao GHCR e autenticação no Docker.'
    }

    Import-ContainerConfiguration -Path $config
    if ($env:COMPOSE_PROJECT_NAME -notmatch '^[a-z0-9][a-z0-9_-]*$') { throw 'Configure um nome válido para COMPOSE_PROJECT_NAME.' }
    $compose = Join-Path $root 'compose.yaml'
    $migration = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet migrate
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível verificar o estado da instalação.' }
    if ([string]::IsNullOrWhiteSpace(($migration -join ''))) {
        & (Join-Path $PSScriptRoot 'Install.ps1') -ConfigurationPath $config
        if ($LASTEXITCODE -ne 0) { exit 1 }
    }
    & (Join-Path $PSScriptRoot 'Start.ps1') -ConfigurationPath $config
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
