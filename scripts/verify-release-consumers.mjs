import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const [version, directory, ...extra] = process.argv.slice(2);
if (!version || extra.length || /\s/.test(version) || !/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$/.test(version)
    || (version.includes('-') && version.slice(version.indexOf('-') + 1).split('.').some(part => /^0\d+$/.test(part)))) {
  console.error('Usage: node scripts/verify-release-consumers.mjs <version> [package-directory]');
  process.exit(2);
}
const packageDirectory = path.resolve(root, directory ?? `artifacts/release/${version}`);
for (const id of ['Salep.ClientGenerator', 'Salep.GraphQLParser']) {
  if (!existsSync(path.join(packageDirectory, `${id}.${version}.nupkg`))) throw new Error(`Missing ${id} release package.`);
}
const scratch = mkdtempSync(path.join(os.tmpdir(), 'salep-release-consumers-'));
const escapeXml = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
function dotnet(args) {
  const result = spawnSync('dotnet', args, {
    cwd: scratch, encoding: 'utf8', timeout: 180_000, maxBuffer: 16 * 1024 * 1024,
    env: { ...process.env, DOTNET_CLI_USE_MSBUILD_SERVER: '0' },
  });
  if (result.error || result.status !== 0) {
    throw new Error(`dotnet ${args[0]} failed:\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  }
  return result.stdout;
}
function project(name, id, items = '') {
  const folder = path.join(scratch, name);
  mkdirSync(folder);
  writeFileSync(path.join(folder, `${name}.csproj`), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><UseAppHost>false</UseAppHost><TargetFrameworks>net10.0;net11.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><DefineConstants>PACKAGE_VERIFICATION</DefineConstants>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="${id}" Version="${version}" PrivateAssets="all" />${items}</ItemGroup>
</Project>`);
  return folder;
}
try {
  copyFileSync(path.join(root, 'global.json'), path.join(scratch, 'global.json'));
  const config = path.join(scratch, 'NuGet.config');
  writeFileSync(config, `<configuration><packageSources><clear /><add key="release" value="${escapeXml(packageDirectory)}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><packageSourceMapping><packageSource key="release"><package pattern="Salep.ClientGenerator" /><package pattern="Salep.GraphQLParser" /></packageSource><packageSource key="nuget.org"><package pattern="Microsoft.NETCore.App.*" /><package pattern="Microsoft.AspNetCore.App.*" /></packageSource></packageSourceMapping></configuration>`);
  const parser = project('ParserConsumer', 'Salep.GraphQLParser', '<None Update="*.json" CopyToOutputDirectory="PreserveNewest" />');
  for (const file of ['Program.cs', 'PackageApiContractVerifier.cs']) {
    copyFileSync(path.join(root, 'src/Salep.GraphQLParser.PublicApiConsumer', file), path.join(parser, file));
  }
  for (const file of ['public-api-contract.json', 'package-api-allowlist.json']) {
    copyFileSync(path.join(root, 'docs/parser', file), path.join(parser, file));
  }
  const client = project('ClientConsumer', 'Salep.ClientGenerator');
  mkdirSync(path.join(client, 'graphql'));
  writeFileSync(path.join(client, 'schema.graphql'), 'type Query { greeting: String! }');
  writeFileSync(path.join(client, 'graphql/Greeting.graphql'), 'query Greeting { greeting }');
  writeFileSync(path.join(client, 'salep.json'), JSON.stringify({
    version: 1, kind: 'client', schema: './schema.graphql', operations: './graphql', output: './Generated',
    namespace: 'ReleaseSmoke', clientName: 'SmokeClient', scalarPreset: 'builtin',

  }));
  writeFileSync(path.join(client, 'Program.cs'), `using System.Text.Json;
using ReleaseSmoke;
var response = JsonSerializer.Deserialize<GreetingResponse>("{\\"greeting\\":\\"hello\\"}");
if (response?.Greeting != "hello") throw new InvalidOperationException("Generated response did not deserialize.");
if (Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll").Any(file =>
    Path.GetFileName(file).StartsWith("Salep", StringComparison.Ordinal) ||
    Path.GetFileName(file).StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)))
    throw new InvalidOperationException("Build tools leaked into the generated application's runtime output.");
Console.WriteLine(typeof(SmokeClient).FullName + ": generated client and response passed.");
`);
  for (const folder of [parser, client]) {
    const projectPath = path.join(folder, `${path.basename(folder)}.csproj`);
    dotnet(['restore', projectPath, '--configfile', config, '--packages', path.join(scratch, 'packages'), '--no-cache']);
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    const assets = JSON.parse(readFileSync(path.join(folder, 'obj/project.assets.json'), 'utf8'));
    const expectedId = folder === parser ? 'Salep.GraphQLParser' : 'Salep.ClientGenerator';
    const packages = Object.entries(assets.libraries).filter(([, value]) => value.type === 'package').map(([key]) => key);
    if (packages.length !== 1 || packages[0] !== `${expectedId}/${version}`) throw new Error(`Unexpected dependencies: ${packages}`);
    for (const framework of ['net10.0', 'net11.0']) {
      const output = dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
      console.log(`${path.basename(folder)} ${framework}: ${output.trim()}`);
    }
  }
  if (!readdirSync(path.join(client, 'Generated')).some(file => file.endsWith('.cs'))) throw new Error('MSBuild generated no client sources.');
  console.log(`Both ${version} packages passed isolated consumption with a fresh cache and local-only Salep package mapping.`);
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
