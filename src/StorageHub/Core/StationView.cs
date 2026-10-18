using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StorageHub.Core;

/// <summary>The game screens the home storage list sits beside.</summary>
public enum StationKind
{
    Inventory,
    Cooking,
    RatCage,
    Brew,
    Dye,
    Shredder,
    Compressor,
    Trade,
}

/// <summary>
/// The JSON for the home storage list beside a station: every home storage with its items merged by kind, and whether
/// the station takes each one. A usable item carries the id of a stack the station takes, so a click can hand the
/// game that exact stack. Ids are sent as strings because a JavaScript number cannot hold every long.
/// </summary>
public static class StationView
{
    public static string Build(Inventory inventory, StationKind kind, Func<ItemStack, bool> isUsable, Func<int, string?> iconOf)
    {
        var storages = inventory.Storages.Where(IsListed).Select(storage => StorageJson(inventory, storage, isUsable, iconOf));

        return new StringBuilder(4096).Append("{\"kind\":").Append(HubView.Str(KindName(kind))).Append(",\"storages\":[").AppendJoin(',', storages).Append("]}").ToString();
    }

    public static string KindName(StationKind kind) => kind switch
    {
        StationKind.Inventory => "inventory",
        StationKind.Cooking => "cooking",
        StationKind.RatCage => "ratcage",
        StationKind.Brew => "brew",
        StationKind.Dye => "dye",
        StationKind.Shredder => "shredder",
        StationKind.Compressor => "compressor",
        StationKind.Trade => "trade",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown station"),
    };

    public static StationKind? Parse(string name) => name switch
    {
        "inventory" => StationKind.Inventory,
        "cooking" => StationKind.Cooking,
        "ratcage" => StationKind.RatCage,
        "brew" => StationKind.Brew,
        "dye" => StationKind.Dye,
        "shredder" => StationKind.Shredder,
        "compressor" => StationKind.Compressor,
        "trade" => StationKind.Trade,
        _ => null,
    };

    /// <summary>Only home furniture: the bag is already on screen, and a drone's bag holds goods it is about to trade away.</summary>
    private static bool IsListed(Storage storage) => storage.Kind == StorageKind.Furniture && storage.Bay != DroneBay.Drone;

    private static string StorageJson(Inventory inventory, Storage storage, Func<ItemStack, bool> isUsable, Func<int, string?> iconOf)
    {
        var items = inventory.Stacks
            .Where(stack => stack.OwnerId == storage.OwnerId)
            .GroupBy(stack => stack.ConfigId)
            .Select(kind => ItemJson(inventory.ItemName(kind.Key), kind.ToList(), isUsable, iconOf(kind.Key)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Json);

        return $"{{\"id\":{Id(storage.OwnerId)},\"name\":{HubView.Str(storage.Name)},\"items\":[{string.Join(",", items)}]}}";
    }

    private static (string Name, string Json) ItemJson(string name, IReadOnlyList<ItemStack> stacks, Func<ItemStack, bool> isUsable, string? icon)
    {
        var usable = stacks.FirstOrDefault(isUsable);
        var shown = usable ?? stacks[0];
        var count = stacks.Sum(stack => stack.Count).ToString(CultureInfo.InvariantCulture);
        var json = $"{{\"item\":{Id(shown.ItemId)},\"cfg\":{shown.ConfigId.ToString(CultureInfo.InvariantCulture)},\"name\":{HubView.Str(name)},\"count\":{count},\"icon\":{(icon is null ? "null" : HubView.Str(icon))},\"ok\":{(usable is null ? "false" : "true")}}}";

        return (name, json);
    }

    private static string Id(long id) => HubView.Str(id.ToString(CultureInfo.InvariantCulture));
}
