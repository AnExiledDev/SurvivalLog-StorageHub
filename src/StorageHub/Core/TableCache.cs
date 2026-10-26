using System.Collections.Generic;

namespace StorageHub.Core;

/// <summary>
/// Answers read from the game's tables (names, rows, flags), kept for as long as the tables and the language stay the
/// same. The game loads its tables once per run, but a table read again (a new pointer) or a language switch drops
/// everything, so nothing from the old tables or in the old language is shown. Main thread only.
/// </summary>
public sealed class TableCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _values = new();
    private long _table;
    private bool _isChinese;

    public int Count => _values.Count;

    /// <summary>Call before each read with the table's address and the game's language; a change empties the cache.</summary>
    public void Use(long table, bool isChinese)
    {
        if (table == _table && isChinese == _isChinese) return;

        _values.Clear();
        _table = table;
        _isChinese = isChinese;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_values.TryGetValue(key, out var found))
        {
            value = found!;

            return true;
        }

        value = default!;

        return false;
    }

    public TValue Set(TKey key, TValue value) => _values[key] = value;

    /// <summary>Empties the cache whatever the table, for a change Use cannot see (a new world loaded).</summary>
    public void Clear() => _values.Clear();
}
