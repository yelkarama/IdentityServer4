<#
.SYNOPSIS
    Install the OSCAR local IdentityServer as a Windows service (no Docker),
    using the portable Servy CLI.

.DESCRIPTION
    Reads two zips from the dropzone, extracts Servy, publishes (or reuses) the
    self-contained IdP, and installs it as an automatic Windows service that
    listens on http://+:5555. The IdP is self-contained (win-x64), so no .NET
    runtime is needed on the target. The signing key is pinned to the app
    directory via Servy's --startupDir so it persists across restarts.

.PARAMETER Uninstall
    Remove the service instead of installing it.

.EXAMPLE
    .\Install-IdPService.ps1            # install + start + verify
.EXAMPLE
    .\Install-IdPService.ps1 -Uninstall # remove the service
#>
[CmdletBinding()]
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 only. `#Requires -Version 5.1` is NOT enough: it only
# sets a floor, so PowerShell 7 (Core, v7+) would still pass. Gate on the edition
# too, which rejects pwsh and any non-Windows PowerShell. The param block must be
# the first statement in the script, so this gate lives just below it.
if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion -lt [version]'5.1') {
    Write-Error "This script requires Windows PowerShell 5.1. It will not run under PowerShell 7 (pwsh). Open a Windows PowerShell 5.1 session and try again."
    exit 1
}

# ---- Configuration ---------------------------------------------------------
$ServiceName = 'idp'
$DisplayName = 'OSCAR IdentityServer'
$Description = 'OSCAR local OIDC identity server (runs without Docker).'
$DevtoolsRoot = 'C:\Devtools'
$AppDir       = 'C:\Devtools\idp'
$ServyDir     = Join-Path $DevtoolsRoot 'servy'
$DropZone     = '\\S10001\grpreg$\dropzone\Younes\idp'
$ServyZip    = Join-Path $DropZone 'servy-10.8-net48-x64-portable.zip'

function Write-Step { param($m) Write-Host ("`n==> {0}" -f $m) -ForegroundColor Cyan }

# ---- Admin check -----------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Re-launching elevated (accept the UAC prompt with your admin account)..." -ForegroundColor Yellow
    $relaunchArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($Uninstall) { $relaunchArgs += '-Uninstall' }
    Start-Process powershell -Verb RunAs -ArgumentList $relaunchArgs
    exit
}

# ---- Uninstall path --------------------------------------------------------
if ($Uninstall) {
    Write-Step "Removing service '$ServiceName'..."
    $servyExe = Join-Path $ServyDir 'servy-cli.exe'
    if (Test-Path $servyExe) { & $servyExe remove -n $ServiceName -q }
    else { sc.exe delete $ServiceName }
    Write-Host "Done." -ForegroundColor Green
    exit 0
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Expand-Zip($zip, $dest) {
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force $dest | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $dest)
}

# ---- Servy -----------------------------------------------------------------
if (-not (Test-Path $ServyZip)) {
    Write-Error "Servy zip not found at $ServyZip. Place servy-10.8-net48-portable.zip in $DropZone."
    exit 1
}
Write-Step "Extracting Servy..."
Expand-Zip $ServyZip $ServyDir
$servy = Get-ChildItem -Path $ServyDir -Filter 'servy-cli.exe' -Recurse | Select-Object -First 1
if (-not $servy) { Write-Error "servy-cli.exe not found inside $ServyZip."; exit 1 }
$servyExe = $servy.FullName
Write-Host "    Servy: $servyExe"

# ---- Locate the IdP zip and place it at C:\Devtools\idp --------------------
$idpZip = Get-ChildItem -Path $DropZone -Filter '*.zip' |
    Where-Object { $_.FullName -ne $ServyZip } | Select-Object -First 1
if (-not $idpZip) {
    Write-Error "No IdP zip found in $DropZone (other than the Servy zip)."
    exit 1
}
Write-Host "    IdP zip: $($idpZip.FullName)"

Write-Step "Placing the IdP at $AppDir..."
$runningSvc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($runningSvc -and $runningSvc.Status -eq 'Running') {
    Write-Host "    Stopping running service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force
}
$Holder = Join-Path $DevtoolsRoot ('idp-extract-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
Expand-Zip $idpZip.FullName $Holder          # zip is rooted at 'idp/' -> $Holder\idp\...

$keyBackup = $null
if (Test-Path $AppDir) {
    if (Test-Path "$AppDir\tempkey.jwk") {
        $keyBackup = Join-Path $Holder 'tempkey.jwk'
        Copy-Item "$AppDir\tempkey.jwk" $keyBackup
    }
    Remove-Item -Recurse -Force $AppDir
}
Move-Item "$Holder\idp" $AppDir
if ($keyBackup) { Copy-Item $keyBackup "$AppDir\tempkey.jwk" }
Remove-Item $Holder -Recurse -Force

# Publish from source, or reuse already-published output.
$csproj = Get-ChildItem -Path $AppDir -Filter 'IdentityServer.csproj' -Recurse | Select-Object -First 1
if ($csproj) {
    Write-Step "Publishing $($csproj.FullName)..."
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Error "dotnet SDK not on PATH. Publish locally and zip the output instead, or install the .NET 11 SDK."
        exit 1
    }
    dotnet publish $csproj.FullName -c Release -r win-x64 --self-contained true -o $AppDir
}
if (-not (Test-Path (Join-Path $AppDir 'IdentityServer.exe'))) {
    Write-Error "IdentityServer.exe missing in $AppDir. Zip the self-contained published output (see RUN-AS-WINDOW-SERVICE.md)."
    exit 1
}

# ---- Install service -------------------------------------------------------
Write-Step "Installing service '$ServiceName'..."
$exePath = Join-Path $AppDir 'IdentityServer.exe'
$startDir = $AppDir
$installArgs = @(
    'install', '-n', $ServiceName,
    '-p', $exePath,
    '--startupDir', "`"$startDir`"",
    '--startupType', 'Automatic',
    '--envVars', "ASPNETCORE_URLS=http://+:5555;ASPNETCORE_ENVIRONMENT=Development",
    '--displayName', $DisplayName,
    '--description', $Description,
    '-q'
)
& $servyExe @installArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Servy install failed (exit $LASTEXITCODE)."
    exit 1
}

# ---- Start + verify --------------------------------------------------------
Write-Step "Starting service '$ServiceName'..."
& $servyExe start -n $ServiceName -q
Start-Sleep -Seconds 3
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc -or $svc.Status -ne 'Running') {
    Write-Error "Service is not Running (status: $($svc.Status)). Check the Windows Services console."
    exit 1
}

$url = 'http://localhost:5555/.well-known/openid-configuration'
try {
    $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
    Write-Host "    Discovery $url -> $($r.StatusCode)" -ForegroundColor Green
} catch {
    Write-Warning "Discovery endpoint did not respond: $_"
}

Write-Host "`nService '$ServiceName' installed and running." -ForegroundColor Green
Write-Host "  App dir     : $AppDir" -ForegroundColor Gray
Write-Host "  Signing key : $AppDir\tempkey.jwk (persists across restarts)" -ForegroundColor Gray
Write-Host "  Manage      : & '$servyExe' start|stop|remove -n $ServiceName" -ForegroundColor Gray
Write-Host "  Uninstall   : .\Install-IdPService.ps1 -Uninstall" -ForegroundColor Gray

# SIG # Begin signature block
# MIIF0QYJKoZIhvcNAQcCoIIFwjCCBb4CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCCXjLE2drQUMySt
# NHO5xKk+GfZc7UbkNFB/YWl1JJ1ReqCCAzIwggMuMIICFqADAgECAhAhSLhs/t+s
# sE0ywLK2GzohMA0GCSqGSIb3DQEBCwUAMC8xLTArBgNVBAMMJFlvdW5lcyBFbCBr
# YXJhbWEgLSBPU0NBUiBJZFAgc2NyaXB0czAeFw0yNjExMjcxNDQ3NTVaFw0yNzEx
# MjcxNTA3NTVaMC8xLTArBgNVBAMMJFlvdW5lcyBFbCBrYXJhbWEgLSBPU0NBUiBJ
# ZFAgc2NyaXB0czCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALmQ/x0h
# AjUtuGrG+8CKUuDvbKHooUrk9NNUmmN5KpP5VjECVrVpxkd2m9ZuTr/RUD5MXm1d
# Y8olaGMlfSyIYLtnQciQPD5TthbK8UyBf02+rTZqfZq3/KKhLB/YasxA5OVL6qIM
# KTrzem/L2sU0gDzq0FQE5p1IxMSMB9Wd23+QcJcmNxw0JB1DumMzsRVnxNGJnjON
# 9NUeYFRhhPdH0bi/HaD4bcFmPZUYov8GeAgXxfi9dTaBCaUX2z/vegXVxFyuzTvA
# Y8aLzXz5t2FPs/qITTHeABkCgCkQl4JvA9vxPFcuTgm0kcVxtMPuspF8EfIVekAt
# bPVtBOGJ0N4a8LkCAwEAAaNGMEQwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoG
# CCsGAQUFBwMDMB0GA1UdDgQWBBQBBR2mTeoUich6XSG5cMn8pbbhVjANBgkqhkiG
# 9w0BAQsFAAOCAQEAtEexZHGPVKyrJat3LQfYfr5/cPkzoidMc+xX3SAV+2L0eqzj
# YHjW/v+Cy6OnuLqS/DPTF4aCXhoBg7+qtKd8GRW2Siylph3hFVw8zDo4xUih3ctW
# PxN4nCgwNkWS77GouHuPgXHkTd1V0b65UiMeh6pFsnvdysi8J5WaF39WgShEDMvS
# 3zzqIG6WkUxcGpOqUQLWfeAZ3WevrmUsHO3c0xakt3OoWMhV80onAOx7bbYcjbrr
# cdCeZfFVka7Rf1NdWO4NTK9KsiYu/rs31vKFZIfNKOFnQ2SuH1NvSG6PSJUG+2ad
# PDZftG6qYlPkCjLvlFERBeHUIQqhCi+rzctMszGCAfUwggHxAgEBMEMwLzEtMCsG
# A1UEAwwkWW91bmVzIEVsIGthcmFtYSAtIE9TQ0FSIElkUCBzY3JpcHRzAhAhSLhs
# /t+ssE0ywLK2GzohMA0GCWCGSAFlAwQCAQUAoIGEMBgGCisGAQQBgjcCAQwxCjAI
# oAKAAKECgAAwGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIB
# CzEOMAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEILtPSr3oL2kLeDdtrMfS
# rZXrmebWm+SuNO3uR1kBhepCMA0GCSqGSIb3DQEBAQUABIIBAHgMDsmvLtRRBFvS
# S40TvJHvZx5myXrWPqOyX/HubEXbwMnJ/5fMdGlHH1FlLdEwYl2UP4AmINjt3vI7
# QwMn46R3qQ+1ukskIJBrIRTt+Aqh43Cms4EWRw9rBiYHWK/sFaT67ENPBb+mXeSo
# IOHtUcXLAeuCnU8EpM/gz+fH53CVzPWOsduwJM17DBrEFOM7khUavAmN4ariRP4P
# S6e7AronXlE8NBVbdn/U1W4Ypqv6KL5V7JuslkCIPSTgAHFe1n3hqqMasMRZ1ENT
# 9GkGXQAcnIYWOLrpDtzua7clCG88wl21yFvH5BqntvY9q509GE+YA0otFEGnPzYB
# JFD5wBc=
# SIG # End signature block
