using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>An item in a bag and the cell it sits on now.</summary>
public sealed record BagItem(PackItem Item, Cell At);

public enum PackStyle
{
    Compact,
    GameOrder,
}

public abstract record OrganizePlan
{
    /// <summary>Write these positions. Every item in the bag has one, and together they pass Packing.IsValid.</summary>
    public sealed record Move(IReadOnlyDictionary<long, Cell> Positions, PackStyle Style) : OrganizePlan;

    /// <summary>The bag already matches the layout, so nothing is written and nothing is redrawn.</summary>
    public sealed record AlreadyTidy : OrganizePlan;

    /// <summary>No layout holds every item, so the bag is left exactly as it is.</summary>
    public sealed record NoRoom : OrganizePlan;
}

public static class Organizer
{
    /// <summary>
    /// The compact layout when it is on and holds everything, otherwise the game's own Sort layout, otherwise nothing.
    /// A bag that already matches the chosen layout is not touched, so opening a tidy storage costs no writes. No layout
    /// uses a blocked cell, so an item sitting on mold always counts as out of place.
    /// </summary>
    public static OrganizePlan Plan(BagSpace space, IReadOnlyList<BagItem> bag, PackOptions options)
    {
        var items = bag.Select(placed => placed.Item).ToList();

        foreach (var style in StylesToTry(options))
        {
            if (Layout(style, space, items, options) is not PackResult.Fits fits || !Packing.IsValid(space, items, fits.Positions)) continue;

            return bag.All(placed => fits.Positions[placed.Item.Id] == placed.At) ? new OrganizePlan.AlreadyTidy() : new OrganizePlan.Move(fits.Positions, style);
        }

        return new OrganizePlan.NoRoom();
    }

    /// <summary>
    /// Where to write each item to undo a move that went wrong, by item id; an item left out is not written. An item
    /// saved on the grid goes back to its saved cell. An item saved off the grid is never written back there while
    /// there is another choice, since the game repairs such a cell by its own rules and may move or store the item
    /// elsewhere: it stays where it is now unless that is off the grid or in another item's way, and otherwise takes the
    /// first free spot. Only when no spot is free, or where it is now is unknown, does it get its saved cell: that is
    /// where the bag had it before, and the game's repair finds it room.
    /// </summary>
    public static IReadOnlyDictionary<long, Cell> PutBack(BagSpace space, IReadOnlyList<BagItem> saved, IReadOnlyDictionary<long, Cell> now)
    {
        var taken = new Occupancy(space);
        var writes = new Dictionary<long, Cell>();
        var offGrid = new List<BagItem>();

        foreach (var placed in saved)
        {
            if (!space.Grid.Holds(placed.At, placed.Item.W, placed.Item.H))
            {
                offGrid.Add(placed);

                continue;
            }

            taken.Take(placed.At, placed.Item.W, placed.Item.H);

            writes[placed.Item.Id] = placed.At;
        }

        foreach (var placed in offGrid)
        {
            Cell? target = now.TryGetValue(placed.Item.Id, out var current) ? Resettle(space.Grid, taken, placed, current) : placed.At;

            if (target is { } at) writes[placed.Item.Id] = at;
        }

        return writes;
    }

    private static IEnumerable<PackStyle> StylesToTry(PackOptions options) =>
        options.SmallCluster ? new[] { PackStyle.Compact, PackStyle.GameOrder } : new[] { PackStyle.GameOrder };

    private static PackResult Layout(PackStyle style, BagSpace space, IReadOnlyList<PackItem> items, PackOptions options) =>
        style switch
        {
            PackStyle.Compact => Packing.Pack(space, items, options),
            PackStyle.GameOrder => Packing.PackLikeGame(space, items),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "unknown pack style"),
        };

    /// <summary>Where an item saved off the grid has to be written, or null when it can stay where it is now.</summary>
    private static Cell? Resettle(GridSize grid, Occupancy taken, BagItem placed, Cell current)
    {
        var item = placed.Item;

        if (!grid.Holds(current, item.W, item.H)) return null;

        if (taken.IsFree(current, item.W, item.H))
        {
            taken.Take(current, item.W, item.H);

            return null;
        }

        if (taken.FirstFit(item.W, item.H, grid.Cols, grid.Rows) is not { } free) return placed.At;

        taken.Take(free, item.W, item.H);

        return free;
    }
}
