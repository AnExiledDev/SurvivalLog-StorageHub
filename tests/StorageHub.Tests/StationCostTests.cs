using System.Text.RegularExpressions;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

/// <summary>
/// A station screen asks for its home list every few hundred milliseconds. These guard that an unchanged home builds
/// nothing, that every real change still gets through, and that the kept answers give the same list as a fresh build.
/// </summary>
public class StationCostTests
{
    private const long Table = 0x1000;

    private static readonly IReadOnlyList<long> Tabs = new List<long> { Bag.OwnerId, Box.OwnerId };

    private static readonly BasketWish Wish = new(true, false, false, false, new List<int> { 1 }, null);

    private static Inventory Idle(bool isChinese = false) =>
        Home(Stack(1, Apple, 3, Box), Stack(2, Rope, 1, Fridge)) with { IsChinese = isChinese };

    private static long? Print(Inventory inventory, StationKind kind = StationKind.Cooking, long table = Table, IReadOnlyList<long>? tabs = null, BasketWish? wish = null, bool isAway = false, bool hasIcons = false) =>
        ViewPrint.OfStation(inventory, kind, table, tabs ?? Tabs, wish ?? Wish, isAway, hasIcons);

    // ---- The station's fingerprint ----

    [Fact]
    public void An_unchanged_home_gives_the_same_station_print_so_nothing_is_built_again()
    {
        Assert.Equal(Print(Idle()), Print(Idle()));
        Assert.NotNull(Print(Idle()));
    }

    [Fact]
    public void A_count_that_changed_in_place_moves_the_print()
    {
        var after = Home(Stack(1, Apple, 2, Box), Stack(2, Rope, 1, Fridge));

        Assert.NotEqual(Print(Idle()), Print(after));
    }

    [Fact]
    public void A_stack_moved_to_another_storage_moves_the_print()
    {
        var after = Home(Stack(1, Apple, 3, Bag), Stack(2, Rope, 1, Fridge));

        Assert.NotEqual(Print(Idle()), Print(after));
    }

    [Fact]
    public void A_stack_taken_away_moves_the_print()
    {
        Assert.NotEqual(Print(Idle()), Print(Home(Stack(1, Apple, 3, Box))));
    }

    [Fact]
    public void A_language_change_moves_the_print()
    {
        Assert.NotEqual(Print(Idle()), Print(Idle(isChinese: true)));
    }

    [Fact]
    public void A_renamed_item_moves_the_print()
    {
        var renamed = Idle() with { ItemNames = new Dictionary<int, string> { [Apple] = "Green Apple", [Rope] = "Rope", [RedBerry] = "Red Berry" } };

        Assert.NotEqual(Print(Idle()), Print(renamed));
    }

    [Fact]
    public void A_reloaded_item_table_moves_the_print()
    {
        Assert.NotEqual(Print(Idle()), Print(Idle(), table: Table + 8));
    }

    [Fact]
    public void Different_tabs_move_the_print_and_no_tabs_differ_from_empty_tabs()
    {
        Assert.NotEqual(Print(Idle()), Print(Idle(), tabs: new List<long> { Box.OwnerId, Bag.OwnerId }));
        Assert.NotEqual(
            ViewPrint.OfStation(Idle(), StationKind.Cooking, Table, null, Wish, false, false),
            ViewPrint.OfStation(Idle(), StationKind.Cooking, Table, new List<long>(), Wish, false, false));
    }

    [Fact]
    public void A_changed_basket_wish_moves_the_print()
    {
        Assert.NotEqual(Print(Idle()), Print(Idle(), wish: Wish with { Categories = new List<int> { 2 } }));
        Assert.NotEqual(Print(Idle()), Print(Idle(), wish: Wish with { IsPending = true }));
        Assert.NotEqual(Print(Idle()), Print(Idle(), wish: Wish with { CrisisCategory = 1 }));
        Assert.NotEqual(
            ViewPrint.OfStation(Idle(), StationKind.Basket, Table, Tabs, null, false, false),
            ViewPrint.OfStation(Idle(), StationKind.Basket, Table, Tabs, Wish, false, false));
    }

    [Fact]
    public void Going_away_and_icons_arriving_move_the_print()
    {
        Assert.NotEqual(Print(Idle()), Print(Idle(), isAway: true));
        Assert.NotEqual(Print(Idle()), Print(Idle(), hasIcons: true));
    }

    [Fact]
    public void Each_station_has_its_own_print()
    {
        Assert.NotEqual(Print(Idle(), StationKind.Cooking), Print(Idle(), StationKind.Brew));
    }

    [Fact]
    public void The_rat_cage_has_no_print_because_its_answer_turns_on_spoilage()
    {
        Assert.Null(Print(Idle(), StationKind.RatCage));
        Assert.False(StationView.TakesByKindOnly(StationKind.RatCage));
        Assert.All(Enum.GetValues<StationKind>().Where(kind => kind != StationKind.RatCage), kind => Assert.True(StationView.TakesByKindOnly(kind)));
    }

    // ---- The station's check, once per kind of item ----

    [Fact]
    public void The_usable_check_is_asked_once_per_kind_of_item()
    {
        var asked = new List<int>();
        var usable = StationView.UsableByKind(configId =>
        {
            asked.Add(configId);

            return configId == Apple;
        });

        Assert.True(usable(Stack(1, Apple, 3, Box)));
        Assert.True(usable(Stack(5, Apple, 1, Bag)));
        Assert.False(usable(Stack(2, Rope, 1, Fridge)));
        Assert.False(usable(Stack(6, Rope, 4, Box)));
        Assert.Equal(new[] { Apple, Rope }, asked);
    }

    [Fact]
    public void A_list_built_with_the_kept_check_matches_one_built_asking_every_stack()
    {
        var home = Home(Stack(1, Apple, 3, Box), Stack(2, Rope, 1, Fridge), Stack(3, Apple, 2, Bag), Stack(4, RedBerry, 5, Box));
        Func<int, bool> takes = configId => configId != Rope;

        var kept = StationView.Build(home, StationKind.Cooking, StationView.UsableByKind(takes), _ => null, null, Tabs);
        var fresh = StationView.Build(home, StationKind.Cooking, stack => takes(stack.ConfigId), _ => null, null, Tabs);

        Assert.Equal(fresh, kept);
    }

    // ---- Stacks kept between reads ----

    [Fact]
    public void An_unchanged_stack_is_handed_back_from_the_last_read()
    {
        var reuse = new StackReuse();

        reuse.Begin();
        var first = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        reuse.Begin();
        var second = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        Assert.Same(first, second);
        Assert.Equal(1, reuse.LastCount);
    }

    [Fact]
    public void A_stack_whose_count_kind_or_storage_changed_is_a_new_one_with_the_new_values()
    {
        var reuse = new StackReuse();

        reuse.Begin();
        var first = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        reuse.Begin();
        var counted = reuse.Get(1, Apple, 2, Box.OwnerId);
        reuse.End();

        Assert.NotSame(first, counted);
        Assert.Equal(new ItemStack(1, Apple, 2, Box.OwnerId), counted);

        reuse.Begin();
        var moved = reuse.Get(1, Apple, 2, Bag.OwnerId);
        reuse.End();

        Assert.Equal(new ItemStack(1, Apple, 2, Bag.OwnerId), moved);

        reuse.Begin();
        var other = reuse.Get(1, Rope, 2, Bag.OwnerId);
        reuse.End();

        Assert.Equal(new ItemStack(1, Rope, 2, Bag.OwnerId), other);
    }

    [Fact]
    public void A_stack_missing_from_a_read_is_let_go_and_comes_back_as_a_new_one()
    {
        var reuse = new StackReuse();

        reuse.Begin();
        var first = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.Get(2, Rope, 1, Box.OwnerId);
        reuse.End();

        reuse.Begin();
        reuse.Get(2, Rope, 1, Box.OwnerId);
        reuse.End();

        Assert.Equal(1, reuse.LastCount);

        reuse.Begin();
        var back = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        Assert.NotSame(first, back);
    }

    [Fact]
    public void A_read_that_stopped_part_way_does_not_leak_into_the_next()
    {
        var reuse = new StackReuse();

        reuse.Begin();
        var kept = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        // A read that threw: Begin without End.
        reuse.Begin();
        reuse.Get(9, Rope, 1, Box.OwnerId);

        reuse.Begin();
        var again = reuse.Get(1, Apple, 3, Box.OwnerId);
        reuse.End();

        Assert.Same(kept, again);
        Assert.Equal(1, reuse.LastCount);
    }

    // ---- The panel's fingerprint after the rewrite ----

    [Fact]
    public void The_panel_print_still_moves_with_a_count_and_stays_put_without_one()
    {
        var days = new Dictionary<long, float>();
        var floor = Array.Empty<ItemStack>();

        var idle = ViewPrint.Of(Idle(), floor, days, KeepList.Empty, true, true, false);

        Assert.Equal(idle, ViewPrint.Of(Idle(), floor, days, KeepList.Empty, true, true, false));
        Assert.NotEqual(idle, ViewPrint.Of(Home(Stack(1, Apple, 2, Box), Stack(2, Rope, 1, Fridge)), floor, days, KeepList.Empty, true, true, false));
        Assert.NotEqual(idle, ViewPrint.Of(Idle(), new[] { Stack(7, Apple, 1, Floor) }, days, KeepList.Empty, true, true, false));
        Assert.NotEqual(idle, ViewPrint.Of(Idle(isChinese: true), floor, days, KeepList.Empty, true, true, false));
    }

    // ---- Warm-up ----

    [Fact]
    public void The_warm_up_runs_all_the_way_and_comes_out_the_same_each_time()
    {
        var first = WarmUp.Run();

        Assert.Equal(first, WarmUp.Run());
        // The panel view, the move count, the delivery line, then one list per station.
        Assert.Equal(3 + Enum.GetValues<StationKind>().Length, first.Split('\n').Length);
        // The made-up delivery went through planning and placing: the box took the apples, the rope stayed in the drone.
        Assert.Equal("Drone delivery: 2 Apple to Box. Stayed in Drone: 1 Rope.", first.Split('\n')[2]);
    }

    // ---- The page's poll ----

    [Fact]
    public void The_page_poll_reads_the_frames_from_one_live_list()
    {
        var script = PageScript();

        Assert.Contains("var allFrames = document.getElementsByTagName('iframe');", script);
        Assert.DoesNotContain("document.querySelectorAll('iframe')", script);
    }

    [Fact]
    public void The_page_works_out_the_hud_stamp_only_when_the_key_or_the_language_changed()
    {
        var script = PageScript();

        Assert.Contains("var stamp = hudStamp();", script);
        Assert.Contains("if (hudStampFor.key !== hud.key || hudStampFor.words !== words()) {", script);
    }

    /// <summary>The page outside the menu block it shares with the other mods, which has its own frame walk.</summary>
    private static string PageScript()
    {
        using var stream = typeof(StationCostTests).Assembly.GetManifestResourceStream("StorageHub.page.js") ?? throw new InvalidOperationException("page.js is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
