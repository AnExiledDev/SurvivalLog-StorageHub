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

    /// <summary>
    /// Every layout that holds all the items, in a fixed order, so the same bag always gives the same layouts. Orders
    /// that come out the same are tried once, and the big items are placed once per big-item order and shared by all
    /// the small-item layouts built on it.
    /// </summary>
    public static IEnumerable<IReadOnlyDictionary<long, Cell>> Layouts(BagSpace space, IReadOnlyList<PackItem> items, PackOptions options)
    {
        var big = items.Where(item => item.Area > options.SmallMaxArea).ToList();
        var small = items.Where(item => item.Area <= options.SmallMaxArea).ToList();
        var smallOrders = DistinctOrders(SmallOrders(small));

        // With no small items to place, every lean gives the same layout.
        var leans = small.Count == 0 ? new[] { SmallLeans[0] } : SmallLeans;

        foreach (var bigOrder in DistinctOrders(BigOrders(big)))
        {
            if (Placed(new ContactGrid(space), bigOrder, Lean.BottomRight) is not { } withBig) continue;

            foreach (var smallOrder in smallOrders)
            {
                foreach (var lean in leans)
                {
                    if (Placed(withBig.Copy(), smallOrder, lean) is { } bag) yield return bag.Positions;
                }
            }
        }
    }

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

    /// <summary>Each order once, the first time it comes up, since the same order always gives the same layout.</summary>
    private static List<List<PackItem>> DistinctOrders(IEnumerable<List<PackItem>> orders)
    {
        var distinct = new List<List<PackItem>>();

        foreach (var order in orders)
        {
            if (!distinct.Any(seen => seen.SequenceEqual(order))) distinct.Add(order);
        }

        return distinct;
    }

    /// <summary>The bag with every item of the order placed in turn, or null when one finds no spot.</summary>
    private static ContactGrid? Placed(ContactGrid bag, List<PackItem> order, Lean lean)
    {
        foreach (var item in order)
        {
            if (!bag.TryPlace(item, lean)) return null;
        }

        return bag;
    }
}

/// <summary>A bag being filled by contact: which cells are taken, and which item sits on each.</summary>
internal sealed class ContactGrid
{
    private readonly int _cols;
    private readonly int _rows;
    private readonly bool[,] _taken;
    private readonly PackItem?[,] _owners;
    private readonly Dictionary<long, Cell> _positions;

    public ContactGrid(BagSpace space) : this(Math.Max(0, space.Grid.Cols), Math.Max(0, space.Grid.Rows))
    {
        foreach (var cell in space.Blocked)
        {
            if (space.Grid.Holds(cell, 1, 1)) _taken[cell.X, cell.Y] = true;
        }
    }

    private ContactGrid(int cols, int rows) : this(new bool[cols, rows], new PackItem?[cols, rows], new Dictionary<long, Cell>())
    {
    }

    private ContactGrid(bool[,] taken, PackItem?[,] owners, Dictionary<long, Cell> positions)
    {
        _cols = taken.GetLength(0);
        _rows = taken.GetLength(1);
        _taken = taken;
        _owners = owners;
        _positions = positions;
    }

    public IReadOnlyDictionary<long, Cell> Positions => _positions;

    /// <summary>A separate bag holding the same items in the same places, to be filled further on its own.</summary>
    public ContactGrid Copy() => new((bool[,])_taken.Clone(), (PackItem?[,])_owners.Clone(), new Dictionary<long, Cell>(_positions));

    /// <summary>Puts the item on its best free spot; false when it has none.</summary>
    public bool TryPlace(PackItem item, Lean lean)
    {
        if (BestSpot(item, lean) is not { } at) return false;

        for (var x = at.X; x < at.X + item.W; x++)
        {
            for (var y = at.Y; y < at.Y + item.H; y++)
            {
                _taken[x, y] = true;
                _owners[x, y] = item;
            }
        }

        _positions[item.Id] = at;

        return true;
    }

    /// <summary>
    /// The free spot touching the most, then touching the most of the same kind, then first in the lean's order:
    /// the lowest row then the rightmost column for BottomRight, the top row then the leftmost column for TopLeftByRow,
    /// and the leftmost column then the top row for TopLeftByColumn.
    /// </summary>
    private Cell? BestSpot(PackItem item, Lean lean)
    {
        var lastX = _cols - item.W;
        var lastY = _rows - item.H;
        Cell? best = null;
        var bestContact = new Contact(-1, -1);

        switch (lean)
        {
            case Lean.BottomRight:
                for (var y = lastY; y >= 0; y--)
                {
                    for (var x = lastX; x >= 0; x--) Consider(x, y);
                }

                break;
            case Lean.TopLeftByRow:
                for (var y = 0; y <= lastY; y++)
                {
                    for (var x = 0; x <= lastX; x++) Consider(x, y);
                }

                break;
            case Lean.TopLeftByColumn:
                for (var x = 0; x <= lastX; x++)
                {
                    for (var y = 0; y <= lastY; y++) Consider(x, y);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "unknown lean");
        }

        return best;

        void Consider(int x, int y)
        {
            if (!IsFree(x, y, item.W, item.H)) return;

            var contact = ContactAt(item, x, y);

            if (!contact.Beats(bestContact)) return;

            best = new Cell(x, y);
            bestContact = contact;
        }
    }

    /// <summary>True when no cell under a w by h item at this cell is taken. The caller keeps the item inside the bag.</summary>
    private bool IsFree(int left, int top, int w, int h)
    {
        for (var x = left; x < left + w; x++)
        {
            for (var y = top; y < top + h; y++)
            {
                if (_taken[x, y]) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// How much of the item's outline touches something when it sits at this cell: Sides counts the cells just outside
    /// its edges that are off the grid, mold or taken, and SameKind those taken by an item of the same kind.
    /// </summary>
    private Contact ContactAt(PackItem item, int left, int top)
    {
        var sides = 0;
        var sameKind = 0;

        for (var x = left; x < left + item.W; x++)
        {
            Count(x, top - 1);
            Count(x, top + item.H);
        }

        for (var y = top; y < top + item.H; y++)
        {
            Count(left - 1, y);
            Count(left + item.W, y);
        }

        return new Contact(sides, sameKind);

        void Count(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _cols || y >= _rows)
            {
                sides++;

                return;
            }

            if (!_taken[x, y]) return;

            sides++;

            if (_owners[x, y]?.ConfigId == item.ConfigId) sameKind++;
        }
    }

    /// <summary>How a spot touches what is around it. More sides wins, then more of the same kind.</summary>
    private readonly record struct Contact(int Sides, int SameKind)
    {
        public bool Beats(Contact other) => Sides > other.Sides || (Sides == other.Sides && SameKind > other.SameKind);
    }
}
