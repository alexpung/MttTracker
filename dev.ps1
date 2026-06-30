#requires -Version 5.1
<#
.SYNOPSIS
    Start (or restart) the three local dev servers for MttTracker.

.DESCRIPTION
    Launches each piece in its own console window so you can read its logs:
        1. API    - Azure Functions host      (http://localhost:7071)
        2. Client - Blazor WASM dev server     (http://localhost:5080)
        3. SWA    - Static Web Apps front door  (http://localhost:4280)  <-- browse here

    Re-running the script frees those three ports first, so it doubles as a
    restart - handy after rebuilding the API.

    Prerequisites (see README): Azure Functions Core Tools (func), .NET SDK,
    the SWA CLI (swa), and a running Cosmos emulator / account.

.PARAMETER StopOnly
    Just stop the running servers; don't start new ones.
#>
[CmdletBinding()]
param(
    [switch]$StopOnly
)

$ErrorActionPreference = 'Stop'

$root      = $PSScriptRoot
$apiDir    = Join-Path $root 'Api'
$clientDir = Join-Path $root 'Client'

# Port -> friendly name, in the order we start them.
$servers = [ordered]@{
    'API'    = 7071
    'Client' = 5080
    'SWA'    = 4280
}

function Stop-Port {
    param([int]$Port, [string]$Name)

    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    $procIds = @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
    foreach ($procId in $procIds) {
        if (-not $procId -or $procId -eq 0) { continue }
        try {
            $p = Get-Process -Id $procId -ErrorAction Stop
            Write-Host ("  stopping {0} on port {1} (pid {2}, {3})" -f $Name, $Port, $procId, $p.ProcessName) -ForegroundColor Yellow
            Stop-Process -Id $procId -Force
        } catch {
            # Already gone - nothing to do.
        }
    }
}

Write-Host 'Freeing dev ports...' -ForegroundColor Cyan
foreach ($name in $servers.Keys) {
    Stop-Port -Port $servers[$name] -Name $name
}

# The Functions host can linger as a child process even after its port frees.
Get-Process func -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if ($StopOnly) {
    Write-Host 'Stopped.' -ForegroundColor Green
    return
}

Start-Sleep -Milliseconds 500

# Use PowerShell 7 (pwsh) for the child windows if available, else Windows PowerShell.
$shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }

function Start-Server {
    param([string]$Title, [string]$WorkDir, [string]$Command)

    $inner = "`$host.UI.RawUI.WindowTitle = '$Title'; Set-Location '$WorkDir'; $Command"
    Start-Process $shell -ArgumentList '-NoExit', '-Command', $inner | Out-Null
    Write-Host ("  started {0}" -f $Title) -ForegroundColor Green
}

Write-Host 'Starting servers in separate windows...' -ForegroundColor Cyan

# API first, then client, then point the SWA front door at both.
Start-Server -Title 'MttTracker API'    -WorkDir $apiDir    -Command 'func start'
Start-Sleep -Seconds 2
Start-Server -Title 'MttTracker Client' -WorkDir $clientDir -Command 'dotnet run'
Start-Sleep -Seconds 2
Start-Server -Title 'MttTracker SWA'    -WorkDir $root      -Command 'swa start --app-devserver-url http://localhost:5080 --api-devserver-url http://localhost:7071'

Write-Host ''
Write-Host 'All three launching. Give them ~10-20s, then browse to:' -ForegroundColor Cyan
Write-Host '  http://localhost:4280' -ForegroundColor White
Write-Host '(Use the SWA front door at 4280 - not 5080.)' -ForegroundColor DarkGray
