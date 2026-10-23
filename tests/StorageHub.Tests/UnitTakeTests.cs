using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class UnitTakeTests
{
    private static readonly Storage DroneBag = new(600, "Drone", StorageKind.Furniture, DroneBay.Drone);

    /// <summary>What the game leaves behind after one unit leaves: the rest of the stack, under a new id.</summary>
    private static ItemStack? Rest(ItemStack stack) => stack.Count > 1 ? stack with { ItemId = stack.ItemId + 100, Count = stack.Count - 1 } : null;

    private static KeepList Keep(params (int ConfigId, int Amount)[] entries) =>
        entries.Aggregate(KeepList.Empty, (list, entry) => list.With(entry.ConfigId, entry.Amount));

    [Fact]
    public void Taking_some_empties_the_smallest_stacks_first_and_splits_only_the_last()
    {
        var big = Stack(1, Rope, 9, Box);
        var small = Stack(2, Rope, 3, Box);

        var steps = UnitTake.Plan(Home(big, small), new[] { big, small }, 5);

        Assert.Equal(new[] { (small, 3, true), (big, 2, false) }, steps.Select(step => (step.Stack, step.Units, step.IsWhole)));
        Assert.All(steps, step => Assert.Equal(Bag.OwnerId, step.Target));
    }

    [Fact]
    public void Asking_for_more_than_the_storage_holds_takes_every_stack_whole()
    {
        var a = Stack(1, Rope, 2, Box);
        var b = Stack(2, Rope, 4, Box);

        var steps = UnitTake.Plan(Home(a, b), new[] { a, b }, 50);

        Assert.Equal(6, steps.Sum(step => step.Units));
        Assert.All(steps, step => Assert.True(step.IsWhole));
    }

    [Fact]
    public void Bag_and_floor_stacks_are_never_taken_from()
    {
        var inBag = Stack(1, Rope, 5, Bag);
        var onFloor = Stack(2, Rope, 5, Floor);

        Assert.Empty(UnitTake.Plan(Home(inBag, onFloor), new[] { inBag, onFloor }, 3));
    }

    [Fact]
    public void A_partial_step_moves_one_unit_at_a_time_until_the_count_is_reached()
    {
        var stack = Stack(1, Rope, 9, Box);
        var calls = 0;

        var taken = UnitTake.Run(new[] { new UnitStep(stack, 4, Bag.OwnerId) }, (_, _) => throw new InvalidOperationException("must not move whole"), (_, _) => { calls++; return 1; }, Rest);

        Assert.Equal(4, calls);
        Assert.Equal(4, taken[Rope]);
    }

    [Fact]
    public void Each_unit_comes_off_the_rest_of_the_stack_not_the_unit_that_already_left()
    {
        // The unit that moves keeps the stack's id and the rest gets a new one, so asking for the old id again moved
        // the unit already in the bag, from a storage it was no longer in, and the game froze.
        var stack = Stack(1, Rope, 9, Box);
        var asked = new List<long>();

        var taken = UnitTake.Run(new[] { new UnitStep(stack, 3, Bag.OwnerId) }, (_, _) => false, (from, _) => { asked.Add(from.ItemId); return 1; }, Rest);

        Assert.Equal(new long[] { 1, 101, 201 }, asked);
        Assert.Equal(3, taken[Rope]);
    }

    [Fact]
    public void A_step_stops_when_the_rest_of_the_stack_cannot_be_found()
    {
        var stack = Stack(1, Rope, 9, Box);
        var calls = 0;

        var taken = UnitTake.Run(new[] { new UnitStep(stack, 4, Bag.OwnerId) }, (_, _) => false, (_, _) => { calls++; return 1; }, _ => null);

        Assert.Equal(1, calls);
        Assert.Equal(1, taken[Rope]);
    }

    [Fact]
    public void A_stack_the_game_cannot_split_is_counted_whole_and_ends_the_step()
    {
        var stack = Stack(1, Rope, 9, Box);
        var calls = 0;

        var taken = UnitTake.Run(new[] { new UnitStep(stack, 4, Bag.OwnerId) }, (_, _) => false, (_, _) => { calls++; return calls == 1 ? 1 : 8; }, Rest);

        Assert.Equal(2, calls);
        Assert.Equal(9, taken[Rope]);
    }

    [Fact]
    public void A_refused_unit_stops_the_step_and_counts_only_what_moved()
    {
        var stack = Stack(1, Rope, 9, Box);
        var calls = 0;

        var taken = UnitTake.Run(new[] { new UnitStep(stack, 5, Bag.OwnerId) }, (_, _) => false, (_, _) => ++calls <= 2 ? 1 : 0, Rest);

        Assert.Equal(2, taken[Rope]);
    }

    [Fact]
    public void Filling_takes_only_what_the_bag_is_short_of()
    {
        var held = Stack(1, Rope, 2, Bag);
        var stored = Stack(2, Rope, 10, Box);
        var apples = Stack(3, Apple, 10, Fridge);

        var steps = UnitTake.Fill(Home(held, stored, apples), Keep((Rope, 5), (Apple, 0)));

        Assert.Equal(new[] { (stored, 3) }, steps.Select(step => (step.Stack, step.Units)));
    }

    [Fact]
    public void A_full_bag_needs_no_fill()
    {
        var held = Stack(1, Rope, 6, Bag);

        Assert.Empty(UnitTake.Fill(Home(held, Stack(2, Rope, 10, Box)), Keep((Rope, 5))));
        Assert.Empty(UnitTake.NoneStored(Home(held), Keep((Rope, 5))));
    }

    [Fact]
    public void Filling_never_takes_from_a_drone()
    {
        var home = Home(Stack(1, Rope, 10, DroneBag)) with { Storages = new[] { Bag, Fridge, Box, DroneBag } };

        Assert.Empty(UnitTake.Fill(home, Keep((Rope, 3))));
        Assert.Equal(new[] { Rope }, UnitTake.NoneStored(home, Keep((Rope, 3))));
    }

    [Fact]
    public void Filling_never_takes_back_what_is_staged_in_the_neighbors_basket()
    {
        var basket = new Storage(800, "Basket", StorageKind.Basket);
        var home = Home(Stack(1, Rope, 10, basket)) with { Storages = new[] { Bag, Fridge, Box, basket } };

        Assert.Empty(UnitTake.Fill(home, Keep((Rope, 3))));
        Assert.Equal(new[] { Rope }, UnitTake.NoneStored(home, Keep((Rope, 3))));
    }

    [Fact]
    public void Short_items_no_storage_holds_are_named_as_none_stored()
    {
        var home = Home(Stack(1, Apple, 1, Bag), Stack(2, Rope, 4, Box), Stack(3, RedBerry, 2, Floor));

        Assert.Equal(new[] { Apple, RedBerry }, UnitTake.NoneStored(home, Keep((Apple, 3), (Rope, 2), (RedBerry, 1))));
    }

    [Fact]
    public void The_status_names_what_came_and_what_storage_had_none_of()
    {
        var report = new UnitReport(new Dictionary<int, int> { [Rope] = 3 }, new[] { Apple });

        Assert.Equal("Took 3 Rope into your bag. None in storage: Red Apple.", UnitTake.Describe(report, Home()));
    }
}
