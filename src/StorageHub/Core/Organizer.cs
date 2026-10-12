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
    /// A bag that already matches the chosen layout is not touched, so opening a tidy storage costs no writes.
    /// </summary>
    public static OrganizePlan Plan(GridSize grid, IReadOnlyList<BagItem> bag, PackOptions options)
    {
        var items = bag.Select(placed => placed.Item).ToList();

        foreach (var style in StylesToTry(options))
        {
            if (Layout(style, grid, items, options) is not PackResult.Fits fits || !Packing.IsValid(grid, items, fits.Positions)) continue;

            return bag.All(placed => fits.Positions[placed.Item.Id] == placed.At) ? new OrganizePlan.AlreadyTidy() : new OrganizePlan.Move(fits.Positions, style);
        }

        return new OrganizePlan.NoRoom();
    }

    private static IEnumerable<PackStyle> StylesToTry(PackOptions options) =>
        options.SmallCluster ? new[] { PackStyle.Compact, PackStyle.GameOrder } : new[] { PackStyle.GameOrder };

    private static PackResult Layout(PackStyle style, GridSize grid, IReadOnlyList<PackItem> items, PackOptions options) =>
        style switch
        {
            PackStyle.Compact => Packing.Pack(grid, items, options),
            PackStyle.GameOrder => Packing.PackLikeGame(grid, items),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "unknown pack style"),
        };
}
