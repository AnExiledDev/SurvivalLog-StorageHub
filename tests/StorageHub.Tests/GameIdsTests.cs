using System.Text.Json;
using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

/// <summary>
/// The ids the mod sends the game, checked against the tables Data Dump wrote for the build the mods compile against
/// (game-ref/BepInEx/data-dump). Without the dump on this machine there is nothing to check against.
/// </summary>
public class GameIdsTests
{
    /// <summary>A delivery put away counts toward events only while it reports the same id as the Retrieve supplies button.</summary>
    [Fact]
    [Trait("Category", "GameBuild")]
    public void The_retrieve_supplies_action_is_the_one_whose_effect_retrieves_the_drone()
    {
        var folder = DumpFolder();

        if (folder == null) return;

        var retrieveEffects = Rows(folder, "Effect").Where(r => r.GetProperty("ConfigName").GetString() == "Action/RetrieveDrone").Select(r => r.GetProperty("ID").GetInt32()).ToHashSet();
        var actions = Rows(folder, "Action").Where(r => retrieveEffects.Contains(r.GetProperty("EffectConfigID").GetInt32())).Select(r => r.GetProperty("ID").GetInt32()).ToList();

        Assert.Equal(new[] { GameIds.RetrieveSuppliesAction }, actions);
    }

    private static IEnumerable<JsonElement> Rows(string folder, string table) =>
        File.ReadLines(Path.Combine(folder, "Config_" + table + ".jsonl")).Where(line => line.Length > 0).Select(line => JsonDocument.Parse(line).RootElement);

    private static string? DumpFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "game-ref", "BepInEx", "data-dump");

            if (File.Exists(Path.Combine(candidate, "Config_Action.jsonl"))) return candidate;
        }

        return null;
    }
}
