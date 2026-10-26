using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

/// <summary>
/// The work kept off the frame: answers kept until the tables or the language change, the catalog built a slice at a
/// time, the view rebuilt only when what it shows moved, and nothing sent to the page that it already has.
/// </summary>
public class RefreshCostTests
{
    // ---- The catalog, a slice at a time ----

    private static readonly IReadOnlyList<int> TableIds = Enumerable.Range(1, 60).ToList();

    /// <summary>Every seventh id is a row the catalog leaves out; names run backwards so the order has to be made.</summary>
    private static CatalogItem? EntryOf(int id) => id % 7 == 0 ? null : new CatalogItem(id, "Item " + (100 - id).ToString("D3"));

    private static string? IconOf(int id) => id % 3 == 0 ? $"Res/Item/{id}.png" : null;

    private static string Whole() => HubView.Catalog(TableIds.Select(EntryOf).OfType<CatalogItem>(), IconOf);

    [Fact]
    public void A_catalog_cut_into_the_smallest_slices_ends_with_what_the_whole_table_makes_at_once()
    {
        string? json = null;
        var job = new SlicedJob(CatalogBuild.Steps(() => true, TableIds, EntryOf, IconOf, (_, built) => json = built));
        var slices = 1;

        // A slice spent after its first step: no frame carries more than one item.
        while (!job.Advance(() => true)) slices++;

        Assert.True(slices > TableIds.Count);
        Assert.Equal(Whole(), json);
    }

    [Fact]
    public void A_catalog_given_all_the_time_it_wants_ends_with_the_same_list()
    {
        string? json = null;
        IReadOnlyList<CatalogItem>? items = null;
        var job = new SlicedJob(CatalogBuild.Steps(() => true, TableIds, EntryOf, IconOf, (built, text) => (items, json) = (built, text)));

        Assert.True(job.Advance(() => false));
        Assert.Equal(Whole(), json);
        Assert.Equal(TableIds.Count(id => id % 7 != 0), items!.Count);
    }

    [Fact]
    public void The_catalog_waits_for_the_icons_so_items_that_have_one_are_not_sent_as_letters()
    {
        var areIconsIn = false;
        string? json = null;
        var job = new SlicedJob(CatalogBuild.Steps(() => areIconsIn, TableIds, EntryOf, id => areIconsIn ? IconOf(id) : null, (_, built) => json = built));

        for (var frame = 0; frame < 5; frame++) Assert.False(job.Advance(() => false));

        Assert.Null(json);

        areIconsIn = true;

        Assert.True(job.Advance(() => false));
        Assert.Equal(Whole(), json);
        Assert.Contains("Res/Item/3.png", json);
    }

    // ---- Answers kept per table and language ----

    [Fact]
    public void A_kept_answer_is_reused_while_the_table_and_the_language_stay_the_same()
    {
        var names = new TableCache<int, string>();

        names.Use(table: 0x1000, isChinese: false);
        names.Set(Apple, "Red Apple");
        names.Use(table: 0x1000, isChinese: false);

        Assert.True(names.TryGet(Apple, out var name));
        Assert.Equal("Red Apple", name);
    }

    [Fact]
    public void A_new_load_of_the_tables_drops_every_kept_answer()
    {
        var names = new TableCache<int, string>();

        names.Use(table: 0x1000, isChinese: false);
        names.Set(Apple, "Red Apple");
        names.Use(table: 0x2000, isChinese: false);

        Assert.False(names.TryGet(Apple, out _));
        Assert.Equal(0, names.Count);
    }

    [Fact]
    public void A_language_switch_drops_the_names_kept_in_the_old_language_and_the_new_ones_are_kept()
    {
        var names = new TableCache<int, string>();

        names.Use(table: 0x1000, isChinese: false);
        names.Set(Apple, "Red Apple");
        names.Use(table: 0x1000, isChinese: true);

        Assert.False(names.TryGet(Apple, out _));

        names.Set(Apple, "Red Apple (zh)");
        names.Use(table: 0x1000, isChinese: true);

        Assert.True(names.TryGet(Apple, out var name));
        Assert.Equal("Red Apple (zh)", name);
    }

    /// <summary>A new world on the same tables (another save, or a language pack) is a change the table and language cannot show.</summary>
    [Fact]
    public void Clearing_drops_kept_answers_even_on_the_same_table_and_language()
    {
        var names = new TableCache<int, string>();

        names.Use(table: 0x1000, isChinese: false);
        names.Set(Apple, "Red Apple");
        names.Clear();
        names.Use(table: 0x1000, isChinese: false);

        Assert.False(names.TryGet(Apple, out _));
    }

    // ---- The view, rebuilt only when it moved ----

    private static readonly IReadOnlyDictionary<long, float> NoDays = new Dictionary<long, float>();

    private static long Print(Inventory? inventory = null, IReadOnlyList<ItemStack>? floor = null, IReadOnlyDictionary<long, float>? days = null, KeepList? keep = null, bool isKeepOn = true, bool hasIcons = true) =>
        ViewPrint.Of(inventory ?? Home(Stack(1, Apple, 2, Fridge), Stack(2, Rope, 1, Box)), floor ?? Array.Empty<ItemStack>(), days ?? new Dictionary<long, float> { [1] = 1.02f }, keep ?? KeepList.Empty.With(Rope, 3), isKeepOn, canSplit: true, hasIcons);

    [Fact]
    public void The_same_home_read_again_prints_the_same()
    {
        Assert.Equal(Print(), Print());
    }

    [Fact]
    public void Food_a_little_older_within_the_same_hour_prints_the_same()
    {
        Assert.Equal(Print(), Print(days: new Dictionary<long, float> { [1] = 1.01f }));
    }

    [Fact]
    public void Every_change_the_panel_shows_moves_the_print()
    {
        var changes = new Dictionary<string, long>
        {
            ["a count"] = Print(inventory: Home(Stack(1, Apple, 3, Fridge), Stack(2, Rope, 1, Box))),
            ["a stack moved"] = Print(inventory: Home(Stack(1, Apple, 2, Box), Stack(2, Rope, 1, Box))),
            ["a stack taken out"] = Print(inventory: Home(Stack(1, Apple, 2, Fridge))),
            ["a storage renamed"] = Print(inventory: Home(Stack(1, Apple, 2, Fridge), Stack(2, Rope, 1, Box)) with { Storages = new[] { Bag, Fridge with { Name = "Cold box" }, Box, Drawer } }),
            ["an item renamed"] = Print(inventory: Home(Stack(1, Apple, 2, Fridge), Stack(2, Rope, 1, Box)) with { ItemNames = new Dictionary<int, string> { [Apple] = "Apple", [Rope] = "Rope", [RedBerry] = "Red Berry" } }),
            ["the language"] = Print(inventory: Home(Stack(1, Apple, 2, Fridge), Stack(2, Rope, 1, Box)) with { IsChinese = true }),
            ["food an hour older"] = Print(days: new Dictionary<long, float> { [1] = 1.02f - 1f / 24f }),
            ["food no longer spoiling"] = Print(days: NoDays),
            ["an item on the floor"] = Print(floor: new[] { Stack(3, RedBerry, 1, Floor) }),
            ["a keep amount"] = Print(keep: KeepList.Empty.With(Rope, 4)),
            ["a kept item added"] = Print(keep: KeepList.Empty.With(Rope, 3).With(Wood, 1)),
            ["the keep list turned off"] = Print(isKeepOn: false),
            ["the icons arriving"] = Print(hasIcons: false)
        };

        var unchanged = Print();

        Assert.All(changes, change => Assert.True(change.Value != unchanged, change.Key + " left the print as it was"));
    }

    [Fact]
    public void An_unchanged_print_hands_back_the_view_already_built_and_a_new_one_asks_for_a_build()
    {
        var built = new BuiltView();
        var view = built.Keep(Print(), "{\"view\":1}");

        Assert.True(built.TryGet(Print(), out var again));
        Assert.Same(view, again);
        Assert.False(built.TryGet(Print(hasIcons: false), out _));
    }

    [Fact]
    public void Nothing_counts_as_built_before_the_first_view()
    {
        Assert.False(new BuiltView().TryGet(0, out _));
    }

    // ---- Nothing sent that the page already has ----

    [Fact]
    public void The_words_go_to_the_page_once_until_the_language_or_key_changes_or_the_page_reloads()
    {
        var words = new WordsOnce();
        var english = PageWords.Script(isChinese: false, "F8");

        Assert.Equal(english, words.Pending(english));

        words.Sent(english);

        Assert.Equal("", words.Pending(english));

        var chinese = PageWords.Script(isChinese: true, "F8");

        Assert.Equal(chinese, words.Pending(chinese));

        words.Forget();

        Assert.Equal(english, words.Pending(english));
    }

    [Fact]
    public void An_unchanged_station_list_is_not_sent_to_a_page_that_still_holds_it()
    {
        var answers = new StationAnswers();

        Assert.True(answers.IsNew("{\"kind\":\"cooking\"}", pageHasList: true));

        answers.Sent("{\"kind\":\"cooking\"}");

        Assert.False(answers.IsNew("{\"kind\":\"cooking\"}", pageHasList: true));
    }

    [Fact]
    public void A_station_list_is_sent_again_when_it_changed_or_the_page_lost_it()
    {
        var answers = new StationAnswers();

        answers.Sent("{\"kind\":\"cooking\"}");

        Assert.True(answers.IsNew("{\"kind\":\"cooking\",\"storages\":[1]}", pageHasList: true));
        Assert.True(answers.IsNew("{\"kind\":\"cooking\"}", pageHasList: false));

        answers.Forget();

        Assert.True(answers.IsNew("{\"kind\":\"cooking\"}", pageHasList: true));
    }

    [Fact]
    public void A_station_ask_says_whether_the_page_still_holds_its_list()
    {
        Assert.Equal(new PageCommand.StationList(StationKind.Cooking, HasList: true), PageCommand.Parse("slmod|storagehub|station|cooking|1"));
        Assert.Equal(new PageCommand.StationList(StationKind.Cooking, HasList: false), PageCommand.Parse("slmod|storagehub|station|cooking"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|storagehub|station|cooking|0"));
    }
}
