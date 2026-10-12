using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.PackingTests;

namespace StorageHub.Tests;

public class OrganizerTests
{
    private static readonly GridSize Grid = new(10, 10);

    [Fact]
    public void A_messy_bag_is_moved_into_the_compact_layout()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Grid, bag, new PackOptions()));

        Assert.Equal(PackStyle.Compact, move.Style);
        Assert.Equal(Fits(Packing.Pack(Grid, Smalls(5, 1, 1), new PackOptions())), move.Positions);
    }

    [Fact]
    public void A_bag_already_in_the_compact_layout_is_left_alone_so_opening_it_again_writes_nothing()
    {
        var items = Smalls(5, 1, 1);
        var packed = Fits(Packing.Pack(Grid, items, new PackOptions()));
        var bag = items.Select(item => new BagItem(item, packed[item.Id])).ToList();

        Assert.IsType<OrganizePlan.AlreadyTidy>(Organizer.Plan(Grid, bag, new PackOptions()));
    }

    [Fact]
    public void One_item_out_of_place_is_enough_to_reorganize()
    {
        var items = Smalls(5, 1, 1);
        var packed = Fits(Packing.Pack(Grid, items, new PackOptions()));
        var bag = items.Select(item => new BagItem(item, item.Id == 5 ? new Cell(9, 9) : packed[item.Id])).ToList();

        Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Grid, bag, new PackOptions()));
    }

    [Fact]
    public void A_bag_the_compact_layout_cannot_hold_falls_back_to_the_games_own_layout()
    {
        var grid = new GridSize(4, 3);
        var bag = Scattered(CrowdedBag());

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(grid, bag, new PackOptions()));

        Assert.Equal(PackStyle.GameOrder, move.Style);
        Assert.Equal(Fits(Packing.PackLikeGame(grid, CrowdedBag())), move.Positions);
    }

    [Fact]
    public void With_compact_packing_off_the_games_own_layout_is_used()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Grid, bag, new PackOptions(SmallCluster: false)));

        Assert.Equal(PackStyle.GameOrder, move.Style);
        Assert.Equal(new Cell(4, 0), move.Positions[5]);
    }

    [Fact]
    public void A_bag_no_layout_can_hold_is_left_exactly_as_it_is()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        Assert.IsType<OrganizePlan.NoRoom>(Organizer.Plan(new GridSize(2, 2), bag, new PackOptions()));
    }

    [Fact]
    public void An_empty_bag_needs_nothing()
    {
        Assert.IsType<OrganizePlan.AlreadyTidy>(Organizer.Plan(Grid, Array.Empty<BagItem>(), new PackOptions()));
    }

    [Fact]
    public void A_planned_move_places_every_item_in_the_bag_once_and_nothing_else()
    {
        var items = new[] { Item(1, 3, 2), Item(2, 2, 2), Item(3, 1, 2), Item(4, 1, 1), Item(5, 2, 1) };

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Grid, Scattered(items), new PackOptions()));

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, move.Positions.Keys.Order());
        Assert.True(Packing.IsValid(Grid, items, move.Positions));
    }

    /// <summary>Gives each item a current cell no layout ever chooses, so whatever the planner picks counts as a move.</summary>
    private static List<BagItem> Scattered(IEnumerable<PackItem> items) =>
        items.Select((item, row) => new BagItem(item, new Cell(-1, row))).ToList();
}
