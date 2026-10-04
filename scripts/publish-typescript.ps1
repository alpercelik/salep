param(
    [Parameter(Position = 0)][string]$RuntimeIdentifier,
    [Parameter(Position = 1)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier) -or $args.Count -gt 0) {
    [Console]::Error.WriteLine('Usage: publish-typescript.ps1 <runtime-identifier> [output-directory]')
    exit 1
}
$repoRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = [System.IO.Path]::Combine($repoRoot, 'artifacts', 'typescript', $RuntimeIdentifier)
}
& dotnet publish ([System.IO.Path]::Combine($repoRoot, 'src', 'Salep.ClientGenerator.TypeScript.Cli')) --framework net10.0 --configuration Release --runtime $RuntimeIdentifier --self-contained true --output $OutputDirectory -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:UseSharedCompilation=false -p:DebugType=None -p:GenerateDocumentationFile=false -m:1 /nodeReuse:false
exit $LASTEXITCODE
