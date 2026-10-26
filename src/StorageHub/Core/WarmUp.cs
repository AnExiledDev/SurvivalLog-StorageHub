using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// Runs what the first panel, the first station list and the first page messages run once, on a throwaway inventory,
/// so the runtime compiles that code before the player opens anything. Without it the first open of a session pays
/// for all of it on the game's frame. Pure .NET and touches nothing shared, so it can run on any thread.
/// </summary>
public static class WarmUp
{
    private const long Bag = 100;
    private const long Box = 300;

    /// <returns>The panel view and every station list it built, for the test that it went all the way through.</returns>
    public static string Run()
    {
        var commands = new List<PageCommand>();

        foreach (var message in Messages) commands.Add(PageCommand.Parse(PageCommand.Prefix + message));

        if (commands.Any(command => command is PageCommand.Unknown)) throw new InvalidOperationException("a warm-up message did not parse");

        var moves = CommandBatch.WithoutRepeatedMoves(commands);
        var inventory = new Inventory(
            new[] { new Storage(Bag, "Bag", StorageKind.Bag), new Storage(Box, "Box", StorageKind.Furniture) },
            new[] { new ItemStack(1, 1, 3, Bag), new ItemStack(2, 1, 2, Box), new ItemStack(3, 2, 1, Box) },
            new Dictionary<int, string> { [1] = "Apple", [2] = "Rope" });
        var floor = new[] { new ItemStack(4, 1, 1, Inventory.FloorOwnerId) };
        var daysLeft = new Dictionary<long, float> { [1] = 2.5f, [2] = -1f, [4] = 0.2f };
        var keep = KeepList.Empty.With(1, 2);

        ViewPrint.Of(inventory, floor, daysLeft, keep, isKeepOn: true, canSplit: true, hasIcons: false);

        var reuse = new StackReuse();
        reuse.Begin();
        reuse.Get(1, 1, 3, Bag);
        reuse.End();

        var rot = RottingSoon.Rows(inventory, floor, daysLeft);
        var view = HubView.Build(inventory, _ => null, rot, new KeepView(true, BagKeep.Rows(inventory, keep)), canSplit: true);
        var lists = new List<string> { view, moves.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var wish = new BasketWish(true, false, false, false, new List<int> { 1 }, null);

        foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
        {
            ViewPrint.OfStation(inventory, kind, 1, new List<long> { Bag, Box }, wish, isAway: false, hasIcons: false);
            lists.Add(StationView.Build(inventory, kind, StationView.UsableByKind(configId => configId == 1), _ => null, configId => configId == 1, new List<long> { Box }));
        }

        return string.Join("\n", lists);
    }

    private static readonly string[] Messages =
    {
        "take|1|300", "takeOne|1|300", "takeSome|1|300|2", "keepFill", "toggle", "putOne|1|300|100", "takeAll|1",
        "putAway|1", "putAwayBag", "putAwayFloor", "keepSet|1|2", "keepRemove|1", "catalog", "station|cooking",
        "station|basket|1", "openStorage|300", "focus|1", "menu|0", "ready", "noscript", "close",
    };
}
