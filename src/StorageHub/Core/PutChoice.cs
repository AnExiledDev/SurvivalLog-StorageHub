using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>How much a storage's tag rule wants an item, as the game's FurnitureTagComponent.TryGetPutRank reports it.</summary>
public readonly record struct PutRank(int Level, int Specificity);

/// <summary>A storage an item could go into. TagRank is null when the storage's tag rule does not want the item.</summary>
public sealed record PutCandidate(long OwnerId, PutRank? TagRank, bool HoldsSameItem);

public static class PutChoice
{
    /// <summary>
    /// Storages to try, best first. A storage whose tag rule wants the item beats one that only holds some already,
    /// and a storage with neither is never picked, so items nobody claims stay in the bag. Among tag matches the more
    /// specific rule wins, then the lower level, then the storage already holding that item.
    /// </summary>
    // Whether a lower game "level" really means a better match is unconfirmed; the adapter logs every rank so it can be checked.
    public static IReadOnlyList<long> Targets(IEnumerable<PutCandidate> candidates) =>
        candidates
            .Where(candidate => candidate.TagRank.HasValue || candidate.HoldsSameItem)
            .OrderByDescending(candidate => candidate.TagRank.HasValue)
            .ThenByDescending(candidate => candidate.TagRank?.Specificity ?? 0)
            .ThenBy(candidate => candidate.TagRank?.Level ?? 0)
            .ThenByDescending(candidate => candidate.HoldsSameItem)
            .Select(candidate => candidate.OwnerId)
            .ToList();
}
