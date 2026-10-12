using System.Text.Json;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class BagKeepTests
{
    private static readonly IReadOnlyDictionary<long, float> NoClocks = new Dictionary<long, float>();

    private static KeepList Keep(int configId, int amount) => KeepList.Empty.With(configId, amount);

    [Fact]
    public void An_item_kept_at_zero_is_put_away_like_any_other()
    {
        var apples = Stack(1, Apple, 5, Bag);

        var split = BagKeep.Split(new[] { apples }, Keep(Apple, 0), NoClocks);

        Assert.Equal(new[] { apples }, split.PutAway);
        Assert.Empty(split.Kept);
    }

    [Fact]
    public void Asking_to_keep_more_than_the_bag_holds_keeps_every_stack_of_it()
    {
        var apples = new[] { Stack(1, Apple, 10, Bag), Stack(2, Apple, 5, Bag) };

        var split = BagKeep.Split(apples, Keep(Apple, 50), NoClocks);

        Assert.Empty(split.PutAway);
        Assert.Equal(apples, split.Kept);
    }

    [Fact]
    public void A_stack_bigger_than_the_amount_stays_whole_because_the_game_cannot_split_it()
    {
        var apples = Stack(1, Apple, 10, Bag);

        var split = BagKeep.Split(new[] { apples }, Keep(Apple, 3), NoClocks);

        Assert.Equal(new[] { apples }, split.Kept);
        Assert.Empty(split.PutAway);
    }

    [Fact]
    public void The_smallest_stack_that_covers_the_amount_is_kept_so_the_bag_holds_as_few_extra_as_possible()
    {
        var full = Stack(1, Apple, 20, Bag);
        var partial = Stack(2, Apple, 6, Bag);

        var split = BagKeep.Split(new[] { full, partial }, Keep(Apple, 5), NoClocks);

        Assert.Equal(new[] { partial }, split.Kept);
        Assert.Equal(new[] { full }, split.PutAway);
    }

    [Fact]
    public void When_no_one_stack_covers_the_amount_the_fullest_are_kept_until_it_is_covered()
    {
        var first = Stack(1, Apple, 20, Bag);
        var second = Stack(2, Apple, 20, Bag);
        var partial = Stack(3, Apple, 6, Bag);
        var days = new Dictionary<long, float> { [1] = 2f, [2] = 7f, [3] = 4f };

        var split = BagKeep.Split(new[] { first, second, partial }, Keep(Apple, 25), days);

        Assert.Equal(new[] { second, partial }, split.Kept);
        Assert.Equal(new[] { first }, split.PutAway);
    }

    [Fact]
    public void Between_two_stacks_of_one_size_the_fresher_one_stays_in_the_bag()
    {
        var older = Stack(1, Apple, 5, Bag);
        var fresher = Stack(2, Apple, 5, Bag);
        var days = new Dictionary<long, float> { [1] = 1.5f, [2] = 6f };

        var split = BagKeep.Split(new[] { older, fresher }, Keep(Apple, 5), days);

        Assert.Equal(new[] { fresher }, split.Kept);
        Assert.Equal(new[] { older }, split.PutAway);
    }

    [Fact]
    public void A_stack_that_never_spoils_counts_as_the_freshest()
    {
        var spoiling = Stack(1, Apple, 5, Bag);
        var stopped = Stack(2, Apple, 5, Bag);
        var days = new Dictionary<long, float> { [1] = 30f, [2] = -1f };

        var split = BagKeep.Split(new[] { spoiling, stopped }, Keep(Apple, 5), days);

        Assert.Equal(new[] { stopped }, split.Kept);
    }

    [Fact]
    public void Items_not_on_the_list_are_all_put_away()
    {
        var apples = Stack(1, Apple, 5, Bag);
        var rope = Stack(2, Rope, 3, Bag);

        var split = BagKeep.Split(new[] { apples, rope }, Keep(Apple, 5), NoClocks);

        Assert.Equal(new[] { rope }, split.PutAway);
        Assert.Equal(new[] { apples }, split.Kept);
    }

    [Fact]
    public void The_status_line_names_what_the_keep_list_held_back()
    {
        var split = new KeepSplit(Array.Empty<ItemStack>(), new[] { Stack(1, Rope, 2, Bag), Stack(2, Apple, 4, Bag), Stack(3, Apple, 2, Bag) });

        Assert.Equal("Kept in your bag: 6 Red Apple, 2 Rope.", BagKeep.Describe(split, Home()));
    }

    [Fact]
    public void Nothing_kept_adds_nothing_to_the_status_line()
    {
        Assert.Equal("", BagKeep.Describe(new KeepSplit(new[] { Stack(1, Rope, 2, Bag) }, Array.Empty<ItemStack>()), Home()));
    }

    [Fact]
    public void The_panel_counts_only_what_is_in_the_bag_and_lists_kept_items_the_player_has_none_of()
    {
        var home = Home(Stack(1, Apple, 4, Bag), Stack(2, Apple, 9, Fridge), Stack(3, Apple, 2, Bag));
        var keep = KeepList.Empty.With(Apple, 5).With(RedBerry, 3);

        var rows = BagKeep.Rows(home, keep);

        Assert.Equal(new[] { new KeepRow(Apple, "Red Apple", 5, 6), new KeepRow(RedBerry, "Red Berry", 3, 0) }, rows);
    }
}

public class KeepListTests
{
    [Fact]
    public void Amounts_are_held_between_zero_and_the_most_the_panel_allows()
    {
        var keep = KeepList.Empty.With(Apple, -4).With(Rope, 1_000_000);

        Assert.Equal(0, keep.AmountOf(Apple));
        Assert.True(keep.Has(Apple));
        Assert.Equal(KeepList.MaxAmount, keep.AmountOf(Rope));
    }

    [Fact]
    public void Removing_an_item_takes_it_off_the_list_and_leaves_the_old_list_alone()
    {
        var keep = KeepList.Empty.With(Apple, 5).With(Rope, 2);

        var without = keep.Without(Apple);

        Assert.False(without.Has(Apple));
        Assert.Equal(2, without.AmountOf(Rope));
        Assert.Equal(5, keep.AmountOf(Apple));
    }

    [Fact]
    public void A_saved_list_reads_back_the_same()
    {
        var keep = KeepList.Empty.With(Rope, 2).With(Apple, 5).With(RedBerry, 0);

        var read = Assert.IsType<KeepListRead.Loaded>(KeepListJson.Read(KeepListJson.Write(keep)));

        Assert.Equal(keep.Amounts.OrderBy(entry => entry.Key), read.List.Amounts.OrderBy(entry => entry.Key));
    }

    [Fact]
    public void The_file_is_versioned_so_a_later_format_can_be_told_apart()
    {
        using var file = JsonDocument.Parse(KeepListJson.Write(KeepList.Empty.With(Apple, 5)));

        Assert.Equal(KeepListJson.Version, file.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(5, file.RootElement.GetProperty("keep")[0].GetProperty("amount").GetInt32());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"version\":2,\"keep\":[]}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"keep\":[{\"item\":1,\"amount\":5},{\"item\":\"rope\",\"amount\":2}]}")]
    [InlineData("{\"version\":1,\"keep\":[{\"item\":1}]}")]
    public void A_file_this_version_cannot_fully_read_is_refused_whole_rather_than_half_used(string text)
    {
        Assert.IsType<KeepListRead.Unreadable>(KeepListJson.Read(text));
    }
}

public class KeepViewTests
{
    [Fact]
    public void The_page_gets_each_kept_item_with_its_icon_amount_and_how_many_the_bag_holds()
    {
        var keep = new KeepView(true, new[] { new KeepRow(Apple, "Red Apple", 5, 6) });

        using var view = JsonDocument.Parse(HubView.Build(Home(), configId => configId == Apple ? "Res/Food/apple.png" : null, keep: keep));
        var kept = view.RootElement.GetProperty("keep");
        var apple = kept.GetProperty("rows")[0];

        Assert.True(kept.GetProperty("on").GetBoolean());
        Assert.Equal((Apple, "Red Apple", "Res/Food/apple.png", 5, 6), (apple.GetProperty("id").GetInt32(), apple.GetProperty("name").GetString(), apple.GetProperty("icon").GetString(), apple.GetProperty("amount").GetInt32(), apple.GetProperty("held").GetInt32()));
    }

    [Fact]
    public void The_page_is_told_when_the_keep_list_is_turned_off_in_the_config()
    {
        using var view = JsonDocument.Parse(HubView.Build(Home(), _ => null, keep: new KeepView(false, Array.Empty<KeepRow>())));

        Assert.False(view.RootElement.GetProperty("keep").GetProperty("on").GetBoolean());
    }

    [Fact]
    public void The_search_catalog_is_sorted_by_name_with_icons()
    {
        var catalog = new[] { new CatalogItem(Rope, "rope"), new CatalogItem(RedBerry, "Red Berry"), new CatalogItem(Apple, "Apple") };

        using var json = JsonDocument.Parse(HubView.Catalog(catalog, configId => configId == Apple ? "Res/Food/apple.png" : null));
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(new[] { "Apple", "Red Berry", "rope" }, rows.Select(row => row.GetProperty("name").GetString()));
        Assert.Equal("Res/Food/apple.png", rows[0].GetProperty("icon").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("icon").ValueKind);
    }
}
