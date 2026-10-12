using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Where one delivered stack ended up: the storage that took it, or null when it stayed where the game put it.</summary>
public sealed record Placement(ItemStack Stack, long? Target);

/// <summary>What a look at the landing bags says about a delivery that is under way.</summary>
public abstract record DeliveryCheck
{
    /// <summary>Nothing has landed yet, or what landed was still changing, so look again next frame.</summary>
    public sealed record Wait : DeliveryCheck;

    /// <summary>The delivered stacks, seen the same on two looks in a row.</summary>
    public sealed record Landed(IReadOnlyList<ItemStack> Stacks) : DeliveryCheck;

    /// <summary>Nothing new turned up in time.</summary>
    public sealed record NothingLanded : DeliveryCheck;
}

/// <summary>
/// A drone delivery's put-away. The game drops the cargo into the drone's dock hub, or into the drone's own bag when
/// the hub cannot fit it; comparing those two bags before and after the drone settles finds what was delivered.
/// </summary>
public static class Delivery
{
    /// <summary>Stacks that are new since the first read or hold more than they did.</summary>
    public static IReadOnlyList<ItemStack> Delivered(IEnumerable<ItemStack> before, IEnumerable<ItemStack> after)
    {
        var had = new Dictionary<long, int>();

        foreach (var stack in before) had[stack.ItemId] = stack.Count;

        return after.Where(stack => !had.TryGetValue(stack.ItemId, out var count) || stack.Count > count).ToList();
    }

    /// <summary>
    /// Waits until a delivery has landed and stopped changing, since the game may hand the cargo over across more than
    /// one frame. Past the time limit, whatever has landed is taken as it is.
    /// </summary>
    public static DeliveryCheck Check(IReadOnlyList<ItemStack> seenLastTime, IReadOnlyList<ItemStack> seenNow, float secondsWaited, float limitSeconds)
    {
        var isOutOfTime = secondsWaited >= limitSeconds;

        if (seenNow.Count == 0) return isOutOfTime ? new DeliveryCheck.NothingLanded() : new DeliveryCheck.Wait();

        return isOutOfTime || IsSame(seenLastTime, seenNow) ? new DeliveryCheck.Landed(seenNow) : new DeliveryCheck.Wait();
    }

    /// <summary>
    /// The put-away plan for a delivery: the same tag rules as the bag's put-away, but the hub and the drone the cargo
    /// landed in are never targets, so a stack nothing wants stays where the game put it.
    /// </summary>
    public static IReadOnlyList<MoveStep> Plan(Inventory inventory, IReadOnlyList<ItemStack> delivered, IReadOnlyCollection<long> landingOwners, Func<ItemStack, Storage, PutRank?> rankOf)
    {
        var targets = inventory with { Storages = inventory.Storages.Where(storage => !landingOwners.Contains(storage.OwnerId)).ToList() };

        return StoragePlan.PutAway(targets, delivered, rankOf);
    }

    /// <summary>Tries each step's targets in order until the game accepts the stack, and records where each one went.</summary>
    public static IReadOnlyList<Placement> Place(IEnumerable<MoveStep> steps, Func<ItemStack, long, bool> tryMove)
    {
        var placements = new List<Placement>();

        foreach (var step in steps)
        {
            long? taker = null;

            foreach (var target in step.Targets)
            {
                if (!tryMove(step.Stack, target)) continue;

                taker = target;
                break;
            }

            placements.Add(new Placement(step.Stack, taker));
        }

        return placements;
    }

    /// <summary>
    /// One line saying what went where and what stayed, items of one kind going to one place counted together, e.g.
    /// "Drone delivery: 12 Wood to Wooden Box, 3 Red Apple to Fridge. Stayed in Drone Hub: 2 Rope." A stack the
    /// delivery topped up is counted whole, since it moves whole.
    /// </summary>
    public static string Describe(IReadOnlyList<Placement> placements, Inventory inventory)
    {
        if (placements.Count == 0) return "Drone delivery: nothing new arrived.";

        var moved = Totals(placements.Where(placement => placement.Target.HasValue), placement => placement.Target ?? 0, inventory);
        var stayed = Totals(placements.Where(placement => !placement.Target.HasValue), placement => placement.Stack.OwnerId, inventory);
        var line = moved.Count == 0 ? "Drone delivery: no storage takes any of it." : $"Drone delivery: {Listing(moved, "to")}.";

        if (stayed.Count == 0) return line;

        var stayedPlaces = stayed.Select(total => total.Place).Distinct().ToList();

        return stayedPlaces.Count == 1 ? $"{line} Stayed in {stayedPlaces[0]}: {Listing(stayed, null)}." : $"{line} Stayed: {Listing(stayed, "in")}.";
    }

    private static bool IsSame(IReadOnlyList<ItemStack> a, IReadOnlyList<ItemStack> b) =>
        a.Count == b.Count && a.OrderBy(stack => stack.ItemId).SequenceEqual(b.OrderBy(stack => stack.ItemId));

    /// <summary>Units per item kind and place, in the order they first appear.</summary>
    private static List<(string Item, int Units, string Place)> Totals(IEnumerable<Placement> placements, Func<Placement, long> placeOf, Inventory inventory) =>
        placements
            .GroupBy(placement => (placement.Stack.ConfigId, Place: placeOf(placement)))
            .Select(group => (inventory.ItemName(group.Key.ConfigId), group.Sum(placement => placement.Stack.Count), inventory.StorageName(group.Key.Place)))
            .ToList();

    /// <summary>"12 Wood to Wooden Box, 3 Red Apple to Fridge", or without the places when linkWord is null; long lists are cut short.</summary>
    private static string Listing(List<(string Item, int Units, string Place)> totals, string? linkWord)
    {
        const int Shown = 6;

        var entries = totals.Select(total => linkWord == null ? $"{total.Units} {total.Item}" : $"{total.Units} {total.Item} {linkWord} {total.Place}").ToList();

        return string.Join(", ", entries.Take(Shown)) + (entries.Count > Shown ? $" and {entries.Count - Shown} more" : "");
    }
}
