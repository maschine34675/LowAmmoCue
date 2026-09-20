import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('..', import.meta.url));
function run(args) {
  const result = spawnSync('dotnet', args, {cwd: root, encoding: 'utf8'});
  process.stdout.write(result.stdout ?? '');
  process.stderr.write(result.stderr ?? '');
  if (result.error) throw result.error;
  assert.equal(result.status, 0, `dotnet ${args.join(' ')} failed`);
}
run(['build', 'LowAmmoCue.csproj', '-c', 'Release', '-p:DeployOnBuild=false', '--nologo']);
const stage = path.join(root, 'artifacts/local-embedded/BepInEx/plugins/LowAmmoCue');
function files(dir, prefix = '') {
  return readdirSync(dir).flatMap(name => {
    const child = path.join(dir, name), relative = prefix + name;
    return statSync(child).isDirectory() ? files(child, relative + '/') : [relative];
  }).sort();
}
assert.deepEqual(files(stage), ['LICENSE', 'AUDIO-LICENSES.md', 'SOURCES.json', 'maschine-LowAmmoCue.dll'].sort());
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex');
const dll = path.join(root, 'bin/Release/netstandard2.1/maschine-LowAmmoCue.dll');
assert.equal(hash(dll), hash(path.join(stage, 'maschine-LowAmmoCue.dll')));
const manifest = JSON.parse(readFileSync(path.join(root, 'audio/SOURCES.json'), 'utf8'));
assert.equal(hash(path.join(root, manifest.localCopy)), manifest.sha256.toLowerCase());
assert.equal(manifest.resourceName, 'LowAmmoCue.Audio.lowammo.wav');
assert.equal(manifest.redistributionApproved, false);
assert.equal(manifest.userOverride.includedAsSeparateFile, false);
for (const name of ['LICENSE', 'AUDIO-LICENSES.md', 'SOURCES.json']) {
  const source = name === 'SOURCES.json' ? path.join(root, 'audio', name) : path.join(root, name);
  assert.equal(hash(path.join(stage, name)), hash(source), `stale documentation: ${name}`);
}
const tracked = spawnSync('git', ['ls-files', '--', 'local-test/lowammo.wav', 'lowammo.wav'], {cwd: root, encoding: 'utf8'});
assert.equal(tracked.status, 0);
assert.equal(tracked.stdout.trim(), '', 'CS source audio must stay untracked');
const ignored = spawnSync('git', ['check-ignore', 'lowammo.wav'], {cwd: root, encoding: 'utf8'});
assert.equal(ignored.status, 0, 'CS source audio must be ignored');
run(['run', '--project', 'tests/LowAmmoCue.Checks.csproj', '-c', 'Release', '--', '--inspect-binary', dll]);
console.log('LOW_AMMO_BUILD_VERIFIED');
