param(
    [string] $ConfigurationPath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.env'),
    [string] $LocalAppImage
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

    if ($LocalAppImage) { $env:APP_IMAGE = $LocalAppImage }
    Assert-ContainerEnvironment
    foreach ($name in @('SQL_MIGRATION_USER', 'SQL_MIGRATION_PASSWORD', 'SQL_APP_USER', 'SQL_APP_PASSWORD')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Configure a variável $name."
        }
    }
    foreach ($name in @('SQL_MIGRATION_USER', 'SQL_APP_USER')) {
        if ([Environment]::GetEnvironmentVariable($name) -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') {
            throw "Configure um identificador SQL válido em $name."
        }
    }
    if ($env:SQL_MIGRATION_USER -ieq $env:SQL_APP_USER -or
        $env:SQL_MIGRATION_USER -ieq 'sa' -or $env:SQL_APP_USER -ieq 'sa') {
        throw 'Configure identidades distintas para migração e aplicação.'
    }
    foreach ($name in @('HTTPS_CERTIFICATE_PATH', 'DATA_PROTECTION_CERTIFICATE_PATH')) {
        $passwordName = if ($name -eq 'HTTPS_CERTIFICATE_PATH') { 'HTTPS_CERTIFICATE_PASSWORD' } else { 'DATA_PROTECTION_CERTIFICATE_PASSWORD' }
        $certificate = $null
        try {
            $certificate = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile(
                [Environment]::GetEnvironmentVariable($name),
                [Environment]::GetEnvironmentVariable($passwordName))
            if (-not $certificate.HasPrivateKey -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) {
                throw 'Certificado inválido.'
            }
        }
        catch { throw "Confira o certificado e a senha indicados por $name." }
        finally { if ($null -ne $certificate) { $certificate.Dispose(); $certificate = $null } }
    }

    Invoke-ContainerExclusive {
        Receive-ContainerImages -LocalAppImage:([bool]$LocalAppImage)
        $compose = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'compose.yaml'
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --wait --wait-timeout 180 sql
        if ($LASTEXITCODE -ne 0) { throw 'O SQL Server não ficou disponível.' }

        $migrationUser = $env:SQL_MIGRATION_USER
        $appUser = $env:SQL_APP_USER
        $migrationPassword = $env:SQL_MIGRATION_PASSWORD.Replace("'", "''")
        $appPassword = $env:SQL_APP_PASSWORD.Replace("'", "''")
        $sql = @"
IF DB_ID(N'FinanceManager') IS NULL CREATE DATABASE [FinanceManager];
GO
IF SUSER_ID(N'$migrationUser') IS NULL CREATE LOGIN [$migrationUser] WITH PASSWORD = N'$migrationPassword';
IF SUSER_ID(N'$appUser') IS NULL CREATE LOGIN [$appUser] WITH PASSWORD = N'$appPassword';
GO
USE [FinanceManager];
IF USER_ID(N'$migrationUser') IS NULL CREATE USER [$migrationUser] FOR LOGIN [$migrationUser];
IF USER_ID(N'$appUser') IS NULL CREATE USER [$appUser] FOR LOGIN [$appUser];
IF IS_ROLEMEMBER(N'db_owner', N'$migrationUser') = 0 ALTER ROLE [db_owner] ADD MEMBER [$migrationUser];
IF IS_ROLEMEMBER(N'db_datareader', N'$appUser') = 0 ALTER ROLE [db_datareader] ADD MEMBER [$appUser];
IF IS_ROLEMEMBER(N'db_datawriter', N'$appUser') = 0 ALTER ROLE [db_datawriter] ADD MEMBER [$appUser];
"@
        $env:SQLCMDPASSWORD = $env:SQL_ADMIN_PASSWORD
        try {
            $sql | & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose exec -T -e SQLCMDPASSWORD sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b *> $null
            if ($LASTEXITCODE -ne 0) { throw 'Não foi possível provisionar o banco e as identidades SQL.' }
        }
        finally { Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue }

        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose stop app
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível parar a aplicação antes da migração.' }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --force-recreate migrate
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível iniciar a migração.' }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose wait migrate
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível verificar a migração.' }
        $migrationId = & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose ps --all --quiet migrate
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($migrationId)) {
            throw 'Não foi possível identificar a migração.'
        }
        $migrationExit = & docker inspect --format '{{.State.ExitCode}}' $migrationId
        if ($LASTEXITCODE -ne 0 -or $migrationExit.Trim() -ne '0') {
            throw 'A migração falhou. A aplicação não foi iniciada.'
        }
        & docker compose -p $env:COMPOSE_PROJECT_NAME -f $compose up -d --wait --wait-timeout 60 --no-deps app
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível iniciar a aplicação.' }
        Write-Output 'Instalação iniciada em https://localhost:7043.'
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
