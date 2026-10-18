using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Move one stack into the first of these storages that accepts it.</summary>
public sealed record MoveStep(ItemStack Stack, IReadOnlyList<long> Targets);

public static class StoragePlan
{
    /// <summary>Send each stack to the best home storage for it. Only furniture storages are ever targets, and never a drone.</summary>
    public static IReadOnlyList<MoveStep> PutAway(Inventory inventory, IEnumerable<ItemStack> stacks, PutFacts facts)
    {
        var homes = inventory.Storages.Where(storage => storage.Kind == StorageKind.Furniture && storage.Bay != DroneBay.Drone).ToList();
        var held = inventory.Stacks.Select(stack => (stack.OwnerId, stack.ConfigId)).ToHashSet();

        return stacks
            .Select(stack => new MoveStep(stack, PutChoice.Targets(homes
                .Where(home => home.OwnerId != stack.OwnerId)
                .Select(home => new PutCandidate(home.OwnerId, facts.RankOf(stack, home), held.Contains((home.OwnerId, stack.ConfigId)))))))
            .ToList();
    }

    /// <summary>
    /// Floor items go where PutAway would send them, and failing every storage, into the bag, so nothing has to be
    /// left lying around just because no storage is tagged for it.
    /// </summary>
    public static IReadOnlyList<MoveStep> PutAwayFloor(Inventory inventory, IEnumerable<ItemStack> floorStacks, PutFacts facts)
    {
        var steps = PutAway(inventory, floorStacks, facts);

        if (inventory.Bag is not { } bag) return steps;

        return steps.Select(step => step with { Targets = step.Targets.Append(bag.OwnerId).ToList() }).ToList();
    }

    /// <summary>Bring stacks into the bag. Stacks already there are left alone.</summary>
    public static IReadOnlyList<MoveStep> Take(Inventory inventory, IEnumerable<ItemStack> stacks)
    {
        var bag = inventory.Bag;

        if (bag is null) return Array.Empty<MoveStep>();

        return stacks
            .Where(stack => stack.OwnerId != bag.OwnerId)
            .Select(stack => new MoveStep(stack, new[] { bag.OwnerId }))
            .ToList();
    }

    /// <summary>
    /// One unit of an item from one storage into the bag. It comes off the smallest stack, so the big stacks stay whole.
    /// </summary>
    public static IReadOnlyList<MoveStep> TakeOne(Inventory inventory, int configId, long ownerId)
    {
        var bag = inventory.Bag;
        var smallest = inventory.Stacks
            .Where(stack => stack.ConfigId == configId && stack.OwnerId == ownerId && stack.Count > 0)
            .OrderBy(stack => stack.Count)
            .FirstOrDefault();

        if (bag is null || smallest is null || ownerId == bag.OwnerId) return Array.Empty<MoveStep>();

        return new[] { new MoveStep(smallest with { Count = 1 }, new[] { bag.OwnerId }) };
    }
}
