using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace StorageHub.Core;

/// <summary>
/// The stacks the player has put on the drone in the trade window. An offered stack stays in the bag, and the offer
/// names it by id and holds its count, so a tidy must not pour units into or out of it.
/// </summary>
public static class OfferedStacks
{
    /// <summary>
    /// The ids in the trade window's offer list: an array of objects, each with an "id" as a number or as text. Empty
    /// when nothing is offered; null when the list cannot be read, since then no stack is known to be free to merge.
    /// </summary>
    public static IReadOnlySet<long>? Read(string? offerJson)
    {
        var ids = new HashSet<long>();

        if (string.IsNullOrWhiteSpace(offerJson)) return ids;

        try
        {
            using var document = JsonDocument.Parse(offerJson);

            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var id) || IdOf(id) is not { } stackId) return null;

                ids.Add(stackId);
            }

            return ids;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? IdOf(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number)) return number;
        if (id.ValueKind == JsonValueKind.String && long.TryParse(id.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var text)) return text;

        return null;
    }
}
