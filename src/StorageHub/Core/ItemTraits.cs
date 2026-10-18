using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// Everything an item carries that a merge would flatten and the freshness rule does not cover: mold, the map-preset and
/// packaging flags, burn value, weight and its per-item value arrays (already turned into text). Stacks merge only when
/// their keys match.
/// </summary>
public readonly record struct ItemTraits(bool Polluted, bool IsMapPreset, bool NoPackage, int BurnValue, int Weight, string Values, string EffectEnds)
{
    private const char PieceSeparator = '|';
    private const char NameSeparator = '=';

    /// <summary>
    /// The key two stacks must share. The map-preset flag only sets how fast an item found on the map spoils, so an item
    /// that never spoils leaves it out and a found stack joins a made or bought one. The shelf factor an item came with
    /// is left out for every item: it only changes the shelf-life text, and the freshness rule already compares the time
    /// each stack has left.
    /// </summary>
    public string Key(bool neverSpoils)
    {
        var pieces = new List<string>
        {
            Piece("polluted", Polluted),
            Piece("noPackage", NoPackage),
            Piece("burn", BurnValue),
            Piece("weight", Weight),
            Piece("values", Values),
            Piece("effectEnds", EffectEnds),
        };

        if (!neverSpoils) pieces.Insert(1, Piece("mapPreset", IsMapPreset));

        return string.Join(PieceSeparator, pieces);
    }

    /// <summary>The names of the pieces whose values differ between the given keys, in key order.</summary>
    public static IReadOnlyList<string> Differing(IEnumerable<string> keys)
    {
        var valuesByName = new Dictionary<string, HashSet<string>>();
        var order = new List<string>();

        foreach (var piece in keys.SelectMany(key => key.Split(PieceSeparator)))
        {
            var at = piece.IndexOf(NameSeparator);
            var name = at < 0 ? piece : piece[..at];
            var value = at < 0 ? "" : piece[(at + 1)..];

            if (!valuesByName.TryGetValue(name, out var values))
            {
                values = new HashSet<string>();
                valuesByName[name] = values;
                order.Add(name);
            }

            values.Add(value);
        }

        return order.Where(name => valuesByName[name].Count > 1).ToList();
    }

    private static string Piece(string name, object value) => $"{name}{NameSeparator}{value}";
}
