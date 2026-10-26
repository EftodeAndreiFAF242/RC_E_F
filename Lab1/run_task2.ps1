# =============================================================================
#  Task 2 on a USB flash drive.
#
#  The lab says: before EVERY run, eject the drive, unplug it and plug it back in,
#  so that no data comes from the Windows file cache. This script does the
#  software part for you:
#     1. it ejects the drive ("Safely Remove Hardware"),
#     2. it waits until YOU pull the drive out and plug it back in,
#     3. it waits until the drive is visible again, then runs the next measurement.
#  You get a beep and a small pop-up each time you should re-plug the drive.
#
#  Usage (from the Lab1 folder, in PowerShell):
#      powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Copy           # first time
#      powershell -ExecutionPolicy Bypass -File .\run_task2.ps1                  # 1, 2, 4 threads, C#
#      powershell -ExecutionPolicy Bypass -File .\run_task2.ps1 -Lang both -Warm
#
#  -Folder  : folder on the USB drive that holds numbers_1.txt ... numbers_8.txt
#             (default lab1_task2; the drive letter is found automatically)
#  -Copy    : copy data\parts\numbers_*.txt to that folder first (only needed once)
#  -Threads : thread counts to run (default 1, 2, 4 as the lab asks)
#  -Lang    : csharp, java or both
#  -Warm    : after each "cold" run, run the same thing once more WITHOUT re-plugging.
#             That run reads from the Windows file cache, so you can see what the cache does.
#  -Manual  : don't eject automatically; you eject the drive yourself (tray icon -> Eject).
#  -StartAt : resume an interrupted session at run number N (e.g. -Lang both -StartAt 3
#             skips runs 1 and 2: C# with 1 and 2 threads).
#
#  If a cheap USB stick disconnects in the middle of a run (Windows then reports
#  "A device which does not exist was specified"), that run is NOT counted: the script
#  asks for another re-plug and repeats it (up to 5 attempts). A measurement starts only
#  after the drive has stayed connected for 8 seconds.
# =============================================================================
param(
    [string]$Folder = 'lab1_task2',
    [switch]$Copy,
    [int[]]$Threads = @(1, 2, 4),
    [ValidateSet('csharp', 'java', 'both')][string]$Lang = 'csharp',
    [switch]$Warm,
    [switch]$Manual,
    [int]$StartAt = 1
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. "$PSScriptRoot\common.ps1"

# ---------------------------------------------------------------------------------
# Ejecting a USB device from code: the Windows function CM_Request_Device_Eject does
# exactly what "Safely Remove Hardware" does. PowerShell can't call it directly, so
# we compile a tiny C# class that declares it (Add-Type) and call that.
# ---------------------------------------------------------------------------------
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class UsbEject {
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Request_Device_EjectW(uint devInst, out int vetoType, StringBuilder vetoName, int nameLength, int flags);

    // Returns null on success, otherwise the reason why Windows refused.
    public static string Eject(string deviceId) {
        uint devInst;
        int result = CM_Locate_DevNodeW(out devInst, deviceId, 0);
        if (result != 0) return "device not found (code " + result + ")";
        var vetoName = new StringBuilder(260);
        int vetoType;
        result = CM_Request_Device_EjectW(devInst, out vetoType, vetoName, vetoName.Capacity, 0);
        if (result != 0 || vetoType != 0)
            return "Windows refused (code " + result + ", veto " + vetoType + ": " + vetoName + "). Close any window or program using the drive.";
        return null;
    }
}
"@

# Returns the letter of the removable drive that contains $Folder, or $null.
function Find-LabDrive {
    foreach ($v in Get-Volume | Where-Object { $_.DriveType -eq 'Removable' -and $_.DriveLetter }) {
        if (Test-Path "$($v.DriveLetter):\$Folder\numbers_1.txt") { return [string]$v.DriveLetter }
    }
    return $null
}

# The Plug-and-Play id of the USB device behind a drive letter, e.g. USB\VID_048D&PID_1234\295...
# Path: drive letter -> partition -> disk (USBSTOR\DISK&...) -> its parent = the USB device.
function Get-UsbDeviceId([string]$letter) {
    $logical   = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($letter):'"
    $partition = Get-CimAssociatedInstance -InputObject $logical   -ResultClassName Win32_DiskPartition
    $disk      = Get-CimAssociatedInstance -InputObject $partition -ResultClassName Win32_DiskDrive
    return (Get-PnpDeviceProperty -InstanceId $disk.PNPDeviceID -KeyName DEVPKEY_Device_Parent).Data
}

# A beep plus a pop-up that closes by itself after 25 s (started as a separate hidden
# process so this script doesn't wait for anyone to click OK).
function Send-Notice([string]$text, [switch]$Silent) {
    Write-Host ">>> $text"
    if (-not $Silent) { try { [console]::Beep(880, 250); [console]::Beep(1175, 250) } catch { } }
    $popup = "(New-Object -ComObject WScript.Shell).Popup('$text', 25, 'Lab 1 - Task 2 (USB)', 64) | Out-Null"
    Start-Process powershell -WindowStyle Hidden -ArgumentList '-NoProfile', '-Command', $popup
}

# Makes sure the next run starts with an EMPTY file cache for the drive:
# eject -> wait until it is gone -> wait until it is plugged in again.
function Wait-ForReplug([string]$why) {
    $ejected = $false
    $letter = Find-LabDrive
    if ($letter -and -not $Manual) {
        $problem = [UsbEject]::Eject((Get-UsbDeviceId $letter))
        if ($problem) {
            Write-Host "Could not eject automatically: $problem" -ForegroundColor Yellow
        } else {
            $ejected = $true
            # the drive letter disappears a moment after the eject; give it up to 5 s
            for ($i = 0; $i -lt 50 -and (Find-LabDrive); $i++) { Start-Sleep -Milliseconds 100 }
        }
    }
    if ($ejected)    { Send-Notice "$why : the USB drive was ejected. Pull it out and plug it back in firmly." }
    elseif ($letter) { Send-Notice "$why : eject the USB drive (tray icon - Eject), pull it out and plug it back in." }
    else             { Send-Notice "$why : the USB drive is not connected. Plug it back in firmly." }

    while (Find-LabDrive) { Start-Sleep -Milliseconds 300 }      # wait until it is gone
    Write-Host "    drive removed, waiting for it to come back..."

    # Wait until the drive has been connected for 8 seconds IN A ROW. A plug that is still being
    # pushed in (or a stick that drops out and reconnects) must not start a measurement.
    $waited = 0
    $presentSince = $null
    while ($true) {
        $letter = Find-LabDrive
        if (-not $letter)       { $presentSince = $null }
        elseif (-not $presentSince) { $presentSince = Get-Date }
        elseif (((Get-Date) - $presentSince).TotalSeconds -ge 8) { break }
        Start-Sleep -Milliseconds 300
        $waited += 300
        if ($waited % 30000 -eq 0) { Write-Host "    still waiting for the drive ($($waited / 1000) s)..." }
    }
    Write-Host "    drive is back as $($letter):"
    Send-Notice "Measuring now ($why). Do NOT touch the USB drive until the next beep." -Silent
    return $letter
}

# --------------------------------- main ---------------------------------
Initialize-Lab1 -Lang $Lang
if (-not (Test-Path 'data\parts\numbers_1.txt')) { Invoke-Lab1 -Lang csharp -Arguments 'gen' }

if ($Copy) {
    $letter = Find-LabDrive
    if (-not $letter) {
        $removable = @(Get-Volume | Where-Object { $_.DriveType -eq 'Removable' -and $_.DriveLetter })
        if ($removable.Count -ne 1) { throw "Plug in exactly one USB drive (found $($removable.Count))." }
        $letter = [string]$removable[0].DriveLetter
    }
    $target = "$($letter):\$Folder"
    Write-Host "Copying the 8 part files to $target (a USB 2.0 drive needs a minute or two)..."
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item 'data\parts\numbers_*.txt' $target -Force
}

$letter = Find-LabDrive
if (-not $letter) { throw "No removable drive has a '$Folder' folder with numbers_*.txt. Run with -Copy first." }
Write-Host "Using drive $($letter): (USB device $(Get-UsbDeviceId $letter))"

New-Item -ItemType Directory -Force 'results' | Out-Null
$csv = "results\task2_{0}.csv" -f (Get-Date -Format 'yyyyMMdd_HHmmss')

$languages = Get-Languages $Lang
$total = $languages.Count * $Threads.Count
$number = 0
foreach ($language in $languages) {
    foreach ($t in $Threads) {
        $number++
        if ($number -lt $StartAt) { continue }   # already done in an earlier session

        for ($attempt = 1; ; $attempt++) {
            $letter = Wait-ForReplug "Run $number of $total ($language, $t thread(s))"
            Write-Host "`n=== $language, $t thread(s), cold (just re-plugged) ==="
            try {
                Invoke-Lab1 -Lang $language -Arguments 'task2', '--dir', "$($letter):\$Folder", '--threads', "$t", '--label', 'cold', '--csv', $csv
                break                                 # success: go on to the next configuration
            } catch {
                # A failed run writes no CSV row, so it is simply repeated after another re-plug.
                if ($attempt -ge 5) { throw }
                Write-Host "    the run failed (did the drive disconnect?) - repeating it after another re-plug" -ForegroundColor Yellow
            }
        }

        if ($Warm) {
            Write-Host "`n=== $language, $t thread(s), warm (same files again, now in the file cache) ==="
            Invoke-Lab1 -Lang $language -Arguments 'task2', '--dir', "$($letter):\$Folder", '--threads', "$t", '--label', 'warm', '--csv', $csv
        }
    }
}

# The files were just read from the drive, so their bytes are in the file cache exactly as the
# drive returned them. Comparing their SHA-256 hashes with the originals proves the drive stored
# (and returned) every byte correctly.
Write-Host "`nChecking that the files on the drive are identical to data\parts ..."
$bad = 0
foreach ($file in Get-ChildItem 'data\parts\numbers_*.txt') {
    $original = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
    $onDrive  = (Get-FileHash "$($letter):\$Folder\$($file.Name)" -Algorithm SHA256).Hash
    if ($original -ne $onDrive) { $bad++; Write-Host "  $($file.Name): DIFFERENT" -ForegroundColor Red }
}
if ($bad -eq 0) { Write-Host "  all 8 files identical (SHA-256)" }

Send-Notice "Task 2 finished. You can leave the USB drive plugged in."
Write-Host "`nResults saved to $csv"
