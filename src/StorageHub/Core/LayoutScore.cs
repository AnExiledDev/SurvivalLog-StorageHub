using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// How useful the free space a layout leaves is. Two scores are compared key by key, and the first key that differs
/// decides:
/// StrandedCells, free cells no free 2 by 2 square covers, the slivers only a small item can use (fewer is better);
/// BigItemRoom, the cells of every big-item shape that still fits somewhere, each shape counted once (more is better);
/// LargestFreeRectangle, in cells (more is better);
/// FreeRegions, separate pieces of free space (fewer is better);
/// SameItemEdges, cell edges shared by two items of the same kind (more is better).
/// </summary>
public readonly record struct LayoutScore(int StrandedCells, int BigItemRoom, int LargestFreeRectangle, int FreeRegions, int SameItemEdges)
    : IComparable<LayoutScore>
{
    public int CompareTo(LayoutScore other) => Rank.CompareTo(other.Rank);

    /// <summary>The keys in order, each turned so that higher is better.</summary>
    private (int, int, int, int, int) Rank => (-StrandedCells, BigItemRoom, LargestFreeRectangle, -FreeRegions, SameItemEdges);
}

/// <summary>An item's width and height in cells, without a place in the bag.</summary>
internal readonly record struct Footprint(int W, int H)
{
    public int Area => W * H;
}

/// <summary>Scores the free space a layout leaves. Pure: the same layout always gets the same score.</summary>
internal static class LayoutScoring
{
    /// <summary>The shapes big items commonly come in, so room is kept for big items the bag does not hold yet.</summary>
    private static readonly Footprint[] CommonBigShapes =
    {
        new(2, 2), new(2, 3), new(3, 2), new(3, 3), new(2, 4), new(4, 2), new(3, 4), new(4, 3), new(4, 4),
    };

    public static LayoutScore Score(BagSpace space, IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions, PackOptions options)
    {
        var grid = space.Grid;
        var taken = TakenCells(space, items, positions);

        return new LayoutScore(
            StrandedCells(taken, grid),
            RoomLeft(taken, grid, BigShapes(items, options)),
            LargestFreeRectangle(taken, grid),
            FreeRegions(taken, grid),
            SameItemEdges(items, positions));
    }

    /// <summary>The shapes room is kept for: the common big shapes and the shape of every big item in the bag, each once.</summary>
    private static IEnumerable<Footprint> BigShapes(IReadOnlyList<PackItem> items, PackOptions options) =>
        CommonBigShapes
            .Concat(items.Where(item => item.Area > options.SmallMaxArea).Select(item => new Footprint(item.W, item.H)))
            .Distinct();

    /// <summary>The bag's taken cells: mold, and every item placed wholly inside the grid.</summary>
    private static Occupancy TakenCells(BagSpace space, IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions)
    {
        var taken = new Occupancy(space);

        foreach (var item in items)
        {
            if (positions.TryGetValue(item.Id, out var at) && space.Grid.Holds(at, item.W, item.H)) taken.Take(at, item.W, item.H);
        }

        return taken;
    }

    private static int RoomLeft(Occupancy taken, GridSize grid, IEnumerable<Footprint> shapes)
    {
        var takenCounts = TakenCounts(taken, grid);

        return shapes.Where(shape => FitsSomewhere(takenCounts, shape)).Sum(shape => shape.Area);
    }

    /// <summary>
    /// How many taken cells lie above and to the left of each grid corner, so counting the taken cells under any
    /// rectangle takes four lookups instead of a walk over it.
    /// </summary>
    private static int[,] TakenCounts(Occupancy taken, GridSize grid)
    {
        var counts = new int[Math.Max(0, grid.Cols) + 1, Math.Max(0, grid.Rows) + 1];

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++)
            {
                var here = taken.IsTaken(x, y) ? 1 : 0;

                counts[x + 1, y + 1] = here + counts[x, y + 1] + counts[x + 1, y] - counts[x, y];
            }
        }

        return counts;
    }

    /// <summary>True when the shape has a free spot anywhere in the bag.</summary>
    private static bool FitsSomewhere(int[,] takenCounts, Footprint shape)
    {
        var cols = takenCounts.GetLength(0) - 1;
        var rows = takenCounts.GetLength(1) - 1;

        if (shape.W <= 0 || shape.H <= 0) return false;

        for (var y = 0; y + shape.H <= rows; y++)
        {
            for (var x = 0; x + shape.W <= cols; x++)
            {
                var takenUnder = takenCounts[x + shape.W, y + shape.H] - takenCounts[x, y + shape.H] - takenCounts[x + shape.W, y] + takenCounts[x, y];

                if (takenUnder == 0) return true;
            }
        }

        return false;
    }

    private static int LargestFreeRectangle(Occupancy taken, GridSize grid)
    {
        var heights = new int[Math.Max(0, grid.Cols)];
        var largest = 0;

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++) heights[x] = taken.IsTaken(x, y) ? 0 : heights[x] + 1;

            largest = Math.Max(largest, LargestUnder(heights));
        }

        return largest;
    }

    /// <summary>The largest rectangle standing on a row, given how many free cells each column has stacked up to it.</summary>
    private static int LargestUnder(int[] heights)
    {
        var largest = 0;

        for (var left = 0; left < heights.Length; left++)
        {
            var height = int.MaxValue;

            for (var right = left; right < heights.Length && heights[right] > 0; right++)
            {
                height = Math.Min(height, heights[right]);
                largest = Math.Max(largest, height * (right - left + 1));
            }
        }

        return largest;
    }

    private static int StrandedCells(Occupancy taken, GridSize grid)
    {
        var stranded = 0;

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++)
            {
                if (!taken.IsTaken(x, y) && !InFreeSquare(taken, x, y)) stranded++;
            }
        }

        return stranded;
    }

    /// <summary>True when some free 2 by 2 square covers the cell.</summary>
    private static bool InFreeSquare(Occupancy taken, int x, int y) =>
        taken.IsFree(new Cell(x, y), 2, 2)
        || taken.IsFree(new Cell(x - 1, y), 2, 2)
        || taken.IsFree(new Cell(x, y - 1), 2, 2)
        || taken.IsFree(new Cell(x - 1, y - 1), 2, 2);

    private static int FreeRegions(Occupancy taken, GridSize grid)
    {
        var seen = new bool[Math.Max(0, grid.Cols), Math.Max(0, grid.Rows)];
        var regions = 0;

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++)
            {
                if (taken.IsTaken(x, y) || seen[x, y]) continue;

                regions++;

                MarkRegion(taken, grid, new Cell(x, y), seen);
            }
        }

        return regions;
    }

    /// <summary>Marks every free cell joined to the start through free side neighbours as seen.</summary>
    private static void MarkRegion(Occupancy taken, GridSize grid, Cell start, bool[,] seen)
    {
        var open = new Stack<Cell>();

        Visit(start.X, start.Y);

        while (open.Count > 0)
        {
            var cell = open.Pop();

            Visit(cell.X + 1, cell.Y);
            Visit(cell.X - 1, cell.Y);
            Visit(cell.X, cell.Y + 1);
            Visit(cell.X, cell.Y - 1);
        }

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= grid.Cols || y >= grid.Rows || taken.IsTaken(x, y) || seen[x, y]) return;

            seen[x, y] = true;

            open.Push(new Cell(x, y));
        }
    }

    private static int SameItemEdges(IReadOnlyList<PackItem> items, IReadOnlyDictionary<long, Cell> positions)
    {
        var placed = items.Where(item => positions.ContainsKey(item.Id)).Select(item => (Item: item, At: positions[item.Id])).ToList();
        var edges = 0;

        for (var i = 0; i < placed.Count; i++)
        {
            for (var j = i + 1; j < placed.Count; j++)
            {
                if (placed[i].Item.ConfigId == placed[j].Item.ConfigId) edges += SharedEdge(placed[i].Item, placed[i].At, placed[j].Item, placed[j].At);
            }
        }

        return edges;
    }

    /// <summary>How many cell edges two placed items share: the length of the side where they touch, or 0.</summary>
    private static int SharedEdge(PackItem a, Cell aAt, PackItem b, Cell bAt)
    {
        var sideBySide = aAt.X + a.W == bAt.X || bAt.X + b.W == aAt.X ? Overlap(aAt.Y, a.H, bAt.Y, b.H) : 0;
        var aboveBelow = aAt.Y + a.H == bAt.Y || bAt.Y + b.H == aAt.Y ? Overlap(aAt.X, a.W, bAt.X, b.W) : 0;

        return sideBySide + aboveBelow;
    }

    /// <summary>How many cells two runs along one line have in common.</summary>
    private static int Overlap(int start, int length, int otherStart, int otherLength) =>
        Math.Max(0, Math.Min(start + length, otherStart + otherLength) - Math.Max(start, otherStart));
}
