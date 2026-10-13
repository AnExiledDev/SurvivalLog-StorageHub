using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.PackingTests;

namespace StorageHub.Tests;

public class OrganizerTests
{
    private static readonly BagSpace Open = Space(10, 10);

    [Fact]
    public void A_messy_bag_is_moved_into_the_compact_layout()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Open, bag, new PackOptions()));

        Assert.Equal(PackStyle.Compact, move.Style);
        Assert.Equal(Fits(Packing.Pack(Open, Smalls(5, 1, 1), new PackOptions())), move.Positions);
    }

    [Fact]
    public void A_bag_already_in_the_compact_layout_is_left_alone_so_opening_it_again_writes_nothing()
    {
        var items = Smalls(5, 1, 1);
        var packed = Fits(Packing.Pack(Open, items, new PackOptions()));
        var bag = items.Select(item => new BagItem(item, packed[item.Id])).ToList();

        Assert.IsType<OrganizePlan.AlreadyTidy>(Organizer.Plan(Open, bag, new PackOptions()));
    }

    [Fact]
    public void One_item_out_of_place_is_enough_to_reorganize()
    {
        var items = Smalls(5, 1, 1);
        var packed = Fits(Packing.Pack(Open, items, new PackOptions()));
        var bag = items.Select(item => new BagItem(item, item.Id == 5 ? new Cell(9, 9) : packed[item.Id])).ToList();

        Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Open, bag, new PackOptions()));
    }

    [Fact]
    public void A_bag_the_compact_layout_cannot_hold_falls_back_to_the_games_own_layout()
    {
        var space = GameOnlySpace();
        var bag = Scattered(GameOnlyBag());

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(space, bag, new PackOptions()));

        Assert.Equal(PackStyle.GameOrder, move.Style);
        Assert.Equal(Fits(Packing.PackLikeGame(space, GameOnlyBag())), move.Positions);
    }

    [Fact]
    public void With_compact_packing_off_the_games_own_layout_is_used()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Open, bag, new PackOptions(SmallCluster: false)));

        Assert.Equal(PackStyle.GameOrder, move.Style);
        Assert.Equal(new Cell(4, 0), move.Positions[5]);
    }

    [Fact]
    public void A_bag_no_layout_can_hold_is_left_exactly_as_it_is()
    {
        var bag = Scattered(Smalls(5, 1, 1));

        Assert.IsType<OrganizePlan.NoRoom>(Organizer.Plan(Space(2, 2), bag, new PackOptions()));
    }

    [Fact]
    public void An_empty_bag_needs_nothing()
    {
        Assert.IsType<OrganizePlan.AlreadyTidy>(Organizer.Plan(Open, Array.Empty<BagItem>(), new PackOptions()));
    }

    [Fact]
    public void A_planned_move_places_every_item_in_the_bag_once_and_nothing_else()
    {
        var items = new[] { Item(1, 3, 2), Item(2, 2, 2), Item(3, 1, 2), Item(4, 1, 1), Item(5, 2, 1) };

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Open, Scattered(items), new PackOptions()));

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, move.Positions.Keys.Order());
        Assert.True(Packing.IsValid(Open, items, move.Positions));
    }

    [Fact]
    public void A_bag_that_only_fits_by_using_a_mold_cell_is_left_exactly_as_it_is()
    {
        // The crowded bag fills every cell of its 4 by 3 grid, so one moldy cell leaves no room for either layout.
        var bag = Scattered(CrowdedBag());

        Assert.IsType<OrganizePlan.NoRoom>(Organizer.Plan(Space(4, 3, new Cell(3, 2)), bag, new PackOptions()));
    }

    [Fact]
    public void A_tidy_bag_with_an_item_on_mold_is_moved_off_it()
    {
        var items = Smalls(5, 1, 1);
        var packed = Fits(Packing.Pack(Open, items, new PackOptions()));
        var bag = items.Select(item => new BagItem(item, packed[item.Id])).ToList();
        var mold = packed[1];

        var move = Assert.IsType<OrganizePlan.Move>(Organizer.Plan(Space(10, 10, mold), bag, new PackOptions()));

        Assert.DoesNotContain(mold, move.Positions.Values);
    }

    [Fact]
    public void Putting_back_returns_every_item_saved_on_the_grid_to_its_saved_cell()
    {
        var saved = new[] { new BagItem(Item(1, 1, 1), new Cell(2, 2)), new BagItem(Item(2, 2, 1), new Cell(0, 3)) };
        var now = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(1, 1) };

        var writes = Organizer.PutBack(Space(4, 4), saved, now);

        Assert.Equal(new Dictionary<long, Cell> { [1] = new(2, 2), [2] = new(0, 3) }, writes);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void An_item_saved_off_the_grid_that_never_moved_is_not_written_back(int x, int y)
    {
        // A 2 by 1 item at 3,0 hangs off a 4 wide grid as surely as one at -1,0.
        var saved = new[] { new BagItem(Item(1, 2, 1), new Cell(x, y)) };
        var now = new Dictionary<long, Cell> { [1] = new(x, y) };

        Assert.Empty(Organizer.PutBack(Space(4, 1), saved, now));
    }

    [Fact]
    public void An_item_saved_off_the_grid_that_was_moved_onto_a_free_spot_stays_there()
    {
        var saved = new[] { new BagItem(Item(1, 1, 1), new Cell(-1, 0)), new BagItem(Item(2, 1, 1), new Cell(0, 0)) };
        var now = new Dictionary<long, Cell> { [1] = new(2, 1), [2] = new(1, 1) };

        var writes = Organizer.PutBack(Space(3, 2), saved, now);

        Assert.Equal(new Dictionary<long, Cell> { [2] = new(0, 0) }, writes);
    }

    [Fact]
    public void An_item_saved_off_the_grid_in_another_items_way_moves_to_the_first_free_spot_clear_of_mold()
    {
        var saved = new[] { new BagItem(Item(1, 1, 1), new Cell(-1, 0)), new BagItem(Item(2, 1, 1), new Cell(0, 0)) };
        var now = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(2, 0) };

        var writes = Organizer.PutBack(Space(3, 1, new Cell(1, 0)), saved, now);

        Assert.Equal(new Cell(0, 0), writes[2]);
        Assert.Equal(new Cell(2, 0), writes[1]);
    }

    [Fact]
    public void An_item_saved_off_the_grid_with_no_free_spot_left_gets_its_saved_cell_for_the_games_repair()
    {
        var saved = new[] { new BagItem(Item(1, 1, 1), new Cell(-1, 0)), new BagItem(Item(2, 1, 1), new Cell(0, 0)) };
        var now = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(0, 0) };

        var writes = Organizer.PutBack(Space(1, 1), saved, now);

        Assert.Equal(new Cell(-1, 0), writes[1]);
        Assert.Equal(new Cell(0, 0), writes[2]);
    }

    [Fact]
    public void An_item_saved_off_the_grid_whose_cell_now_cannot_be_read_gets_its_saved_cell_for_the_games_repair()
    {
        var saved = new[] { new BagItem(Item(1, 1, 1), new Cell(-1, 0)), new BagItem(Item(2, 1, 1), new Cell(0, 0)) };

        var writes = Organizer.PutBack(Space(3, 1), saved, new Dictionary<long, Cell>());

        Assert.Equal(new Cell(-1, 0), writes[1]);
        Assert.Equal(new Cell(0, 0), writes[2]);
    }

    /// <summary>Gives each item a current cell no layout ever chooses, so whatever the planner picks counts as a move.</summary>
    private static List<BagItem> Scattered(IEnumerable<PackItem> items) =>
        items.Select((item, row) => new BagItem(item, new Cell(-1, row))).ToList();
}
