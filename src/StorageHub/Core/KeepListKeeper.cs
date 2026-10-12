using System;
using System.Globalization;

namespace StorageHub.Core;

/// <summary>What became of one change to the keep list.</summary>
public enum KeepEdit
{
    Saved,

    /// <summary>The saved list could not be read, so nothing was written over it.</summary>
    NotRead,

    NotSaved
}

/// <summary>
/// The keep list in memory and the rule that keeps the saved one safe: a list that is there but could not be read is
/// never written over. A file that is not a keep list is copied aside under a timestamped name first, so a later bad
/// file cannot replace an earlier copy; a file that could not be read at all (locked, say) refuses every change until
/// a later read gets through.
/// </summary>
public sealed class KeepListKeeper
{
    private readonly IKeepListStore _store;
    private readonly Func<DateTime> _now;
    private readonly Action<string> _log;

    public KeepListKeeper(IKeepListStore store, Func<DateTime> now, Action<string> log)
    {
        _store = store;
        _now = now;
        _log = log;
        Read();
    }

    public KeepList List { get; private set; } = KeepList.Empty;

    public bool IsRead { get; private set; }

    /// <summary>Reads the saved list again when the last read failed. True once it is read.</summary>
    public bool TryRead()
    {
        if (!IsRead) Read();

        return IsRead;
    }

    /// <summary>
    /// The edit is applied to the list as last read, so a read that only now gets through is not saved over with an
    /// edit of the empty list. The list in memory changes only once the file holds the change.
    /// </summary>
    public KeepEdit Change(Func<KeepList, KeepList> edit)
    {
        if (!TryRead()) return KeepEdit.NotRead;

        var changed = edit(List);

        if (!_store.TryWrite(KeepListJson.Write(changed), out var why))
        {
            _log($"saving the keep list {_store.Name} failed: {why}");

            return KeepEdit.NotSaved;
        }

        List = changed;

        return KeepEdit.Saved;
    }

    private void Read()
    {
        switch (_store.Read())
        {
            case StoredText.Missing:
                Accept(KeepList.Empty);
                break;
            case StoredText.Found found:
                FromText(found.Text);
                break;
            case StoredText.Failed failed:
                Refuse($"the keep list {_store.Name} could not be read ({failed.Why}); it is left as it is and changes wait until it can be read");
                break;
            default:
                throw new InvalidOperationException("unknown stored text");
        }
    }

    private void FromText(string text)
    {
        switch (KeepListJson.Read(text))
        {
            case KeepListRead.Loaded loaded:
                Accept(loaded.List);
                _log($"keep list: {loaded.List.Amounts.Count} item(s) from {_store.Name}");
                break;
            case KeepListRead.Unreadable unreadable:
                SetAside(unreadable.Why);
                break;
            default:
                throw new InvalidOperationException("unknown keep list read");
        }
    }

    private void SetAside(string unreadableWhy)
    {
        var suffix = ".broken-" + _now().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        if (!_store.TryCopyAside(suffix, out var why))
        {
            Refuse($"the keep list {_store.Name} is not one this version reads ({unreadableWhy}) and copying it aside failed ({why}); it is left as it is and changes wait until it can be read");

            return;
        }

        Accept(KeepList.Empty);
        _log($"the keep list {_store.Name} is not one this version reads ({unreadableWhy}); it was copied to {_store.Name}{suffix} and the list starts empty");
    }

    private void Accept(KeepList list)
    {
        List = list;
        IsRead = true;
    }

    private void Refuse(string why)
    {
        List = KeepList.Empty;
        IsRead = false;
        _log(why);
    }
}
