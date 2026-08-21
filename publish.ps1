$ErrorActionPreference = 'Stop'

$dotnet = Join-Path $PSScriptRoot '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw '.dotnet SDK를 찾을 수 없습니다. README의 개발 환경 준비 단계를 먼저 실행하세요.'
}

& $dotnet publish $PSScriptRoot `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output (Join-Path $PSScriptRoot 'dist')

Write-Host "완료: $(Join-Path $PSScriptRoot 'dist\MeetRecorder.exe')"
