using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LowAmmoCue;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.maschine.LowAmmoCue";
    public const string PluginName = "maschine-LowAmmoCue";
    public const string PluginVersion = "1.0.0";
    internal static Plugin Instance;
    private Diagnostics _diagnostics;
    private bool _refreshConfigMenu;
    private readonly ClickOutput _output = new();
    private Harmony _harmony;
    private PcmWave _wave;
    private AudioClip _clip;
    private string _soundPath;
    private string _pluginDirectory, _soundFilesStamp, _loadedSoundFilesStamp;
    private float _nextSoundCheck;
    private bool _retrySoundLoad;
    private Player _lastLocal;
    private volatile bool _audioReset;
    private bool _ready;
    private int _playRequests;
    private ConfigEntry<bool> _enabled, _testSound, _reloadSound, _debugLog;
    private ConfigEntry<float> _volume;
    private ConfigEntry<WarningMode> _mode;
    private ConfigEntry<int> _percent, _rounds;

    private void Awake()
    {
        Instance = this;
        _enabled = Config.Bind("General", "Enabled", true,
            Tagged("Enable Low Ammo Clicks", 100, "Play a click on the final shots of your current weapon."));
        _mode = Config.Bind("Warning", "Mode", WarningMode.Percentage,
            Tagged("Warning Mode", 100, "Use a percentage of loaded capacity or a fixed number of final shots."));
        _percent = Config.Bind("Warning", "Percentage", 20,
            Tagged("Warning Threshold (%)", 90, "Final percentage of total weapon capacity (magazine plus chamber). Rounded down, minimum one shot.", new AcceptableValueRange<int>(1, 100)));
        _rounds = Config.Bind("Warning", "Rounds", 5,
            Tagged("Warning Shots (rounds)", 80, "Number of final shots that click in Rounds mode.", new AcceptableValueRange<int>(1, 100)));
        _volume = Config.Bind("Audio", "Volume", 0.35f,
            Tagged("Maximum Click Volume", 100, "Maximum click volume on the last shot. Earlier warning clicks rise from 35% of this value. Also respects the game's master volume.", new AcceptableValueRange<float>(0f, 1f), showRangeAsPercent: true));
        _debugLog = Config.Bind("Diagnostics", "Debug logging", false,
            Tagged("Enable Debug Logging", 100, "Enable diagnostic logs and show the Test Click and Reload Click Sound controls."));
        _diagnostics = new Diagnostics(message => Logger.LogInfo(message), message => Logger.LogWarning(message));
        _diagnostics.SetEnabled(_debugLog.Value);
        _testSound = Config.Bind("Audio", "Test sound", false,
            new ConfigDescription("Play the sound once at maximum configured volume. Requires Enable Debug Logging and resets automatically.", null, _diagnostics.TestSound));
        _reloadSound = Config.Bind("Audio", "Reload sound file", false,
            new ConfigDescription("Reload now. A valid lowammo.wav beside the DLL takes priority over the embedded click. Requires Enable Debug Logging and resets automatically. File changes are always detected automatically; supply custom audio only with the necessary rights.", null, _diagnostics.ReloadSound));
        _testSound.Value = _reloadSound.Value = false;
        _debugLog.SettingChanged += OnDebugLoggingChanged;
        _refreshConfigMenu = true;
        _pluginDirectory = Path.GetDirectoryName(Info.Location);
        _soundPath = SoundFiles.EmbeddedSource;
        ReloadClip();
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        try
        {
            var target = ShotPatch.Target ?? throw new MissingMethodException("SPT 4.1 FirearmController.InitiateShot was not found.");
            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(ShotPatch), nameof(ShotPatch.Prefix)),
                postfix: new HarmonyMethod(typeof(ShotPatch), nameof(ShotPatch.Postfix)));
            _ready = true;
            _diagnostics.Info($"{PluginName} {PluginVersion} loaded. Local shot hook active; sound={_soundPath}.");
        }
        catch (Exception ex) { WarnOnce("initialization", ex); }
    }

    private static ConfigDescription Tagged(string displayName, int order, string description,
        AcceptableValueBase acceptableValues = null, bool showRangeAsPercent = false)
        => new(description, acceptableValues, new ConfigurationManagerAttributes
        {
            DispName = displayName,
            Order = order,
            ShowRangeAsPercent = showRangeAsPercent
        });

    private static Player LocalPlayer()
        => Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance?.MainPlayer : null;

    internal ShotSnapshot CaptureShot(Player.FirearmController firearm, IWeapon weapon)
    {
        if (!_ready || !_enabled.Value || CuePolicy.Volume(_volume.Value) <= 0 || AudioListener.pause || Time.timeScale <= 0) return default;
        var local = LocalPlayer();
        if (!ShotObserver.TryObserve(local, firearm, weapon, out int remaining, out int capacity)) return default;
        return new ShotSnapshot(local, remaining, capacity);
    }

    internal void OnShot(Player.FirearmController firearm, IWeapon weapon, ShotSnapshot snapshot)
    {
        if (!_ready || snapshot.Local == null || !_enabled.Value || CuePolicy.Volume(_volume.Value) <= 0
            || AudioListener.pause || Time.timeScale <= 0 || !ReferenceEquals(snapshot.Local, LocalPlayer())
            || !ReferenceEquals(snapshot.Local.HandsController, firearm) || !ReferenceEquals(firearm.Item, weapon)) return;
        ObserveLocal(snapshot.Local);
        float shotVolumeScale = CuePolicy.ShotVolumeScale(snapshot.Remaining, snapshot.Capacity, _mode.Value, _percent.Value, _rounds.Value);
        if (shotVolumeScale <= 0f) return;
        PlayClick(shotVolumeScale);
        _diagnostics.Info($"Low-ammo shot: remaining={snapshot.Remaining}, capacity={snapshot.Capacity}, mode={_mode.Value}.");
    }

    private void ObserveLocal(Player local)
    {
        if (ReferenceEquals(local, _lastLocal)) return;
        _output.Dispose();
        _lastLocal = local;
    }

    private void Update()
    {
        if (_enabled == null) return;
        if (_refreshConfigMenu) RefreshConfigurationMenu();
        try
        {
            if (_audioReset)
            {
                _audioReset = false;
                _output.Dispose();
                DestroyClip();
                if (_wave != null) _clip = CreateClip(_wave);
            }
            var local = LocalPlayer();
            ObserveLocal(local);
            bool enabled = _enabled.Value && CuePolicy.Volume(_volume.Value) > 0 && !AudioListener.pause && Time.timeScale > 0;
            _output.Tick(enabled && (local == null || local.HealthController?.IsAlive == true), _volume.Value);
            bool reloadSound = _diagnostics.Enabled && _reloadSound.Value;
            bool forceReload = reloadSound;
            if (_reloadSound.Value) _reloadSound.Value = false;
            if (Time.unscaledTime >= _nextSoundCheck)
            {
                _nextSoundCheck = Time.unscaledTime + 1f;
                reloadSound |= _retrySoundLoad || SoundFiles.Fingerprint(_pluginDirectory) != _soundFilesStamp;
            }
            if (reloadSound) ReloadClip(forceReload);
            if (_testSound.Value)
            {
                _testSound.Value = false;
                if (enabled && _diagnostics.Enabled) PlayClick();
            }
        }
        catch (Exception ex) { WarnOnce("audio-update", ex); }
    }

    private void PlayClick(float shotVolumeScale = 1f)
    {
        if (_wave == null) { WarnOnce("missing-audio", new FileNotFoundException("No valid click loaded. Use Reload Click Sound after providing a WAV.", _soundPath)); return; }
        if (_clip == null || _clip.loadState != AudioDataLoadState.Loaded) _audioReset = true;
        if (_audioReset)
        {
            _audioReset = false;
            _output.Dispose();
            DestroyClip();
            _clip = CreateClip(_wave);
        }
        _output.Play(_clip, _volume.Value, shotVolumeScale);
        _playRequests++;
        _diagnostics.Info($"Click play request #{_playRequests}, volume={_volume.Value:0.##}, shotScale={shotVolumeScale:0.##}, route=Master.");
    }

    private void ReloadClip(bool force = true)
    {
        AudioClip pendingClip = null;
        try
        {
            _soundFilesStamp = SoundFiles.Fingerprint(_pluginDirectory);
            var selected = SoundFiles.Load(_pluginDirectory);
            _retrySoundLoad = SoundFiles.RequiresRetry(selected.OverrideError);
            if (!force && _loadedSoundFilesStamp == _soundFilesStamp && selected.Path == _soundPath
                && _clip != null && _clip.loadState == AudioDataLoadState.Loaded) return;
            var wave = selected.Wave;
            pendingClip = CreateClip(wave);
            _output.Dispose();
            var oldClip = _clip;
            _wave = wave;
            _clip = pendingClip;
            _soundPath = selected.Path;
            _loadedSoundFilesStamp = _soundFilesStamp;
            pendingClip = null;
            _audioReset = false;
            if (oldClip != null) Destroy(oldClip);
            _diagnostics.ClearWarnings();
            if (selected.OverrideError != null) WarnOnce("custom-sound-fallback", selected.OverrideError);
            _diagnostics.Info($"Click loaded: source={_soundPath}, {wave.Frames / (double)wave.Rate:0.000}s, {wave.Rate} Hz, channels={wave.Channels}.");
        }
        catch (Exception ex)
        {
            _loadedSoundFilesStamp = null;
            _retrySoundLoad = SoundFiles.RequiresRetry(ex);
            WarnOnce("sound-load", ex);
        }
        finally { if (pendingClip != null) Destroy(pendingClip); }
    }

    private static AudioClip CreateClip(PcmWave wave)
    {
        var clip = AudioClip.Create("LowAmmoCue click", wave.Frames, wave.Channels, wave.Rate, false);
        try
        {
            if (clip == null) throw new InvalidOperationException("Unity could not create the click clip.");
            clip.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            if (!clip.SetData(wave.Samples, 0) || clip.samples != wave.Frames || clip.channels != wave.Channels
                || clip.frequency != wave.Rate || clip.loadState != AudioDataLoadState.Loaded)
                throw new InvalidOperationException("Unity PCM upload failed.");
            return clip;
        }
        catch { if (clip != null) Destroy(clip); throw; }
    }

    private void OnAudioConfigurationChanged(bool deviceWasChanged) => _audioReset = true;
    private void DestroyClip()
    {
        var clip = _clip;
        _clip = null;
        if (clip != null) Destroy(clip);
    }

    internal void WarnOnce(string key, Exception ex)
        => _diagnostics?.WarnOnce(key, ex);

    private void OnDebugLoggingChanged(object sender, EventArgs args)
    {
        _diagnostics.SetEnabled(_debugLog.Value);
        _testSound.Value = _reloadSound.Value = false;
        _diagnostics.Info($"{PluginName} {PluginVersion} diagnostics enabled; sound={_soundPath}.");
        _refreshConfigMenu = true;
    }

    private void RefreshConfigurationMenu()
    {
        _refreshConfigMenu = false;
        try
        {
            if (Chainloader.PluginInfos.TryGetValue("com.bepis.bepinex.configurationmanager", out var manager)
                && manager.Instance != null)
                AccessTools.Method(manager.Instance.GetType(), "BuildSettingList", Type.EmptyTypes)?.Invoke(manager.Instance, null);
        }
        catch (Exception ex) { WarnOnce("config-menu", ex); }
    }

    private void OnDestroy()
    {
        _ready = false;
        if (ReferenceEquals(Instance, this)) Instance = null;
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        if (_debugLog != null) _debugLog.SettingChanged -= OnDebugLoggingChanged;
        try { _harmony?.UnpatchSelf(); }
        finally
        {
            try { _output.Dispose(); }
            finally { DestroyClip(); _wave = null; _lastLocal = null; }
        }
    }
}
