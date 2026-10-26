using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StorageHub.Core;

/// <summary>One item from the game's item table, for the keep list's search.</summary>
public sealed record CatalogItem(int ConfigId, string Name);

/// <summary>The keep list as the panel shows it, and whether the config has it turned on.</summary>
public sealed record KeepView(bool IsOn, IReadOnlyList<KeepRow> Rows);

/// <summary>
/// The JSON the web panel draws from: every storage, and every item kind with its total and where it sits. Ids are
/// sent as strings because a JavaScript number cannot hold every long.
/// </summary>
public static class HubView
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>
    /// The view; rot is the "Rotting soon" list, soonest first, and keep is the keep list. Each is sent empty when not
    /// given, the keep list as turned on. canSplit says whether part of a stack can be taken (Stacking is installed).
    /// </summary>
    public static string Build(Inventory inventory, Func<int, string?> iconOf, IReadOnlyList<RotRow>? rot = null, KeepView? keep = null, bool canSplit = true)
    {
        var json = new StringBuilder(4096);
        var storages = inventory.Storages.Select(storage => $"{{\"id\":{Id(storage.OwnerId)},\"name\":{Str(storage.Name)},\"kind\":{Str(KindName(storage.Kind))}{(storage.Bay == DroneBay.Drone ? ",\"drone\":true" : "")}}}");
        var items = ItemSearch.Find(inventory, "").Select(row => ItemJson(row, iconOf(row.ConfigId)));
        var rotting = (rot ?? Array.Empty<RotRow>()).Select(row => RotJson(row, iconOf(row.ConfigId)));
        var keeping = keep ?? new KeepView(true, Array.Empty<KeepRow>());
        var kept = keeping.Rows.Select(row => KeepJson(row, iconOf(row.ConfigId)));

        json.Append("{\"storages\":[").AppendJoin(',', storages).Append("],\"items\":[").AppendJoin(',', items).Append("],\"rot\":[").AppendJoin(',', rotting)
            .Append("],\"keep\":{\"on\":").Append(keeping.IsOn ? "true" : "false").Append(",\"rows\":[").AppendJoin(',', kept).Append("]},\"canSplit\":").Append(canSplit ? "true" : "false").Append('}');

        return json.ToString();
    }

    /// <summary>Every catalog item as [{id, name, icon}], sorted by name, for the keep list's search.</summary>
    public static string Catalog(IEnumerable<CatalogItem> items, Func<int, string?> iconOf) =>
        "[" + string.Join(",", CatalogOrder(items).Select(item => CatalogRow(item, iconOf(item.ConfigId)))) + "]";

    /// <summary>The catalog's order: by name, then by id.</summary>
    public static List<CatalogItem> CatalogOrder(IEnumerable<CatalogItem> items) =>
        items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.ConfigId).ToList();

    /// <summary>One catalog item as the page reads it.</summary>
    public static string CatalogRow(CatalogItem item, string? icon) => $"{{\"id\":{Num(item.ConfigId)},\"name\":{Str(item.Name)},\"icon\":{Icon(icon)}}}";

    /// <summary>A JavaScript string literal, safe to paste into a script.</summary>
    public static string Str(string? text)
    {
        var literal = new StringBuilder((text?.Length ?? 0) + 2).Append('"');

        foreach (var character in text ?? "")
        {
            switch (character)
            {
                case '"':
                    literal.Append("\\\"");
                    break;
                case '\\':
                    literal.Append("\\\\");
                    break;
                // Line and paragraph separators end a line in older JavaScript, and '<' keeps "</script>" inert.
                case < ' ' or LineSeparator or ParagraphSeparator or '<':
                    literal.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    literal.Append(character);
                    break;
            }
        }

        return literal.Append('"').ToString();
    }

    private static string ItemJson(ItemRow row, string? icon)
    {
        var at = row.Locations.Select(location => $"[{Id(location.Storage.OwnerId)},{location.Count.ToString(CultureInfo.InvariantCulture)}]");

        return $"{{\"id\":{row.ConfigId.ToString(CultureInfo.InvariantCulture)},\"name\":{Str(row.Name)},\"icon\":{(icon is null ? "null" : Str(icon))},\"total\":{row.Total.ToString(CultureInfo.InvariantCulture)},\"at\":[{string.Join(",", at)}]}}";
    }

    private static string KeepJson(KeepRow row, string? icon) =>
        $"{{\"id\":{Num(row.ConfigId)},\"name\":{Str(row.Name)},\"icon\":{Icon(icon)},\"amount\":{Num(row.Amount)},\"held\":{Num(row.Held)}}}";

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Icon(string? icon) => icon is null ? "null" : Str(icon);

    private static string RotJson(RotRow row, string? icon) =>
        $"{{\"id\":{row.ConfigId.ToString(CultureInfo.InvariantCulture)},\"name\":{Str(row.Name)},\"icon\":{(icon is null ? "null" : Str(icon))},\"count\":{row.Count.ToString(CultureInfo.InvariantCulture)},\"at\":{Id(row.OwnerId)},\"where\":{Str(row.Where)},\"left\":{Str(row.Label)},\"urgent\":{(row.IsUrgent ? "true" : "false")},\"hours\":{Num(row.HoursLeft)}}}";

    private static string Id(long ownerId) => Str(ownerId.ToString(CultureInfo.InvariantCulture));

    private static string KindName(StorageKind kind) => kind switch
    {
        StorageKind.Bag => "bag",
        StorageKind.Furniture => "storage",
        StorageKind.Drawer => "drawer",
        StorageKind.Basket => "basket",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown storage kind"),
    };
}
