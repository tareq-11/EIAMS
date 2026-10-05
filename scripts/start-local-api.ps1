<#
.SYNOPSIS
    Starts the local EIAMS API against the WSL-hosted PostgreSQL instance.

.DESCRIPTION
    The development database runs inside WSL2 because PostgreSQL 18 on Windows
    cannot start on this host (child processes fail with STATUS_DLL_INIT_FAILED
    and Windows only supports the "windows" shared memory type). WSL does not
    boot with Windows and suspends when idle, so each run repairs the
    environment before the API starts:

      1. Boots the WSL Ubuntu distribution if it is stopped.
      2. Starts the PostgreSQL 18 cluster if it is offline.
      3. Restarts the keepalive loop so the VM does not suspend mid-session.
      4. Confirms the database answers on 127.0.0.1:5432 before starting, so a
         connectivity problem is reported here instead of as a startup crash.
      5. Writes the connection string into the user secret store and runs the
         API with the "http" launch profile.

    The connection string uses 127.0.0.1 because %USERPROFILE%\.wslconfig sets
    networkingMode=mirrored, which shares the Windows network stack with WSL.
    That address is permanent; the NAT-mode WSL address is reassigned on every
    restart and silently breaks a saved connection string.

    No Docker is used. Nothing is written to appsettings.json; the connection
    string and JWT key stay in the per-user secret store.

    One-time setup of the WSL-side helpers is handled by
    scripts/install-wsl-postgres.ps1.

.PARAMETER Profile
    Launch profile to run. Defaults to "http".

.PARAMETER NoRebuild
    Skip the build step and run the existing binaries.

.EXAMPLE
    .\scripts\start-local-api.ps1
    .\scripts\start-local-api.ps1 -Profile https
#>
[CmdletBinding()]
param(
    [string]$Profile = 'http',
    [switch]$NoRebuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Web.Api\Web.Api.csproj'
$distro = 'Ubuntu'
$dbName = 'eiams'
$dbHost = '127.0.0.1'
$dbUser = 'postgres'
$dbPassword = 'postgres'

function Invoke-Wsl {
    param(
        [string[]]$Arguments,
        # Allow a non-zero exit when the command reports "nothing to do", such
        # as pkill finding no matching keepalive process on the first run.
        [switch]$AllowFailure
    )

    # Trim trailing newlines: wsl.exe emits them on every call and they would
    # otherwise corrupt captured values.
    $output = & wsl.exe -d $distro -u root -- @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $text = $output.Trim()

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "WSL command failed (exit $exitCode): $($Arguments -join ' ')`n$text"
    }

    return $text
}

Write-Host '==> Booting WSL' -ForegroundColor Cyan
# A no-op command boots the VM. The first boot after a reboot is slow, so this
# step is given a generous timeout by running before any probing.
Invoke-Wsl -Arguments @('true') | Out-Null

if (-not (Test-Path '\\wsl$\Ubuntu\usr\local\bin\pg-status')) {
    Write-Warning 'The WSL-side helpers are missing. Run scripts\install-wsl-postgres.ps1 once to install them.'
}

$status = Invoke-Wsl -Arguments @('/usr/local/bin/pg-status')
if ($status -ne 'online') {
    Write-Host '==> Starting PostgreSQL cluster' -ForegroundColor Cyan
    Invoke-Wsl -Arguments @('pg_ctlcluster', '18', 'main', 'start') | Out-Null
    Start-Sleep -Seconds 3
    $status = Invoke-Wsl -Arguments @('/usr/local/bin/pg-status')
}

if ($status -ne 'online') {
    throw 'PostgreSQL did not come up. Check /var/log/postgresql inside WSL.'
}

Write-Host '    PostgreSQL is accepting connections' -ForegroundColor Green

Write-Host '==> Refreshing WSL keepalive' -ForegroundColor Cyan
Invoke-Wsl -Arguments @('pkill', '-f', 'pg-keepalive') -AllowFailure | Out-Null
Invoke-Wsl -Arguments @(
    'bash', '-c',
    "nohup /usr/local/bin/pg-keepalive >/tmp/pg-keepalive.log 2>&1 </dev/null & disown"
) | Out-Null

$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$connectionString = "Host=$dbHost;Port=5432;Database=$dbName;Username=$dbUser;Password=$dbPassword;Include Error Detail=true;Timeout=15"
Write-Host '==> Verifying the database is reachable over TCP' -ForegroundColor Cyan

$reachable = $false
foreach ($attempt in 1..10) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect($dbHost, 5432, $null, $null)
        if ($connect.AsyncWaitHandle.WaitOne(3000) -and $client.Connected) {
            $client.EndConnect($connect)
            $reachable = $true
            break
        }
    } catch {
        # Retry below; the cluster may still be opening connections.
    } finally {
        $client.Close()
    }
    Start-Sleep -Seconds 2
}

if (-not $reachable) {
    throw "Cannot reach PostgreSQL at ${dbHost}:5432. Confirm WSL is running and networkingMode=mirrored is set in %USERPROFILE%\.wslconfig."
}

Write-Host "    ${dbHost}:5432 is accepting connections" -ForegroundColor Green

Write-Host '==> Writing connection string to the user secret store' -ForegroundColor Cyan
& $dotnet user-secrets --project $project set 'ConnectionStrings:Database' $connectionString | Out-Null

Write-Host "==> Running API (profile: $Profile)" -ForegroundColor Cyan
$runArgs = @('run', '--project', $project, '--launch-profile', $Profile)
if ($NoRebuild) {
    $runArgs += '--no-build'
}

& $dotnet @runArgs
