$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..'))
$sample = [System.IO.Path]::Combine($repoRoot, 'src', 'samples', 'TypeScript', 'Node')
& dotnet run --project ([System.IO.Path]::Combine($repoRoot, 'src', 'Salep.ClientGenerator.TypeScript.Cli')) --framework net10.0 -- generate --config ([System.IO.Path]::Combine($sample, 'salep.typescript.json'))
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project ([System.IO.Path]::Combine($repoRoot, 'src', 'Salep.ClientGenerator.TypeScript.Cli')) --framework net10.0 -- generate --config ([System.IO.Path]::Combine($sample, 'salep.masked.typescript.json'))
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& npm ci --prefix $sample --ignore-scripts
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& npm test --prefix $sample
exit $LASTEXITCODE
