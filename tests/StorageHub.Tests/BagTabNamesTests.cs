using System.Collections.Generic;
using System.Text.Json;
using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

/// <summary>The basket's storage tabs carry their storage's name, where the game would call every one "Backpack".</summary>
public class BagTabNamesTests
{
    // The shape Reducer_Web_BackpackUI.BuildATabsJson writes.
    private const string GameTabs = "[{\"name\":\"Character Inventory\",\"cols\":8,\"rows\":6},{\"name\":\"Backpack\",\"cols\":6,\"rows\":4},{\"name\":\"Backpack\",\"cols\":10,\"rows\":8,\"isTrunkTab\":false}]";

    private static List<(string Name, int Cols)> Tabs(string json)
    {
        var tabs = new List<(string, int)>();

        foreach (var tab in JsonDocument.Parse(json).RootElement.EnumerateArray()) tabs.Add((tab.GetProperty("name").GetString()!, tab.GetProperty("cols").GetInt32()));

        return tabs;
    }

    [Fact]
    public void Each_named_tab_gets_its_storage_name_and_keeps_its_size_while_the_bag_tab_keeps_the_game_name()
    {
        var json = BagTabNames.Rename(GameTabs, new Dictionary<int, string> { [1] = "Fridge", [2] = "Wooden Box 2" });

        Assert.Equal(new List<(string, int)> { ("Character Inventory", 8), ("Fridge", 6), ("Wooden Box 2", 10) }, Tabs(json));
        Assert.Contains("\"isTrunkTab\":false", json);
    }

    [Fact]
    public void Chinese_names_and_quotes_survive_for_the_page_to_parse()
    {
        var json = BagTabNames.Rename(GameTabs, new Dictionary<int, string> { [1] = "冰箱", [2] = "Box \"A\"" });

        Assert.Equal("冰箱", Tabs(json)[1].Name);
        Assert.Equal("Box \"A\"", Tabs(json)[2].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"name\":\"Backpack\"}")]
    public void Anything_that_is_not_a_tab_list_comes_back_unchanged(string json)
    {
        Assert.Equal(json, BagTabNames.Rename(json, new Dictionary<int, string> { [0] = "Fridge" }));
    }

    [Fact]
    public void A_place_outside_the_list_is_ignored()
    {
        Assert.Equal(new List<(string, int)> { ("Character Inventory", 8), ("Backpack", 6), ("Backpack", 10) }, Tabs(BagTabNames.Rename(GameTabs, new Dictionary<int, string> { [3] = "Fridge", [-1] = "Shelf" })));
    }
}
