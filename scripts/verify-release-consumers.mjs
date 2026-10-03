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
  const consumerSchema = 'enum Status { ACTIVE } extend enum Status { INACTIVE } input Filter { status: Status! labels: [String!]! optional: String } type Query { greeting(filter: Filter): String! } extend type Query { count: Int! }';
  const consumerOperation = 'query Greeting($filter: Filter!) { greeting(filter: $filter) count }';
  const consumerProgram = namespace => `using System.Net;
using System.Text;
using System.Text.Json;
using ${namespace};
using var handler = new RecordingHandler();
using var http = new HttpClient(handler);
var api = new SmokeClient(http, new Uri("https://example.test/graphql"));
var filter = new Filter { Status = Status.ACTIVE, Labels = ["one", "two"] };
filter.Status = Status.INACTIVE;
var operation = new GreetingOperation();
operation = operation with { Variables = new GreetingVariables { Filter = filter } };
var response = await api.ExecuteAsync(operation);
if (response.Data?.Greeting != "hello" || response.Data.Count != 2 || response.Errors?[0].Message != "partial")
    throw new InvalidOperationException("Generated client did not preserve partial data and GraphQL errors.");
using (var request = JsonDocument.Parse(handler.Body!)) {
    if (request.RootElement.GetProperty("query").GetString() != operation.Query || !operation.Query.Contains(Environment.NewLine))
        throw new Exception("Formatted raw query was not preserved in the HTTP payload.");
    var variables = request.RootElement.GetProperty("variables").GetProperty("filter");
    if (variables.GetProperty("status").GetString() != "INACTIVE" || variables.GetProperty("labels").GetArrayLength() != 2 || variables.TryGetProperty("optional", out _))
        throw new InvalidOperationException("Generated client serialized nested inputs incorrectly.");
}
if (Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll").Any(file =>
    Path.GetFileName(file).StartsWith("Salep", StringComparison.Ordinal) ||
    Path.GetFileName(file).StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ||
    Path.GetFileName(file).Equals("Scriban.dll", StringComparison.Ordinal)))
    throw new InvalidOperationException("Build tools leaked into the generated application's runtime output.");
Console.WriteLine(JsonSerializer.Serialize(new { handler.Method, handler.Body, response.Data.Greeting, response.Data.Count, Error = response.Errors[0].Message }));
sealed class RecordingHandler : HttpMessageHandler {
    public string? Method { get; private set; }
    public string? Body { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Method = request.Method.Method;
        Body = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\\"data\\":{\\"greeting\\":\\"hello\\",\\"count\\":2},\\"errors\\":[{\\"message\\":\\"partial\\"}]}", Encoding.UTF8, "application/json")
        };
    }
}
`;
  const scriban = project('ScribanConsumer', 'Salep.ClientGenerator');
  mkdirSync(path.join(scriban, 'graphql'));
  writeFileSync(path.join(scriban, 'schema.graphql'), consumerSchema);
  writeFileSync(path.join(scriban, 'graphql/Greeting.graphql'), consumerOperation);
  writeFileSync(path.join(scriban, 'salep.json'), JSON.stringify({
    version: 1, kind: 'client', schema: './schema.graphql', operations: './graphql', output: './Generated',
    namespace: 'ScribanReleaseSmoke', clientName: 'SmokeClient', emitSample: true,
  }));
  writeFileSync(path.join(scriban, 'Program.cs'), consumerProgram('ScribanReleaseSmoke'));
  const consumerEntries = [[parser, 'Salep.GraphQLParser'], [scriban, 'Salep.ClientGenerator']];
  for (const [folder, expectedId] of consumerEntries) {
    const projectPath = path.join(folder, `${path.basename(folder)}.csproj`);
    dotnet(['restore', projectPath, '--configfile', config, '--packages', path.join(scratch, 'packages'), '--no-cache']);
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    const assets = JSON.parse(readFileSync(path.join(folder, 'obj/project.assets.json'), 'utf8'));
    const packages = Object.entries(assets.libraries).filter(([, value]) => value.type === 'package').map(([key]) => key);
    if (packages.length !== 1 || packages[0] !== `${expectedId}/${version}`) throw new Error(`Unexpected dependencies: ${packages}`);
    if (folder === scriban) {
      const packageRoot = path.join(scratch, 'packages', 'salep.clientgenerator', version);
      for (const framework of ['net10.0', 'net11.0']) {
        const tools = path.join(packageRoot, 'tools', framework, 'any');
        const names = readdirSync(tools);
        if (!names.includes('Scriban.dll') || names.some(name => name.startsWith('Microsoft.CodeAnalysis') || name.startsWith('Salep.ClientGenerator.Roslyn'))) {
          throw new Error('Salep.ClientGenerator package must contain the Scriban backend without Roslyn tooling.');
        }
      }
      console.log('Salep.ClientGenerator: Scriban-only private tool payload verified');
    }

    for (const framework of ['net10.0', 'net11.0']) {
      const output = dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
      console.log(`${path.basename(folder)} ${framework}: passed`);
    }
    if (folder === parser) continue;
    const generated = path.join(folder, 'Generated');
    const manifestPath = path.join(generated, '.salep.manifest.json');
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
    const manifestPaths = [manifest.Configuration, manifest.ConfigurationIdentity, manifest.Output,
      ...Object.keys(manifest.Inputs), ...Object.keys(manifest.Dependencies),
      ...Object.values(manifest.Symbols ?? {}).map(symbol => symbol.Owner)].filter(value => value !== undefined);
    if (manifestPaths.some(value => path.isAbsolute(value) || value.includes('\\'))) {
      throw new Error('Packed generator wrote a non-portable manifest path.');
    }
    for (const input of Object.keys(manifest.Inputs)) {
      if (!existsSync(path.resolve(generated, input))) throw new Error('Manifest input is not relative to its own directory.');
    }
    const snapshot = () => Object.fromEntries(readdirSync(generated).filter(file => file.endsWith('.cs')).sort().map(file => [file, readFileSync(path.join(generated, file), 'utf8')]));
    const before = JSON.stringify(snapshot());
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    if (before !== JSON.stringify(snapshot())) throw new Error('Unchanged package build altered generated sources.');
    rmSync(path.join(generated, 'Operations.cs'));
    writeFileSync(path.join(generated, 'SchemaTypes.cs'), '// changed owned source\n');
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    if (before !== JSON.stringify(snapshot())) throw new Error('Package build did not recover missing and changed owned sources.');
    const configPath = path.join(folder, 'salep.json');
    const settings = JSON.parse(readFileSync(configPath, 'utf8'));
    settings.emitSample = false;
    writeFileSync(configPath, JSON.stringify(settings));
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    if (existsSync(path.join(generated, 'Operations.Sample.cs'))) throw new Error('Package build retained obsolete sample source.');
    writeFileSync(path.join(folder, 'graphql/Greeting.graphql'), consumerOperation.replace('Greeting(', 'UpdatedGreeting('));
    writeFileSync(path.join(folder, 'Program.cs'), consumerProgram(folder === scriban ? 'ScribanReleaseSmoke' : 'ReleaseSmoke').replaceAll('GreetingOperation', 'UpdatedGreetingOperation').replaceAll('GreetingVariables', 'UpdatedGreetingVariables'));
    dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
    const operations = readFileSync(path.join(generated, 'Operations.cs'), 'utf8');
    if (!/public string Query =>\s*"{3,}/.test(operations)) throw new Error('Packaged generator did not emit raw queries.');
    if (!operations.includes('UpdatedGreetingOperation') || /record GreetingOperation\b/.test(operations)) throw new Error('Package build did not replace the operation contract.');
    for (const framework of ['net10.0', 'net11.0']) dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
    if (folder === scriban) {
      const exported = path.join(folder, 'exported-templates');
      const packagedCli = path.join(scratch, 'packages', 'salep.clientgenerator', version, 'tools', 'net11.0', 'any', 'Salep.ClientGenerator.Cli.dll');
      dotnet(['exec', packagedCli, 'templates', '--output-directory', exported]);
      for (const name of ['CSharpClientMembers.scriban-cs', 'CSharpClientReadResponse.scriban-cs', 'CSharpSchemaInputProperty.scriban-cs']) {
        if (!existsSync(path.join(exported, name))) throw new Error(`Missing exported consumer fragment: ${name}`);
      }
      const members = path.join(folder, 'client-members.scriban-cs');
      writeFileSync(members, '    public string ConsumerMarker => "fragment one";\n');
      const response = path.join(folder, 'read-response.scriban-cs');
      writeFileSync(response, '// overridden response method\n{{ include "default:client.read-response" }}');
      settings.templates = { 'client.members': './client-members.scriban-cs', 'client.read-response': './read-response.scriban-cs' };
      writeFileSync(configPath, JSON.stringify(settings));
      const programPath = path.join(folder, 'Program.cs');
      const check = 'if (api.ConsumerMarker != "fragment one") throw new InvalidOperationException("Consumer fragment was not executed.");\n';
      writeFileSync(programPath, readFileSync(programPath, 'utf8').replace('var filter =', check + 'var filter ='));
      dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
      if (!readFileSync(path.join(generated, 'GraphQLClient.cs'), 'utf8').includes('overridden response method')) throw new Error('Nested replacement fragment did not render.');
      const tracked = JSON.parse(readFileSync(manifestPath, 'utf8')).Inputs;
      for (const name of ['../client-members.scriban-cs', '../read-response.scriban-cs']) {
        if (!(name in tracked)) throw new Error('Consumer fragment was not tracked in the manifest.');
      }
      for (const framework of ['net10.0', 'net11.0']) dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
      writeFileSync(members, readFileSync(members, 'utf8').replace('fragment one', 'fragment two'));
      writeFileSync(programPath, readFileSync(programPath, 'utf8').replace('fragment one', 'fragment two'));
      dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
      for (const framework of ['net10.0', 'net11.0']) dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
      console.log('Salep.ClientGenerator: exported fragments, default composition, tracking and runtime override invalidation verified');
      const template = path.join(folder, 'custom-schema.scriban-cs');
      writeFileSync(template, '// consumer template override one\n' + readFileSync(path.join(generated, 'SchemaTypes.cs'), 'utf8'));
      settings.templates.schema = './custom-schema.scriban-cs';
      writeFileSync(configPath, JSON.stringify(settings));
      dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
      writeFileSync(template, readFileSync(template, 'utf8').replace('override one', 'override two'));
      dotnet(['build', projectPath, '--configuration', 'Release', '--no-restore', '--disable-build-servers']);
      if (!readFileSync(path.join(generated, 'SchemaTypes.cs'), 'utf8').includes('consumer template override two')) throw new Error('Scriban template change did not invalidate the package build.');
      for (const framework of ['net10.0', 'net11.0']) dotnet(['run', '--project', projectPath, '--configuration', 'Release', '--framework', framework, '--no-build', '--no-restore']);
    }
  }
  console.log(`All ${version} packages passed isolated consumption with a fresh cache and local-only Salep package mapping.`);
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
