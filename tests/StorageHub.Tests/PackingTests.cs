using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class PackingTests
{
    private static readonly PackOptions Compact = new();

    [Fact]
    public void Small_items_form_a_square_in_the_top_left_instead_of_a_strip_along_the_top()
    {
        var grid = new GridSize(10, 10);
        var smalls = Smalls(5, 1, 1);

        var compact = Fits(Packing.Pack(grid, smalls, Compact));
        var game = Fits(Packing.PackLikeGame(grid, smalls));

        Assert.All(compact.Values, at => Assert.True(at.X < 3 && at.Y < 3, $"{at} is outside the 3 by 3 square"));
        Assert.Equal(new Cell(4, 0), game[5]);
    }

    [Fact]
    public void The_square_grows_a_cell_at_a_time_when_the_items_do_not_fit_its_shape()
    {
        // Four 2 by 1 items cover 8 cells, so a 3 by 3 square has the area but not the shape: only one fits per row.
        var positions = Fits(Packing.Pack(new GridSize(10, 10), Smalls(4, 2, 1), Compact));

        Assert.Equal(new Cell(0, 0), positions[1]);
        Assert.Equal(new Cell(2, 0), positions[2]);
        Assert.Equal(new Cell(0, 1), positions[3]);
        Assert.Equal(new Cell(2, 1), positions[4]);
    }

    [Fact]
    public void Big_items_keep_to_the_bottom_right_and_small_ones_to_the_top_left()
    {
        var items = new[] { Item(1, 3, 2), Item(2, 1, 1) };

        var positions = Fits(Packing.Pack(new GridSize(6, 4), items, Compact));

        Assert.Equal(new Cell(3, 2), positions[1]);
        Assert.Equal(new Cell(0, 0), positions[2]);
    }

    [Fact]
    public void The_compact_layout_leaves_room_for_a_big_item_the_games_layout_has_no_space_for()
    {
        var grid = new GridSize(6, 4);
        var smalls = Smalls(10, 1, 1);
        var tallChest = Item(99, 2, 4);

        Assert.True(FitsAnywhere(grid, smalls, Fits(Packing.Pack(grid, smalls, Compact)), tallChest));
        Assert.False(FitsAnywhere(grid, smalls, Fits(Packing.PackLikeGame(grid, smalls)), tallChest));
    }

    [Fact]
    public void Compact_packing_gives_up_rather_than_squeeze_when_the_small_items_cannot_all_fit()
    {
        Assert.IsType<PackResult.DoesNotFit>(Packing.Pack(new GridSize(4, 3), CrowdedBag(), Compact));
    }

    [Fact]
    public void The_games_layout_still_fits_a_bag_the_compact_one_cannot()
    {
        var positions = Fits(Packing.PackLikeGame(new GridSize(4, 3), CrowdedBag()));

        Assert.Equal(new Cell(0, 0), positions[1]);
        Assert.Equal(new Cell(0, 2), positions[2]);
        Assert.Equal(new Cell(2, 0), positions[3]);
        Assert.Equal(new Cell(3, 1), positions[4]);
        Assert.Equal(new Cell(2, 1), positions[5]);
    }

    [Fact]
    public void The_games_layout_orders_by_size_then_category_then_item_type()
    {
        var items = new[] { Item(1, 1, 1, configId: 7, category: 2), Item(2, 1, 1, configId: 9, category: 1), Item(3, 1, 1, configId: 8, category: 1), Item(4, 2, 1, configId: 99, category: 9) };

        var positions = Fits(Packing.PackLikeGame(new GridSize(10, 1), items));

        Assert.Equal(new Cell(0, 0), positions[4]);
        Assert.Equal(new Cell(2, 0), positions[3]);
        Assert.Equal(new Cell(3, 0), positions[2]);
        Assert.Equal(new Cell(4, 0), positions[1]);
    }

    [Fact]
    public void A_bag_with_more_items_than_cells_does_not_fit_either_way()
    {
        var items = Smalls(5, 1, 1);

        Assert.IsType<PackResult.DoesNotFit>(Packing.Pack(new GridSize(2, 2), items, Compact));
        Assert.IsType<PackResult.DoesNotFit>(Packing.PackLikeGame(new GridSize(2, 2), items));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Every_item_is_placed_exactly_once_on_its_own_cells(int seed)
    {
        var grid = new GridSize(8, 6);
        var items = RandomBag(seed, grid);

        foreach (var result in new[] { Packing.Pack(grid, items, Compact), Packing.PackLikeGame(grid, items) })
        {
            if (result is not PackResult.Fits fits) continue;

            Assert.Equal(items.Select(item => item.Id).Order(), fits.Positions.Keys.Order());
            Assert.True(Packing.IsValid(grid, items, fits.Positions));
        }
    }

    [Fact]
    public void A_layout_with_two_items_on_one_cell_is_rejected()
    {
        var items = new[] { Item(1, 2, 1), Item(2, 1, 1) };
        var positions = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(1, 0) };

        Assert.False(Packing.IsValid(new GridSize(4, 4), items, positions));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(0, 4)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void A_layout_that_puts_an_item_off_the_grid_is_rejected(int x, int y)
    {
        var items = new[] { Item(1, 2, 1) };
        var positions = new Dictionary<long, Cell> { [1] = new(x, y) };

        Assert.False(Packing.IsValid(new GridSize(4, 4), items, positions));
    }

    [Fact]
    public void A_layout_that_leaves_an_item_out_is_rejected()
    {
        var items = new[] { Item(1, 1, 1), Item(2, 1, 1) };
        var positions = new Dictionary<long, Cell> { [1] = new(0, 0) };

        Assert.False(Packing.IsValid(new GridSize(4, 4), items, positions));
    }

    [Fact]
    public void A_layout_that_places_an_item_the_bag_does_not_hold_is_rejected()
    {
        var items = new[] { Item(1, 1, 1), Item(2, 1, 1) };
        var positions = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(1, 0), [3] = new(2, 0) };

        Assert.False(Packing.IsValid(new GridSize(4, 4), items, positions));
    }

    [Fact]
    public void A_layout_with_every_item_inside_and_apart_is_accepted()
    {
        var items = new[] { Item(1, 2, 2), Item(2, 2, 2) };
        var positions = new Dictionary<long, Cell> { [1] = new(0, 0), [2] = new(2, 2) };

        Assert.True(Packing.IsValid(new GridSize(4, 4), items, positions));
    }

    /// <summary>
    /// A 4 by 3 bag the compact layout cannot hold: the two big items split the free cells so the 1 by 2 item has no
    /// column, while the game's order fills every cell.
    /// </summary>
    internal static PackItem[] CrowdedBag() =>
        new[] { Item(1, 2, 2, configId: 10), Item(2, 3, 1, configId: 11), Item(3, 2, 1, configId: 20), Item(4, 1, 2, configId: 21), Item(5, 1, 1, configId: 22) };

    internal static PackItem Item(long id, int w, int h, int configId = 0, int category = 0) => new(id, w, h, configId, category);

    /// <summary>Count identical items with ids 1 to count, so the game's order is their id order.</summary>
    internal static PackItem[] Smalls(int count, int w, int h) =>
        Enumerable.Range(1, count).Select(id => Item(id, w, h)).ToArray();

    internal static IReadOnlyDictionary<long, Cell> Fits(PackResult result) =>
        Assert.IsType<PackResult.Fits>(result).Positions;

    private static bool FitsAnywhere(GridSize grid, IReadOnlyList<PackItem> packed, IReadOnlyDictionary<long, Cell> positions, PackItem extra)
    {
        var items = packed.Append(extra).ToList();

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++)
            {
                var withExtra = new Dictionary<long, Cell>(positions) { [extra.Id] = new(x, y) };

                if (Packing.IsValid(grid, items, withExtra)) return true;
            }
        }

        return false;
    }

    /// <summary>A mixed bag that covers at most two thirds of the grid, the same for the same seed.</summary>
    private static PackItem[] RandomBag(int seed, GridSize grid)
    {
        var random = new Random(seed);
        var items = new List<PackItem>();
        var area = 0;

        while (true)
        {
            var item = Item(items.Count + 1, random.Next(1, 4), random.Next(1, 3), configId: random.Next(1, 6), category: random.Next(0, 3));

            if (area + item.Area > grid.Cols * grid.Rows * 2 / 3) return items.ToArray();

            items.Add(item);

            area += item.Area;
        }
    }
}
