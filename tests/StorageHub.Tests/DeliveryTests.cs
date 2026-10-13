using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class DeliveryTests
{
    private static readonly Storage Hub = new(500, "Drone Hub", StorageKind.Furniture, DroneBay.Hub);
    private static readonly Storage OtherHub = new(501, "Drone Hub", StorageKind.Furniture, DroneBay.Hub);
    private static readonly Storage Drone = new(600, "Drone", StorageKind.Furniture, DroneBay.Drone);
    private static readonly Storage OtherDrone = new(601, "Drone", StorageKind.Furniture, DroneBay.Drone);
    private static readonly TripAtSettle Returning = new(IsSettleLocked: false, IsBusy: true, IsCharity: false, HasCargo: true);

    private static Inventory HomeWithHub(params ItemStack[] stacks) =>
        Home(stacks) with { Storages = new[] { Bag, Fridge, Box, Drawer, Hub, Drone } };

    private static Inventory HomeWithTwoHubs(params ItemStack[] stacks) =>
        Home(stacks) with { Storages = new[] { Bag, Fridge, Box, Drawer, Hub, OtherHub, Drone, OtherDrone } };

    private static Landing LandingOf(Storage drone, Storage hub, params ItemStack[] before) => new(drone.OwnerId, hub.OwnerId, 2, before);

    private static Func<long, bool> AwaitingRetrieve(params Storage[] drones) => droneId => drones.Any(drone => drone.OwnerId == droneId);
    private static Func<long, bool> SurelyRetrieved(params Storage[] drones) => droneId => drones.Any(drone => drone.OwnerId == droneId);

    [Fact]
    public void A_returning_drone_with_cargo_hands_it_over()
    {
        Assert.True(Delivery.HandsOverCargo(Returning));
    }

    public static TheoryData<string, TripAtSettle> SettlesThatBringNothing => new()
    {
        { "the trip is not under way", Returning with { IsBusy = false } },
        { "the cargo was already handed over", Returning with { HasCargo = false } },
        { "a charity trip brings nothing home", Returning with { IsCharity = true } },
        { "the game skips the settle while locked", Returning with { IsSettleLocked = true } },
    };

    [Theory]
    [MemberData(nameof(SettlesThatBringNothing))]
    public void A_settle_that_brings_nothing_records_no_landing(string why, TripAtSettle trip)
    {
        Assert.False(Delivery.HandsOverCargo(trip), why);
    }

    [Fact]
    public void Two_drones_settling_on_one_hub_in_one_tick_are_put_away_in_one_window()
    {
        var hubBefore = Stack(1, Rope, 2, Hub);
        var first = LandingOf(Drone, Hub, hubBefore);
        var second = LandingOf(OtherDrone, Hub, hubBefore, Stack(2, Apple, 3, Hub));

        var (group, waiting) = Delivery.Absorb(new LandingGroup(new[] { first }), new[] { second });
        var landed = Delivery.Delivered(group.Before, new[] { hubBefore, Stack(2, Apple, 3, Hub), Stack(3, RedBerry, 4, Hub) });

        Assert.Empty(waiting);
        Assert.Equal(new[] { first, second }, group.Landings);
        Assert.Equal(new[] { Stack(2, Apple, 3, Hub), Stack(3, RedBerry, 4, Hub) }, landed);
    }

    [Fact]
    public void A_drone_on_another_hub_keeps_its_own_window()
    {
        var onOtherHub = LandingOf(OtherDrone, OtherHub);

        var (group, waiting) = Delivery.Absorb(new LandingGroup(new[] { LandingOf(Drone, Hub) }), new[] { onOtherHub });

        Assert.Single(group.Landings);
        Assert.Equal(new[] { onOtherHub }, waiting);
    }

    [Fact]
    public void A_joined_drone_s_own_bag_is_watched_from_before_it_settled()
    {
        var second = LandingOf(OtherDrone, Hub, Stack(5, Rope, 1, OtherDrone));

        var (group, _) = Delivery.Absorb(new LandingGroup(new[] { LandingOf(Drone, Hub) }), new[] { second });

        Assert.Equal(new[] { Hub.OwnerId, Drone.OwnerId, OtherDrone.OwnerId }, group.Owners);
        Assert.Equal(new[] { Stack(5, Rope, 1, OtherDrone) }, group.Before);
    }

    [Fact]
    public void Only_stacks_that_are_new_or_grew_count_as_delivered()
    {
        var before = new[] { Stack(1, Rope, 2, Hub), Stack(2, Apple, 3, Hub) };
        var after = new[] { Stack(1, Rope, 2, Hub), Stack(2, Apple, 5, Hub), Stack(3, RedBerry, 4, Hub) };

        Assert.Equal(new[] { Stack(2, Apple, 5, Hub), Stack(3, RedBerry, 4, Hub) }, Delivery.Delivered(before, after));
    }

    [Fact]
    public void A_stack_that_only_moved_from_the_drone_to_its_hub_was_not_delivered()
    {
        Assert.Empty(Delivery.Delivered(new[] { Stack(1, Rope, 2, Drone) }, new[] { Stack(1, Rope, 2, Hub) }));
    }

    [Fact]
    public void A_delivery_is_taken_once_it_looks_the_same_twice()
    {
        var landed = new[] { Stack(3, RedBerry, 4, Hub) };

        Assert.IsType<DeliveryCheck.Wait>(Delivery.Check(Array.Empty<ItemStack>(), landed, 0.1f, 5f));
        Assert.Equal(landed, Assert.IsType<DeliveryCheck.Landed>(Delivery.Check(landed, new[] { Stack(3, RedBerry, 4, Hub) }, 0.2f, 5f)).Stacks);
        Assert.IsType<DeliveryCheck.Wait>(Delivery.Check(landed, new[] { Stack(3, RedBerry, 6, Hub) }, 0.3f, 5f));
    }

    [Fact]
    public void Out_of_time_takes_whatever_landed_or_gives_up_when_nothing_did()
    {
        var landed = new[] { Stack(3, RedBerry, 4, Hub) };

        Assert.IsType<DeliveryCheck.Landed>(Delivery.Check(Array.Empty<ItemStack>(), landed, 5f, 5f));
        Assert.IsType<DeliveryCheck.NothingLanded>(Delivery.Check(Array.Empty<ItemStack>(), Array.Empty<ItemStack>(), 5f, 5f));
        Assert.IsType<DeliveryCheck.Wait>(Delivery.Check(Array.Empty<ItemStack>(), Array.Empty<ItemStack>(), 1f, 5f));
    }

    [Fact]
    public void Delivered_items_go_to_the_storage_tagged_for_them()
    {
        var berries = Stack(3, RedBerry, 4, Hub);
        var home = HomeWithHub(berries);

        var steps = Delivery.Plan(home, new[] { berries }, new[] { Hub.OwnerId, Drone.OwnerId }, (stack, storage) => storage == Fridge ? new PutRank(1, 1) : null);

        Assert.Equal(new[] { Fridge.OwnerId }, Assert.Single(steps).Targets);
    }

    [Fact]
    public void Neither_the_hub_nor_the_drone_is_ever_a_target_even_when_it_already_holds_the_item()
    {
        var rope = Stack(1, Rope, 2, Drone);
        var home = HomeWithHub(Stack(9, Rope, 5, Hub), rope);

        var steps = Delivery.Plan(home, new[] { rope }, new[] { Hub.OwnerId, Drone.OwnerId }, (_, _) => null);

        Assert.Empty(Assert.Single(steps).Targets);
    }

    [Fact]
    public void Another_drone_already_holding_the_item_is_never_a_target()
    {
        var rope = Stack(1, Rope, 2, Hub);
        var home = HomeWithTwoHubs(rope, Stack(9, Rope, 5, OtherDrone));

        var steps = Delivery.Plan(home, new[] { rope }, new[] { Hub.OwnerId, Drone.OwnerId }, (_, _) => null);

        Assert.Empty(Assert.Single(steps).Targets);
    }

    [Fact]
    public void Another_drone_hub_already_holding_the_item_is_never_a_target()
    {
        var rope = Stack(1, Rope, 2, Hub);
        var home = HomeWithTwoHubs(rope, Stack(9, Rope, 5, OtherHub));

        var steps = Delivery.Plan(home, new[] { rope }, new[] { Hub.OwnerId, Drone.OwnerId }, (_, _) => null);

        Assert.Empty(Assert.Single(steps).Targets);
    }

    [Fact]
    public void A_drone_bay_whose_tags_fit_is_still_never_a_target()
    {
        var berries = Stack(3, RedBerry, 4, Hub);
        var home = HomeWithTwoHubs(berries);

        var steps = Delivery.Plan(home, new[] { berries }, new[] { Hub.OwnerId, Drone.OwnerId }, (_, storage) => storage == Fridge || storage.IsDroneBay ? new PutRank(1, 1) : null);

        Assert.Equal(new[] { Fridge.OwnerId }, Assert.Single(steps).Targets);
    }

    [Fact]
    public void Each_stack_lands_in_the_first_storage_that_takes_it_or_stays_put()
    {
        var berries = Stack(3, RedBerry, 4, Hub);
        var rope = Stack(1, Rope, 2, Hub);
        var steps = new[] { new MoveStep(berries, new[] { Fridge.OwnerId, Box.OwnerId }), new MoveStep(rope, new[] { Box.OwnerId }) };

        var placements = Delivery.Place(steps, (stack, target) => target == Box.OwnerId && stack == berries);

        Assert.Equal(new[] { new Placement(berries, Box.OwnerId), new Placement(rope, null) }, placements);
    }

    [Fact]
    public void A_drone_the_put_away_emptied_has_its_retrieve_finished()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesToFinish(group, Array.Empty<ItemStack>(), AwaitingRetrieve(Drone)));
    }

    [Fact]
    public void A_drone_still_holding_what_no_storage_took_keeps_its_retrieve()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub) });

        Assert.Empty(Delivery.RetrievesToFinish(group, new[] { Stack(1, Rope, 2, Drone) }, AwaitingRetrieve(Drone)));
    }

    [Fact]
    public void A_drone_whose_cargo_went_to_the_hub_is_left_to_the_game()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub) });

        Assert.Empty(Delivery.RetrievesToFinish(group, Array.Empty<ItemStack>(), AwaitingRetrieve()));
    }

    [Fact]
    public void What_stayed_in_the_hub_or_another_drone_does_not_keep_an_emptied_drone_s_retrieve()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub) });
        var heldElsewhere = new[] { Stack(1, Rope, 2, Hub), Stack(2, Apple, 1, OtherDrone) };

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesToFinish(group, heldElsewhere, AwaitingRetrieve(Drone, OtherDrone)));
    }

    [Fact]
    public void Of_two_drones_on_one_hub_only_the_emptied_one_has_its_retrieve_finished()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub), LandingOf(OtherDrone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesToFinish(group, new[] { Stack(2, Apple, 1, OtherDrone) }, AwaitingRetrieve(Drone, OtherDrone)));
    }

    [Fact]
    public void A_drone_that_landed_twice_in_one_delivery_has_its_retrieve_finished_once()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub), LandingOf(Drone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesToFinish(group, Array.Empty<ItemStack>(), AwaitingRetrieve(Drone)));
    }

    [Fact]
    public void A_drone_whose_cargo_the_game_unloaded_into_the_hub_counts_as_retrieved()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesDone(group, SurelyRetrieved(Drone)));
    }

    [Fact]
    public void A_drone_that_may_still_wait_for_the_player_to_retrieve_is_left_for_the_game_to_count()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub), LandingOf(OtherDrone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesDone(group, SurelyRetrieved(Drone)));
    }

    [Fact]
    public void A_drone_that_landed_twice_in_one_delivery_counts_as_retrieved_once()
    {
        var group = new LandingGroup(new[] { LandingOf(Drone, Hub), LandingOf(Drone, Hub) });

        Assert.Equal(new[] { Drone.OwnerId }, Delivery.RetrievesDone(group, SurelyRetrieved(Drone)));
    }

    [Fact]
    public void The_log_line_says_what_went_where_and_what_stayed()
    {
        var home = HomeWithHub();
        var placements = new[]
        {
            new Placement(Stack(3, RedBerry, 4, Hub), Fridge.OwnerId),
            new Placement(Stack(4, RedBerry, 2, Hub), Fridge.OwnerId),
            new Placement(Stack(5, Apple, 1, Hub), Box.OwnerId),
            new Placement(Stack(1, Rope, 2, Hub), null),
        };

        Assert.Equal("Drone delivery: 6 Red Berry to Fridge, 1 Red Apple to Wooden Box. Stayed in Drone Hub: 2 Rope.", Delivery.Describe(placements, home));
    }

    [Fact]
    public void The_log_line_names_each_place_when_items_stayed_in_both_the_hub_and_the_drone()
    {
        var placements = new[] { new Placement(Stack(1, Rope, 2, Hub), null), new Placement(Stack(2, Apple, 1, Drone), null) };

        Assert.Equal("Drone delivery: no storage takes any of it. Stayed: 2 Rope in Drone Hub, 1 Red Apple in Drone.", Delivery.Describe(placements, HomeWithHub()));
    }
}
