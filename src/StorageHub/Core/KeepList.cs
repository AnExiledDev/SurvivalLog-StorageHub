using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace StorageHub.Core;

/// <summary>
/// How many of each item kind putting away leaves in the player's bag, by item config id. An entry of 0 stays on the
/// list but keeps nothing, so the player can turn one item down without losing its place on the list.
/// </summary>
public sealed class KeepList
{
    public const int MaxAmount = 9999;

    public static readonly KeepList Empty = new(new Dictionary<int, int>());

    private readonly Dictionary<int, int> _amounts;

    private KeepList(Dictionary<int, int> amounts)
    {
        _amounts = amounts;
    }

    public IReadOnlyDictionary<int, int> Amounts => _amounts;

    public bool Has(int configId) => _amounts.ContainsKey(configId);

    public int AmountOf(int configId) => _amounts.TryGetValue(configId, out var amount) ? amount : 0;

    /// <summary>Adds the item or changes its amount, held to 0 through MaxAmount.</summary>
    public KeepList With(int configId, int amount) => new(new Dictionary<int, int>(_amounts) { [configId] = Math.Clamp(amount, 0, MaxAmount) });

    public KeepList Without(int configId)
    {
        var rest = new Dictionary<int, int>(_amounts);
        rest.Remove(configId);

        return new KeepList(rest);
    }
}

/// <summary>
/// Where the keep list lives between sessions. Today that is one list for every save; a per-save list or a Settings Hub
/// profile would be another store behind this same port.
/// </summary>
public interface IKeepListStore
{
    KeepList Load();

    /// <summary>False when the list could not be written; the store logs why.</summary>
    bool Save(KeepList list);
}

/// <summary>What reading a saved keep list found.</summary>
public abstract record KeepListRead
{
    private KeepListRead()
    {
    }

    public sealed record Loaded(KeepList List) : KeepListRead;

    /// <summary>The text is not a keep list this version understands; nothing in it is used.</summary>
    public sealed record Unreadable(string Why) : KeepListRead;
}

/// <summary>
/// The keep list file: {"version": 1, "keep": [{"item": config id, "amount": n}]}. One bad entry makes the whole file
/// unreadable rather than quietly dropping that item, so the next save cannot lose it without the log saying so.
/// </summary>
public static class KeepListJson
{
    public const int Version = 1;

    public static string Write(KeepList list)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            json.WriteStartArray("keep");

            foreach (var entry in list.Amounts.OrderBy(entry => entry.Key))
            {
                json.WriteStartObject();
                json.WriteNumber("item", entry.Key);
                json.WriteNumber("amount", entry.Value);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static KeepListRead Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);

            return FromRoot(document.RootElement);
        }
        catch (JsonException ex)
        {
            return new KeepListRead.Unreadable("not valid JSON: " + ex.Message);
        }
    }

    private static KeepListRead FromRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new KeepListRead.Unreadable("the file is not a JSON object");

        if (root.TryGetProperty("version", out var version) && !IsInt(version, Version)) return new KeepListRead.Unreadable($"version {version.GetRawText()} is not one this Storage Hub reads");

        if (!root.TryGetProperty("keep", out var entries) || entries.ValueKind != JsonValueKind.Array) return new KeepListRead.Unreadable("there is no \"keep\" list");

        var list = KeepList.Empty;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!TryEntry(entry, out var configId, out var amount)) return new KeepListRead.Unreadable($"an entry is not {{\"item\": id, \"amount\": n}}: {entry.GetRawText()}");

            list = list.With(configId, amount);
        }

        return new KeepListRead.Loaded(list);
    }

    private static bool TryEntry(JsonElement entry, out int configId, out int amount)
    {
        configId = 0;
        amount = 0;

        return entry.ValueKind == JsonValueKind.Object
            && entry.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out configId)
            && entry.TryGetProperty("amount", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out amount);
    }

    private static bool IsInt(JsonElement element, int wanted) => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) && value == wanted;
}
