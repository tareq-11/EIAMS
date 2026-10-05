<#
.SYNOPSIS
    Starts the local EIAMS API against the Docker-hosted PostgreSQL instance.

.DESCRIPTION
    This is the Docker-backed equivalent of start-local-api.ps1. The repo's
    default start script expects WSL2 (because PostgreSQL on Windows cannot
    fork child processes for non-admin users: child processes terminate with
    STATUS_DLL_INIT_FAILED because Windows only supports the "windows" shared
    memory type). When WSL2 is unavailable on the host but Docker Desktop is,
    this script spins up a single-user PostgreSQL 17 container on 127.0.0.1:5432
    and then runs the API with the "http" launch profile.

    The flow:
      1. Confirms Docker Desktop is responsive and pulls postgres:17 if missing.
      2. Starts (or recreates) the eiams-local-pg container with a persistent
         named volume and the same credentials the API uses in development.
      3. Waits until 127.0.0.1:5432 answers before starting the API.
      4. Writes the connection string into the user secret store and runs the
         API with the "http" launch profile.

    No Docker is used for the API itself; the API runs locally on the user's
    machine the same way start-local-api.ps1 runs it against WSL. Nothing is
    written to appsettings.json; the connection string and JWT key stay in
    the per-user secret store.

.PARAMETER ContainerName
    Name of the local PostgreSQL container. Defaults to eiams-local-pg.

.PARAMETER DbPassword
    Password for the postgres superuser inside the container. Defaults to
    postgres, matching the value baked into .env.example.

.PARAMETER NoRebuild
    Skip the dotnet build step and run the existing binaries.

.EXAMPLE
    .\scripts\start-local-api-docker.ps1
#>
[CmdletBinding()]
param(
    [string]$ContainerName = 'eiams-local-pg',
    [string]$DbName = 'eiams',
    [string]$DbUser = 'postgres',
    [string]$DbPassword = 'postgres',
    [switch]$NoRebuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Web.Api\Web.Api.csproj'
$image = 'postgres:17'
$dbHost = '127.0.0.1'
$dbPort = 5432

function Invoke-Docker {
    param(
        [string[]]$Arguments
    )
    $output = & docker.exe @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "docker $($Arguments -join ' ') failed (exit $exitCode):`n$output"
    }
    return $output.Trim()
}

Write-Host '==> Verifying Docker is reachable' -ForegroundColor Cyan
$null = Invoke-Docker -Arguments @('version')

Write-Host "==> Ensuring image '$image' is present" -ForegroundColor Cyan
$pulled = docker images --format '{{.Repository}}:{{.Tag}}' | Select-String -SimpleMatch $image -CaseSensitive:$false
if (-not $pulled) {
    Write-Host "    Pulling $image (one-time)" -ForegroundColor Cyan
    $null = Invoke-Docker -Arguments @('pull', $image)
} else {
    Write-Host "    $image already present locally" -ForegroundColor Green
}

Write-Host "==> Starting container '$ContainerName'" -ForegroundColor Cyan
$existing = docker ps -a --format '{{.Names}}' | Select-String -SimpleMatch $ContainerName
if ($existing) {
    $running = docker ps --format '{{.Names}}' | Select-String -SimpleMatch $ContainerName
    if (-not $running) {
        Write-Host "    Container exists; starting it" -ForegroundColor Cyan
        $null = Invoke-Docker -Arguments @('start', $ContainerName)
    } else {
        Write-Host "    Container already running" -ForegroundColor Green
    }
} else {
    $null = Invoke-Docker -Arguments @(
        'run', '-d',
        '--name', $ContainerName,
        '-p', "${dbHost}:${dbPort}:5432",
        '-e', "POSTGRES_USER=$DbUser",
        '-e', "POSTGRES_PASSWORD=$DbPassword",
        '-e', "POSTGRES_DB=$DbName",
        '-v', "${ContainerName}-data:/var/lib/postgresql/data",
        $image
    )
}

Write-Host "==> Waiting for PostgreSQL on ${dbHost}:${dbPort}" -ForegroundColor Cyan
$reachable = $false
foreach ($attempt in 1..30) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect($dbHost, $dbPort, $null, $null)
        if ($connect.AsyncWaitHandle.WaitOne(2000) -and $client.Connected) {
            $client.EndConnect($connect)
            $reachable = $true
            break
        }
    } catch {
        # Cluster may still be opening the listener; retry.
    } finally {
        $client.Close()
    }
    Write-Host "    attempt $attempt/30, retrying..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 2
}

if (-not $reachable) {
    $logs = docker logs $ContainerName --tail 30 2>&1 | Out-String
    throw "Cannot reach PostgreSQL at ${dbHost}:${dbPort}. Last container logs:`n$logs"
}

Write-Host "    PostgreSQL is accepting connections" -ForegroundColor Green

$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$connectionString = "Host=$dbHost;Port=$dbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword;Include Error Detail=true;Timeout=15"
Write-Host '==> Writing connection string to the user secret store' -ForegroundColor Cyan
& $dotnet user-secrets --project $project set 'ConnectionStrings:Database' $connectionString | Out-Null

Write-Host '==> Running API (profile: http)' -ForegroundColor Cyan
$runArgs = @('run', '--project', $project, '--launch-profile', 'http')
if ($NoRebuild) {
    $runArgs += '--no-build'
}

& $dotnet @runArgs