using System.Text.Json;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class RottingSoonTests
{
    private const float NeverSpoilsByGame = -1f;

    [Fact]
    public void Food_is_listed_soonest_first_across_the_bag_storages_and_floor()
    {
        var home = Home(Stack(1, Apple, 2, Fridge), Stack(2, RedBerry, 5, Bag));
        var floor = new[] { Stack(3, Apple, 1, Floor) };
        var days = new Dictionary<long, float> { [1] = 3f, [2] = 0.5f, [3] = 1.25f };

        var rows = RottingSoon.Rows(home, floor, days);

        Assert.Equal(new[] { ("Red Berry", "Bag", "12h"), ("Red Apple", "Floor", "1d 6h"), ("Red Apple", "Fridge", "3d 0h") }, rows.Select(row => (row.Name, row.Where, row.Label)));
    }

    [Fact]
    public void Items_that_never_spoil_or_have_no_shelf_life_read_are_left_out()
    {
        var home = Home(Stack(1, Rope, 1, Box), Stack(2, Apple, 2, Fridge), Stack(3, RedBerry, 1, Bag));
        var days = new Dictionary<long, float> { [1] = NeverSpoilsByGame, [2] = Freshness.NeverSpoils };

        Assert.Empty(RottingSoon.Rows(home, Array.Empty<ItemStack>(), days));
    }

    [Fact]
    public void Spoiled_food_comes_first_and_says_so()
    {
        var home = Home(Stack(1, Apple, 2, Fridge), Stack(2, RedBerry, 1, Bag));
        var days = new Dictionary<long, float> { [1] = 0.01f, [2] = 0f };

        var rows = RottingSoon.Rows(home, Array.Empty<ItemStack>(), days);

        Assert.Equal(new[] { "spoiled", "under 1h" }, rows.Select(row => row.Label));
    }

    [Fact]
    public void Stacks_of_one_kind_in_one_place_spoiling_in_the_same_hour_share_a_row()
    {
        var home = Home(Stack(1, Apple, 2, Fridge), Stack(2, Apple, 3, Fridge), Stack(3, Apple, 4, Fridge), Stack(4, Apple, 1, Box));
        var days = new Dictionary<long, float> { [1] = 1.01f, [2] = 1.02f, [3] = 2f, [4] = 1.01f };

        var rows = RottingSoon.Rows(home, Array.Empty<ItemStack>(), days);

        Assert.Equal(new[] { ("Fridge", 5, "1d 0h"), ("Wooden Box", 1, "1d 0h"), ("Fridge", 4, "2d 0h") }, rows.Select(row => (row.Where, row.Count, row.Label)));
    }

    [Theory]
    [InlineData(-1, "spoiled")]
    [InlineData(0, "under 1h")]
    [InlineData(5, "5h")]
    [InlineData(23, "23h")]
    [InlineData(24, "1d 0h")]
    [InlineData(51, "2d 3h")]
    public void Time_left_reads_in_days_and_whole_hours(int hoursLeft, string label)
    {
        Assert.Equal(label, RottingSoon.Label(hoursLeft));
    }

    [Fact]
    public void The_page_gets_the_rotting_list_with_where_each_row_sits_and_its_time_left()
    {
        var home = Home(Stack(1, Apple, 2, Fridge));
        var rot = RottingSoon.Rows(home, Array.Empty<ItemStack>(), new Dictionary<long, float> { [1] = 0.25f });

        using var view = JsonDocument.Parse(HubView.Build(home, configId => configId == Apple ? "Res/Food/apple.png" : null, rot));
        var row = Assert.Single(view.RootElement.GetProperty("rot").EnumerateArray().ToList());

        Assert.Equal(Apple, row.GetProperty("id").GetInt32());
        Assert.Equal("Red Apple", row.GetProperty("name").GetString());
        Assert.Equal("Res/Food/apple.png", row.GetProperty("icon").GetString());
        Assert.Equal(2, row.GetProperty("count").GetInt32());
        Assert.Equal("200", row.GetProperty("at").GetString());
        Assert.Equal("Fridge", row.GetProperty("where").GetString());
        Assert.Equal("6h", row.GetProperty("left").GetString());
    }

    [Fact]
    public void Without_a_rotting_list_the_page_gets_an_empty_one()
    {
        using var view = JsonDocument.Parse(HubView.Build(Home(), _ => null));

        Assert.Equal(0, view.RootElement.GetProperty("rot").GetArrayLength());
    }
}
