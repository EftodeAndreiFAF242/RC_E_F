# Helpers shared by run_task1.ps1 and run_task2.ps1 for building and running the C# and Java versions.

function Build-CSharp {
    Write-Host "Building the C# version (Release)..."
    dotnet build "$PSScriptRoot\Lab1.csproj" -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "C# build failed (run 'dotnet build -c Release' to see why)" }

    # Inside RC_E_F the build goes to ..\artifacts; a Lab1 folder built on its own uses bin\Release\net10.0.
    $exe = @('..\artifacts\bin\Lab1\release\Lab1.exe', 'bin\Release\net10.0\Lab1.exe') |
           ForEach-Object { Join-Path $PSScriptRoot $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $exe) { throw "Lab1.exe not found after the build" }
    return $exe
}

function Find-JavaBin {
    $javac = Get-Command javac -ErrorAction SilentlyContinue
    if ($javac) { return Split-Path $javac.Source }
    if ($env:JAVA_HOME -and (Test-Path "$env:JAVA_HOME\bin\javac.exe")) { return "$env:JAVA_HOME\bin" }
    $found = Get-ChildItem 'C:\Program Files\Eclipse Adoptium', 'C:\Program Files\Java', 'C:\Program Files\Microsoft' `
                 -Directory -ErrorAction SilentlyContinue |
             Where-Object { Test-Path "$($_.FullName)\bin\javac.exe" } |
             Sort-Object Name -Descending | Select-Object -First 1
    if ($found) { return "$($found.FullName)\bin" }
    throw "No JDK found. Install one with:  winget install EclipseAdoptium.Temurin.25.JDK"
}

function Build-Java {
    $bin = Find-JavaBin
    Write-Host "Building the Java version with $bin\javac.exe ..."
    $sources = (Get-ChildItem "$PSScriptRoot\java\src" -Recurse -Filter *.java).FullName
    & "$bin\javac.exe" -d "$PSScriptRoot\java\out" -encoding UTF-8 @sources
    if ($LASTEXITCODE -ne 0) { throw "Java build failed" }
    return $bin
}

function Invoke-Lab1 {
    param([string]$Lang, [string[]]$Arguments)
    if ($Lang -eq 'java') {
        # --enable-native-access lets PeakMemory.java call the Windows memory function.
        & "$script:JavaBin\java.exe" --enable-native-access=ALL-UNNAMED -cp "$PSScriptRoot\java\out" lab1.Main @Arguments
    } else {
        & $script:CSharpExe @Arguments
    }
    if ($LASTEXITCODE -ne 0) { throw "$Lang $Arguments failed" }
}

function Initialize-Lab1 {
    param([string]$Lang)
    $script:CSharpExe = Build-CSharp
    if ($Lang -in 'java', 'both') { $script:JavaBin = Build-Java }
}

function Get-Languages {
    param([string]$Lang)
    if ($Lang -eq 'both') { return @('csharp', 'java') }
    return @($Lang)
}
