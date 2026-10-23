using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Move Units of one stack into the bag: the whole stack when Units covers it, otherwise one unit at a time.</summary>
public sealed record UnitStep(ItemStack Stack, int Units, long Target)
{
    public bool IsWhole => Units >= Stack.Count;
}

/// <summary>What a unit take moved, per item config id, and the kept items that no storage had any of.</summary>
public sealed record UnitReport(IReadOnlyDictionary<int, int> Taken, IReadOnlyList<int> NoneStored);

/// <summary>
/// Taking a set number of units into the bag: from one storage (the panel's slider), or from every storage to bring
/// the bag up to its keep list. The smallest stacks go first, whole, so the big stacks stay whole; the last stack
/// gives only the units still wanted.
/// </summary>
public static class UnitTake
{
    /// <summary>Up to count units from these stacks into the bag. Stacks already in the bag or on the floor are skipped.</summary>
    public static IReadOnlyList<UnitStep> Plan(Inventory inventory, IEnumerable<ItemStack> candidates, int count)
    {
        if (inventory.Bag is not { } bag || count <= 0) return Array.Empty<UnitStep>();

        var steps = new List<UnitStep>();
        var left = count;

        foreach (var stack in Stored(inventory, candidates).OrderBy(stack => stack.Count).ThenBy(stack => stack.ItemId))
        {
            if (left <= 0) break;

            var units = Math.Min(left, stack.Count);
            steps.Add(new UnitStep(stack, units, bag.OwnerId));
            left -= units;
        }

        return steps;
    }

    /// <summary>For each kept item the bag is short of, the units still owed, taken from the home storages but never a drone.</summary>
    public static IReadOnlyList<UnitStep> Fill(Inventory inventory, KeepList keep)
    {
        var bagOwner = inventory.Bag?.OwnerId;

        return keep.Amounts
            .OrderBy(entry => entry.Key)
            .SelectMany(entry => Plan(inventory, FillSources(inventory, entry.Key), entry.Value - Held(inventory, entry.Key, bagOwner)))
            .ToList();
    }

    /// <summary>Kept items the bag is short of with none in any home storage, so a fill cannot bring them.</summary>
    public static IReadOnlyList<int> NoneStored(Inventory inventory, KeepList keep)
    {
        var bagOwner = inventory.Bag?.OwnerId;

        return keep.Amounts
            .Where(entry => entry.Value > Held(inventory, entry.Key, bagOwner))
            .Where(entry => !Stored(inventory, FillSources(inventory, entry.Key)).Any())
            .Select(entry => entry.Key)
            .OrderBy(configId => configId)
            .ToList();
    }

    /// <summary>
    /// Runs the steps. moveOne returns the units the game moved: 0 when it refused, 1 for a single unit, or more when
    /// it could not split the stack and moved the rest of it, which ends that step. The unit that moves keeps the stack's
    /// id, so rest finds what stayed behind (null when it cannot), and the next unit comes off that.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Run(IEnumerable<UnitStep> steps, Func<ItemStack, long, bool> moveWhole, Func<ItemStack, long, int> moveOne, Func<ItemStack, ItemStack?> rest)
    {
        var taken = new Dictionary<int, int>();

        foreach (var step in steps)
        {
            var moved = step.IsWhole ? (moveWhole(step.Stack, step.Target) ? step.Stack.Count : 0) : MoveUnits(step, moveOne, rest);

            if (moved > 0) taken[step.Stack.ConfigId] = (taken.TryGetValue(step.Stack.ConfigId, out var sum) ? sum : 0) + moved;
        }

        return taken;
    }

    /// <summary>"Took 5 Rope into your bag." with the items that could not be pulled named after it.</summary>
    public static string Describe(UnitReport report, Inventory inventory)
    {
        var isChinese = inventory.IsChinese;
        var names = report.Taken
            .Select(entry => (Name: inventory.ItemName(entry.Key), Count: entry.Value))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => Texts.Units.Say(isChinese, entry.Count, entry.Name))
            .ToList();
        var took = names.Count == 0 ? Texts.NothingToMove.Pick(isChinese) : Texts.Took.Say(isChinese, Texts.List(isChinese, names, 5));

        if (report.NoneStored.Count == 0) return took;

        var missing = report.NoneStored.Select(inventory.ItemName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        return Texts.Sentences(isChinese, took, Texts.NoneStored.Say(isChinese, Texts.List(isChinese, missing, 5)));
    }

    private static int MoveUnits(UnitStep step, Func<ItemStack, long, int> moveOne, Func<ItemStack, ItemStack?> rest)
    {
        var moved = 0;
        var from = step.Stack;

        while (moved < step.Units)
        {
            var units = moveOne(from, step.Target);

            if (units <= 0) break;

            // The game moved what was left of the stack in one go, so the step is over and all of it came.
            if (units > 1) return moved + units;

            moved += units;

            if (moved >= step.Units) break;

            if (rest(from) is not { } next) break;

            from = next;
        }

        return moved;
    }

    /// <summary>A fill never empties a drone, whose bag can hold goods it is about to trade away.</summary>
    private static IEnumerable<ItemStack> FillSources(Inventory inventory, int configId)
    {
        var drones = inventory.Storages.Where(storage => storage.Bay == DroneBay.Drone).Select(storage => storage.OwnerId).ToHashSet();

        return inventory.Stacks.Where(stack => stack.ConfigId == configId && !drones.Contains(stack.OwnerId));
    }

    /// <summary>Stacks in a home storage: not the bag, and not the floor, which is not a storage.</summary>
    private static IEnumerable<ItemStack> Stored(Inventory inventory, IEnumerable<ItemStack> stacks)
    {
        var stores = inventory.Storages.Where(storage => storage.Kind != StorageKind.Bag).Select(storage => storage.OwnerId).ToHashSet();

        return stacks.Where(stack => stack.Count > 0 && stores.Contains(stack.OwnerId));
    }

    private static int Held(Inventory inventory, int configId, long? bagOwner) =>
        bagOwner is { } owner ? inventory.Stacks.Where(stack => stack.OwnerId == owner && stack.ConfigId == configId).Sum(stack => stack.Count) : 0;
}
