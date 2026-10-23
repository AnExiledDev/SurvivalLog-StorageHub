using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StorageHub.Core;

/// <summary>
/// The game names every bag tab it does not know "Backpack", so the storage tabs added at the basket get their own
/// names written into the game's tab list (a JSON array of objects with a "name"), by position. Anything that is not
/// such a list is left as it came.
/// </summary>
public static class BagTabNames
{
    private static readonly JsonSerializerOptions Output = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Rename(string? tabsJson, IReadOnlyDictionary<int, string> namesByIndex)
    {
        if (string.IsNullOrEmpty(tabsJson) || namesByIndex.Count == 0) return tabsJson ?? "";

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(tabsJson);
        }
        catch (JsonException)
        {
            return tabsJson;
        }

        if (root is not JsonArray tabs) return tabsJson;

        foreach (var (index, name) in namesByIndex)
        {
            if (index >= 0 && index < tabs.Count && tabs[index] is JsonObject tab) tab["name"] = name;
        }

        return tabs.ToJsonString(Output);
    }
}
