using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Items of one kind in one place that spoil in the same hour, for the "Rotting soon" view.</summary>
public sealed record RotRow(int ConfigId, string Name, int Count, long OwnerId, string Where, int HoursLeft, bool IsChinese = false)
{
    /// <summary>How long is left, the way the list shows it: "spoiled", "under 1h", "5h", "2d 3h".</summary>
    public string Label => RottingSoon.Label(HoursLeft, IsChinese);

    /// <summary>Spoiled, or will be within the hour; the list draws these in a warning colour.</summary>
    public bool IsUrgent => HoursLeft < 1;
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
            .Select(group => new RotRow(group.Key.ConfigId, inventory.ItemName(group.Key.ConfigId), group.Sum(stack => stack.Count), group.Key.OwnerId, inventory.StorageName(group.Key.OwnerId), group.Key.Hours, inventory.IsChinese))
            .OrderBy(row => row.HoursLeft)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Where, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.OwnerId)
            .ToList();

    /// <summary>Hours below 0 mean the item has already spoiled.</summary>
    public static string Label(int hoursLeft, bool isChinese = false)
    {
        if (hoursLeft < 0) return Texts.Spoiled.Pick(isChinese);

        if (hoursLeft == 0) return Texts.UnderAnHour.Pick(isChinese);

        var days = hoursLeft / 24;
        var hours = hoursLeft % 24;

        return days == 0 ? Texts.HoursLeft.Say(isChinese, hours) : Texts.DaysLeft.Say(isChinese, days, hours);
    }

    /// <summary>
    /// The hour the list would put a stack in, by the same rule as Rows, or int.MinValue for one the list leaves out.
    /// Two readings with the same key put the stack in the same row.
    /// </summary>
    public static int HourKey(float daysLeft) => Spoils(daysLeft) ? HoursLeft(daysLeft) : int.MinValue;

    /// <summary>The game answers -1 for an item that never spoils or has stopped, and Freshness.NeverSpoils stands in when the game cannot be asked.</summary>
    private static bool Spoils(float daysLeft) => daysLeft >= 0f && daysLeft < Freshness.NeverSpoils && !float.IsNaN(daysLeft);

    /// <summary>Whole hours left, rounded down, so the list only changes once an hour; -1 means already spoiled.</summary>
    private static int HoursLeft(float daysLeft) => daysLeft <= 0f ? -1 : (int)Math.Floor(daysLeft * 24f);
}
