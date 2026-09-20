import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('..', import.meta.url));
function run(command, args, expected) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8' });
  process.stdout.write(result.stdout ?? '');
  process.stderr.write(result.stderr ?? '');
  if (result.error) throw result.error;
  assert.equal(result.status, 0, `${command} failed`);
  if (expected) assert.ok(result.stdout.includes(expected), `missing success marker ${expected}`);
}
run('dotnet', ['run', '--project', 'tests/LowAmmoCue.Checks.csproj', '-c', 'Release'], 'LOW_AMMO_BEHAVIOR_VERIFIED');
run('dotnet', ['build', 'tests/HookChecks/HookChecks.csproj', '-c', 'Release', '--nologo']);
run(path.join(root, 'tests/HookChecks/bin/Release/net48/HookChecks.exe'), [], 'LOW_AMMO_HOOK_VERIFIED');
console.log('LOW_AMMO_ALL_BEHAVIOR_VERIFIED');
