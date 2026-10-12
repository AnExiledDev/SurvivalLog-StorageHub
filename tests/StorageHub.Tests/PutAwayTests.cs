using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class PutAwayTests
{
    private static readonly Storage DroneHub = new(500, "Drone Hub", StorageKind.Furniture, DroneBay.Hub);
    private static readonly Storage Drone = new(600, "Drone", StorageKind.Furniture, DroneBay.Drone);

    [Fact]
    public void A_storage_tagged_for_the_item_beats_one_that_only_already_holds_it()
    {
        var targets = PutChoice.Targets(new[]
        {
            new PutCandidate(1, TagRank: null, HoldsSameItem: true),
            new PutCandidate(2, new PutRank(Level: 5, Specificity: 0), HoldsSameItem: false),
        });

        Assert.Equal(new long[] { 2, 1 }, targets);
    }

    [Fact]
    public void A_more_specific_tag_rule_wins_then_the_lower_level()
    {
        var targets = PutChoice.Targets(new[]
        {
            new PutCandidate(1, new PutRank(Level: 1, Specificity: 1), false),
            new PutCandidate(2, new PutRank(Level: 3, Specificity: 2), false),
            new PutCandidate(3, new PutRank(Level: 2, Specificity: 2), false),
        });

        Assert.Equal(new long[] { 3, 2, 1 }, targets);
    }

    [Fact]
    public void Between_equal_tag_matches_the_storage_already_holding_the_item_wins()
    {
        var rank = new PutRank(1, 1);

        var targets = PutChoice.Targets(new[] { new PutCandidate(1, rank, false), new PutCandidate(2, rank, true) });

        Assert.Equal(new long[] { 2, 1 }, targets);
    }

    [Fact]
    public void A_storage_that_neither_wants_nor_holds_the_item_is_never_picked()
    {
        Assert.Empty(PutChoice.Targets(new[] { new PutCandidate(1, null, false) }));
    }

    [Fact]
    public void Putting_away_only_ever_targets_home_furniture_and_never_the_stacks_own_storage()
    {
        var apple = Stack(1, Apple, 2, Fridge);
        var home = Home(apple, Stack(2, Apple, 1, Box), Stack(3, Apple, 1, Bag), Stack(4, Apple, 1, Drawer));

        var step = StoragePlan.PutAway(home, new[] { apple }, (_, _) => new PutRank(0, 0)).Single();

        Assert.Equal(new[] { Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void Putting_the_bag_away_never_fills_a_drone_even_one_tagged_for_the_item_that_already_holds_it()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var withDrone = Home(apple, Stack(2, Apple, 1, Drone)) with { Storages = new[] { Bag, Fridge, Drone } };

        var step = StoragePlan.PutAway(withDrone, new[] { apple }, (_, storage) => storage == Drone ? new PutRank(0, 1) : null).Single();

        Assert.Empty(step.Targets);
    }

    [Fact]
    public void Putting_the_bag_away_still_fills_a_drone_hub_tagged_for_the_item()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var withHub = Home(apple) with { Storages = new[] { Bag, Fridge, DroneHub } };

        var step = StoragePlan.PutAway(withHub, new[] { apple }, (_, storage) => storage == DroneHub ? new PutRank(0, 1) : null).Single();

        Assert.Equal(new[] { DroneHub.OwnerId }, step.Targets);
    }

    [Fact]
    public void Putting_away_passes_each_stack_and_storage_to_the_tag_rule()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var rope = Stack(2, Rope, 1, Bag);
        PutRank? FridgeTakesFood(ItemStack stack, Storage storage) =>
            storage == Fridge && stack.ConfigId == Apple ? new PutRank(0, 1) : null;

        var steps = StoragePlan.PutAway(Home(apple, rope), new[] { apple, rope }, FridgeTakesFood);

        Assert.Equal(new[] { Fridge.OwnerId }, steps[0].Targets);
        Assert.Empty(steps[1].Targets);
    }

    [Fact]
    public void Tag_put_away_offers_a_floor_stack_only_home_furniture_leaving_the_bag_fallback_to_the_floor_plan()
    {
        var appleOnFloor = Stack(1, Apple, 3, Floor);

        var step = StoragePlan.PutAway(Home(), new[] { appleOnFloor }, (_, _) => new PutRank(0, 1)).Single();

        Assert.Equal(new[] { Fridge.OwnerId, Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void A_floor_item_no_storage_wants_or_holds_stays_on_the_floor()
    {
        var ropeOnFloor = Stack(1, Rope, 1, Floor);

        var step = StoragePlan.PutAway(Home(Stack(2, Apple, 1, Box)), new[] { ropeOnFloor }, (_, _) => null).Single();

        Assert.Empty(step.Targets);
    }

    [Fact]
    public void A_floor_item_no_storage_wants_falls_back_to_the_bag()
    {
        var ropeOnFloor = Stack(1, Rope, 1, Floor);

        var step = StoragePlan.PutAwayFloor(Home(), new[] { ropeOnFloor }, (_, _) => null).Single();

        Assert.Equal(new[] { Bag.OwnerId }, step.Targets);
    }

    [Fact]
    public void Tagged_storages_get_floor_items_before_the_bag_does()
    {
        var appleOnFloor = Stack(1, Apple, 3, Floor);
        PutRank? FridgeTakesFood(ItemStack stack, Storage storage) => storage == Fridge ? new PutRank(0, 1) : null;

        var step = StoragePlan.PutAwayFloor(Home(), new[] { appleOnFloor }, FridgeTakesFood).Single();

        Assert.Equal(new[] { Fridge.OwnerId, Bag.OwnerId }, step.Targets);
    }

    [Fact]
    public void Without_a_bag_an_unwanted_floor_item_stays_put()
    {
        var noBag = new Inventory(new[] { Fridge }, Array.Empty<ItemStack>(), new Dictionary<int, string>());

        var step = StoragePlan.PutAwayFloor(noBag, new[] { Stack(1, Rope, 1, Floor) }, (_, _) => null).Single();

        Assert.Empty(step.Targets);
    }

    [Fact]
    public void The_floor_is_named_in_reports_even_though_it_is_not_a_storage()
    {
        Assert.Equal("Floor", Home().StorageName(Inventory.FloorOwnerId));
    }

    [Fact]
    public void Taking_moves_stacks_into_the_bag_and_leaves_the_ones_already_there()
    {
        var inBox = Stack(1, Rope, 1, Box);
        var inBag = Stack(2, Rope, 1, Bag);

        var steps = StoragePlan.Take(Home(inBox, inBag), new[] { inBox, inBag });

        Assert.Equal(new[] { (inBox, Bag.OwnerId) }, steps.Select(s => (s.Stack, s.Targets.Single())));
    }

    [Fact]
    public void Taking_without_a_bag_plans_nothing()
    {
        var noBag = new Inventory(new[] { Box }, new[] { Stack(1, Rope, 1, Box) }, new Dictionary<int, string>());

        Assert.Empty(StoragePlan.Take(noBag, noBag.Stacks));
    }
}
