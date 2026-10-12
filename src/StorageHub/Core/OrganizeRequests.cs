namespace StorageHub.Core;

/// <summary>What the game's bag screen shows: the left side (A), and for a storage the right side (B), with the grid size each was drawn at.</summary>
public sealed record BagScreen(bool IsDual, long OwnerA, GridSize GridA, long OwnerB, GridSize GridB);

public enum OrganizeReason
{
    Opened,
    Sorted,
}

/// <summary>
/// One bag to tidy. PartnerId is the bag shown beside it, and both are redrawn afterwards. ShownGrid is the size the
/// screen drew the bag at, so a bag whose drawn grid differs from its real one can be left alone.
/// </summary>
public sealed record OrganizeRequest(long OwnerId, long PartnerId, GridSize ShownGrid, OrganizeReason Reason);

/// <summary>
/// Turns what the bag screen did into at most one tidy, handed out a frame later, because the hooks that report it
/// run inside the game's UI update and must not change items there. A storage is tidied once per opening: if the
/// game builds the screen again while it is open, a second tidy would undo whatever the player has moved by hand.
/// </summary>
public sealed class OrganizeRequests
{
    /// <summary>The side name the game's Sort button sends for the storage; anything else sorts the left side.</summary>
    private const string StorageSide = "B";

    private OrganizeRequest? _pending;
    private bool _isPendingDue;
    private long? _tidiedSinceOpen;

    /// <summary>The screen was built. Only a storage is tidied on open, never the player's own bag.</summary>
    public void Opened(BagScreen screen)
    {
        if (!screen.IsDual || screen.OwnerB == 0 || screen.OwnerB == _tidiedSinceOpen) return;

        _tidiedSinceOpen = screen.OwnerB;
        Ask(StorageRequest(screen, OrganizeReason.Opened));
    }

    /// <summary>The game's Sort button was pressed and the game has already sorted that side its own way.</summary>
    public void Sorted(string? side, BagScreen screen)
    {
        if (side == StorageSide)
        {
            // The game sorts nothing when there is no storage side, so neither does this.
            if (screen.IsDual && screen.OwnerB != 0) Ask(StorageRequest(screen, OrganizeReason.Sorted));

            return;
        }

        if (screen.OwnerA == 0) return;

        var partner = screen.IsDual && screen.OwnerB != 0 ? screen.OwnerB : screen.OwnerA;

        Ask(new OrganizeRequest(screen.OwnerA, partner, screen.GridA, OrganizeReason.Sorted));
    }

    public void Closed()
    {
        _pending = null;
        _tidiedSinceOpen = null;
    }

    /// <summary>
    /// Called once a frame. A request is handed out, once, by the call after the one that first saw it, so a whole
    /// frame has passed since the screen asked for it.
    /// </summary>
    public OrganizeRequest? TakeDue()
    {
        if (_pending == null) return null;

        if (!_isPendingDue)
        {
            _isPendingDue = true;

            return null;
        }

        var request = _pending;
        _pending = null;

        return request;
    }

    private void Ask(OrganizeRequest request)
    {
        _pending = request;
        _isPendingDue = false;
    }

    private static OrganizeRequest StorageRequest(BagScreen screen, OrganizeReason reason) =>
        new(screen.OwnerB, screen.OwnerA != 0 ? screen.OwnerA : screen.OwnerB, screen.GridB, reason);
}
