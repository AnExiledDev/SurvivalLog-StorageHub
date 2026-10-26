using System;
using System.Collections.Generic;

namespace StorageHub.Core;

/// <summary>
/// The keep list's catalog (every named item in the game, with its icon) built a step at a time, to run as a
/// <see cref="SlicedJob"/>: a table of a few thousand items read and written out in one frame was a visible hitch. It
/// waits for the icons first, then takes one item per step, so it ends with what <see cref="HubView.Catalog"/> makes
/// of the whole table at once.
/// </summary>
public static class CatalogBuild
{
    /// <summary>entryOf answers an item's catalog entry, or null for one the catalog leaves out; done gets the JSON.</summary>
    public static IEnumerable<bool> Steps(Func<bool> areIconsIn, IEnumerable<int> ids, Func<int, CatalogItem?> entryOf, Func<int, string?> iconOf, Action<IReadOnlyList<CatalogItem>, string> done)
    {
        while (!areIconsIn()) yield return false;

        var items = new List<CatalogItem>();

        foreach (var id in ids)
        {
            if (entryOf(id) is { } item) items.Add(item);

            yield return true;
        }

        var ordered = HubView.CatalogOrder(items);
        var rows = new List<string>(ordered.Count);

        yield return true;

        foreach (var item in ordered)
        {
            rows.Add(HubView.CatalogRow(item, iconOf(item.ConfigId)));

            yield return true;
        }

        done(ordered, "[" + string.Join(",", rows) + "]");
    }
}
