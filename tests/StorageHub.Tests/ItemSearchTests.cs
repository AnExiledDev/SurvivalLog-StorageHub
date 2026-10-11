using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class ItemSearchTests
{
    [Fact]
    public void One_item_is_added_up_across_every_storage_that_holds_it()
    {
        var home = Home(Stack(1, Apple, 3, Bag), Stack(2, Apple, 5, Fridge), Stack(3, Apple, 2, Fridge), Stack(4, Rope, 1, Box));

        var apple = ItemSearch.Find(home, "apple").Single();

        Assert.Equal(10, apple.Total);
        Assert.Equal(new[] { ("Bag", 3), ("Fridge", 7) }, apple.Locations.Select(l => (l.Storage.Name, l.Count)));
        Assert.Equal(new long[] { 2, 3 }, apple.Locations[1].Stacks.Select(s => s.ItemId));
    }

    [Fact]
    public void Every_word_of_the_query_must_appear_in_the_name_ignoring_case()
    {
        var home = Home(Stack(1, Apple, 1, Box), Stack(2, RedBerry, 1, Box), Stack(3, Rope, 1, Box));

        Assert.Equal(new[] { "Red Apple" }, ItemSearch.Find(home, "APP red").Select(r => r.Name));
        Assert.Equal(new[] { "Red Apple", "Red Berry" }, ItemSearch.Find(home, "  red ").Select(r => r.Name));
    }

    [Fact]
    public void A_blank_query_lists_everything_from_A_to_Z()
    {
        var home = Home(Stack(1, Rope, 1, Box), Stack(2, RedBerry, 1, Box), Stack(3, Apple, 1, Bag));

        Assert.Equal(new[] { "Red Apple", "Red Berry", "Rope" }, ItemSearch.Find(home, "").Select(r => r.Name));
    }

    [Fact]
    public void Storages_are_listed_in_inventory_order_so_the_bag_comes_first()
    {
        var home = Home(Stack(1, Rope, 1, Drawer), Stack(2, Rope, 1, Box), Stack(3, Rope, 1, Bag));

        Assert.Equal(new[] { "Bag", "Wooden Box", "Workbench drawer" }, ItemSearch.Find(home, "").Single().Locations.Select(l => l.Storage.Name));
    }

    [Fact]
    public void Stacks_in_storages_outside_the_home_are_not_counted()
    {
        var home = Home(Stack(1, Rope, 4, Box), new ItemStack(2, Rope, 9, OwnerId: 999));

        Assert.Equal(4, ItemSearch.Find(home, "rope").Single().Total);
    }

    [Fact]
    public void An_item_with_no_known_name_is_still_listed_and_findable_by_its_id()
    {
        var home = Home(Stack(1, 4242, 1, Box));

        Assert.Equal("Item 4242", ItemSearch.Find(home, "4242").Single().Name);
    }
}
