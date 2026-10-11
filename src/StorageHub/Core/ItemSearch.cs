using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>How many of one item a storage holds, and the stacks that make it up.</summary>
public sealed record Location(Storage Storage, int Count, IReadOnlyList<ItemStack> Stacks);

/// <summary>One kind of item, added up across every storage.</summary>
public sealed record ItemRow(int ConfigId, string Name, int Total, IReadOnlyList<Location> Locations);

public static class ItemSearch
{
    /// <summary>Every item kind whose name contains every word of the query, A to Z.</summary>
    public static IReadOnlyList<ItemRow> Find(Inventory inventory, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var storageOrder = inventory.Storages
            .Select((storage, index) => (Storage: storage, Index: index))
            .ToDictionary(entry => entry.Storage.OwnerId);

        return inventory.Stacks
            .Where(stack => storageOrder.ContainsKey(stack.OwnerId))
            .GroupBy(stack => stack.ConfigId)
            .Select(kind => ToRow(kind, inventory.ItemName(kind.Key), storageOrder))
            .Where(row => words.All(word => row.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ConfigId)
            .ToList();
    }

    private static ItemRow ToRow(IGrouping<int, ItemStack> kind, string name, IReadOnlyDictionary<long, (Storage Storage, int Index)> storageOrder)
    {
        var locations = kind
            .GroupBy(stack => stack.OwnerId)
            .OrderBy(held => storageOrder[held.Key].Index)
            .Select(held => new Location(storageOrder[held.Key].Storage, held.Sum(stack => stack.Count), held.ToList()))
            .ToList();

        return new ItemRow(kind.Key, name, locations.Sum(location => location.Count), locations);
    }
}
