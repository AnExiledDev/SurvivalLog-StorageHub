using System;

namespace StorageHub.Core;

public static class Freshness
{
    public const float NeverSpoils = 9999f;

    /// <summary>
    /// Days until an item spoils, the way ProjectCook computes it: the hours left on the item's clock, stretched by the
    /// shelf factor of where it sits. Fridge tag rules use this to pick fresh food over food about to turn.
    /// </summary>
    public static float DaysLeft(int life, float startTime, float timeLeft, float presetShelfScale, float nowHours)
    {
        if (life < 1) return NeverSpoils;

        var shelfScale = presetShelfScale > 0f ? presetShelfScale : 1f;

        return Math.Max(0f, (timeLeft + startTime - nowHours) * shelfScale / 24f);
    }
}
