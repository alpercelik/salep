import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const result = spawnSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' });
if (result.error || result.status !== 0) {
  throw result.error ?? new Error(result.stderr || 'Could not list repository scripts.');
}
const files = new Set(result.stdout.split('\0').filter(file => file && existsSync(path.join(root, file))));
const scripts = [...files].filter(file => /\.(sh|ps1)$/.test(file));
const missing = scripts.flatMap(file => {
  const counterpart = file.endsWith('.sh') ? file.replace(/\.sh$/, '.ps1') : file.replace(/\.ps1$/, '.sh');
  return files.has(counterpart) ? [] : [`${file} requires ${counterpart}`];
});
if (missing.length) {
  console.error(missing.join('\n'));
  process.exitCode = 1;
} else {
  console.log(`Script parity: ${scripts.length / 2} shell/PowerShell pairs found. Review behavior parity whenever either file changes.`);
}
