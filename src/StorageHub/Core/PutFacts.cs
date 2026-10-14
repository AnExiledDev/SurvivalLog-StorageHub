using System;

namespace StorageHub.Core;

/// <summary>
/// What the game says about a stack and a storage, which put-away turns into a rank. TagRank is the storage's own tag
/// rule, MaterialsRank is what a storage tagged Materials and nothing else would say, and RackHasRow is whether a
/// material rack has a slot for the item at all.
/// </summary>
public sealed record PutFacts(
    Func<ItemStack, Storage, PutRank?> TagRank,
    Func<ItemStack, PutRank?> MaterialsRank,
    Func<ItemStack, Storage, bool> RackHasRow)
{
    /// <summary>
    /// A material rack has no tags, so it ranks an item it has a slot for the way a storage tagged Materials and
    /// nothing else would, one step more specific. That is how the game's own robots rank a rack against such a
    /// storage, and it lets a rack fill even while a Materials box already holds the item. Any storage the game ranks
    /// more specifically still goes first.
    /// </summary>
    public PutRank? RankOf(ItemStack stack, Storage storage)
    {
        if (!storage.IsMaterialRack) return TagRank(stack, storage);

        if (!RackHasRow(stack, storage) || MaterialsRank(stack) is not { } materials) return null;

        return materials with { Specificity = materials.Specificity + 1 };
    }
}
