using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StorageHub.Core;

/// <summary>
/// The JSON the web panel draws from: every storage, and every item kind with its total and where it sits. Ids are
/// sent as strings because a JavaScript number cannot hold every long.
/// </summary>
public static class HubView
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    public static string Build(Inventory inventory, Func<int, string?> iconOf)
    {
        var json = new StringBuilder(4096);
        var storages = inventory.Storages.Select(storage => $"{{\"id\":{Id(storage.OwnerId)},\"name\":{Str(storage.Name)},\"kind\":{Str(KindName(storage.Kind))}}}");
        var items = ItemSearch.Find(inventory, "").Select(row => ItemJson(row, iconOf(row.ConfigId)));

        json.Append("{\"storages\":[").AppendJoin(',', storages).Append("],\"items\":[").AppendJoin(',', items).Append("]}");

        return json.ToString();
    }

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

    private static string Id(long ownerId) => Str(ownerId.ToString(CultureInfo.InvariantCulture));

    private static string KindName(StorageKind kind) => kind switch
    {
        StorageKind.Bag => "bag",
        StorageKind.Furniture => "storage",
        StorageKind.Drawer => "drawer",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown storage kind"),
    };
}
