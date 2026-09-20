import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('..', import.meta.url));
const spt = path.resolve(process.env.LOW_AMMO_SPT_ROOT ?? path.join(root, '../..'));
assert.ok(existsSync(path.join(spt, 'EscapeFromTarkov.exe')));
const live = path.join(spt, 'BepInEx/plugins/LowAmmoCue');
const stage = path.join(root, 'artifacts/local-embedded/BepInEx/plugins/LowAmmoCue');
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex');
const names = ['maschine-LowAmmoCue.dll', 'LICENSE', 'AUDIO-LICENSES.md', 'SOURCES.json'];
for (const name of names) assert.equal(hash(path.join(live, name)), hash(path.join(stage, name)), `mismatch: ${name}`);
const manifest = JSON.parse(readFileSync(path.join(root, 'audio/SOURCES.json'), 'utf8'));
assert.equal(hash(path.join(root, manifest.localCopy)), manifest.sha256.toLowerCase());
assert.equal(manifest.redistributionApproved, false);
function dllCopies(dir) {
  return readdirSync(dir).flatMap(name => {
    const child = path.join(dir, name);
    return statSync(child).isDirectory() ? dllCopies(child) : name.toLowerCase() === 'maschine-lowammocue.dll' ? [child] : [];
  });
}
assert.deepEqual(dllCopies(path.join(spt, 'BepInEx/plugins')), [path.join(live, 'maschine-LowAmmoCue.dll')]);
console.log(`LOW_AMMO_DEPLOYMENT_VERIFIED (DLL sha256 ${hash(path.join(live, 'maschine-LowAmmoCue.dll'))})`);
