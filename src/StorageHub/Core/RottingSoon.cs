using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Items of one kind in one place that spoil in the same hour, for the "Rotting soon" view.</summary>
public sealed record RotRow(int ConfigId, string Name, int Count, long OwnerId, string Where, int HoursLeft)
{
    /// <summary>How long is left, the way the list shows it: "spoiled", "under 1h", "5h", "2d 3h".</summary>
    public string Label => RottingSoon.Label(HoursLeft);
}

public static class RottingSoon
{
    /// <summary>
    /// Every stack that spoils, soonest first, from the bag, the storages, the workbench drawer and the floor. A stack
    /// with no shelf life read, or one the game says never spoils or has stopped spoiling, is left out. Stacks of one
    /// kind in one place spoiling in the same hour share a row. Ties go by name, then by place.
    /// </summary>
    public static IReadOnlyList<RotRow> Rows(Inventory inventory, IEnumerable<ItemStack> floor, IReadOnlyDictionary<long, float> daysLeft) =>
        inventory.Stacks
            .Concat(floor)
            .Where(stack => daysLeft.TryGetValue(stack.ItemId, out var days) && Spoils(days))
            .GroupBy(stack => (stack.ConfigId, stack.OwnerId, Hours: HoursLeft(daysLeft[stack.ItemId])))
            .Select(group => new RotRow(group.Key.ConfigId, inventory.ItemName(group.Key.ConfigId), group.Sum(stack => stack.Count), group.Key.OwnerId, inventory.StorageName(group.Key.OwnerId), group.Key.Hours))
            .OrderBy(row => row.HoursLeft)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Where, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.OwnerId)
            .ToList();

    /// <summary>Hours below 0 mean the item has already spoiled.</summary>
    public static string Label(int hoursLeft)
    {
        if (hoursLeft < 0) return "spoiled";

        if (hoursLeft == 0) return "under 1h";

        var days = hoursLeft / 24;
        var hours = hoursLeft % 24;

        return days == 0 ? Text(hours) + "h" : $"{Text(days)}d {Text(hours)}h";
    }

    /// <summary>The game answers -1 for an item that never spoils or has stopped, and Freshness.NeverSpoils stands in when the game cannot be asked.</summary>
    private static bool Spoils(float daysLeft) => daysLeft >= 0f && daysLeft < Freshness.NeverSpoils && !float.IsNaN(daysLeft);

    /// <summary>Whole hours left, rounded down, so the list only changes once an hour; -1 means already spoiled.</summary>
    private static int HoursLeft(float daysLeft) => daysLeft <= 0f ? -1 : (int)Math.Floor(daysLeft * 24f);

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
