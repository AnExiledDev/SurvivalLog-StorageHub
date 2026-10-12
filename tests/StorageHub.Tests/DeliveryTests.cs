using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class DeliveryTests
{
    private static readonly Storage Hub = new(500, "Drone Hub", StorageKind.Furniture);
    private static readonly Storage Drone = new(600, "Drone", StorageKind.Furniture);

    private static Inventory HomeWithHub(params ItemStack[] stacks) =>
        Home(stacks) with { Storages = new[] { Bag, Fridge, Box, Drawer, Hub, Drone } };

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
    public void Each_stack_lands_in_the_first_storage_that_takes_it_or_stays_put()
    {
        var berries = Stack(3, RedBerry, 4, Hub);
        var rope = Stack(1, Rope, 2, Hub);
        var steps = new[] { new MoveStep(berries, new[] { Fridge.OwnerId, Box.OwnerId }), new MoveStep(rope, new[] { Box.OwnerId }) };

        var placements = Delivery.Place(steps, (stack, target) => target == Box.OwnerId && stack == berries);

        Assert.Equal(new[] { new Placement(berries, Box.OwnerId), new Placement(rope, null) }, placements);
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
