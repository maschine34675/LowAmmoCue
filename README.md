# LowAmmoCue

Version **1.0.0**, local client build for **SPT 4.1.5**.

Adds a short metallic click to the final shots of your held firearm. It uses the same sound for all supported weapons. The cue is local to you; it creates no AI hearing event and does not change ammunition, damage, firing, reloads, or malfunctions.

Clicks get louder as ammunition runs out: the first warning click uses 35% of your configured volume, rising evenly to 100% on the last shot. If the warning covers just one shot, it uses 100%. Reloads and weapon switches immediately follow the current ammunition count.

## Local installation

Builds install to `D:\SPT41\BepInEx\plugins\LowAmmoCue\` by default. Restart the game after installing the DLL. A server restart is unnecessary.

## Settings (F12 → maschine-LowAmmoCue)

| Setting | Default | Effect |
|---|---|---|
| Enable Low Ammo Clicks | true | Enable shot feedback. |
| Warning Mode | Percentage | Percentage of weapon capacity or fixed final-shot count. |
| Warning Threshold (%) | 20 | Last 20% of total capacity, rounded down with a minimum of one shot. |
| Warning Shots (rounds) | 5 | Final five shots when using Rounds mode; displayed as a round count. |
| Maximum Click Volume | 50% | Maximum volume per click on the last shot, additionally controlled by the game's master volume. Existing saved volume values are retained. |
| Enable Debug Logging | false | Enable mod diagnostic logs and reveal the two diagnostic controls below. |
| Test Click at Maximum Volume | false | Visible and active only with debug enabled. Play one click at the configured maximum volume; resets automatically. |
| Reload Click Sound | false | Visible and active only with debug enabled. Recheck the override and embedded default immediately; resets automatically. |

The menu uses readable labels and orders sections as General, Warning, Audio, Diagnostics. Stored `.cfg` keys and section names are unchanged: `Rounds` and `Percentage` display their actual integer values, while `Volume` remains a 0–1 value displayed as a percentage.

With **Enable Debug Logging** off, this mod writes no diagnostic messages, including startup, playback, loading or warning messages, and hides/disables its test and manual reload controls. Enabling debug reveals those controls under Audio without restarting the game. Automatic sound detection and normal gameplay clicks work independently of debug mode. BepInEx's own plugin-loading messages are controlled by BepInEx.

Capacity includes the magazine and chamber(s). A 30-round magazine plus one chamber gives capacity 31, so 20% means **six warning shots**: the hook sees 5, 4, 3, 2, 1, 0 rounds remaining after those shots. A revolver uses its cylinder capacity alone. The last real shot clicks too; subsequently pressing fire on an empty weapon does not produce additional mod clicks.

For those six warning shots, the volume factors are **35%, 48%, 61%, 74%, 87%, 100%**. Each overlapping click keeps its own factor during automatic fire; their combined output can be louder than a single click.

## Supported behavior

- Normal magazines, internal magazines, revolvers, and multi-barrel firearms.
- Semi-auto, burst and automatic fire; buckshot generates one cue per shell, not per pellet.
- Drawing, checking, unloading or replacing a magazine does not trigger a cue.
- Other players/bots, stationary weapons, grenade launchers, underbarrel launchers and single-shot capacity are excluded.
- Simultaneous double-barrel fire may produce one overlapping click per fired barrel; both observe the final shared ammunition state.
- A misfire produces no cue because it never reaches the actual-shot hook. A round that fires before a jam can still click.
- Clicks use an independent 2D audio source on the game's Master mixer and can overlap during rapid fire. Playback stops on pause, disabling, player death/change or leaving the raid. Audio-device changes recreate the cached clip.

## Build and checks

Requires a .NET SDK and the local SPT 4.1 game assemblies. Data checks target .NET 10; the real-Harmony hook harness uses the installed .NET Framework 4.8 runtime/developer pack. The game's Mono-targeted Harmony cannot initialize under .NET 10.

Provide the original recording as `local-test/lowammo.wav` before building. The source file is intentionally ignored by Git. Both the plugin and test assembly embed it; the build checks verify the actual production DLL resource against the original hash. Current output is under `artifacts/local-embedded/` and contains the DLL and rights/provenance documents, with no separate WAV. This local build is not a cleared public-release package.

```powershell
node tools/verify-behavior.mjs
dotnet build LowAmmoCue.csproj -c Release
node tools/verify-build.mjs
node tools/verify-deployment.mjs
```

Use `-p:DeployOnBuild=false` for a build that only stages locally. Override `-p:SptRoot=D:\SPT41` for a different installation. Offline source checks do not certify Unity/Harmony runtime behavior or audible quality. See [TESTING.md](TESTING.md) for the listening checklist and [REVIEW.md](REVIEW.md) for the vanilla hook evidence.

## Optional external sound override

The original CS click works without additional files. To replace it, place a compatible **lowammo.wav directly beside the plugin DLL**:

```text
BepInEx/plugins/LowAmmoCue/
  maschine-LowAmmoCue.dll
  lowammo.wav          <- optional replacement; not required
```

A valid override takes priority automatically, including when added while the game is running. Changes are checked once per second even with debug disabled. **Reload Click Sound**, available after enabling debug, forces a fresh read, including replacements whose file size and timestamp are unchanged. Remove or rename the override to return to the embedded CS sound. A malformed override falls back to the embedded sound and logs a warning only when debug is enabled; temporary file locks are retried automatically. Failed loads retain the last valid clip when available.

**Use recordings only with the necessary rights and on your own responsibility.** The current DLL contains the user-selected CS recording, without granting rights to it. It is not downloaded or extracted at runtime. Separate user overrides are left untouched by installation.

Supported files are non-silent, uncompressed PCM16/PCM24 mono/stereo WAVs at 8–96 kHz, at most two seconds and 1.2 MB. Legacy `sounds/lowammo.wav` files from 0.2.0 are no longer read.
