using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

/// <summary>Which home storages become tabs beside the bag when a storage, the basket or the trunk is open.</summary>
public class LootTabsTests
{
    private static readonly LootTabs.Place Fridge = new(200, false);
    private static readonly LootTabs.Place Cabinet = new(300, false);
    private static readonly LootTabs.Place EmptyShelf = new(400, false);
    private static readonly LootTabs.Place Drone = new(500, true);
    private static readonly LootTabs.Place[] Homes = { Fridge, Cabinet, EmptyShelf, Drone };

    [Fact]
    public void At_home_every_storage_gets_a_tab_whatever_it_holds_and_whether_or_not_anything_is_asked_for()
    {
        Assert.Equal(new long[] { 200, 300, 400 }, LootTabs.ToAdd(true, false, Homes, new long[] { 1 }));
    }

    [Fact]
    public void The_open_storage_and_tabs_the_game_already_shows_are_not_added_twice()
    {
        // The cabinet is open on the right; the bag (1) and a car box (9) are the game's own tabs.
        Assert.Equal(new long[] { 200, 400 }, LootTabs.ToAdd(true, false, Homes, new long[] { 300, 1, 9 }));
    }

    [Fact]
    public void Out_exploring_the_looting_screen_stays_as_the_game_made_it()
    {
        Assert.Empty(LootTabs.ToAdd(true, true, Homes, new long[] { 1 }));
    }

    [Fact]
    public void The_bag_alone_gets_no_storage_tabs()
    {
        Assert.Empty(LootTabs.ToAdd(false, false, Homes, new long[] { 1 }));
    }

    [Fact]
    public void A_drone_never_gets_a_tab_since_its_bag_holds_goods_it_is_about_to_trade_away()
    {
        Assert.DoesNotContain(500L, LootTabs.ToAdd(true, false, Homes, new long[] { 1 }));
    }
}
