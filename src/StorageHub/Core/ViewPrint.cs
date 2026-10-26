using System.Collections.Generic;

namespace StorageHub.Core;

/// <summary>
/// A 64-bit fingerprint of everything the panel's view is built from: the storages, every stack with the hour its
/// food spoils in, the floor, the names, the keep list and the switches. Equal prints mean the view would come out
/// the same, so the panel builds and sends it only when the print moves. Food moves the print once an hour, the same
/// step the "Rotting soon" list counts in.
/// </summary>
public static class ViewPrint
{
    public static long Of(Inventory inventory, IReadOnlyList<ItemStack> floor, IReadOnlyDictionary<long, float> daysLeft, KeepList keep, bool isKeepOn, bool canSplit, bool hasIcons)
    {
        var print = AddStorages(0UL, inventory.Storages);
        print = AddStacks(print, inventory.Stacks, daysLeft);
        print = AddStacks(print, floor, daysLeft);

        var kept = 0UL;

        foreach (var entry in keep.Amounts) kept += Scramble((ulong)(uint)entry.Key << 32 | (uint)entry.Value);

        print = AddNames(print, inventory.ItemNames);
        print = Add(print, kept);
        print = Add(print, (ulong)keep.Amounts.Count);
        print = Add(print, (inventory.IsChinese ? 8UL : 0UL) | (isKeepOn ? 4UL : 0UL) | (canSplit ? 2UL : 0UL) | (hasIcons ? 1UL : 0UL));

        return (long)print;
    }

    /// <summary>
    /// A fingerprint of everything a station's home list is built from: the station, the storages, every stack, the
    /// names, the item table its rules read (by address), the screen's tabs, the basket's wish, whether the player is
    /// away and whether icons are in. Null for the rat cage, whose answer turns on each stack's spoilage and so is
    /// built every time; every other station judges an item by its kind and the table alone.
    /// </summary>
    public static long? OfStation(Inventory inventory, StationKind kind, long table, IReadOnlyList<long>? tabs, BasketWish? wish, bool isAway, bool hasIcons)
    {
        if (!StationView.TakesByKindOnly(kind)) return null;

        var print = Add(0UL, (ulong)kind);
        print = Add(print, (ulong)table);
        print = AddStorages(print, inventory.Storages);

        for (var i = 0; i < inventory.Stacks.Count; i++)
        {
            var stack = inventory.Stacks[i];

            print = Add(print, (ulong)stack.ItemId);
            print = Add(print, (ulong)(uint)stack.ConfigId << 32 | (uint)stack.Count);
            print = Add(print, (ulong)stack.OwnerId);
        }

        print = Add(print, (ulong)inventory.Stacks.Count);
        print = AddNames(print, inventory.ItemNames);

        if (tabs == null) print = Add(print, ulong.MaxValue);
        else
        {
            for (var i = 0; i < tabs.Count; i++) print = Add(print, (ulong)tabs[i]);

            print = Add(print, (ulong)tabs.Count);
        }

        if (wish == null) print = Add(print, ulong.MaxValue);
        else
        {
            var categories = 0UL;

            foreach (var category in wish.Categories) categories += Scramble((ulong)(uint)category);

            print = Add(print, categories);
            print = Add(print, (ulong)wish.Categories.Count);
            print = Add(print, wish.CrisisCategory is { } crisis ? (uint)crisis : ulong.MaxValue);
            print = Add(print, (wish.IsActive ? 8UL : 0UL) | (wish.IsPending ? 4UL : 0UL) | (wish.IsAway ? 2UL : 0UL) | (wish.IsDying ? 1UL : 0UL));
        }

        return (long)Add(print, (inventory.IsChinese ? 4UL : 0UL) | (isAway ? 2UL : 0UL) | (hasIcons ? 1UL : 0UL));
    }

    private static ulong AddStorages(ulong print, IReadOnlyList<Storage> storages)
    {
        for (var i = 0; i < storages.Count; i++)
        {
            var storage = storages[i];

            print = Add(print, (ulong)storage.OwnerId);
            print = Add(print, Text(storage.Name));
            print = Add(print, (ulong)storage.Kind << 8 | (ulong)storage.Bay << 4 | (storage.IsMaterialRack ? 2UL : 0UL) | (storage.IsRobotPutOff ? 1UL : 0UL));
        }

        return Add(print, (ulong)storages.Count);
    }

    private static ulong AddNames(ulong print, IReadOnlyDictionary<int, string> itemNames)
    {
        var names = 0UL;

        foreach (var entry in itemNames) names += Scramble((ulong)(uint)entry.Key << 32 ^ Text(entry.Value));

        print = Add(print, names);

        return Add(print, (ulong)itemNames.Count);
    }

    private static ulong AddStacks(ulong print, IReadOnlyList<ItemStack> stacks, IReadOnlyDictionary<long, float> daysLeft)
    {
        for (var i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];

            print = Add(print, (ulong)stack.ItemId);
            print = Add(print, (ulong)(uint)stack.ConfigId << 32 | (uint)stack.Count);
            print = Add(print, (ulong)stack.OwnerId);
            print = Add(print, (ulong)(uint)(daysLeft.TryGetValue(stack.ItemId, out var days) ? RottingSoon.HourKey(days) : RottingSoon.HourKey(float.NaN)));
        }

        return Add(print, (ulong)stacks.Count);
    }

    /// <summary>Each step is a bijection of the print so far, so two lists that differ in one value never print the same.</summary>
    private static ulong Add(ulong print, ulong value) => Scramble(unchecked(print * 31UL + value));

    private static ulong Text(string? text) => (uint)(text ?? "").GetHashCode();

    private static ulong Scramble(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;

            return value;
        }
    }
}

/// <summary>The last view built and the print it was built from, so an unchanged print hands back the very same string.</summary>
public sealed class BuiltView
{
    private long _print;
    private string? _view;

    public bool TryGet(long print, out string view)
    {
        view = _view ?? "";

        return _view != null && print == _print;
    }

    public string Keep(long print, string view)
    {
        _print = print;

        return _view = view;
    }
}
