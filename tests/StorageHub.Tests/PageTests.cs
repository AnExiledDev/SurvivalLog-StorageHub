using System.Text.Json;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class PageCommandTests
{
    [Theory]
    [InlineData("slmod|storagehub|take|1|200")]
    [InlineData("slmod|storagehub|takeAll|1")]
    [InlineData("slmod|storagehub|putAway|1")]
    public void Item_commands_name_the_item_and_storage_the_player_clicked(string message)
    {
        var expected = message.Split('|')[2] switch
        {
            "take" => (PageCommand)new PageCommand.Take(Apple, Fridge.OwnerId),
            "takeAll" => new PageCommand.TakeAll(Apple),
            _ => new PageCommand.PutAway(Apple),
        };

        Assert.Equal(expected, PageCommand.Parse(message));
    }

    [Fact]
    public void Take_one_names_the_item_and_the_storage_it_comes_from()
    {
        Assert.Equal(new PageCommand.TakeOne(Apple, Fridge.OwnerId), PageCommand.Parse("slmod|storagehub|takeOne|1|200"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|takeOne|1"));
    }

    [Fact]
    public void Put_one_names_the_item_the_storage_it_comes_from_and_the_station_container_it_goes_to()
    {
        Assert.Equal(new PageCommand.PutOne(Apple, Fridge.OwnerId, 900), PageCommand.Parse("slmod|storagehub|putOne|1|200|900"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|putOne|1|200"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|putOne|1|200|hopper"));
    }

    [Fact]
    public void Storage_ids_beyond_the_int_range_survive_the_round_trip()
    {
        Assert.Equal(new PageCommand.Take(7, 9_007_199_254_740_993), PageCommand.Parse("slmod|storagehub|take|7|9007199254740993"));
    }

    [Fact]
    public void Panel_commands_parse_without_arguments()
    {
        Assert.Equal(new PageCommand.PutAwayBag(), PageCommand.Parse("slmod|storagehub|putAwayBag"));
        Assert.Equal(new PageCommand.PutAwayFloor(), PageCommand.Parse("slmod|storagehub|putAwayFloor"));
        Assert.Equal(new PageCommand.Close(), PageCommand.Parse("slmod|storagehub|close"));
        Assert.Equal(new PageCommand.Ready(), PageCommand.Parse("slmod|storagehub|ready"));
        Assert.Equal(new PageCommand.NoScript(), PageCommand.Parse("slmod|storagehub|noscript"));
        Assert.Equal(new PageCommand.Toggle(), PageCommand.Parse("slmod|storagehub|toggle"));
        Assert.Equal(new PageCommand.KeepFill(), PageCommand.Parse("slmod|storagehub|keepFill"));
    }

    [Fact]
    public void Take_some_names_the_item_the_storage_and_how_many_and_refuses_zero()
    {
        Assert.Equal(new PageCommand.TakeSome(Apple, Fridge.OwnerId, 7), PageCommand.Parse("slmod|storagehub|takeSome|1|200|7"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|takeSome|1|200|0"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|takeSome|1|200"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|takeSome|1|200|-2"));
    }

    [Fact]
    public void The_view_says_whether_part_of_a_stack_can_be_taken()
    {
        using var on = JsonDocument.Parse(HubView.Build(Home(), _ => null));
        using var off = JsonDocument.Parse(HubView.Build(Home(), _ => null, canSplit: false));

        Assert.True(on.RootElement.GetProperty("canSplit").GetBoolean());
        Assert.False(off.RootElement.GetProperty("canSplit").GetBoolean());
    }

    [Fact]
    public void Drone_bags_are_marked_so_the_page_does_not_count_them_as_storage_to_fill_from()
    {
        var drone = new Storage(600, "Drone", StorageKind.Furniture, DroneBay.Drone);
        using var view = JsonDocument.Parse(HubView.Build(Home() with { Storages = new[] { Bag, Box, drone } }, _ => null));
        var storages = view.RootElement.GetProperty("storages").EnumerateArray().ToList();

        Assert.True(storages.Single(storage => storage.GetProperty("id").GetString() == "600").GetProperty("drone").GetBoolean());
        Assert.False(storages.Single(storage => storage.GetProperty("name").GetString() == Box.Name).TryGetProperty("drone", out _));
    }

    [Fact]
    public void Focus_tells_whether_the_search_box_wants_the_keyboard()
    {
        Assert.Equal(new PageCommand.Focus(true), PageCommand.Parse("slmod|storagehub|focus|1"));
        Assert.Equal(new PageCommand.Focus(false), PageCommand.Parse("slmod|storagehub|focus|0"));
    }

    [Fact]
    public void Menu_tells_whether_the_mods_menu_is_open_so_esc_closes_it_and_not_the_inventory()
    {
        Assert.Equal(new PageCommand.Menu(true), PageCommand.Parse("slmod|storagehub|menu|1"));
        Assert.Equal(new PageCommand.Menu(false), PageCommand.Parse("slmod|storagehub|menu|0"));
    }

    [Fact]
    public void Keep_commands_name_the_item_and_the_amount_to_leave_in_the_bag()
    {
        Assert.Equal(new PageCommand.SetKeep(Apple, 6), PageCommand.Parse("slmod|storagehub|keepSet|1|6"));
        Assert.Equal(new PageCommand.SetKeep(Apple, 0), PageCommand.Parse("slmod|storagehub|keepSet|1|0"));
        Assert.Equal(new PageCommand.RemoveKeep(Apple), PageCommand.Parse("slmod|storagehub|keepRemove|1"));
        Assert.Equal(new PageCommand.SendCatalog(), PageCommand.Parse("slmod|storagehub|catalog"));
    }

    [Theory]
    [InlineData("cooking", StationKind.Cooking)]
    [InlineData("ratcage", StationKind.RatCage)]
    [InlineData("inventory", StationKind.Inventory)]
    [InlineData("brew", StationKind.Brew)]
    [InlineData("dye", StationKind.Dye)]
    [InlineData("shredder", StationKind.Shredder)]
    [InlineData("compressor", StationKind.Compressor)]
    [InlineData("trade", StationKind.Trade)]
    [InlineData("workbench", StationKind.Workbench)]
    public void The_home_list_names_the_station_it_sits_beside(string name, StationKind kind)
    {
        Assert.Equal(new PageCommand.StationList(kind), PageCommand.Parse("slmod|storagehub|station|" + name));
    }

    [Fact]
    public void Open_names_the_storage_to_show_beside_the_bag_and_minus_one_closes_it()
    {
        Assert.Equal(new PageCommand.OpenStorage(9_007_199_254_740_993), PageCommand.Parse("slmod|storagehub|openStorage|9007199254740993"));
        Assert.Equal(new PageCommand.OpenStorage(-1), PageCommand.Parse("slmod|storagehub|openStorage|-1"));
    }

    [Theory]
    [InlineData("slmod|storagehub|take|apple|200")]
    [InlineData("slmod|storagehub|take|1")]
    [InlineData("slmod|storagehub|explode")]
    [InlineData("slmod|storagehub|")]
    [InlineData("slmod|storagehub|menu")]
    [InlineData("slmod|storagehub|menu|yes")]
    [InlineData("slmod|storagehub|keepSet|1")]
    [InlineData("slmod|storagehub|keepSet|1|-2")]
    [InlineData("slmod|storagehub|keepSet|1|2.5")]
    [InlineData("slmod|storagehub|keepSet|apple|2")]
    [InlineData("slmod|storagehub|keepSet|1|99999999999")]
    [InlineData("slmod|storagehub|keepRemove")]
    [InlineData("slmod|storagehub|keepRemove|apple")]
    [InlineData("slmod|storagehub|catalog|all")]
    [InlineData("slmod|storagehub|station")]
    [InlineData("slmod|storagehub|station|anvil")]
    [InlineData("slmod|storagehub|openStorage")]
    [InlineData("slmod|storagehub|openStorage|box")]
    public void A_malformed_command_is_never_acted_on(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void Only_messages_with_our_prefix_are_claimed_so_other_mods_and_the_game_keep_theirs()
    {
        Assert.True(PageCommand.IsOurs("slmod|storagehub|close"));
        Assert.False(PageCommand.IsOurs("slmod|itemtotals|popup shown"));
        Assert.False(PageCommand.IsOurs("3\u001eBackpack\u001eSORT_BACKPACK\u001e{}"));
        Assert.False(PageCommand.IsOurs(null));
    }
}

public class IconIndexTests
{
    private static readonly IconIndex Icons = IconIndex.Build(new[]
    {
        "Res/Food/UI_Item_Icon_Food_steak.png",
        "Res/Material/UI_Item_Icon_Mat_S_1503002201.png",
        "Res/Material/UI_Item_Icon_Shared.png",
        "Res/Structure/UI_Item_Icon_Shared.png",
    });

    [Theory]
    [InlineData("UI_Item_Icon_Food_steak")]
    [InlineData("UI_Item_Icon_Food_steak.png")]
    [InlineData("../../Res/Food/UI_Item_Icon_Food_steak.png")]
    [InlineData("Assets\\Art\\UI\\ui_item_icon_food_steak.png")]
    public void Any_form_of_an_icon_name_finds_the_web_file(string named)
    {
        Assert.Equal("Res/Food/UI_Item_Icon_Food_steak.png", Icons.Resolve(named));
    }

    [Fact]
    public void Two_files_with_one_name_are_told_apart_by_the_folder_the_item_names()
    {
        Assert.Equal("Res/Structure/UI_Item_Icon_Shared.png", Icons.Resolve("Res/Structure/UI_Item_Icon_Shared.png"));
        Assert.Equal("Res/Material/UI_Item_Icon_Shared.png", Icons.Resolve("UI_Item_Icon_Shared"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UI_Item_Icon_Missing")]
    public void An_item_without_a_web_icon_gets_none(string? named)
    {
        Assert.Null(Icons.Resolve(named));
    }
}

public class HubViewTests
{
    [Fact]
    public void The_page_gets_each_item_with_its_total_icon_and_where_it_sits()
    {
        var home = Home(Stack(1, Apple, 3, Bag), Stack(2, Apple, 5, Fridge), Stack(3, Apple, 2, Fridge), Stack(4, Rope, 1, Box));

        using var view = JsonDocument.Parse(HubView.Build(home, configId => configId == Apple ? "Res/Food/apple.png" : null));
        var apple = view.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "Red Apple");
        var rope = view.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "Rope");

        Assert.Equal(Apple, apple.GetProperty("id").GetInt32());
        Assert.Equal(10, apple.GetProperty("total").GetInt32());
        Assert.Equal("Res/Food/apple.png", apple.GetProperty("icon").GetString());
        Assert.Equal(new (string?, int)[] { ("100", 3), ("200", 7) }, apple.GetProperty("at").EnumerateArray().Select(at => (at[0].GetString(), at[1].GetInt32())));
        Assert.Equal(JsonValueKind.Null, rope.GetProperty("icon").ValueKind);
    }

    [Fact]
    public void Every_storage_is_listed_with_its_kind_so_the_page_knows_which_one_is_the_bag()
    {
        using var view = JsonDocument.Parse(HubView.Build(Home(), _ => null));

        var storages = view.RootElement.GetProperty("storages").EnumerateArray()
            .Select(storage => (storage.GetProperty("id").GetString(), storage.GetProperty("name").GetString(), storage.GetProperty("kind").GetString()));

        Assert.Equal(new (string?, string?, string?)[] { ("100", "Bag", "bag"), ("200", "Fridge", "storage"), ("300", "Wooden Box", "storage"), ("400", "Workbench drawer", "drawer") }, storages);
    }

    [Fact]
    public void Names_with_quotes_slashes_and_line_breaks_reach_the_page_intact()
    {
        var name = "Bob's \"best\" \\ jerky\n" + (char)0x2028 + "</script>";
        var home = new Inventory(new[] { Bag }, new[] { Stack(1, Apple, 1, Bag) }, new Dictionary<int, string> { [Apple] = name });

        using var view = JsonDocument.Parse(HubView.Build(home, _ => null));

        Assert.Equal(name, view.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void A_string_literal_survives_being_pasted_into_a_script()
    {
        var text = "Moved 3 stacks. \"Rope\" had nowhere to go" + (char)0x2029;

        Assert.Equal(text, JsonSerializer.Deserialize<string>(HubView.Str(text)));
        Assert.Equal("\"\"", HubView.Str(null));
    }
}
