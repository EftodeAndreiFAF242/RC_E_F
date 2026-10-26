# =============================================================================
#  Helpers shared by run_task1.ps1 and run_task2.ps1.
#  They load this file with:   . "$PSScriptRoot\common.ps1"
#  (the leading dot = "run it inside MY script", so its functions become available there)
# =============================================================================

# Builds the C# program (Release = optimised) and returns the full path of Lab1.exe.
function Build-CSharp {
    Write-Host "Building the C# version (Release)..."
    dotnet build "$PSScriptRoot\Lab1.csproj" -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "C# build failed (run 'dotnet build -c Release' to see why)" }

    # Inside RC_E_F the build goes to ..\artifacts (see RC_E_F\Directory.Build.props);
    # if the Lab1 folder is built on its own, it goes to bin\Release\net10.0. Use whichever exists.
    $exe = @('..\artifacts\bin\Lab1\release\Lab1.exe', 'bin\Release\net10.0\Lab1.exe') |
           ForEach-Object { Join-Path $PSScriptRoot $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $exe) { throw "Lab1.exe not found after the build" }
    return $exe
}

# Finds the JDK's bin folder: first on PATH, then JAVA_HOME, then the usual install folders.
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

# Compiles the Java version into java\out and returns the JDK's bin folder.
function Build-Java {
    $bin = Find-JavaBin
    Write-Host "Building the Java version with $bin\javac.exe ..."
    $sources = (Get-ChildItem "$PSScriptRoot\java\src" -Recurse -Filter *.java).FullName
    & "$bin\javac.exe" -d "$PSScriptRoot\java\out" -encoding UTF-8 @sources
    if ($LASTEXITCODE -ne 0) { throw "Java build failed" }
    return $bin
}

# Runs one configuration in C# or Java with the given arguments, e.g.
#     Invoke-Lab1 -Lang csharp -Arguments 'task1a','--threads','8','--csv','results\x.csv'
# and stops the whole script if the program reports an error.
function Invoke-Lab1 {
    param([string]$Lang, [string[]]$Arguments)
    if ($Lang -eq 'java') {
        # --enable-native-access: the Java version calls one Windows function (to read the
        # peak memory) through Java's Foreign Function API, and Java asks us to allow that.
        & "$script:JavaBin\java.exe" --enable-native-access=ALL-UNNAMED -cp "$PSScriptRoot\java\out" lab1.Main @Arguments
    } else {
        & $script:CSharpExe @Arguments
    }
    if ($LASTEXITCODE -ne 0) { throw "$Lang $Arguments failed" }
}

# Builds whatever -Lang asks for and remembers where the programs are.
function Initialize-Lab1 {
    param([string]$Lang)
    $script:CSharpExe = Build-CSharp        # always built: it also creates the data files
    if ($Lang -in 'java', 'both') { $script:JavaBin = Build-Java }
}

# 'both' -> csharp, java   /   'java' -> java   /   'csharp' -> csharp
function Get-Languages {
    param([string]$Lang)
    if ($Lang -eq 'both') { return @('csharp', 'java') }
    return @($Lang)
}
