# Runs every Task 1 configuration -Runs times in C# and/or Java and saves the results to results\task1_<time>.csv.
param(
    [int]$Runs = 5,
    [ValidateSet('csharp', 'java', 'both')][string]$Lang = 'csharp',
    [switch]$Extras,
    [ValidateSet('1a', '1b', '1c')][string[]]$Tasks = @('1a', '1b', '1c')
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. "$PSScriptRoot\common.ps1"

Initialize-Lab1 -Lang $Lang
if (-not (Test-Path 'data\numbers.txt')) { Invoke-Lab1 -Lang csharp -Arguments 'gen' }

New-Item -ItemType Directory -Force 'results' | Out-Null
$csv = "results\task1_{0}.csv" -f (Get-Date -Format 'yyyyMMdd_HHmmss')
$threadCounts = 1, 2, 4, 8, 16, 32, 64

foreach ($language in Get-Languages $Lang) {
    function Run([string[]]$arguments) { Invoke-Lab1 -Lang $language -Arguments ($arguments + '--csv', $csv) }

    Write-Host "`n##### $language #####"
    Write-Host "Warm-up run (loads numbers.txt into the Windows file cache, not recorded)..."
    Invoke-Lab1 -Lang $language -Arguments 'task1a', '--threads', '4' | Out-Null

    if ('1a' -in $Tasks) {
        Write-Host "`n=== Task 1a ==="
        foreach ($run in 1..$Runs) {
            foreach ($t in $threadCounts) { Run 'task1a', '--threads', "$t" }
        }
    }

    if ('1b' -in $Tasks) {
        Write-Host "`n=== Task 1b (each run: version 1 three times, then 2 and 3) ==="
        foreach ($run in 1..$Runs) { Run 'task1b', '--version', 'all' }
    }

    if ('1c' -in $Tasks) {
        Write-Host "`n=== Task 1c ==="
        foreach ($run in 1..$Runs) {
            foreach ($w in $threadCounts) { Run 'task1c', '--workers', "$w" }
        }
    }

    # Question 6 of Task 1c: batches of a single line, and a queue with no size limit.
    if ($Extras -and '1c' -in $Tasks) {
        Write-Host "`n=== Task 1c, question 6: batches of ONE line (expect it to be much slower) ==="
        foreach ($w in 1, 4) { Run 'task1c', '--workers', "$w", '--batch', '1' }

        Write-Host "`n=== Task 1c, question 6: queue with NO size limit (watch the peak memory) ==="
        foreach ($w in 1, 4) { Run 'task1c', '--workers', "$w", '--capacity', '0' }
    }
}

Write-Host "`nAll results saved to $csv"
