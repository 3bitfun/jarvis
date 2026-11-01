param([switch]$Extended)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Push-Location $workspace
try {
    & dotnet build JARVIS.csproj -c Release '-p:IncludeLocalModels=false'
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $checks = @('readiness', 'startup', 'features', 'memory')
    if ($Extended) { $checks += @('launcher', 'fonts-glass') }
    foreach ($check in $checks) {
        Write-Host "Checking $check..."
        & dotnet bin\Release\net8.0\JARVIS.dll "--check-$check"
        if ($LASTEXITCODE -ne 0) { throw "$check check failed (exit $LASTEXITCODE)." }
    }
    Write-Host 'All requested checks passed.'
} finally { Pop-Location }
