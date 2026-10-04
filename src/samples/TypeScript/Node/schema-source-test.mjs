import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { mkdtemp, readFile, realpath, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildSchema, graphql } from 'graphql';

const sdl = 'type Query { title: String! }';
const schema = buildSchema(sdl);
const requests = [];
const server = createServer(async (request, response) => {
  requests.push(request.method);
  assert.equal(request.headers.authorization, 'Bearer schema-test');
  if (request.method === 'POST') {
    let body = '';
    for await (const chunk of request) body += chunk;
    const query = JSON.parse(body).query;
    assert.ok(query.includes('__schema'));
    const result = await graphql({ schema, source: query });
    assert.equal(result.errors, undefined);
    response.end(JSON.stringify(result));
  } else response.end(sdl);
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const directory = await realpath(await mkdtemp(join(tmpdir(), 'salep-schema-source-')));
const dll = process.env.SALEP_TYPESCRIPT_CLI ?? fileURLToPath(new URL('../../../../artifacts/bin/Salep.ClientGenerator.TypeScript.Cli/debug_net10.0/salep-typescript.dll', import.meta.url));
try {
  await writeFile(join(directory, 'operation.graphql'), 'query Read { title }');
  for (const method of ['get', 'introspection']) {
    await writeFile(join(directory, 'salep.json'), JSON.stringify({ version: 1, target: 'typescript',
      schema: { url: `http://127.0.0.1:${server.address().port}/schema?private=value`, method, headers: { Authorization: 'Bearer schema-test' } },
      documents: 'operation.graphql', output: method }));
    await new Promise((resolve, reject) => {
      const child = spawn('dotnet', [dll, 'generate', '--config', join(directory, 'salep.json')], { stdio: ['ignore', 'pipe', 'pipe'] });
      let error = '';
      child.stderr.on('data', chunk => { error += chunk; });
      child.on('error', reject);
      child.on('exit', status => status === 0 ? resolve() : reject(new Error(error)));
    });
    const manifest = await readFile(join(directory, method, '.salep.manifest.json'), 'utf8');
    assert.ok(!manifest.includes('Bearer') && !manifest.includes('127.0.0.1') && !manifest.includes('private=value'));
    assert.equal(Object.keys(JSON.parse(manifest).RemoteInputs).length, 1);
    assert.ok((await readFile(join(directory, method, 'gql', 'graphql.ts'), 'utf8')).includes('"title": string'));
  }
  assert.deepEqual(requests, ['GET', 'POST']);
  console.log('Remote schema loading passed real HTTP SDL and introspection requests, headers and private manifest checks');
} finally {
  await new Promise(resolve => server.close(resolve));
  await rm(directory, { recursive: true, force: true });
}
