param([string]$GgufPath = '')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Push-Location $workspace
try {
    $project = [xml](Get-Content -LiteralPath (Join-Path $workspace 'JARVIS.csproj') -Raw)
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Set a numeric major.minor.patch Version in JARVIS.csproj.' }
    $toolsDirectory = Join-Path $workspace 'artifacts\installer-tools'
    $compiler = Join-Path $toolsDirectory 'nsis-3.11\makensis.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
        $archive = Join-Path $toolsDirectory 'nsis-3.11.zip'
        $expectedHash = 'C7D27F780DDB6CFFB4730138CD1591E841F4B7EDB155856901CDF5F214394FA1'
        $verified = $false
        foreach ($url in @('https://downloads.sourceforge.net/project/nsis/NSIS%203/3.11/nsis-3.11.zip', 'https://distfiles.macports.org/nsis/nsis-3.11.zip')) {
            try {
                Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
                if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -eq $expectedHash) { $verified = $true; break }
            } catch { Write-Host 'Trying the next compiler download mirror...' }
        }
        if (-not $verified) { throw 'Portable compiler download failed its pinned SHA-256 check.' }
        Expand-Archive -LiteralPath $archive -DestinationPath $toolsDirectory -Force
    }
    if ($GgufPath) {
        $GgufPath = (Resolve-Path -LiteralPath $GgufPath).Path
        if ([IO.Path]::GetExtension($GgufPath) -ne '.gguf') { throw 'Choose a .gguf file to bundle.' }
        $stream = [IO.File]::OpenRead($GgufPath)
        try { $header = New-Object byte[] 4; if ($stream.Read($header, 0, 4) -ne 4 -or [Text.Encoding]::ASCII.GetString($header) -ne 'GGUF') { throw 'Invalid GGUF header.' } }
        finally { $stream.Dispose() }
    }
    $staging = Join-Path $workspace ('artifacts\installer-build\' + [Guid]::NewGuid().ToString('N'))
    $payload = Join-Path $staging 'payload'
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    & dotnet publish (Join-Path $workspace 'JARVIS.csproj') -c Release -r win-x64 --self-contained true '-p:IncludeLocalModels=false' -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    if (Get-ChildItem -LiteralPath $payload -Filter '*.gguf' -File -Recurse) { throw 'Unexpected GGUF in the application payload.' }
    Copy-Item -LiteralPath (Join-Path $workspace 'MODELS-AND-COMMANDS.md') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $workspace 'MCP.md') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $workspace 'RELEASE.md') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $workspace 'CHANGELOG.md') -Destination $payload
    # Delete only known app payload files on uninstall. Models and user data are retained.
    $uninstallFiles = Join-Path $staging 'UninstallFiles.nsh'
    $lines = @()
    foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
        $relative = $file.FullName.Substring($payload.Length + 1)
        $lines += 'Delete "$INSTDIR\' + $relative + '"'
    }
    foreach ($folder in Get-ChildItem -LiteralPath $payload -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending) {
        $relative = $folder.FullName.Substring($payload.Length + 1)
        $lines += 'RMDir "$INSTDIR\' + $relative + '"'
    }
    [IO.File]::WriteAllLines($uninstallFiles, $lines, [Text.Encoding]::UTF8)
    $outputDirectory = Join-Path $workspace 'dist\installer'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $fileName = if ($GgufPath) { "JARVIS-Setup-$version-win-x64-with-model.exe" } else { "JARVIS-Setup-$version-win-x64.exe" }
    $output = Join-Path $outputDirectory $fileName
    $compilerArguments = @('/V3', "/DPAYLOAD_DIR=$payload", "/DOUTPUT_FILE=$output", "/DUNINSTALL_FILES=$uninstallFiles", "/DAPP_VERSION=$version", ('/DAPP_ICON=' + (Join-Path $workspace 'Assets\Jarvis.ico')))
    if ($GgufPath) { $compilerArguments += "/DBUNDLED_GGUF=$GgufPath"; $compilerArguments += '/DBUNDLED_NAME=' + [IO.Path]::GetFileName($GgufPath) }
    & $compiler @compilerArguments (Join-Path $PSScriptRoot 'JARVIS.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($output + '.sha256', $hash + '  ' + $fileName + [Environment]::NewLine)
    $sourceCommit = (& git rev-parse --short HEAD 2>$null)
    $manifest = @{ version = $version; platform = 'win-x64'; file = $fileName; bytes = (Get-Item -LiteralPath $output).Length; sha256 = $hash; sourceCommit = $sourceCommit; builtUtc = [DateTime]::UtcNow.ToString('O'); bundledModel = if ($GgufPath) { [IO.Path]::GetFileName($GgufPath) } else { $null } }
    [IO.File]::WriteAllText($output + '.json', ($manifest | ConvertTo-Json))
    Write-Host "Installer ready: $output"
    Write-Host 'Setup can import an optional GGUF, or install without a local model.'
} finally { Pop-Location }
