using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Move one stack into the first of these storages that accepts it.</summary>
public sealed record MoveStep(ItemStack Stack, IReadOnlyList<long> Targets);

public static class StoragePlan
{
    /// <summary>Send each stack to the best home storage for it. Only furniture storages are ever targets.</summary>
    public static IReadOnlyList<MoveStep> PutAway(Inventory inventory, IEnumerable<ItemStack> stacks, Func<ItemStack, Storage, PutRank?> rankOf)
    {
        var homes = inventory.Storages.Where(storage => storage.Kind == StorageKind.Furniture).ToList();
        var held = inventory.Stacks.Select(stack => (stack.OwnerId, stack.ConfigId)).ToHashSet();

        return stacks
            .Select(stack => new MoveStep(stack, PutChoice.Targets(homes
                .Where(home => home.OwnerId != stack.OwnerId)
                .Select(home => new PutCandidate(home.OwnerId, rankOf(stack, home), held.Contains((home.OwnerId, stack.ConfigId)))))))
            .ToList();
    }

    /// <summary>
    /// Floor items go where PutAway would send them, and failing every storage, into the bag, so nothing has to be
    /// left lying around just because no storage is tagged for it.
    /// </summary>
    public static IReadOnlyList<MoveStep> PutAwayFloor(Inventory inventory, IEnumerable<ItemStack> floorStacks, Func<ItemStack, Storage, PutRank?> rankOf)
    {
        var steps = PutAway(inventory, floorStacks, rankOf);

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
}
