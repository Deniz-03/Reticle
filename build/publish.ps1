param(
    [string]$Output = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Reticle.App\Reticle.App.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $Output

if (-not (Test-Path (Join-Path $Output 'Reticle.exe'))) {
    throw "Publish did not produce Reticle.exe in $Output"
}
Write-Host "Portable build: $Output"
