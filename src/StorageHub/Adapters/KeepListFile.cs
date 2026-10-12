using System;
using System.IO;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// The keep list as one JSON file beside the mod's config, shared by every save. A file this version cannot read is
/// set aside as .broken and the list starts empty, so the player's next change does not silently overwrite it.
/// </summary>
internal sealed class KeepListFile : IKeepListStore
{
    private readonly string _path;
    private readonly Action<string> _log;

    public KeepListFile(string path, Action<string> log)
    {
        _path = path;
        _log = log;
    }

    public KeepList Load()
    {
        try
        {
            return File.Exists(_path) ? FromText(File.ReadAllText(_path)) : KeepList.Empty;
        }
        catch (Exception ex)
        {
            _log($"reading the keep list {_path} failed, starting with an empty one: {ex.Message}");

            return KeepList.Empty;
        }
    }

    public bool Save(KeepList list)
    {
        var temporary = _path + ".tmp";

        try
        {
            File.WriteAllText(temporary, KeepListJson.Write(list));
            File.Move(temporary, _path, true);

            return true;
        }
        catch (Exception ex)
        {
            _log($"saving the keep list {_path} failed: {ex.Message}");

            return false;
        }
    }

    private KeepList FromText(string text)
    {
        switch (KeepListJson.Read(text))
        {
            case KeepListRead.Loaded loaded:
                _log($"keep list: {loaded.List.Amounts.Count} item(s) from {_path}");

                return loaded.List;
            case KeepListRead.Unreadable unreadable:
                SetAside(unreadable.Why);

                return KeepList.Empty;
            default:
                throw new InvalidOperationException("unknown keep list read");
        }
    }

    private void SetAside(string why)
    {
        var broken = _path + ".broken";

        File.Copy(_path, broken, true);
        _log($"the keep list {_path} could not be read ({why}); it was copied to {broken} and the list starts empty");
    }
}
