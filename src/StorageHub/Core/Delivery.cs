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

/// <summary>A drone's trip as it reads just before the game settles the drone's return.</summary>
public sealed record TripAtSettle(bool IsSettleLocked, bool IsBusy, bool IsCharity, bool HasCargo);

/// <summary>A drone about to hand over its cargo: the bags it can land in, and what they held just before.</summary>
public sealed record Landing(long DroneId, long? HubId, int TripType, IReadOnlyList<ItemStack> Before)
{
    /// <summary>The dock hub when it has room, otherwise the drone's own bag.</summary>
    public IReadOnlyList<long> Owners => HubId is { } hub ? new[] { hub, DroneId } : new[] { DroneId };
}

/// <summary>Landings watched as one delivery because they share a bag, such as two drones docked on one hub.</summary>
public sealed record LandingGroup(IReadOnlyList<Landing> Landings)
{
    public IReadOnlyList<long> Owners => Landings.SelectMany(landing => landing.Owners).Distinct().ToList();

    /// <summary>Each bag as it was before the first of these landings touched it.</summary>
    public IReadOnlyList<ItemStack> Before =>
        Owners.SelectMany(owner => Landings.First(landing => landing.Owners.Contains(owner)).Before.Where(stack => stack.OwnerId == owner)).ToList();
}

/// <summary>
/// A drone delivery's put-away. The game drops the cargo into the drone's dock hub, or into the drone's own bag when
/// the hub cannot fit it; comparing those two bags before and after the drone settles finds what was delivered.
/// </summary>
public static class Delivery
{
    /// <summary>
    /// Whether the game's settle hands cargo over. It does nothing while locked or when the trip is not under way, a
    /// charity trip brings nothing home, and a trip whose cargo was already handed over has none left; the game settles
    /// such trips again, and a landing recorded for them would only watch the bags for nothing.
    /// </summary>
    public static bool HandsOverCargo(TripAtSettle trip) => !trip.IsSettleLocked && trip.IsBusy && !trip.IsCharity && trip.HasCargo;

    /// <summary>
    /// Adds to the group every waiting landing that shares a bag with it, so one hub is watched by one delivery: a second
    /// watch would find the first one had already put its cargo away. The rest keep waiting, in order.
    /// </summary>
    public static (LandingGroup Group, IReadOnlyList<Landing> Waiting) Absorb(LandingGroup group, IReadOnlyList<Landing> waiting)
    {
        var landings = group.Landings.ToList();
        var rest = waiting.ToList();
        var joiner = rest.FindIndex(landing => SharesABag(landing, landings));

        while (joiner >= 0)
        {
            landings.Add(rest[joiner]);
            rest.RemoveAt(joiner);
            joiner = rest.FindIndex(landing => SharesABag(landing, landings));
        }

        return (new LandingGroup(landings), rest);
    }

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
    /// The put-away plan for a delivery: the same tag rules as the bag's put-away, but no drone and no drone hub is ever
    /// a target, the ones the cargo landed in or any other, so a stack nothing wants stays where the game put it.
    /// </summary>
    public static IReadOnlyList<MoveStep> Plan(Inventory inventory, IReadOnlyList<ItemStack> delivered, IReadOnlyCollection<long> landingOwners, Func<ItemStack, Storage, PutRank?> rankOf)
    {
        var targets = inventory with { Storages = inventory.Storages.Where(storage => !storage.IsDroneBay && !landingOwners.Contains(storage.OwnerId)).ToList() };

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
    /// The drones whose retrieve the put-away has done for them: still waiting for the player to retrieve their cargo,
    /// yet with nothing left in their bag. A drone that still holds anything, because no storage took part of the
    /// cargo or it had items from before, keeps its retrieve, and one whose cargo went to the hub was finished by the game.
    /// </summary>
    public static IReadOnlyList<long> RetrievesToFinish(LandingGroup group, IReadOnlyList<ItemStack> heldNow, Func<long, bool> isAwaitingRetrieve) =>
        group.Landings
            .Select(landing => landing.DroneId)
            .Distinct()
            .Where(drone => heldNow.All(stack => stack.OwnerId != drone) && isAwaitingRetrieve(drone))
            .ToList();

    /// <summary>
    /// The drones whose cargo is out of their bag once the delivery is over, whether the game unloaded it into the hub or
    /// the put-away emptied the drone. Neither way passes through the game's Retrieve supplies, so an event that asks for
    /// a retrieve would never count them. A drone still waiting for the player to retrieve is left out: the game counts
    /// that retrieve itself when the player does it.
    /// </summary>
    public static IReadOnlyList<long> RetrievesDone(LandingGroup group, Func<long, bool> isAwaitingRetrieve) =>
        group.Landings
            .Select(landing => landing.DroneId)
            .Distinct()
            .Where(drone => !isAwaitingRetrieve(drone))
            .ToList();

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

    private static bool SharesABag(Landing landing, IReadOnlyList<Landing> group) =>
        landing.Owners.Any(owner => group.Any(member => member.Owners.Contains(owner)));

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
