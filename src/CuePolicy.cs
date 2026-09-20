using System;

namespace LowAmmoCue;

public enum WarningMode { Percentage, Rounds }

internal static class CuePolicy
{
    internal static bool ShouldPlay(int remainingAfterShot, int capacity, WarningMode mode, int percent, int rounds)
        => ShotVolumeScale(remainingAfterShot, capacity, mode, percent, rounds) > 0f;

    internal static float ShotVolumeScale(int remainingAfterShot, int capacity, WarningMode mode, int percent, int rounds)
    {
        if (capacity <= 1 || remainingAfterShot < 0 || remainingAfterShot >= capacity) return 0f;
        int threshold;
        switch (mode)
        {
            case WarningMode.Percentage:
                if (percent < 1 || percent > 100) return 0f;
                threshold = Math.Max(1, (int)((long)capacity * percent / 100));
                break;
            case WarningMode.Rounds:
                if (rounds < 1) return 0f;
                threshold = Math.Min(rounds, capacity);
                break;
            default: return 0f;
        }
        if (remainingAfterShot >= threshold) return 0f;
        if (threshold == 1) return 1f;
        float progress = (threshold - 1 - remainingAfterShot) / (float)(threshold - 1);
        return 0.35f + 0.65f * progress;
    }

    internal static float Volume(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
}
