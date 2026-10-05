<#
.SYNOPSIS
    Installs PostgreSQL inside the WSL Ubuntu distribution for local development.

.DESCRIPTION
    One-time setup. Run this once, then use scripts\start-local-api.ps1 to
    start the API.

    Windows-native PostgreSQL 18 cannot run on this host: every child process
    fails with STATUS_DLL_INIT_FAILED, and Windows accepts only the "windows"
    shared memory type, so there is no configuration workaround. WSL2 is used
    instead.

    The script is idempotent: re-running it leaves an existing cluster alone.
#>
[CmdletBinding()]
param(
    [string]$Distro = 'Ubuntu',
    [string]$Database = 'eiams',
    [string]$DbUser = 'postgres',
    [string]$DbPassword = 'postgres'
)

$ErrorActionPreference = 'Stop'

function Invoke-Wsl {
    param([string[]]$Arguments)
    $output = & wsl.exe -d $Distro -u root -- @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "WSL command failed (exit $exitCode): $($Arguments -join ' ')`n$output"
    }
    return $output.Trim()
}

Write-Host '==> Installing PostgreSQL 18' -ForegroundColor Cyan
Invoke-Wsl -Arguments @('DEBIAN_FRONTEND=noninteractive', 'apt-get', 'install', '-y',
    '--no-install-recommends', 'postgresql', 'postgresql-contrib') | Out-Null

# Start the cluster and make it start with the distro.
Invoke-Wsl -Arguments @('pg_ctlcluster', '18', 'main', 'start') | Out-Null
Invoke-Wsl -Arguments @('systemctl', 'enable', 'postgresql') 2>$null | Out-Null

Write-Host '==> Configuring authentication' -ForegroundColor Cyan
# Rewrite pg_hba.conf so password authentication is accepted from the Windows
# host. The default rules only cover the WSL loopback address.
$hba = @"
local   all             all                                     peer
host    all             all             127.0.0.1/32            scram-sha-256
host    all             all             ::1/128                 scram-sha-256
host    all             all             172.16.0.0/12           scram-sha-256
host    all             all             192.168.0.0/16          scram-sha-256
local   replication     all                                     peer
host    replication     all             127.0.0.1/32            scram-sha-256
host    replication     all             ::1/128                 scram-sha-256
"@

$hbaPath = '\\wsl$\Ubuntu\etc\postgresql\18\main\pg_hba.conf'
Set-Content -LiteralPath $hbaPath -Value $hba -Encoding utf8NoBOM
Invoke-Wsl -Arguments @('chown', 'postgres:postgres', '/etc/postgresql/18/main/pg_hba.conf') | Out-Null

Write-Host '==> Binding to all interfaces' -ForegroundColor Cyan
# WSL2 exposes the VM on a dedicated NAT address, so PostgreSQL must listen on
# more than 127.0.0.1 for Windows to reach it.
$conf = '\\wsl$\Ubuntu\etc\postgresql\18\main\postgresql.conf'
$content = Get-Content -LiteralPath $conf
$content = $content -replace '^#?listen_addresses = .*$', "listen_addresses = '*'"
if (-not ($content -match '^listen_addresses')) {
    $content += "`nlisten_addresses = '*'"
}
Set-Content -LiteralPath $conf -Value $content -Encoding utf8NoBOM
Invoke-Wsl -Arguments @('chown', 'postgres:postgres', '/etc/postgresql/18/main/postgresql.conf') | Out-Null

Write-Host "==> Creating role and database" -ForegroundColor Cyan
Invoke-Wsl -Arguments @('su', 'postgres', '-c',
    "psql -v ON_ERROR_STOP=1 -c `"ALTER USER $DbUser PASSWORD '$DbPassword';`"") | Out-Null

$exists = Invoke-Wsl -Arguments @('su', 'postgres', '-c',
    "psql -tAc `"SELECT 1 FROM pg_database WHERE datname='$Database'`"")
if ($exists -ne '1') {
    Invoke-Wsl -Arguments @('su', 'postgres', '-c', "createdb $Database") | Out-Null
}

Invoke-Wsl -Arguments @('pg_ctlcluster', '18', 'main', 'restart') | Out-Null
Start-Sleep -Seconds 3

Write-Host '==> Installing helper scripts' -ForegroundColor Cyan
# Helpers live in /usr/local/bin so the launcher can call them without
# inline shell quoting, which does not survive the wsl.exe argument boundary.
$helpers = Join-Path $PSScriptRoot 'wsl'
$helperSource = '\\wsl$\Ubuntu\tmp\eiams-helpers'
New-Item -ItemType Directory -Path $helperSource -Force | Out-Null
Copy-Item -Path (Join-Path $helpers '*') -Destination $helperSource -Force

Invoke-Wsl -Arguments @('cp', '/tmp/eiams-helpers/pg-status.sh', '/usr/local/bin/pg-status') | Out-Null
Invoke-Wsl -Arguments @('cp', '/tmp/eiams-helpers/pg-keepalive.sh', '/usr/local/bin/pg-keepalive') | Out-Null
Invoke-Wsl -Arguments @('chmod', '+x', '/usr/local/bin/pg-status', '/usr/local/bin/pg-keepalive') | Out-Null
Invoke-Wsl -Arguments @('rm', '-rf', '/tmp/eiams-helpers') | Out-Null

$status = Invoke-Wsl -Arguments @('/usr/local/bin/pg-status')
Write-Host "`nPostgreSQL is '$status'." -ForegroundColor Green
Write-Host "Next: .\scripts\start-local-api.ps1" -ForegroundColor Green
