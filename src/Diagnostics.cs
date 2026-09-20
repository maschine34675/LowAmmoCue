using System;
using System.Collections.Generic;

namespace LowAmmoCue;

internal sealed class Diagnostics
{
    private readonly Action<string> _info, _warning;
    private readonly HashSet<string> _warnings = new();
    internal bool Enabled { get; private set; }
    internal readonly ConfigurationManagerAttributes TestSound = new()
    {
        DispName = "Test Click at Maximum Volume", Order = 90, Browsable = false, ShowRangeAsPercent = false
    };
    internal readonly ConfigurationManagerAttributes ReloadSound = new()
    {
        DispName = "Reload Click Sound", Order = 80, Browsable = false, ShowRangeAsPercent = false
    };

    internal Diagnostics(Action<string> info, Action<string> warning)
    { _info = info; _warning = warning; }

    internal void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        TestSound.Browsable = ReloadSound.Browsable = enabled;
        _warnings.Clear();
    }

    internal void Info(string message)
    {
        if (Enabled) _info(message);
    }

    internal void WarnOnce(string key, Exception error)
    {
        if (Enabled && _warnings.Add(key)) _warning($"LowAmmoCue {key}: {error.GetBaseException().Message}");
    }

    internal void ClearWarnings() => _warnings.Clear();
}
