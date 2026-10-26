using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// Indexes the icon files the game ships for its web UI, so the panel can show the same pictures. The folder walk
/// covers thousands of files, so it runs on a worker thread; only the folder's path is asked of Unity, on the main
/// thread, since Unity answers nothing off it.
/// </summary>
internal static class WebIcons
{
    /// <summary>Call on the main thread. The task never fails: a folder that cannot be read gives no icons and says why.</summary>
    public static Task<(IconIndex Icons, string Note)> StartScan()
    {
        var webUi = Path.Combine(Application.streamingAssetsPath, "WebUI");

        return Task.Run(() => Scan(webUi));
    }

    private static (IconIndex Icons, string Note) Scan(string webUi)
    {
        var res = Path.Combine(webUi, "Res");

        try
        {
            var files = Directory.EnumerateFiles(res, "*.png", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(webUi, file)).ToList();

            return (IconIndex.Build(files), $"{files.Count} web icons found under {res}");
        }
        catch (Exception ex)
        {
            return (IconIndex.Empty, $"no web icons, items will show letters instead: {ex.Message}");
        }
    }
}
