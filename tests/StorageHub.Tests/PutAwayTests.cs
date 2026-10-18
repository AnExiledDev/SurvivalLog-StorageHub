using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class PutAwayTests
{
    private static readonly Storage DroneHub = new(500, "Drone Hub", StorageKind.Furniture, DroneBay.Hub);
    private static readonly Storage Drone = new(600, "Drone", StorageKind.Furniture, DroneBay.Drone);

    /// <summary>What the game ranks an item at in a storage tagged Materials and nothing else.</summary>
    private static readonly PutRank MaterialsOnly = new(Level: 4, Specificity: 15);

    /// <summary>What the game's own rack rule ranks a material it has a slot for: one step more specific than MaterialsOnly.</summary>
    private static readonly PutRank RackSlot = new(Level: 4, Specificity: 16);

    private static Inventory WithRack(params ItemStack[] stacks) => Home(stacks) with { Storages = new[] { Bag, Fridge, Box, Rack } };

    /// <summary>The rack has a slot for Wood only; Rope is a material it has none for, the way it has none for straps.</summary>
    private static PutFacts RackFacts(Func<ItemStack, Storage, PutRank?> tagRank) =>
        new(tagRank, (stack, storage) => storage == Rack && stack.ConfigId == Wood ? RackSlot : null);

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

        var step = StoragePlan.PutAway(home, new[] { apple }, Tags((_, _) => new PutRank(0, 0))).Single();

        Assert.Equal(new[] { Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void Putting_the_bag_away_never_fills_a_drone_even_one_tagged_for_the_item_that_already_holds_it()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var withDrone = Home(apple, Stack(2, Apple, 1, Drone)) with { Storages = new[] { Bag, Fridge, Drone } };

        var step = StoragePlan.PutAway(withDrone, new[] { apple }, Tags((_, storage) => storage == Drone ? new PutRank(0, 1) : null)).Single();

        Assert.Empty(step.Targets);
    }

    [Fact]
    public void Putting_the_bag_away_still_fills_a_drone_hub_tagged_for_the_item()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var withHub = Home(apple) with { Storages = new[] { Bag, Fridge, DroneHub } };

        var step = StoragePlan.PutAway(withHub, new[] { apple }, Tags((_, storage) => storage == DroneHub ? new PutRank(0, 1) : null)).Single();

        Assert.Equal(new[] { DroneHub.OwnerId }, step.Targets);
    }

    [Fact]
    public void A_material_goes_into_an_empty_material_rack_that_has_a_slot_for_it()
    {
        var wood = Stack(1, Wood, 5, Bag);

        var step = StoragePlan.PutAway(WithRack(wood), new[] { wood }, RackFacts((_, _) => null)).Single();

        Assert.Equal(new[] { Rack.OwnerId }, step.Targets);
    }

    [Fact]
    public void An_item_the_rack_has_no_slot_for_is_never_offered_to_it()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var rope = Stack(2, Rope, 2, Bag);

        var steps = StoragePlan.PutAway(WithRack(apple, rope), new[] { apple, rope }, RackFacts((_, _) => null));

        Assert.All(steps, step => Assert.Empty(step.Targets));
    }

    [Fact]
    public void A_material_racks_storage_tag_rank_is_never_consulted()
    {
        var rope = Stack(1, Rope, 2, Bag);
        var everyStorageTagged = RackFacts((_, _) => new PutRank(0, 20));

        var step = StoragePlan.PutAway(WithRack(rope), new[] { rope }, everyStorageTagged).Single();

        Assert.Equal(new[] { Fridge.OwnerId, Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void Floor_put_away_fills_a_material_rack_with_what_it_has_a_slot_for_and_bags_the_rest()
    {
        var woodOnFloor = Stack(1, Wood, 5, Floor);
        var ropeOnFloor = Stack(2, Rope, 2, Floor);

        var steps = StoragePlan.PutAwayFloor(WithRack(), new[] { woodOnFloor, ropeOnFloor }, RackFacts((_, _) => null));

        Assert.Equal(new[] { Rack.OwnerId, Bag.OwnerId }, steps[0].Targets);
        Assert.Equal(new[] { Bag.OwnerId }, steps[1].Targets);
    }

    [Fact]
    public void A_material_rack_goes_before_a_box_tagged_only_Materials_even_one_already_holding_the_item()
    {
        var wood = Stack(1, Wood, 5, Bag);
        var facts = RackFacts((_, storage) => storage == Box ? MaterialsOnly : null);

        var step = StoragePlan.PutAway(WithRack(wood, Stack(2, Wood, 3, Box)), new[] { wood }, facts).Single();

        Assert.Equal(new[] { Rack.OwnerId, Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void A_storage_the_game_ranks_more_specifically_than_a_material_rack_still_goes_first()
    {
        var wood = Stack(1, Wood, 5, Bag);
        var closerThanARack = RackSlot with { Specificity = RackSlot.Specificity + 1 };

        var step = StoragePlan.PutAway(WithRack(wood), new[] { wood }, RackFacts((_, storage) => storage == Box ? closerThanARack : null)).Single();

        Assert.Equal(new[] { Box.OwnerId, Rack.OwnerId }, step.Targets);
    }

    [Fact]
    public void Putting_away_passes_each_stack_and_storage_to_the_tag_rule()
    {
        var apple = Stack(1, Apple, 2, Bag);
        var rope = Stack(2, Rope, 1, Bag);
        PutRank? FridgeTakesFood(ItemStack stack, Storage storage) =>
            storage == Fridge && stack.ConfigId == Apple ? new PutRank(0, 1) : null;

        var steps = StoragePlan.PutAway(Home(apple, rope), new[] { apple, rope }, Tags(FridgeTakesFood));

        Assert.Equal(new[] { Fridge.OwnerId }, steps[0].Targets);
        Assert.Empty(steps[1].Targets);
    }

    [Fact]
    public void Tag_put_away_offers_a_floor_stack_only_home_furniture_leaving_the_bag_fallback_to_the_floor_plan()
    {
        var appleOnFloor = Stack(1, Apple, 3, Floor);

        var step = StoragePlan.PutAway(Home(), new[] { appleOnFloor }, Tags((_, _) => new PutRank(0, 1))).Single();

        Assert.Equal(new[] { Fridge.OwnerId, Box.OwnerId }, step.Targets);
    }

    [Fact]
    public void A_floor_item_no_storage_wants_or_holds_stays_on_the_floor()
    {
        var ropeOnFloor = Stack(1, Rope, 1, Floor);

        var step = StoragePlan.PutAway(Home(Stack(2, Apple, 1, Box)), new[] { ropeOnFloor }, Tags((_, _) => null)).Single();

        Assert.Empty(step.Targets);
    }

    [Fact]
    public void A_floor_item_no_storage_wants_falls_back_to_the_bag()
    {
        var ropeOnFloor = Stack(1, Rope, 1, Floor);

        var step = StoragePlan.PutAwayFloor(Home(), new[] { ropeOnFloor }, Tags((_, _) => null)).Single();

        Assert.Equal(new[] { Bag.OwnerId }, step.Targets);
    }

    [Fact]
    public void Tagged_storages_get_floor_items_before_the_bag_does()
    {
        var appleOnFloor = Stack(1, Apple, 3, Floor);
        PutRank? FridgeTakesFood(ItemStack stack, Storage storage) => storage == Fridge ? new PutRank(0, 1) : null;

        var step = StoragePlan.PutAwayFloor(Home(), new[] { appleOnFloor }, Tags(FridgeTakesFood)).Single();

        Assert.Equal(new[] { Fridge.OwnerId, Bag.OwnerId }, step.Targets);
    }

    [Fact]
    public void Without_a_bag_an_unwanted_floor_item_stays_put()
    {
        var noBag = new Inventory(new[] { Fridge }, Array.Empty<ItemStack>(), new Dictionary<int, string>());

        var step = StoragePlan.PutAwayFloor(noBag, new[] { Stack(1, Rope, 1, Floor) }, Tags((_, _) => null)).Single();

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
    public void Taking_one_takes_a_single_unit_off_the_smallest_stack_in_that_storage()
    {
        var big = Stack(1, Rope, 9, Box);
        var small = Stack(2, Rope, 3, Box);
        var elsewhere = Stack(3, Rope, 1, Fridge);
        var otherItem = Stack(4, Apple, 1, Box);

        var steps = StoragePlan.TakeOne(Home(big, small, elsewhere, otherItem), Rope, Box.OwnerId);

        Assert.Equal(new[] { (small with { Count = 1 }, Bag.OwnerId) }, steps.Select(s => (s.Stack, s.Targets.Single())));
    }

    [Fact]
    public void Taking_one_from_the_bag_or_without_a_bag_plans_nothing()
    {
        var inBag = Stack(1, Rope, 4, Bag);
        var noBag = new Inventory(new[] { Box }, new[] { Stack(2, Rope, 4, Box) }, new Dictionary<int, string>());

        Assert.Empty(StoragePlan.TakeOne(Home(inBag), Rope, Bag.OwnerId));
        Assert.Empty(StoragePlan.TakeOne(noBag, Rope, Box.OwnerId));
    }

    [Fact]
    public void Taking_without_a_bag_plans_nothing()
    {
        var noBag = new Inventory(new[] { Box }, new[] { Stack(1, Rope, 1, Box) }, new Dictionary<int, string>());

        Assert.Empty(StoragePlan.Take(noBag, noBag.Stacks));
    }
}
