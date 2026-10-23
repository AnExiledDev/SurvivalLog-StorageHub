using StorageHub.Adapters;

namespace StorageHub;

/// <summary>
/// For other mods, found by reflection so they need no reference to this one: puts what the neighbour's basket holds
/// away into the storages at home, the way the bag's put-away does. Call on the main thread.
/// </summary>
public static class BasketApi
{
    /// <summary>The result line in the game's language, or null before Storage Hub has started.</summary>
    public static string? PutAwayBasket() => StoragePanel.Session?.PutAwayBasketNow();
}
