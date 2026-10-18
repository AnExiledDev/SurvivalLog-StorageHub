using System;
using System.Globalization;

namespace StorageHub.Core;

/// <summary>
/// What the web panel asks for. The page sends "slmod|storagehub|" followed by a verb and its arguments, split by '|';
/// anything else, or a verb with bad arguments, parses to Unknown so it can be logged rather than acted on.
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|storagehub|";

    private PageCommand()
    {
    }

    /// <summary>Move every stack of one item that sits in one storage into the bag.</summary>
    public sealed record Take(int ConfigId, long OwnerId) : PageCommand;

    /// <summary>Move one unit of an item from one storage into the bag.</summary>
    public sealed record TakeOne(int ConfigId, long OwnerId) : PageCommand;

    /// <summary>Move every stack of one item from every storage into the bag.</summary>
    public sealed record TakeAll(int ConfigId) : PageCommand;

    /// <summary>Put the bag's stacks of one item into the storages whose tags want it.</summary>
    public sealed record PutAway(int ConfigId) : PageCommand;

    /// <summary>Put everything in the bag into the storages whose tags want it.</summary>
    public sealed record PutAwayBag : PageCommand;

    /// <summary>Put every item lying on the floor at home into the storages whose tags want it.</summary>
    public sealed record PutAwayFloor : PageCommand;

    /// <summary>Add an item to the keep list, or change how many of it putting away leaves in the bag.</summary>
    public sealed record SetKeep(int ConfigId, int Amount) : PageCommand;

    /// <summary>Take an item off the keep list.</summary>
    public sealed record RemoveKeep(int ConfigId) : PageCommand;

    /// <summary>The keep list's search wants every item in the game, not just the ones the player owns.</summary>
    public sealed record SendCatalog : PageCommand;

    /// <summary>The home storage list beside a station wants its storages and items.</summary>
    public sealed record StationList(StationKind Kind) : PageCommand;

    /// <summary>Open one home storage beside the bag on the inventory screen; -1 closes it again.</summary>
    public sealed record OpenStorage(long OwnerId) : PageCommand;

    public sealed record Close : PageCommand;

    /// <summary>The search box gained or lost focus, so the game should send keys to the page or stop.</summary>
    public sealed record Focus(bool IsFocused) : PageCommand;

    /// <summary>The inventory toolbar's Mods menu opened or closed; the page gets no keys, so Esc has to close it from here.</summary>
    public sealed record Menu(bool IsOpen) : PageCommand;

    /// <summary>The page script just installed itself.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        return (parts[0], parts.Length) switch
        {
            ("take", 3) when TryInt(parts[1], out var configId) && TryLong(parts[2], out var ownerId) => new Take(configId, ownerId),
            ("takeOne", 3) when TryInt(parts[1], out var configId) && TryLong(parts[2], out var ownerId) => new TakeOne(configId, ownerId),
            ("takeAll", 2) when TryInt(parts[1], out var configId) => new TakeAll(configId),
            ("putAway", 2) when TryInt(parts[1], out var configId) => new PutAway(configId),
            ("putAwayBag", 1) => new PutAwayBag(),
            ("putAwayFloor", 1) => new PutAwayFloor(),
            ("keepSet", 3) when TryInt(parts[1], out var configId) && TryInt(parts[2], out var amount) => new SetKeep(configId, amount),
            ("keepRemove", 2) when TryInt(parts[1], out var configId) => new RemoveKeep(configId),
            ("catalog", 1) => new SendCatalog(),
            ("station", 2) when StationView.Parse(parts[1]) is { } kind => new StationList(kind),
            ("openStorage", 2) when TryLong(parts[1], out var ownerId) => new OpenStorage(ownerId),
            ("close", 1) => new Close(),
            ("focus", 2) when parts[1] is "0" or "1" => new Focus(parts[1] == "1"),
            ("menu", 2) when parts[1] is "0" or "1" => new Menu(parts[1] == "1"),
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            _ => new Unknown(message),
        };
    }

    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool TryLong(string text, out long value) => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}
