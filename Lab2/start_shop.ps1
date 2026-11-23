# Lab 2: builds, then opens server, two clients, provider.
param([int]$Port = 5050, [string]$WindowStyle = "Normal")

dotnet build (Join-Path $PSScriptRoot "Lab2.csproj") -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $PSScriptRoot "..\artifacts\bin\Lab2\release\Lab2.exe"
if (-not (Test-Path $exe)) { $exe = Join-Path $PSScriptRoot "bin\Release\net10.0\Lab2.exe" }

Start-Process $exe -ArgumentList "server $Port" -WindowStyle $WindowStyle
Start-Sleep -Milliseconds 700
foreach ($role in "client", "client", "provider") {
    Start-Process $exe -ArgumentList "$role 127.0.0.1 $Port" -WindowStyle $WindowStyle
}
