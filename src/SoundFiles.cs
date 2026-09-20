using System;
using System.IO;

namespace LowAmmoCue;

internal sealed class SoundSelection
{
    internal readonly PcmWave Wave;
    internal readonly string Path;
    internal readonly Exception OverrideError;

    internal SoundSelection(PcmWave wave, string path, Exception overrideError = null)
    { Wave = wave; Path = path; OverrideError = overrideError; }
}

internal static class SoundFiles
{
    internal const string OverrideName = "lowammo.wav";
    internal const string EmbeddedResourceName = "LowAmmoCue.Audio.lowammo.wav";
    internal const string EmbeddedSource = "embedded:" + EmbeddedResourceName;
    internal static string OverridePath(string pluginDirectory) => System.IO.Path.Combine(pluginDirectory, OverrideName);
    internal static bool RequiresRetry(Exception error) => error is IOException || error is UnauthorizedAccessException;

    internal static SoundSelection Load(string pluginDirectory)
    {
        string custom = OverridePath(pluginDirectory);
        Exception overrideError = null;
        if (File.Exists(custom))
        {
            try { return new SoundSelection(PcmWave.Load(custom), custom); }
            catch (InvalidDataException ex) { overrideError = ex; }
            catch (IOException ex) { overrideError = ex; }
            catch (UnauthorizedAccessException ex) { overrideError = ex; }
        }
        using var stream = typeof(SoundFiles).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidDataException("The embedded click is missing from the plugin DLL.");
        return new SoundSelection(PcmWave.Read(stream), EmbeddedSource, overrideError);
    }
    internal static string Fingerprint(string pluginDirectory)
        => Stamp(OverridePath(pluginDirectory));

    private static string Stamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}" : "missing";
    }
}
