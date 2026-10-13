using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.PackingTests;

namespace StorageHub.Tests;

public class LayoutScoreTests
{
    private static readonly PackOptions Options = new();

    [Fact]
    public void A_free_cell_no_free_2_by_2_square_covers_counts_as_stranded()
    {
        // In a 3 by 2 bag with the top-left cell taken, 0,1 is the only free cell outside the free square at 1,0.
        var score = ScoreOf(Space(3, 2), (Item(1, 1, 1), new Cell(0, 0)));

        Assert.Equal(1, score.StrandedCells);
    }

    [Fact]
    public void Big_item_room_adds_up_every_big_shape_that_still_fits_counting_each_shape_once()
    {
        // Two 3 by 1 items leave a 3 by 2 space: a 2 by 2 (4), a 3 by 2 (6) and the bag's own 3 by 1 (3) still fit.
        var score = ScoreOf(Space(3, 4), (Item(1, 3, 1), new Cell(0, 0)), (Item(2, 3, 1), new Cell(0, 1)));

        Assert.Equal(13, score.BigItemRoom);
    }

    [Fact]
    public void The_largest_free_rectangle_is_measured_in_cells()
    {
        var score = ScoreOf(Space(4, 2), (Item(1, 1, 1), new Cell(0, 0)));

        Assert.Equal(6, score.LargestFreeRectangle);
    }

    [Fact]
    public void Free_cells_that_only_meet_at_a_corner_are_separate_regions()
    {
        var score = ScoreOf(Space(2, 2), (Item(1, 1, 1), new Cell(1, 0)), (Item(2, 1, 1), new Cell(0, 1)));

        Assert.Equal(2, score.FreeRegions);
    }

    [Fact]
    public void Same_item_edges_count_the_side_two_items_of_one_kind_share()
    {
        var sameKind = ScoreOf(Space(4, 2), (Item(1, 2, 2, configId: 7), new Cell(0, 0)), (Item(2, 2, 2, configId: 7), new Cell(2, 0)));
        var twoKinds = ScoreOf(Space(4, 2), (Item(1, 2, 2, configId: 7), new Cell(0, 0)), (Item(2, 2, 2, configId: 8), new Cell(2, 0)));

        Assert.Equal(2, sameKind.SameItemEdges);
        Assert.Equal(0, twoKinds.SameItemEdges);
    }

    [Fact]
    public void Mold_is_not_free_space()
    {
        var score = Packing.Score(Space(3, 2, new Cell(0, 0)), Array.Empty<PackItem>(), new Dictionary<long, Cell>(), Options);

        Assert.Equal(1, score.StrandedCells);
        Assert.Equal(4, score.LargestFreeRectangle);
    }

    [Fact]
    public void Fewer_stranded_cells_outrank_every_other_key()
    {
        var fewerStranded = new LayoutScore(StrandedCells: 0, BigItemRoom: 0, LargestFreeRectangle: 0, FreeRegions: 9, SameItemEdges: 0);
        var betterElsewhere = new LayoutScore(StrandedCells: 1, BigItemRoom: 99, LargestFreeRectangle: 99, FreeRegions: 1, SameItemEdges: 99);

        Assert.True(fewerStranded.CompareTo(betterElsewhere) > 0);
    }

    [Fact]
    public void More_big_item_room_outranks_the_keys_after_it()
    {
        var moreRoom = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 0, FreeRegions: 9, SameItemEdges: 0);
        var betterLater = new LayoutScore(StrandedCells: 0, BigItemRoom: 9, LargestFreeRectangle: 99, FreeRegions: 1, SameItemEdges: 99);

        Assert.True(moreRoom.CompareTo(betterLater) > 0);
    }

    [Fact]
    public void A_larger_free_rectangle_outranks_the_keys_after_it()
    {
        var largerRectangle = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 8, FreeRegions: 9, SameItemEdges: 0);
        var betterLater = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 7, FreeRegions: 1, SameItemEdges: 99);

        Assert.True(largerRectangle.CompareTo(betterLater) > 0);
    }

    [Fact]
    public void Fewer_free_regions_outrank_same_item_edges()
    {
        var fewerRegions = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 8, FreeRegions: 1, SameItemEdges: 0);
        var moreEdges = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 8, FreeRegions: 2, SameItemEdges: 99);

        Assert.True(fewerRegions.CompareTo(moreEdges) > 0);
    }

    [Fact]
    public void More_same_item_edges_break_a_tie_on_everything_else()
    {
        var moreEdges = new LayoutScore(StrandedCells: 0, BigItemRoom: 10, LargestFreeRectangle: 8, FreeRegions: 1, SameItemEdges: 3);
        var fewerEdges = moreEdges with { SameItemEdges = 2 };

        Assert.True(moreEdges.CompareTo(fewerEdges) > 0);
        Assert.Equal(0, moreEdges.CompareTo(moreEdges with { }));
    }

    private static LayoutScore ScoreOf(BagSpace space, params (PackItem Item, Cell At)[] placed) =>
        Packing.Score(space, placed.Select(entry => entry.Item).ToList(), placed.ToDictionary(entry => entry.Item.Id, entry => entry.At), Options);
}
