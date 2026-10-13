using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>Which free spot an item takes when several touch it on the same number of sides.</summary>
internal enum Lean
{
    /// <summary>The lowest row first, then the rightmost column.</summary>
    BottomRight,

    /// <summary>The top row first, then the leftmost column, so items run along the top of the bag.</summary>
    TopLeftByRow,

    /// <summary>The leftmost column first, then the top row, so items run down the left side and keep the right side free.</summary>
    TopLeftByColumn,
}

/// <summary>
/// Contact-point layouts. Items go in one at a time, each into the free spot where the most of its outline touches the
/// bag's edge, mold or an item already placed, so items settle into corners and against each other instead of leaving
/// slivers. On a tie the spot touching more items of the same kind wins, then the spot nearest the item's corner. Big
/// items go in first and lean to the bottom-right; small items lean to the top-left, or to the bottom-right too. Every
/// pairing of a big-item order, a small-item order and a small-item lean gives one layout.
/// </summary>
internal static class ContactPacking
{
    private static readonly Lean[] SmallLeans = { Lean.TopLeftByRow, Lean.TopLeftByColumn, Lean.BottomRight };

    /// <summary>Every layout that holds all the items, in a fixed order, so the same bag always gives the same layouts.</summary>
    public static IEnumerable<IReadOnlyDictionary<long, Cell>> Layouts(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options)
    {
        var big = items.Where(item => item.Area > options.SmallMaxArea).ToList();
        var small = items.Where(item => item.Area <= options.SmallMaxArea).ToList();

        foreach (var variant in Variants(big, small).DistinctBy(variant => variant.Key))
        {
            if (Place(space, variant) is { } positions) yield return positions;
        }
    }

    private static IEnumerable<Variant> Variants(List<PackItem> big, List<PackItem> small) =>
        from bigOrder in BigOrders(big)
        from smallOrder in SmallOrders(small)
        from lean in SmallLeans
        select new Variant(bigOrder, smallOrder, lean);

    private static IEnumerable<List<PackItem>> BigOrders(List<PackItem> big)
    {
        yield return big.OrderByDescending(item => item.Area).ThenBy(item => item.Id).ToList();
        yield return big.OrderByDescending(item => item.H).ThenByDescending(item => item.W).ThenBy(item => item.Id).ToList();
        yield return big.OrderByDescending(item => item.W).ThenByDescending(item => item.H).ThenBy(item => item.Id).ToList();
        yield return GroupedByKind(big);
    }

    private static IEnumerable<List<PackItem>> SmallOrders(List<PackItem> small)
    {
        yield return Packing.GameOrder(small);
        yield return GroupedByKind(small);
        yield return small.OrderByDescending(item => item.H).ThenByDescending(item => item.W).ThenBy(item => item.Id).ToList();
    }

    /// <summary>Items of one kind together, the kinds with the biggest items first.</summary>
    private static List<PackItem> GroupedByKind(IEnumerable<PackItem> items) =>
        items
            .GroupBy(item => item.ConfigId)
            .OrderByDescending(kind => kind.Max(item => item.Area))
            .ThenBy(kind => kind.Key)
            .SelectMany(kind => kind.OrderBy(item => item.Id))
            .ToList();

    /// <summary>Where every item goes when placed in the variant's order, or null when one finds no spot.</summary>
    private static IReadOnlyDictionary<long, Cell>? Place(BagSpace space, Variant variant)
    {
        var bag = new ContactGrid(space);
        var placements = variant.Big.Select(item => (item, Lean.BottomRight)).Concat(variant.Small.Select(item => (item, variant.SmallLean)));

        foreach (var (item, lean) in placements)
        {
            if (!bag.TryPlace(item, lean)) return null;
        }

        return bag.Positions;
    }

    /// <summary>One order to place the items in. The key tells two variants with the same orders apart from different ones.</summary>
    private sealed record Variant(List<PackItem> Big, List<PackItem> Small, Lean SmallLean)
    {
        public string Key { get; } = $"{string.Join(",", Big.Select(item => item.Id))}|{string.Join(",", Small.Select(item => item.Id))}|{SmallLean}";
    }
}

/// <summary>A bag being filled by contact: which cells are taken, and which item sits on each.</summary>
internal sealed class ContactGrid
{
    private readonly GridSize _grid;
    private readonly Occupancy _taken;
    private readonly PackItem?[,] _owners;
    private readonly Dictionary<long, Cell> _positions = new();

    public ContactGrid(BagSpace space)
    {
        _grid = space.Grid;
        _taken = new Occupancy(space);
        _owners = new PackItem?[Math.Max(0, _grid.Cols), Math.Max(0, _grid.Rows)];
    }

    public IReadOnlyDictionary<long, Cell> Positions => _positions;

    /// <summary>Puts the item on its best free spot; false when it has none.</summary>
    public bool TryPlace(PackItem item, Lean lean)
    {
        if (BestSpot(item, lean) is not { } at) return false;

        _taken.Take(at, item.W, item.H);

        for (var x = at.X; x < at.X + item.W; x++)
        {
            for (var y = at.Y; y < at.Y + item.H; y++) _owners[x, y] = item;
        }

        _positions[item.Id] = at;

        return true;
    }

    /// <summary>The free spot touching the most, then touching the most of the same kind, then first in the lean's order.</summary>
    private Cell? BestSpot(PackItem item, Lean lean)
    {
        Cell? best = null;
        var bestTouch = (Sides: -1, SameKind: -1);

        foreach (var at in Spots(item, lean))
        {
            if (!_taken.IsFree(at, item.W, item.H)) continue;

            var touch = Touch(item, at);

            if (touch.CompareTo(bestTouch) <= 0) continue;

            best = at;
            bestTouch = touch;
        }

        return best;
    }

    /// <summary>Every top-left cell where the item lies inside the bag, in the order the lean prefers them.</summary>
    private IEnumerable<Cell> Spots(PackItem item, Lean lean)
    {
        var lastX = _grid.Cols - item.W;
        var lastY = _grid.Rows - item.H;

        switch (lean)
        {
            case Lean.BottomRight:
                for (var y = lastY; y >= 0; y--)
                {
                    for (var x = lastX; x >= 0; x--) yield return new Cell(x, y);
                }

                break;
            case Lean.TopLeftByRow:
                for (var y = 0; y <= lastY; y++)
                {
                    for (var x = 0; x <= lastX; x++) yield return new Cell(x, y);
                }

                break;
            case Lean.TopLeftByColumn:
                for (var x = 0; x <= lastX; x++)
                {
                    for (var y = 0; y <= lastY; y++) yield return new Cell(x, y);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "unknown lean");
        }
    }

    /// <summary>
    /// How much of the item's outline touches something when it sits at this cell: Sides counts the cells just outside
    /// its edges that are off the grid, mold or taken, and SameKind those taken by an item of the same kind.
    /// </summary>
    private (int Sides, int SameKind) Touch(PackItem item, Cell at)
    {
        var touch = (Sides: 0, SameKind: 0);

        for (var x = at.X; x < at.X + item.W; x++)
        {
            Count(item, new Cell(x, at.Y - 1), ref touch);
            Count(item, new Cell(x, at.Y + item.H), ref touch);
        }

        for (var y = at.Y; y < at.Y + item.H; y++)
        {
            Count(item, new Cell(at.X - 1, y), ref touch);
            Count(item, new Cell(at.X + item.W, y), ref touch);
        }

        return touch;
    }

    private void Count(PackItem item, Cell neighbour, ref (int Sides, int SameKind) touch)
    {
        if (_taken.IsFree(neighbour, 1, 1)) return;

        touch.Sides++;

        if (_grid.Holds(neighbour, 1, 1) && _owners[neighbour.X, neighbour.Y]?.ConfigId == item.ConfigId) touch.SameKind++;
    }
}
