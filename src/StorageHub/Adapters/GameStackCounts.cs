using System;
using GameCore.HotUpdate.Battle.Logic;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// The item counts of one bag, through the game's own count calls. A stack is looked up in the bag's item list on
/// every read, never kept: the game pools a removed item's object, so a kept reference could still answer with the
/// count it had before it was removed. A stack that has left the bag reads as gone, and a read that throws as
/// unreadable, never as gone.
/// </summary>
internal sealed class GameStackCounts : IStackCounts
{
    private readonly ItemManager _items;
    private readonly long _ownerId;

    public GameStackCounts(ItemManager items, long ownerId)
    {
        _items = items;
        _ownerId = ownerId;
    }

    public StackRead CountOf(long stackId)
    {
        try
        {
            return InBag(stackId) is { } item ? new StackRead.Count(item.ItemCount) : new StackRead.Gone();
        }
        catch (Exception ex)
        {
            return new StackRead.Unreadable(ex.Message);
        }
    }

    public bool TryIncrease(long stackId, int units) => TryWrite(stackId, item => _items.IncreaseItemCount(item, units));

    // DecreaseItemCount's bool says whether the stack emptied, not whether the call worked; the runner reads the count back.
    public bool TryDecrease(long stackId, int units) => TryWrite(stackId, item => _items.DecreaseItemCount(item, units));

    public bool TryRemove(long stackId) => TryWrite(stackId, _ => _items.ForceRemoveItem(stackId));

    private bool TryWrite(long stackId, Action<ItemData> write)
    {
        try
        {
            if (InBag(stackId) is not { } item) return false;

            write(item);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private ItemData? InBag(long stackId)
    {
        var inBag = _items.GetItemDataList(_ownerId);

        if (inBag == null) return null;

        for (var i = 0; i < inBag.Count; i++)
        {
            var item = inBag[i];

            if (item != null && item.InstanceId == stackId) return item;
        }

        return null;
    }
}
