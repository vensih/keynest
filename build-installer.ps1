#Requires -Version 5.1
<#
.SYNOPSIS
    Publish Keynest and compile the Inno Setup installer.

.DESCRIPTION
    1. dotnet restore  (ensures win-x64 assets are present)
    2. dotnet publish  (Release, win-x64, self-contained)
    3. ISCC.exe        (Inno Setup compiler) -> installer\output\keynest-setup-*.exe

.EXAMPLE
    .\build-installer.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root    = $PSScriptRoot
$project = Join-Path $root "Keynest.Windows\Keynest.Windows.csproj"
$iss     = Join-Path $root "installer\keynest.iss"

# ── 0. Kill any running instance so DLLs aren't locked ───────────────────────
$running = Get-Process -Name "Keynest.Windows" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "==> Stopping running Keynest instance..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# ── 1. Restore (ensures win-x64 RID assets exist) ────────────────────────────
Write-Host "`n==> Restoring packages (win-x64)..." -ForegroundColor Cyan
dotnet restore $project -r win-x64
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet restore failed."; exit 1 }

# ── 2. Publish ────────────────────────────────────────────────────────────────
Write-Host "`n==> Publishing Keynest (Release, win-x64, self-contained)..." -ForegroundColor Cyan
dotnet publish $project -c Release --self-contained true
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }

# ── 3. Verify prereqs exist ───────────────────────────────────────────────────
$prereqDir = Join-Path $root "installer\prereqs"
$prereqs   = @("VC_redist.x64.exe", "WindowsAppRuntimeInstall-x64.exe")
foreach ($p in $prereqs) {
    if (-not (Test-Path (Join-Path $prereqDir $p))) {
        Write-Error "Missing prereq: installer\prereqs\$p`nDownload it and place it in the prereqs folder."
        exit 1
    }
}

# ── 4. Find Inno Setup compiler ───────────────────────────────────────────────
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup not found. Download and install it from:" -ForegroundColor Yellow
    Write-Host "  https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    Write-Host "Then re-run this script." -ForegroundColor Yellow
    exit 1
}

# ── 5. Compile installer ──────────────────────────────────────────────────────
Write-Host "`n==> Compiling installer with Inno Setup..." -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup compilation failed."; exit 1 }

$outputDir = Join-Path $root "installer\output"
$exe       = Get-ChildItem $outputDir -Filter "keynest-setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "==> Done! Installer:" -ForegroundColor Green
Write-Host "    $($exe.FullName)" -ForegroundColor Green
