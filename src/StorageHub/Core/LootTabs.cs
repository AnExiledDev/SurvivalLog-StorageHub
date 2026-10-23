using System;
using System.Collections.Generic;

namespace StorageHub.Core;

/// <summary>
/// Which home storages become tabs on the bag side of the looting screen (the bag beside an opened storage, the basket
/// or the trunk). At home every storage gets one, empty or not, so any of them can be emptied into or filled from the
/// one that is open, as at a crafting bench. Out exploring the screen stays as the game made it.
/// </summary>
public static class LootTabs
{
    /// <summary>A home storage; a drone's bag holds goods it is about to trade away, so it never gets a tab.</summary>
    public readonly record struct Place(long OwnerId, bool IsDrone);

    /// <summary>The storages to append, in the game's order, leaving out those the screen already shows.</summary>
    public static IReadOnlyList<long> ToAdd(bool isDualBag, bool isExploring, IEnumerable<Place> homes, IEnumerable<long> shown)
    {
        if (!isDualBag || isExploring) return Array.Empty<long>();

        var seen = new HashSet<long>(shown);
        var added = new List<long>();

        foreach (var home in homes)
        {
            if (!home.IsDrone && seen.Add(home.OwnerId)) added.Add(home.OwnerId);
        }

        return added;
    }
}
