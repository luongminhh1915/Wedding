import { spawnSync } from 'node:child_process';
import { dirname, isAbsolute, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// An actual empty child-process value overrides client/.env on Windows as well.
// Nothing in the developer's environment or .env files is modified.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const client = resolve(root, 'client');
const destination = process.argv[2];
if (!destination || !isAbsolute(destination)) {
  throw new Error('Pass the absolute, newly created staging output directory.');
}
const env = { ...process.env, VITE_API_URL: '', VITE_STUDY_MODE: 'true' };
for (const args of [
  [resolve(client, 'node_modules/typescript/bin/tsc'), '-b'],
  [resolve(client, 'node_modules/vite/bin/vite.js'), 'build', '--outDir', destination],
]) {
  const result = spawnSync(process.execPath, args, { cwd: client, env, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
