using System;
using Comfort.Common;
using EFT.UI;
using UnityEngine;
using UnityEngine.Audio;

namespace LowAmmoCue;
internal sealed class ClickOutput : IDisposable
{
    private GUISounds _host;
    private AudioMixer _mixer;
    private AudioSource _source;

    internal void Play(AudioClip clip, float volume, float shotVolumeScale)
    {
        var host = Singleton<GUISounds>.Instantiated ? Singleton<GUISounds>.Instance : null;
        if (host == null || !host.isActiveAndEnabled || !host.gameObject.activeInHierarchy || host.MasterMixer == null)
            throw new InvalidOperationException("Game audio mixer is not ready.");
        if (_source == null || !ReferenceEquals(host, _host) || !ReferenceEquals(host.MasterMixer, _mixer)
            || _source.outputAudioMixerGroup == null)
        {
            Dispose();
            AudioMixerGroup master = null;
            foreach (var candidate in host.MasterMixer.FindMatchingGroups("Master"))
            {
                if (candidate == null || candidate.name != "Master") continue;
                if (master != null || !ReferenceEquals(candidate.audioMixer, host.MasterMixer))
                    throw new InvalidOperationException("Game Master mixer group is ambiguous.");
                master = candidate;
            }
            if (master == null) throw new InvalidOperationException("Game Master mixer group was not found.");
            _host = host;
            _mixer = host.MasterMixer;
            _source = host.gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0;
            _source.spatialize = false;
            _source.dopplerLevel = 0;
            _source.bypassReverbZones = true;
            _source.ignoreListenerPause = false;
            _source.ignoreListenerVolume = false;
            _source.priority = 32;
            _source.outputAudioMixerGroup = master;
        }
        _source.volume = CuePolicy.Volume(volume);
        _source.PlayOneShot(clip, CuePolicy.Volume(shotVolumeScale));
    }

    internal void Tick(bool enabled, float volume)
    {
        if (_source == null) return;
        var host = Singleton<GUISounds>.Instantiated ? Singleton<GUISounds>.Instance : null;
        if (!enabled || host == null || !host.isActiveAndEnabled || !host.gameObject.activeInHierarchy
            || !ReferenceEquals(host, _host) || !ReferenceEquals(host.MasterMixer, _mixer)
            || _source.outputAudioMixerGroup == null)
        { Dispose(); return; }
        _source.volume = CuePolicy.Volume(volume);
    }

    public void Dispose()
    {
        var source = _source;
        _source = null;
        _host = null;
        _mixer = null;
        if (source == null) return;
        try { source.Stop(); }
        finally { UnityEngine.Object.Destroy(source); }
    }
}
