using System.Text.Json;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

/// <summary>The home storage list beside a station: what each storage holds, and which of it the station takes.</summary>
public class StationViewTests
{
    private static readonly Storage Drone = new(500, "Drone", StorageKind.Furniture, DroneBay.Drone);
    private static readonly Storage Hub = new(600, "Drone hub", StorageKind.Furniture, DroneBay.Hub);

    [Fact]
    public void Only_home_furniture_is_listed_since_the_bag_is_already_on_screen_and_a_drone_holds_trade_goods()
    {
        var home = new Inventory(new[] { Bag, Fridge, Drawer, Drone, Hub }, Array.Empty<ItemStack>(), new Dictionary<int, string>());

        using var view = Parse(home, _ => false);

        Assert.Equal(new[] { "200", "600" }, view.RootElement.GetProperty("storages").EnumerateArray().Select(storage => storage.GetProperty("id").GetString()));
    }

    [Fact]
    public void Stacks_of_one_item_in_one_storage_show_as_one_row_with_their_total()
    {
        var home = Home(Stack(1, Apple, 3, Fridge), Stack(2, Apple, 4, Fridge), Stack(3, Apple, 9, Box), Stack(4, Apple, 5, Bag));

        using var view = Parse(home, _ => false);
        var fridge = Storage(view, Fridge);

        Assert.Single(fridge);
        Assert.Equal(7, fridge[0].GetProperty("count").GetInt32());
        Assert.Equal(9, Storage(view, Box)[0].GetProperty("count").GetInt32());
    }

    [Fact]
    public void A_row_the_station_takes_hands_over_a_stack_the_station_takes_even_when_another_stack_of_it_is_listed_first()
    {
        // Two stacks of one food, the first too spoiled for the rat cage: the click must send the fresh one.
        var home = Home(Stack(1, Apple, 3, Fridge), Stack(2, Apple, 4, Fridge));

        using var view = Parse(home, stack => stack.ItemId == 2);
        var apple = Storage(view, Fridge)[0];

        Assert.True(apple.GetProperty("ok").GetBoolean());
        Assert.Equal("2", apple.GetProperty("item").GetString());
    }

    [Fact]
    public void A_row_the_station_does_not_take_is_marked_so_a_click_sends_it_to_the_bag()
    {
        using var view = Parse(Home(Stack(1, Rope, 1, Box)), stack => stack.ConfigId == Apple);
        var rope = Storage(view, Box)[0];

        Assert.False(rope.GetProperty("ok").GetBoolean());
        Assert.Equal(Rope, rope.GetProperty("cfg").GetInt32());
        Assert.Equal("Rope", rope.GetProperty("name").GetString());
    }

    [Fact]
    public void Rows_are_in_name_order_so_the_list_does_not_jump_around_as_stacks_move()
    {
        using var view = Parse(Home(Stack(1, Rope, 1, Box), Stack(2, RedBerry, 1, Box), Stack(3, Apple, 1, Box)), _ => false);

        Assert.Equal(new[] { "Red Apple", "Red Berry", "Rope" }, Storage(view, Box).Select(row => row.GetProperty("name").GetString()));
    }

    [Fact]
    public void The_view_names_the_station_and_carries_icons()
    {
        using var view = JsonDocument.Parse(StationView.Build(Home(Stack(1, Apple, 1, Fridge)), StationKind.RatCage, _ => true, _ => "Res/Food/apple.png"));

        Assert.Equal("ratcage", view.RootElement.GetProperty("kind").GetString());
        Assert.Equal("Res/Food/apple.png", Storage(view, Fridge)[0].GetProperty("icon").GetString());
    }

    [Fact]
    public void The_neighbors_basket_is_not_listed_beside_a_station_since_nothing_is_taken_back_out_of_it()
    {
        var basket = new Storage(800, "Basket", StorageKind.Basket);
        var home = new Inventory(new[] { Bag, Fridge, basket }, new[] { Stack(1, Apple, 1, basket) }, new Dictionary<int, string>());

        using var view = Parse(home, _ => true);

        Assert.Equal(new[] { "200" }, view.RootElement.GetProperty("storages").EnumerateArray().Select(storage => storage.GetProperty("id").GetString()));
    }

    [Fact]
    public void At_the_basket_the_rows_she_wants_are_marked_and_come_before_the_rest()
    {
        var home = Home(Stack(1, Apple, 1, Box), Stack(2, Rope, 1, Box), Stack(3, RedBerry, 1, Box));

        using var view = JsonDocument.Parse(StationView.Build(home, StationKind.Basket, _ => true, _ => null, configId => configId == Rope));
        var rows = Storage(view, Box);

        Assert.Equal(new[] { "Rope", "Red Apple", "Red Berry" }, rows.Select(row => row.GetProperty("name").GetString()));
        Assert.True(rows[0].GetProperty("wish").GetBoolean());
        Assert.False(rows[1].TryGetProperty("wish", out _));
    }

    [Fact]
    public void The_basket_view_names_the_storage_behind_each_tab_in_order()
    {
        using var view = JsonDocument.Parse(StationView.Build(Home(), StationKind.Basket, _ => true, _ => null, null, new long[] { 100, 300, 200 }));

        Assert.Equal(new[] { "100", "300", "200" }, view.RootElement.GetProperty("tabs").EnumerateArray().Select(tab => tab.GetString()));
    }

    [Fact]
    public void A_station_without_storage_tabs_sends_no_tab_list()
    {
        using var view = Parse(Home(), _ => true);

        Assert.False(view.RootElement.TryGetProperty("tabs", out _));
    }

    [Fact]
    public void Out_exploring_the_storage_view_says_it_is_away_so_the_page_keeps_its_inventory_window()
    {
        using var away = JsonDocument.Parse(StationView.Build(Home(), StationKind.Storage, _ => false, _ => null, tabs: new long[] { 100 }, isAway: true));
        using var atHome = JsonDocument.Parse(StationView.Build(Home(), StationKind.Storage, _ => true, _ => null, tabs: new long[] { 100, 200 }));

        Assert.Equal("storage", away.RootElement.GetProperty("kind").GetString());
        Assert.True(away.RootElement.GetProperty("away").GetBoolean());
        Assert.False(atHome.RootElement.TryGetProperty("away", out _));
    }

    [Fact]
    public void Every_station_the_view_names_is_one_the_page_can_ask_for_by_that_name()
    {
        foreach (var kind in Enum.GetValues<StationKind>()) Assert.Equal(kind, StationView.Parse(StationView.KindName(kind)));
    }

    private static JsonDocument Parse(Inventory home, Func<ItemStack, bool> isUsable) => JsonDocument.Parse(StationView.Build(home, StationKind.Cooking, isUsable, _ => null));

    private static List<JsonElement> Storage(JsonDocument view, Storage storage) =>
        view.RootElement.GetProperty("storages").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == storage.OwnerId.ToString()).GetProperty("items").EnumerateArray().ToList();
}
