using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// Runs what the first panel, the first station list, the first page messages and the first drone delivery run once,
/// on a throwaway inventory, so the runtime compiles that code before the player opens anything or a drone comes home.
/// Without it the first of each in a session pays for all of it on the game's frame. Pure .NET and touches nothing
/// shared, so it can run on any thread.
/// </summary>
public static class WarmUp
{
    private const long Bag = 100;
    private const long Box = 300;
    private const long Hub = 400;
    private const long Drone = 500;

    /// <returns>The panel view, the move count, the delivery line and every station list it built, for the test that it went all the way through.</returns>
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
        var lists = new List<string> { view, moves.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), Delivered() };
        var wish = new BasketWish(true, false, false, false, new List<int> { 1 }, null);

        foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
        {
            ViewPrint.OfStation(inventory, kind, 1, new List<long> { Bag, Box }, wish, isAway: false, hasIcons: false);
            lists.Add(StationView.Build(inventory, kind, StationView.UsableByKind(configId => configId == 1), _ => null, configId => configId == 1, new List<long> { Box }));
        }

        return string.Join("\n", lists);
    }

    /// <summary>Two drones land on one hub: the box takes the two apples, and the rope nothing wants stays in the drone.</summary>
    private static string Delivered()
    {
        var before = new[] { new ItemStack(5, 2, 1, Hub) };
        var (group, _) = Delivery.Absorb(new LandingGroup(new[] { new Landing(Drone, Hub, 1, before) }), new[] { new Landing(Drone + 1, Hub, 1, before) });
        var landed = new[] { new ItemStack(5, 2, 1, Hub), new ItemStack(6, 1, 2, Hub), new ItemStack(7, 2, 1, Drone) };
        var delivered = Delivery.Delivered(group.Before, landed);

        Delivery.Check(Array.Empty<ItemStack>(), delivered, 0.5f, 5f);

        if (Delivery.Check(delivered, delivered, 1f, 5f) is not DeliveryCheck.Landed) throw new InvalidOperationException("the warm-up delivery did not land");

        var home = new Inventory(
            new[]
            {
                new Storage(Bag, "Bag", StorageKind.Bag), new Storage(Box, "Box", StorageKind.Furniture),
                new Storage(Hub, "Drone Hub", StorageKind.Furniture, DroneBay.Hub), new Storage(Drone, "Drone", StorageKind.Furniture, DroneBay.Drone),
            },
            landed,
            new Dictionary<int, string> { [1] = "Apple", [2] = "Rope" });
        var facts = new PutFacts((stack, storage) => storage.OwnerId == Box && stack.ConfigId == 1 ? new PutRank(1, 1) : null, (_, _) => null);
        var placements = Delivery.Place(Delivery.Plan(home, delivered, group.Owners, facts, skipRobotPutOff: true), (_, target) => target == Box);

        Delivery.RetrievesToFinish(group, landed, _ => true);
        Delivery.RetrievesDone(group, _ => true);

        return Delivery.Describe(placements, home);
    }

    private static readonly string[] Messages =
    {
        "take|1|300", "takeOne|1|300", "takeSome|1|300|2", "keepFill", "toggle", "putOne|1|300|100", "takeAll|1",
        "putAway|1", "putAwayBag", "putAwayFloor", "keepSet|1|2", "keepRemove|1", "catalog", "station|cooking",
        "station|basket|1", "openStorage|300", "focus|1", "menu|0", "ready", "noscript", "close",
    };
}
