using System;

namespace StorageHub.Core;

/// <summary>
/// What the game says about a stack and a storage, which put-away turns into a rank. TagRank is a storage's own tag
/// rule; RackRank is a material rack's own rule, which the game's robots use: a rack has no tags, and wants the
/// materials it has a slot for one step more than a storage tagged Materials and nothing else would.
/// </summary>
public sealed record PutFacts(Func<ItemStack, Storage, PutRank?> TagRank, Func<ItemStack, Storage, PutRank?> RackRank)
{
    /// <summary>A material rack is ranked by its rack rule alone, and every other storage by its tag rule.</summary>
    public PutRank? RankOf(ItemStack stack, Storage storage) =>
        storage.IsMaterialRack ? RackRank(stack, storage) : TagRank(stack, storage);
}
