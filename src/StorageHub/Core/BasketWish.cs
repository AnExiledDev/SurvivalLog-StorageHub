using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// What the neighbor's basket screen says she wants, as the game's own wish strip shows it: today's item categories,
/// a crisis category, and food alone while she is dying. Nothing is wished while there is no rescue stage, while her
/// wish is not shown yet, or while the basket is away, since the strip then shows no categories either.
/// </summary>
public sealed record BasketWish(bool IsActive, bool IsPending, bool IsAway, bool IsDying, IReadOnlyCollection<int> Categories, int? CrisisCategory)
{
    /// <summary>The game's item category for food (Config_Item.Category 1).</summary>
    public const int FoodCategory = 1;

    public static readonly BasketWish None = new(false, false, false, false, new List<int>(), null);

    public bool Wants(int category)
    {
        if (!IsActive || IsPending || IsAway) return false;
        if (IsDying) return category == FoodCategory;

        return Categories.Contains(category) || CrisisCategory == category;
    }
}
