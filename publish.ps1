$ErrorActionPreference = 'Stop'

$dotnet = Join-Path $PSScriptRoot '.dotnet\dotnet.exe'
$output = Join-Path $PSScriptRoot 'dist\MeetRecorder.exe'
if (-not (Test-Path $dotnet)) {
    throw '.dotnet SDK was not found. Follow the setup steps in README.md first.'
}

$running = Get-Process -Name 'MeetRecorder' -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -eq $output } catch { $false }
}
if ($running) {
    throw 'MeetRecorder.exe is running. Stop recording and close the app, then run publish.ps1 again.'
}

& $dotnet publish $PSScriptRoot `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output (Join-Path $PSScriptRoot 'dist')

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Created: $output"
