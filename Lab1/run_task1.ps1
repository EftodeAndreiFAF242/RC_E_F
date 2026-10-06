# =============================================================================
#  Runs every Task 1 configuration (1a, 1b, 1c) and saves all results to a CSV
#  file in the results folder.
#
#  Usage (from the Lab1 folder, in PowerShell):
#      powershell -ExecutionPolicy Bypass -File .\run_task1.ps1
#      powershell -ExecutionPolicy Bypass -File .\run_task1.ps1 -Runs 5
#
#  -Runs N : how many times to repeat each 1a / 1c configuration (use the median
#            in your charts; single runs of ~50 ms are noisy).
# =============================================================================
param([int]$Runs = 3)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot                       # always work from the Lab1 folder

Write-Host "Building (Release)..."
dotnet build -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
# Inside RC_E_F the build goes to ..\artifacts (see RC_E_F\Directory.Build.props);
# if the Lab1 folder is built on its own, it goes to bin\Release\net10.0. Use whichever exists.
$exe = @('..\artifacts\bin\Lab1\release\Lab1.exe', 'bin\Release\net10.0\Lab1.exe') |
       ForEach-Object { Join-Path $PSScriptRoot $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) { throw "Lab1.exe not found after the build" }

if (-not (Test-Path 'data\numbers.txt')) {
    & $exe gen
    if ($LASTEXITCODE -ne 0) { throw "gen failed" }
}

New-Item -ItemType Directory -Force 'results' | Out-Null
$csv = "results\task1_{0}.csv" -f (Get-Date -Format 'yyyyMMdd_HHmmss')
$threadCounts = 1, 2, 4, 8, 16, 32, 64

# Helper: run the program and stop the script if it reports an error.
function Invoke-Lab1 {
    & $exe @args --csv $csv
    if ($LASTEXITCODE -ne 0) { throw "Lab1 $args failed" }
}

Write-Host "`nWarm-up run (loads numbers.txt into the Windows file cache, not recorded)..."
& $exe task1a --threads 4 | Out-Null

Write-Host "`n=== Task 1a ==="
foreach ($run in 1..$Runs) {
    foreach ($t in $threadCounts) { Invoke-Lab1 task1a --threads $t }
}

Write-Host "`n=== Task 1b ==="
Invoke-Lab1 task1b --version all

Write-Host "`n=== Task 1c ==="
foreach ($run in 1..$Runs) {
    foreach ($w in $threadCounts) { Invoke-Lab1 task1c --workers $w }
}

Write-Host "`nAll results saved to $csv"
