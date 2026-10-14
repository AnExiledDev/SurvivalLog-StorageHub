using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

public enum StorageKind
{
    Bag,
    Furniture,
    Drawer,
}

/// <summary>
/// Which part of the drone setup a storage is, if any. A drone delivery puts nothing into either. No put-away ever
/// fills a drone, whose bag can hold goods it is about to trade away; a hub takes the player's put-aways like any
/// other storage.
/// </summary>
public enum DroneBay
{
    None,
    Drone,
    Hub,
}

/// <summary>
/// A place items live: the player's bag, a home storage, or the workbench drawer. A material rack is a home storage
/// with no tags of its own and a slot for each material it holds.
/// </summary>
public sealed record Storage(long OwnerId, string Name, StorageKind Kind, DroneBay Bay = DroneBay.None, bool IsMaterialRack = false)
{
    public bool IsDroneBay => Bay != DroneBay.None;
}

/// <summary>One stack, which is one item instance in the game, and the storage holding it.</summary>
public sealed record ItemStack(long ItemId, int ConfigId, int Count, long OwnerId);

/// <summary>Everything the player owns at home, read in one go.</summary>
public sealed record Inventory(IReadOnlyList<Storage> Storages, IReadOnlyList<ItemStack> Stacks, IReadOnlyDictionary<int, string> ItemNames)
{
    public static readonly Inventory Empty = new(new List<Storage>(), new List<ItemStack>(), new Dictionary<int, string>());

    /// <summary>The owner floor items carry. The floor is not a storage, so nothing is ever moved onto it.</summary>
    public const long FloorOwnerId = long.MinValue;

    public Storage? Bag => Storages.FirstOrDefault(storage => storage.Kind == StorageKind.Bag);

    public string ItemName(int configId) => ItemNames.TryGetValue(configId, out var name) ? name : $"Item {configId}";

    public string StorageName(long ownerId) => ownerId == FloorOwnerId ? "Floor" : Storages.FirstOrDefault(storage => storage.OwnerId == ownerId)?.Name ?? $"Storage {ownerId}";
}
