using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>The bag's stacks sorted into the ones putting away may move and the ones the keep list holds back.</summary>
public sealed record KeepSplit(IReadOnlyList<ItemStack> PutAway, IReadOnlyList<ItemStack> Kept);

/// <summary>One keep list entry as the panel shows it: what to keep, and how many the bag holds right now.</summary>
public sealed record KeepRow(int ConfigId, string Name, int Amount, int Held);

/// <summary>
/// Applies the keep list to a bag put-away. The game moves a stack whole and cannot split a stackable one, so the bag
/// keeps whole stacks: at least the amount asked for when it holds that many, and often a few more.
/// </summary>
public static class BagKeep
{
    /// <summary>
    /// For each kept item, while units are still owed: if one stack covers what is owed, keep the smallest such stack and
    /// stop; otherwise keep the fullest stack and go on. Between stacks of the same size the freshest is kept (most days
    /// left; a stack that never spoils counts as freshest), then the lowest item id, so the result never depends on the
    /// order the game listed them in.
    /// </summary>
    public static KeepSplit Split(IEnumerable<ItemStack> bagStacks, KeepList keep, IReadOnlyDictionary<long, float> daysLeft)
    {
        var stacks = bagStacks.ToList();
        var kept = new HashSet<ItemStack>();

        foreach (var kind in stacks.GroupBy(stack => stack.ConfigId)) kept.UnionWith(KeepOf(kind, keep.AmountOf(kind.Key), daysLeft));

        return new KeepSplit(stacks.Where(stack => !kept.Contains(stack)).ToList(), stacks.Where(kept.Contains).ToList());
    }

    /// <summary>Every kept item, sorted by name, with the number in the bag now. Other storages do not count.</summary>
    public static IReadOnlyList<KeepRow> Rows(Inventory inventory, KeepList keep)
    {
        var bagOwner = inventory.Bag?.OwnerId;
        var held = inventory.Stacks
            .Where(stack => stack.OwnerId == bagOwner)
            .GroupBy(stack => stack.ConfigId)
            .ToDictionary(kind => kind.Key, kind => kind.Sum(stack => stack.Count));

        return keep.Amounts
            .Select(entry => new KeepRow(entry.Key, inventory.ItemName(entry.Key), entry.Value, held.TryGetValue(entry.Key, out var count) ? count : 0))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ConfigId)
            .ToList();
    }

    /// <summary>"Kept in your bag: 6 Red Apple, 2 Rope." for the status line, or "" when the keep list held nothing back.</summary>
    public static string Describe(KeepSplit split, Inventory inventory)
    {
        if (split.Kept.Count == 0) return "";

        var kinds = split.Kept
            .GroupBy(stack => stack.ConfigId)
            .Select(kind => (Name: inventory.ItemName(kind.Key), Count: kind.Sum(stack => stack.Count)))
            .OrderBy(kind => kind.Name, StringComparer.OrdinalIgnoreCase)
            .Select(kind => Texts.Units.Say(inventory.IsChinese, kind.Count, kind.Name))
            .ToList();

        return Texts.KeptInBag.Say(inventory.IsChinese, Texts.List(inventory.IsChinese, kinds, kinds.Count));
    }

    private static IEnumerable<ItemStack> KeepOf(IEnumerable<ItemStack> kind, int amount, IReadOnlyDictionary<long, float> daysLeft)
    {
        var left = kind
            .OrderByDescending(stack => stack.Count)
            .ThenByDescending(stack => DaysLeftOf(stack, daysLeft))
            .ThenBy(stack => stack.ItemId)
            .ToList();
        var owed = amount;

        while (owed > 0 && left.Count > 0)
        {
            var covering = left.Where(stack => stack.Count >= owed).OrderBy(stack => stack.Count).FirstOrDefault();
            var next = covering ?? left[0];

            yield return next;

            left.Remove(next);
            owed -= next.Count;
        }
    }

    /// <summary>A stack with no days left recorded, or -1 (never spoils, or has stopped spoiling), counts as freshest.</summary>
    private static float DaysLeftOf(ItemStack stack, IReadOnlyDictionary<long, float> daysLeft) =>
        daysLeft.TryGetValue(stack.ItemId, out var days) && days >= 0 ? days : Freshness.NeverSpoils;
}
