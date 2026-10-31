using System;
using ExiledToolkit;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>Storage Hub's rows in the Mods menu that Exiled's Toolkit draws, worded like the page.</summary>
internal static class MenuRows
{
    // A magnifier.
    private const string HomeIcon = "M10 3a7 7 0 1 0 4.2 12.6l5.1 5.1 1.4-1.4-5.1-5.1A7 7 0 0 0 10 3zm0 2a5 5 0 1 1 0 10a5 5 0 1 1 0-10z";

    // A tag with its string hole.
    private const string PutAwayIcon = "M3 3h8l10 10-8 8L3 11zM7.5 6a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0-3z";

    // An arrow lifting off the floor.
    private const string FloorIcon = "M3 19h18v2H3zM12 3l6 6h-4v7h-4V9H6z";

    /// <summary>The home storage window. Inventory only: the station screens have the list as a bag tab of their own.</summary>
    public static ModsMenuEntry Home(Action onClick, Func<bool> isShown) =>
        Row("storagehub.home", "homeTitle", "menuHomeTip", HomeIcon, 90, stations: false, onClick, isShown);

    public static ModsMenuEntry PutAwayBag(Action onClick) => Row("storagehub.putaway", "putAwayTag", "menuPutAwayTip", PutAwayIcon, 100, stations: true, onClick);

    public static ModsMenuEntry PutAwayFloor(Action onClick) => Row("storagehub.floor", "menuFloor", "menuFloorTip", FloorIcon, 110, stations: true, onClick);

    private static ModsMenuEntry Row(string id, string labelWord, string titleWord, string icon, int order, bool stations, Action onClick, Func<bool>? isShown = null)
    {
        var label = Texts.PageWord(labelWord);
        var title = Texts.PageWord(titleWord);

        return new ModsMenuEntry(id, label.En, onClick)
        {
            ChineseLabel = label.Zh,
            Title = title.En,
            ChineseTitle = title.Zh,
            Icon = icon,
            Order = order,
            Stations = stations,
            IsShown = isShown,
        };
    }
}
