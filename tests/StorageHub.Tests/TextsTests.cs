using System.Text.Json;
using System.Text.RegularExpressions;
using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

/// <summary>Every word the mod shows has an English and a Chinese version, and the game's language picks between them.</summary>
public class TextsTests
{
    public static TheoryData<int> Entries()
    {
        var data = new TheoryData<int>();

        foreach (var index in Enumerable.Range(0, Texts.All.Count)) data.Add(index);

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_text_has_plain_english_and_a_real_chinese_translation(int index)
    {
        var phrase = Texts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
        Assert.NotEqual(phrase.En, phrase.Zh);

        // A pattern of blanks only, like "{1} ×{0}", has no words to translate; everything else must be Chinese.
        var words = Regex.Replace(phrase.Zh, @"\{\d\}", "");

        if (words.Any(char.IsLetter)) Assert.Contains(phrase.Zh, ch => ch is >= '一' and <= '鿿');
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Both_languages_fill_in_the_same_blanks(int index)
    {
        var phrase = Texts.All[index];

        // English spells out the 1 in "Moved 1 stack.", so only that count blank may go unused.
        if (phrase != Texts.Moved.One) Assert.Equal(phrase.En.Contains("{0}"), phrase.Zh.Contains("{0}"));

        Assert.Equal(phrase.En.Contains("{1}"), phrase.Zh.Contains("{1}"));
        Assert.Equal(phrase.En.Contains("{2}"), phrase.Zh.Contains("{2}"));
    }

    [Fact]
    public void Every_panel_label_has_one_key()
    {
        Assert.Equal(Texts.Page.Count, Texts.Page.Select(word => word.Key).Distinct().Count());
    }

    [Fact]
    public void A_chinese_game_gets_the_put_away_result_in_chinese()
    {
        var home = Home() with { IsChinese = true };
        var report = new MoveReport(3, new[] { Stack(1, Rope, 1, Bag), Stack(2, Apple, 1, Bag), Stack(3, Rope, 1, Bag) });

        Assert.Equal("移动了 3 堆。3 堆无处可放：Rope、Red Apple。", MoveRunner.Describe(report, home));
        Assert.Equal("移动了 1 堆。", MoveRunner.Describe(new MoveReport(1, Array.Empty<ItemStack>()), home));
        Assert.Equal("Moved 1 stack.", MoveRunner.Describe(new MoveReport(1, Array.Empty<ItemStack>()), Home()));
    }

    [Fact]
    public void A_chinese_game_gets_the_drone_delivery_line_in_chinese()
    {
        var placements = new[]
        {
            new Placement(Stack(1, RedBerry, 6, Bag), Fridge.OwnerId),
            new Placement(Stack(2, Apple, 1, Bag), Box.OwnerId),
            new Placement(Stack(3, Rope, 2, Bag), null),
        };

        Assert.Equal("无人机送货：Red Berry ×6 放进Fridge、Red Apple ×1 放进Wooden Box。留在Bag：Rope ×2。", Delivery.Describe(placements, Home() with { IsChinese = true }));
    }

    [Fact]
    public void A_chinese_game_gets_the_keep_list_line_and_the_floor_name_in_chinese()
    {
        var split = new KeepSplit(Array.Empty<ItemStack>(), new[] { Stack(1, Rope, 2, Bag), Stack(2, Apple, 6, Bag) });
        var home = Home() with { IsChinese = true };

        Assert.Equal("留在背包里：Red Apple ×6、Rope ×2。", BagKeep.Describe(split, home));
        Assert.Equal("地面", home.StorageName(Inventory.FloorOwnerId));
    }

    [Theory]
    [InlineData(-1, "spoiled", "已腐败")]
    [InlineData(0, "under 1h", "不到 1 小时")]
    [InlineData(5, "5h", "5 小时")]
    [InlineData(51, "2d 3h", "2 天 3 小时")]
    public void Time_left_before_food_spoils_is_said_in_the_games_language(int hoursLeft, string english, string chinese)
    {
        Assert.Equal(english, RottingSoon.Label(hoursLeft));
        Assert.Equal(chinese, RottingSoon.Label(hoursLeft, isChinese: true));
    }

    [Fact]
    public void The_page_colours_food_by_a_flag_so_the_warning_works_in_either_language()
    {
        var rot = new[]
        {
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", -1, IsChinese: true),
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", 0, IsChinese: true),
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", 30, IsChinese: true),
        };

        using var view = JsonDocument.Parse(HubView.Build(Home(), _ => null, rot: rot));

        Assert.Equal(new[] { true, true, false }, view.RootElement.GetProperty("rot").EnumerateArray().Select(row => row.GetProperty("urgent").GetBoolean()));
    }

    [Fact]
    public void The_page_groups_food_by_the_hours_left_so_spoiled_food_sorts_apart_from_food_with_minutes_left()
    {
        var rot = new[]
        {
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", -1),
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", 0),
            new RotRow(Apple, "Red Apple", 1, Fridge.OwnerId, "Fridge", 30),
        };

        using var view = JsonDocument.Parse(HubView.Build(Home(), _ => null, rot: rot));

        Assert.Equal(new[] { -1, 0, 30 }, view.RootElement.GetProperty("rot").EnumerateArray().Select(row => row.GetProperty("hours").GetInt32()));
    }

    [Fact]
    public void The_page_asks_only_for_labels_the_plugin_sends()
    {
        var keys = Texts.Page.Select(word => word.Key).ToHashSet();
        var script = PageScript();
        var asked = Regex.Matches(script, @"say\((?:[^'()]*\?\s*)?'(\w+)'(?:\s*:\s*'(\w+)')?").SelectMany(match => new[] { match.Groups[1], match.Groups[2] })
            .Concat(Regex.Matches(script, @"(?:labelWord|titleWord|bag|storage|drawer): '(\w+)'").Select(match => match.Groups[1]))
            .Where(group => group.Success)
            .Select(group => group.Value)
            .ToList();

        Assert.NotEmpty(asked);
        Assert.All(asked, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void The_pages_own_english_matches_what_the_plugin_sends_in_english()
    {
        var block = Regex.Match(PageScript(), @"var ENGLISH = \{\r?\n(.*?)\r?\n  \};", RegexOptions.Singleline).Groups[1].Value;
        var english = Regex.Matches(block, @"^\s+(\w+): '((?:[^'\\]|\\.)*)',?\r?$", RegexOptions.Multiline).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

        Assert.Equal(Texts.Page.ToDictionary(word => word.Key, word => word.Phrase.En), english);
    }

    [Fact]
    public void The_words_statement_carries_every_label_in_the_chosen_language()
    {
        var script = PageWords.Script(isChinese: true, "F8");

        Assert.StartsWith("window.__storagehubWords={", script);
        Assert.All(Texts.Page, word => Assert.Contains(HubView.Str(word.Key) + ":" + HubView.Str(word.Phrase.Zh), script));
    }

    /// <summary>Here rather than with the other cost tests: the statement is kept in one static, which only this class also uses.</summary>
    [Fact]
    public void The_words_statement_is_built_once_per_language_and_key()
    {
        var first = PageWords.Script(isChinese: false, "F7");

        Assert.Same(first, PageWords.Script(isChinese: false, "F7"));

        var chinese = PageWords.Script(isChinese: true, "F7");
        var otherKey = PageWords.Script(isChinese: false, "F9");

        Assert.Contains(HubView.Str("homeTab") + ":" + HubView.Str(Texts.Page.Single(word => word.Key == "homeTab").Phrase.Zh), chinese);
        Assert.Contains("window.__storagehubToggleKey=\"F9\";", otherKey);
        Assert.Contains("window.__storagehubToggleKey=\"F7\";", PageWords.Script(isChinese: false, "F7"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_footer_names_the_key_the_player_set_instead_of_F8(bool isChinese)
    {
        var script = PageWords.Script(isChinese, "Insert");

        Assert.Contains("window.__storagehubToggleKey=\"Insert\";", script);
        Assert.Contains("{0} / Esc", Texts.Page.Single(word => word.Key == "closeKeys").Phrase.Pick(isChinese));
        Assert.Contains("say('closeKeys', window.__storagehubToggleKey", PageScript());
    }

    /// <summary>The trailing 1 is what lets the plugin hold back a list the page already shows.</summary>
    [Fact]
    public void A_station_ask_tells_the_plugin_when_the_page_still_holds_its_list()
    {
        Assert.Contains("send('station|' + home.station.kind + (hasList ? '|1' : ''));", PageScript());
    }

    [Fact]
    public void Every_station_has_a_page_entry_that_asks_for_its_list()
    {
        var script = PageScript();

        Assert.All(Enum.GetValues<StationKind>(), kind => Assert.Contains("kind: '" + StationView.KindName(kind) + "'", script));
    }

    [Fact]
    public void Every_station_page_names_a_scope_label_the_mod_translates()
    {
        var keys = Texts.Page.Select(word => word.Key).ToHashSet();
        var scopes = Regex.Matches(PageScript(), @"scope: '(\w+)'").Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(Enum.GetValues<StationKind>().Length - 1, scopes.Count);
        Assert.All(scopes, scope => Assert.Contains(scope, keys));
    }

    [Fact]
    public void The_page_script_declares_each_outer_function_once()
    {
        // Two declarations of one name in the same scope: the later one silently wins, and every call to the
        // earlier one breaks. The panel's addStyle() was shadowed this way and the panel never drew.
        var names = Regex.Matches(PageScript(), @"^  function (\w+)\(", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToList();

        Assert.NotEmpty(names);
        Assert.Empty(names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key));
    }

    private static string PageScript()
    {
        using var stream = typeof(TextsTests).Assembly.GetManifestResourceStream("StorageHub.page.js") ?? throw new InvalidOperationException("page.js is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
