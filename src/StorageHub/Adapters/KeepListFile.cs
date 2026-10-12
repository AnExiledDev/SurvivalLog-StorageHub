using System;
using System.IO;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// The keep list as one JSON file beside the mod's config, shared by every save. Only the file work lives here; what
/// to do when it cannot be read is KeepListKeeper's.
/// </summary>
internal sealed class KeepListFile : IKeepListStore
{
    private readonly string _path;

    public KeepListFile(string path)
    {
        _path = path;
    }

    public string Name => _path;

    /// <summary>Missing only when the file is really not there; File.Exists would also say false for a file it may not read.</summary>
    public StoredText Read()
    {
        try
        {
            return new StoredText.Found(File.ReadAllText(_path));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new StoredText.Missing();
        }
        catch (Exception ex)
        {
            return new StoredText.Failed(ex.Message);
        }
    }

    public bool TryCopyAside(string suffix, out string why)
    {
        try
        {
            File.Copy(_path, _path + suffix, false);
            why = "";

            return true;
        }
        catch (Exception ex)
        {
            why = ex.Message;

            return false;
        }
    }

    public bool TryWrite(string text, out string why)
    {
        var temporary = _path + ".tmp";

        try
        {
            File.WriteAllText(temporary, text);
            File.Move(temporary, _path, true);
            why = "";

            return true;
        }
        catch (Exception ex)
        {
            why = ex.Message;

            return false;
        }
    }
}
