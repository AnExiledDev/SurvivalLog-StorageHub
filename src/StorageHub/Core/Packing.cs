using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>A cell of a bag's grid, counted from the top-left corner as the game counts it.</summary>
public readonly record struct Cell(int X, int Y);

/// <summary>A bag's size in cells.</summary>
public readonly record struct GridSize(int Cols, int Rows);

/// <summary>
/// One item as a layout sees it: its footprint in cells and the keys the game's Sort orders by. The game never turns
/// an item, so no layout does either.
/// </summary>
public sealed record PackItem(long Id, int W, int H, int ConfigId, int Category)
{
    public int Area => W * H;
}

/// <summary>An item covering SmallMaxArea cells or fewer is small. SmallCluster turns the compact layout on.</summary>
public sealed record PackOptions(int SmallMaxArea = 2, bool SmallCluster = true);

public abstract record PackResult
{
    /// <summary>Where every item goes, by item id.</summary>
    public sealed record Fits(IReadOnlyDictionary<long, Cell> Positions) : PackResult;

    public sealed record DoesNotFit : PackResult;
}

public static class Packing
{
    /// <summary>
    /// The compact layout. Big items go in first, each into the free spot nearest the bottom-right corner. Small items
    /// then fill the smallest square in the top-left corner that holds them all, so the space left over stays in one
    /// piece for the next big item. The square grows a cell at a time until it is the whole bag.
    /// </summary>
    public static PackResult Pack(GridSize grid, IReadOnlyList<PackItem> items, PackOptions options)
    {
        if (items.Any(item => item.W <= 0 || item.H <= 0)) return new PackResult.DoesNotFit();

        var large = GameOrder(items.Where(item => item.Area > options.SmallMaxArea));
        var small = GameOrder(items.Where(item => item.Area <= options.SmallMaxArea));
        var taken = new Occupancy(grid);
        var positions = new Dictionary<long, Cell>();

        foreach (var item in large)
        {
            if (taken.LastFit(item.W, item.H) is not { } at) return new PackResult.DoesNotFit();

            taken.Take(at, item.W, item.H);

            positions[item.Id] = at;
        }

        for (var side = SquareSide(small); side <= Math.Max(grid.Cols, grid.Rows); side++)
        {
            if (FillFromTopLeft(small, taken.Copy(), Math.Min(side, grid.Cols), Math.Min(side, grid.Rows)) is not { } square) continue;

            foreach (var placed in square) positions[placed.Key] = placed.Value;

            return new PackResult.Fits(positions);
        }

        return new PackResult.DoesNotFit();
    }

    /// <summary>
    /// The game's own Sort (BagPacker.PackItems): biggest first, then by category and item type, each into the first
    /// free spot reading row by row from the top-left.
    /// </summary>
    public static PackResult PackLikeGame(GridSize grid, IReadOnlyList<PackItem> items) =>
        FillFromTopLeft(GameOrder(items), new Occupancy(grid), grid.Cols, grid.Rows) is { } positions
            ? new PackResult.Fits(positions)
            : new PackResult.DoesNotFit();

    /// <summary>
    /// A layout the game can be given: every item placed exactly once, inside the bag, on no other item. Checked
    /// before anything moves, so a packing mistake can never put two items on one cell or push one off the grid.
    /// </summary>
    public static bool IsValid(GridSize grid, IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions)
    {
        if (positions.Count != items.Count) return false;

        var taken = new Occupancy(grid);

        foreach (var item in items)
        {
            if (!positions.TryGetValue(item.Id, out var at) || !taken.IsFree(at, item.W, item.H)) return false;

            taken.Take(at, item.W, item.H);
        }

        return true;
    }

    /// <summary>The order the game's Sort uses, with the item id last so the same bag always packs the same way.</summary>
    private static List<PackItem> GameOrder(IEnumerable<PackItem> items) =>
        items
            .OrderByDescending(item => item.Area)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.ConfigId)
            .ThenBy(item => item.Id)
            .ToList();

    /// <summary>The first square worth trying: the smallest whose area covers the items, before their shapes are taken into account.</summary>
    private static int SquareSide(IEnumerable<PackItem> items) => (int)Math.Ceiling(Math.Sqrt(items.Sum(item => item.Area)));

    /// <summary>Places each item, in order, at the first free spot inside the top-left cols by rows of the bag; null when one does not fit.</summary>
    private static Dictionary<long, Cell>? FillFromTopLeft(IEnumerable<PackItem> items, Occupancy taken, int cols, int rows)
    {
        var positions = new Dictionary<long, Cell>();

        foreach (var item in items)
        {
            if (taken.FirstFit(item.W, item.H, cols, rows) is not { } at) return null;

            taken.Take(at, item.W, item.H);

            positions[item.Id] = at;
        }

        return positions;
    }
}
