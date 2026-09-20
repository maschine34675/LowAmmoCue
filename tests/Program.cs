using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EFT;
using EFT.InventoryLogic;
using LowAmmoCue;

if (args.Length == 2 && args[0] == "--inspect-binary")
{
    var identity = AssemblyName.GetAssemblyName(args[1]);
    if (identity.Name != "maschine-LowAmmoCue" || identity.Version != new Version(1, 0, 0, 0))
        throw new Exception("Unexpected compiled plugin identity/version.");
    var assembly = Assembly.LoadFile(Path.GetFullPath(args[1]));
    if (!assembly.GetManifestResourceNames().SequenceEqual(new[] { SoundFiles.EmbeddedResourceName }))
        throw new Exception("Unexpected production DLL resources.");
    using var embedded = assembly.GetManifestResourceStream(SoundFiles.EmbeddedResourceName)
        ?? throw new Exception("Production DLL is missing the embedded click.");
    if (embedded.Length != 45700 || Convert.ToHexString(SHA256.HashData(embedded)) != "BE7421385F4C64E2570D2BAFDB0A98FFAF6A803B655A27792B31909660EB7636")
        throw new Exception("Production DLL embedded WAV differs from the original.");
    embedded.Position = 0;
    var embeddedWave = PcmWave.Read(embedded);
    if (embeddedWave.Frames != 22785 || embeddedWave.Rate != 44100 || embeddedWave.Channels != 1)
        throw new Exception("Production DLL embedded WAV cannot be decoded as the original click.");
    Console.WriteLine("LOW_AMMO_BINARY_VERIFIED");
    return;
}

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks++;
}
bool Cue(int left, int capacity = 31, int percent = 20)
    => CuePolicy.ShouldPlay(left, capacity, WarningMode.Percentage, percent, 5);

var diagnosticOutput = new List<string>();
var diagnostics = new Diagnostics(message => diagnosticOutput.Add("info:" + message), message => diagnosticOutput.Add("warning:" + message));
var diagnosticError = new InvalidOperationException("fixture");
Check(!diagnostics.Enabled && diagnostics.TestSound.Browsable == false && diagnostics.ReloadSound.Browsable == false,
    "diagnostics start disabled with both actions hidden");
diagnostics.Info("startup");
diagnostics.WarnOnce("fixture", diagnosticError);
Check(diagnosticOutput.Count == 0, "disabled diagnostics emit neither info nor warnings");
diagnostics.SetEnabled(true);
Check(diagnostics.Enabled && diagnostics.TestSound.Browsable == true && diagnostics.ReloadSound.Browsable == true,
    "enabling debug reveals both action controls");
diagnostics.Info("loaded");
diagnostics.WarnOnce("fixture", diagnosticError);
diagnostics.WarnOnce("fixture", diagnosticError);
Check(diagnosticOutput.SequenceEqual(new[] { "info:loaded", "warning:LowAmmoCue fixture: fixture" }),
    "enabled diagnostics log info and warn once without caching suppressed warnings");
diagnostics.SetEnabled(false);
diagnostics.Info("hidden");
diagnostics.WarnOnce("other", diagnosticError);
Check(diagnosticOutput.Count == 2 && diagnostics.TestSound.Browsable == false && diagnostics.ReloadSound.Browsable == false,
    "disabling debug hides both actions and immediately silences all output");
diagnostics.SetEnabled(true);
diagnostics.WarnOnce("fixture", diagnosticError);
Check(diagnosticOutput.Count == 3, "new debug session may report a previously seen warning");
diagnostics.ClearWarnings();
diagnostics.WarnOnce("fixture", diagnosticError);
Check(diagnosticOutput.Count == 4, "successful sound reload allows a fresh diagnostic warning");

// These are the full remaining-ammo sequences of a 30+1 rifle and small weapons.
Check(Enumerable.Range(0, 31).Where(n => Cue(n)).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }), "30+1 rifle: exactly last six shots");
Check(!Cue(6) && Cue(5), "threshold boundary is not one shot early");
Check(Cue(0), "actual final shot still clicks");
Check(!Cue(31) && !Cue(-1), "invalid/unconsumed counts do not click");
Check(Cue(0, 6) && !Cue(1, 6), "six-shot revolver: final one at 20 percent");
Check(Cue(0, 2) && !Cue(1, 2), "two barrels: minimum one warning shot");
Check(!Cue(0, 1) && !Cue(0, 0), "single-shot/no-capacity weapons excluded");
Check(Enumerable.Range(0, 101).Count(n => Cue(n, 101)) == 20, "100+1 magazine scales by capacity");
Check(CuePolicy.ShouldPlay(4, 31, WarningMode.Rounds, 20, 5)
      && !CuePolicy.ShouldPlay(5, 31, WarningMode.Rounds, 20, 5), "fixed five-shot mode");
Check(CuePolicy.ShouldPlay(1, 2, WarningMode.Rounds, 20, 100), "fixed count limited to weapon capacity");
Check(!Cue(0, 31, 0) && !Cue(0, 31, 101)
      && !CuePolicy.ShouldPlay(0, 31, (WarningMode)99, 20, 5), "bad settings fail closed");
Check(CuePolicy.Volume(float.NaN) == 0 && CuePolicy.Volume(float.PositiveInfinity) == 0
      && CuePolicy.Volume(-1) == 0 && CuePolicy.Volume(2) == 1, "invalid volume cannot spike output");

float Gain(int left, int capacity = 31, WarningMode mode = WarningMode.Percentage, int percent = 20, int rounds = 5)
    => CuePolicy.ShotVolumeScale(left, capacity, mode, percent, rounds);
bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.00001f;
var rifleGains = Enumerable.Range(0, 6).Reverse().Select(n => Gain(n)).ToArray();
Check(rifleGains.Zip(new[] { 0.35f, 0.48f, 0.61f, 0.74f, 0.87f, 1f }, Near).All(match => match),
    "six warning clicks rise evenly from 35 percent to full volume");
Check(Gain(6) == 0f && Gain(31) == 0f && Gain(-1) == 0f, "no gain outside the warning window");
Check(Gain(0, 6) == 1f && Gain(0, 2) == 1f && Gain(0, 31, WarningMode.Rounds, rounds: 1) == 1f,
    "one warning shot uses full volume without division by zero");
Check(Near(Gain(4, mode: WarningMode.Rounds), 0.35f)
      && Near(Gain(2, mode: WarningMode.Rounds), 0.675f) && Gain(0, mode: WarningMode.Rounds) == 1f,
    "fixed five-shot mode scales across its own warning window");
Check(Near(Gain(1, 2, WarningMode.Rounds, rounds: 100), 0.35f)
      && Gain(0, 2, WarningMode.Rounds, rounds: 100) == 1f, "ramp uses capacity-clamped warning count");
Check(Gain(0, 1) == 0f && Gain(0, 0) == 0f && Gain(0, percent: 0) == 0f && Gain(0, percent: 101) == 0f
      && Gain(0, mode: (WarningMode)99) == 0f && Gain(0, mode: WarningMode.Rounds, rounds: 0) == 0f,
    "invalid or excluded state stays silent with progressive volume");
Check(Gain(0) == 1f && Gain(30) == 0f && Near(Gain(5), 0.35f) && Near(Gain(1), 0.87f),
    "reload and partially empty weapon switches derive gain directly from current ammunition");
foreach (int size in new[] { 2, 6, 31, 61, 101 })
foreach (var mode in new[] { WarningMode.Percentage, WarningMode.Rounds })
foreach (int setting in new[] { 1, 20, 100 })
{
    var gains = Enumerable.Range(0, size).Reverse().Select(n => Gain(n, size, mode, setting, setting)).ToArray();
    Check(gains.All(n => float.IsFinite(n) && n >= 0f && n <= 1f)
          && gains.Zip(gains.Skip(1), (before, after) => before <= after).All(increasing => increasing)
          && gains[^1] == 1f, $"bounded monotonic volume ending at maximum: capacity={size}, mode={mode}, setting={setting}");
}

var gun = new Weapon { Magazine = new Magazine { Count = 4, MaxCount = 30 }, Chambers = new object[1], ChamberAmmoCount = 1 };
var controller = new Player.FirearmController { Item = gun };
var local = new Player { IsYourPlayer = true, HealthController = new Player.Health { IsAlive = true }, HandsController = controller };
bool Observe(Player p, Player.FirearmController f, IWeapon w) => ShotObserver.TryObserve(p, f, w, out _, out _);
Check(ShotObserver.TryObserve(local, controller, gun, out int remaining, out int capacity) && remaining == 5 && capacity == 31,
    "observer retains already-consumed post-shot count including chamber");
Check(Cue(remaining, capacity), "positive local low-ammo shot control");
local.IsAI = true;
Check(!Observe(local, controller, gun), "AI excluded");
local.IsAI = false;
local.IsYourPlayer = false;
Check(!Observe(local, controller, gun), "remote human excluded");
local.IsYourPlayer = true;
local.HealthController.IsAlive = false;
Check(!Observe(local, controller, gun), "dead player excluded");
local.HealthController.IsAlive = true;
Check(!Observe(null, controller, gun) && !Observe(local, null, gun), "missing player/controller excluded");
Check(!Observe(local, controller, new Weapon()) && !Observe(local, new Player.FirearmController { Item = gun }, gun), "weapon/owner identity mismatch excluded");
Check(!Observe(local, controller, new Launcher()), "underbarrel cannot reuse primary magazine");
gun.IsGrenadeLauncher = true;
Check(!Observe(local, controller, gun), "standalone grenade launcher excluded");
gun.IsGrenadeLauncher = false;
gun.IsStationaryWeapon = true;
Check(!Observe(local, controller, gun), "stationary gun excluded");
gun.IsStationaryWeapon = false;
gun.IsOneOff = true;
Check(!Observe(local, controller, gun), "one-off weapons excluded");
gun.IsOneOff = false;
gun.Magazine = new Magazine { Count = 0, MaxCount = 30 };
gun.ChamberAmmoCount = 0;
Check(ShotObserver.TryObserve(local, controller, gun, out remaining, out capacity) && remaining == 0, "empty-after-real-shot admitted");
gun.Magazine = new Magazine { Count = 29, MaxCount = 30 };
gun.ChamberAmmoCount = 1;
Check(ShotObserver.TryObserve(local, controller, gun, out remaining, out capacity) && !Cue(remaining, capacity), "fresh magazine stops warning without stale state");
gun.Magazine = new CylinderMagazine { Count = 1, MaxCount = 6 };
gun.Chambers = new object[1];
gun.ChamberAmmoCount = 0;
Check(ShotObserver.TryObserve(local, controller, gun, out remaining, out capacity) && remaining == 1 && capacity == 6,
    "cylinder capacity is not counted twice");
gun.Magazine = null;
gun.Chambers = new object[2];
gun.ChamberAmmoCount = 1;
Check(ShotObserver.TryObserve(local, controller, gun, out remaining, out capacity) && remaining == 1 && capacity == 2, "magazineless multi-barrel count");

byte[] MakeWave(ushort bits = 16, ushort channels = 1, byte[] samples = null, bool metadata = false, bool duplicateData = false)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
    void Tag(string tag) => writer.Write(Encoding.ASCII.GetBytes(tag));
    Tag("RIFF"); writer.Write(0); Tag("WAVE");
    Tag("fmt "); writer.Write(16); writer.Write((ushort)1); writer.Write(channels); writer.Write(44100);
    ushort align = (ushort)(channels * bits / 8);
    writer.Write(44100 * align); writer.Write(align); writer.Write(bits);
    var data = samples ?? new byte[] { 0, 128, 255, 127 };
    for (int i = 0; i < (duplicateData ? 2 : 1); i++)
    { Tag("data"); writer.Write(data.Length); writer.Write(data); if (data.Length % 2 != 0) writer.Write((byte)0); }
    if (metadata) { Tag("LIST"); writer.Write(3); writer.Write(new byte[] { 1, 2, 3, 0 }); }
    stream.Position = 4; writer.Write((int)stream.Length - 8);
    return stream.ToArray();
}
PcmWave Decode(byte[] bytes) { using var stream = new MemoryStream(bytes); return PcmWave.Read(stream); }
void Reject(byte[] bytes, string name)
{
    try { Decode(bytes); }
    catch (InvalidDataException) { checks++; return; }
    throw new Exception("FAILED: decoder accepted " + name);
}
var mono = Decode(MakeWave(metadata: true));
Check(mono.Rate == 44100 && mono.Channels == 1 && mono.Frames == 2
      && mono.Samples[0] == -1 && mono.Samples[1] > 0.9999f, "PCM16 endpoints and odd metadata padding");
var stereo24 = Decode(MakeWave(24, 2, new byte[] { 0, 0, 128, 255, 255, 127 }));
Check(stereo24.Channels == 2 && stereo24.Frames == 1 && stereo24.Samples[0] == -1
      && stereo24.Samples[1] > 0.99999f, "PCM24 signed stereo samples");
Reject(new byte[44], "invalid header");
var corrupt = MakeWave(); corrupt[4]++;
Reject(corrupt, "wrong RIFF length");
corrupt = MakeWave(); corrupt[40] = 255;
Reject(corrupt, "out of bounds data chunk");
corrupt = MakeWave(); corrupt[20] = 3;
Reject(corrupt, "IEEE float disguised as PCM16");
corrupt = MakeWave(); corrupt[28] = 0;
Reject(corrupt, "wrong byte rate");
Reject(MakeWave(samples: new byte[] { 1, 1, 1 }), "incomplete PCM frame");
Reject(MakeWave(samples: new byte[8]), "silent file");
Reject(MakeWave(duplicateData: true), "duplicate data chunks");
var tooLong = new byte[44100 * 2 * 2 + 2]; tooLong[1] = 1;
Reject(MakeWave(samples: tooLong), "more than two seconds");
Reject(new byte[1200001], "unbounded input");
// Recheck the valid control after negative fixtures to expose fail-all decoders.
Check(Decode(MakeWave()).Frames == 2, "decoder still accepts positive control");

string fixtureDirectory = Path.Combine(Path.GetTempPath(), "LowAmmoCue-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDirectory);
try
{
    string defaultSource = SoundFiles.EmbeddedSource;
    string customFile = SoundFiles.OverridePath(fixtureDirectory);
    var selection = SoundFiles.Load(fixtureDirectory);
    Check(!Directory.EnumerateFileSystemEntries(fixtureDirectory).Any() && selection.Path == defaultSource
          && selection.OverrideError == null && selection.Wave.Frames == 22785
          && selection.Wave.Rate == 44100 && selection.Wave.Channels == 1,
        "original CS click loads from embedded resource with no external files");
    string stamp = SoundFiles.Fingerprint(fixtureDirectory);
    Check(SoundFiles.Fingerprint(fixtureDirectory) == stamp, "unchanged files do not request repeated decoding");
    File.WriteAllBytes(customFile, MakeWave(metadata: true));
    Check(SoundFiles.Fingerprint(fixtureDirectory) != stamp, "adding override is detected");
    selection = SoundFiles.Load(fixtureDirectory);
    Check(selection.Path == customFile && selection.OverrideError == null && selection.Wave.Frames == 2,
        "valid override beside DLL takes priority");
    stamp = SoundFiles.Fingerprint(fixtureDirectory);
    File.WriteAllBytes(customFile, new byte[44]);
    Check(SoundFiles.Fingerprint(fixtureDirectory) != stamp, "changed override size is detected");
    selection = SoundFiles.Load(fixtureDirectory);
    Check(selection.Path == defaultSource && selection.OverrideError is InvalidDataException,
        "malformed override falls back to embedded audio with diagnostic");
    Check(!SoundFiles.RequiresRetry(selection.OverrideError), "invalid format waits for file changes instead of looping");
    stamp = SoundFiles.Fingerprint(fixtureDirectory);
    File.SetLastWriteTimeUtc(customFile, File.GetLastWriteTimeUtc(customFile).AddSeconds(2));
    Check(SoundFiles.Fingerprint(fixtureDirectory) != stamp, "same-size replacement timestamp is detected");
    File.WriteAllBytes(customFile, MakeWave());
    Check(SoundFiles.Load(fixtureDirectory).Path == customFile, "corrected override becomes active again");
    stamp = SoundFiles.Fingerprint(fixtureDirectory);
    using (var locked = new FileStream(customFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        selection = SoundFiles.Load(fixtureDirectory);
        Check(selection.Path == defaultSource && selection.OverrideError is IOException,
            "temporarily locked override falls back safely");
        Check(SoundFiles.RequiresRetry(selection.OverrideError), "locked override schedules another load attempt");
    }
    selection = SoundFiles.Load(fixtureDirectory);
    Check(SoundFiles.Fingerprint(fixtureDirectory) == stamp && selection.Path == customFile
          && !SoundFiles.RequiresRetry(selection.OverrideError), "retry restores unlocked override with unchanged metadata");
    stamp = SoundFiles.Fingerprint(fixtureDirectory);
    File.Delete(customFile);
    Check(SoundFiles.Fingerprint(fixtureDirectory) != stamp && SoundFiles.Load(fixtureDirectory).Path == defaultSource,
        "removing override restores embedded sound");
    stamp = SoundFiles.Fingerprint(fixtureDirectory);
    Directory.CreateDirectory(Path.Combine(fixtureDirectory, "sounds"));
    File.WriteAllBytes(Path.Combine(fixtureDirectory, "sounds", "lowammo.wav"), MakeWave());
    Check(SoundFiles.Load(fixtureDirectory).Wave.Frames == 22785 && SoundFiles.Fingerprint(fixtureDirectory) == stamp,
        "legacy loose default neither replaces embedded CS sound nor triggers reloads");
    File.WriteAllBytes(customFile, MakeWave());
    Check(SoundFiles.Load(fixtureDirectory).Path == customFile, "explicit override still takes precedence over embedded and legacy sounds");
}
finally { Directory.Delete(fixtureDirectory, recursive: true); }

using (var original = typeof(SoundFiles).Assembly.GetManifestResourceStream(SoundFiles.EmbeddedResourceName))
{
    Check(original != null && Convert.ToHexString(SHA256.HashData(original)) == "BE7421385F4C64E2570D2BAFDB0A98FFAF6A803B655A27792B31909660EB7636",
        "embedded test source retains every original WAV byte including metadata");
}
Console.WriteLine($"LOW_AMMO_BEHAVIOR_VERIFIED ({checks} assertions; native runtime requires in-game check)");
