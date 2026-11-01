$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet build JARVIS.csproj -c Release -p:IncludeLocalModels=false
    if ($LASTEXITCODE -ne 0) { throw 'Icon export build failed.' }
    dotnet bin\Release\net8.0\JARVIS.dll --export-icons Assets\Icons
    if ($LASTEXITCODE -ne 0) { throw 'Icon export failed.' }
    dotnet build JARVIS.csproj -c Release -p:IncludeLocalModels=false --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Embedding the updated app icon failed.' }
    $project = [xml](Get-Content JARVIS.csproj -Raw)
    $version = [string]$project.Project.PropertyGroup.Version
    New-Item -ItemType Directory -Path dist\design -Force | Out-Null
    Compress-Archive -LiteralPath Assets\Icons -DestinationPath "dist\design\JARVIS-Icons-$version.zip" -Force
    Write-Host "Icon pack ready: dist\design\JARVIS-Icons-$version.zip"
} finally { Pop-Location }
