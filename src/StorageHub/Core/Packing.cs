using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>A cell of a bag's grid, counted from the top-left corner as the game counts it.</summary>
public readonly record struct Cell(int X, int Y);

/// <summary>A bag's size in cells.</summary>
public readonly record struct GridSize(int Cols, int Rows)
{
    /// <summary>True when a w by h item at this cell lies wholly inside the grid.</summary>
    public bool Holds(Cell at, int w, int h) => w > 0 && h > 0 && at.X >= 0 && at.Y >= 0 && at.X + w <= Cols && at.Y + h <= Rows;
}

/// <summary>
/// The room a layout may use: the bag's grid less its Blocked cells. Blocked cells are the bag's mold. The game's own
/// PackNewItemAvoidingMold packs around them too, and food left on one is infected at the next spread round, so no
/// layout ever places an item on one.
/// </summary>
public sealed record BagSpace(GridSize Grid, IReadOnlyCollection<Cell> Blocked)
{
    public static BagSpace Unblocked(GridSize grid) => new(grid, Array.Empty<Cell>());
}

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
    /// The compact layout: the tidiest of several layouts built for the bag. They are the corner layout and the
    /// contact-point layouts (see ContactPacking). The one whose free space scores best wins (see LayoutScore), the
    /// earlier one on a tie, so the same bag always packs the same way.
    /// </summary>
    public static PackResult Pack(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options)
    {
        if (items.Any(item => item.W <= 0 || item.H <= 0)) return new PackResult.DoesNotFit();

        return Best(space, items, options, Candidates(space, items, options)) is { } best
            ? new PackResult.Fits(best)
            : new PackResult.DoesNotFit();
    }

    /// <summary>
    /// How good the free space a layout leaves is, for comparing layouts of one bag. Pure, and total: an item with no
    /// position or one off the grid simply takes no cells.
    /// </summary>
    public static LayoutScore Score(BagSpace space, IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions, PackOptions options) =>
        LayoutScoring.Score(space, items, positions, options);

    /// <summary>
    /// The corner layout. Big items go in first, each into the free spot nearest the bottom-right corner. Small items
    /// then fill the smallest square in the top-left corner that holds them all, so the space left over stays in one
    /// piece for the next big item. The square grows a cell at a time until it is the whole bag.
    /// </summary>
    internal static PackResult PackCorners(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options)
    {
        var grid = space.Grid;
        var large = GameOrder(items.Where(item => item.Area > options.SmallMaxArea));
        var small = GameOrder(items.Where(item => item.Area <= options.SmallMaxArea));
        var taken = new Occupancy(space);
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
    public static PackResult PackLikeGame(BagSpace space, IReadOnlyList<PackItem> items) =>
        FillFromTopLeft(GameOrder(items), new Occupancy(space), space.Grid.Cols, space.Grid.Rows) is { } positions
            ? new PackResult.Fits(positions)
            : new PackResult.DoesNotFit();

    /// <summary>
    /// A layout the game can be given: every item placed exactly once, inside the bag, on no other item and no blocked
    /// cell. Checked before anything moves, so a packing mistake can never put two items on one cell, push one off the
    /// grid or set one on mold.
    /// </summary>
    public static bool IsValid(BagSpace space, IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions)
    {
        if (positions.Count != items.Count) return false;

        var taken = new Occupancy(space);

        foreach (var item in items)
        {
            if (!positions.TryGetValue(item.Id, out var at) || !taken.IsFree(at, item.W, item.H)) return false;

            taken.Take(at, item.W, item.H);
        }

        return true;
    }

    /// <summary>The corner layout first, then every contact-point layout that holds all the items.</summary>
    private static IEnumerable<IReadOnlyDictionary<long, Cell>> Candidates(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options)
    {
        if (PackCorners(space, items, options) is PackResult.Fits corners) yield return corners.Positions;

        foreach (var layout in ContactPacking.Layouts(space, items, options)) yield return layout;
    }

    /// <summary>The layout with the best score, the earliest on a tie; null when there is none.</summary>
    private static IReadOnlyDictionary<long, Cell>? Best(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options, IEnumerable<IReadOnlyDictionary<long, Cell>> layouts)
    {
        IReadOnlyDictionary<long, Cell>? best = null;
        var bestScore = default(LayoutScore);

        foreach (var layout in layouts)
        {
            var score = Score(space, items, layout, options);

            if (best is not null && score.CompareTo(bestScore) <= 0) continue;

            best = layout;
            bestScore = score;
        }

        return best;
    }

    /// <summary>The order the game's Sort uses, with the item id last so the same bag always packs the same way.</summary>
    internal static List<PackItem> GameOrder(IEnumerable<PackItem> items) =>
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
