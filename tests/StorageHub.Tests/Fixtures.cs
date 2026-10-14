using StorageHub.Core;

namespace StorageHub.Tests;

internal static class Fixtures
{
    public const int Apple = 1;
    public const int Rope = 2;
    public const int RedBerry = 3;
    public const int Wood = 4;

    public static readonly Storage Bag = new(100, "Bag", StorageKind.Bag);
    public static readonly Storage Fridge = new(200, "Fridge", StorageKind.Furniture);
    public static readonly Storage Box = new(300, "Wooden Box", StorageKind.Furniture);
    public static readonly Storage Drawer = new(400, "Workbench drawer", StorageKind.Drawer);
    public static readonly Storage Rack = new(700, "Material Rack", StorageKind.Furniture, IsMaterialRack: true);

    /// <summary>Not a storage: floor items carry this owner so plans can tell them apart.</summary>
    public static readonly Storage Floor = new(Inventory.FloorOwnerId, "Floor", StorageKind.Furniture);

    public static Inventory Home(params ItemStack[] stacks) =>
        new(new[] { Bag, Fridge, Box, Drawer },
            stacks,
            new Dictionary<int, string> { [Apple] = "Red Apple", [Rope] = "Rope", [RedBerry] = "Red Berry" });

    /// <summary>Put-away facts for a home without a material rack, where only the storages' own tag rules count.</summary>
    public static PutFacts Tags(Func<ItemStack, Storage, PutRank?> tagRank) => new(tagRank, _ => null, (_, _) => false);

    public static ItemStack Stack(long itemId, int configId, int count, Storage where) => new(itemId, configId, count, where.OwnerId);
}
