# =============================================================================
#  Task 2 on a USB flash drive: runs 1, 2 and 4 threads, asking you to
#  eject / unplug / re-plug the drive before EVERY run (so no data comes from
#  the Windows file cache).
#
#  Usage (from the Lab1 folder, in PowerShell), assuming the drive is E:
#      powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Dir E:\ -Copy
#      powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Dir E:\
#
#  -Dir  : folder on the USB drive that holds numbers_1.txt ... numbers_8.txt
#  -Copy : copy data\parts\numbers_*.txt to -Dir first (only needed once)
# =============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [switch]$Copy,
    [int[]]$Threads = @(1, 2, 4)
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
# Inside RC_E_F the build goes to ..\artifacts (see RC_E_F\Directory.Build.props);
# if the Lab1 folder is built on its own, it goes to bin\Release\net10.0. Use whichever exists.
$exe = @('..\artifacts\bin\Lab1\release\Lab1.exe', 'bin\Release\net10.0\Lab1.exe') |
       ForEach-Object { Join-Path $PSScriptRoot $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) { throw "Lab1.exe not found after the build" }

if ($Copy) {
    Write-Host "Copying the 8 part files to $Dir ..."
    New-Item -ItemType Directory -Force $Dir | Out-Null
    Copy-Item 'data\parts\numbers_*.txt' $Dir
}

New-Item -ItemType Directory -Force 'results' | Out-Null
$csv = "results\task2_{0}.csv" -f (Get-Date -Format 'yyyyMMdd_HHmmss')

foreach ($t in $Threads) {
    Write-Host ""
    Write-Host "Next: $t thread(s)."
    Write-Host "  1. Eject the USB drive (tray icon -> Eject), 2. unplug it, 3. plug it back in."
    Read-Host "  When $Dir is visible again, press Enter"

    & $exe task2 --dir $Dir --threads $t --csv $csv
    if ($LASTEXITCODE -ne 0) { throw "task2 failed" }
}

Write-Host "`nResults saved to $csv"
