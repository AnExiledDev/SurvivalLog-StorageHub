using System.Collections.Generic;

namespace StorageHub.Core;

/// <summary>
/// Hands back the stack from the last read when nothing about it changed, so an inventory read of an idle home makes
/// no new stack objects. A stack not seen in a read is let go at the end of it. Main thread only.
/// </summary>
public sealed class StackReuse
{
    private Dictionary<long, ItemStack> _last = new();
    private Dictionary<long, ItemStack> _next = new();

    /// <summary>How many stacks the last finished read had, to size the next read's list.</summary>
    public int LastCount => _last.Count;

    /// <summary>Call before a read; a read that threw part way is thrown away here.</summary>
    public void Begin() => _next.Clear();

    public ItemStack Get(long itemId, int configId, int count, long ownerId)
    {
        if (!_last.TryGetValue(itemId, out var stack) || stack.ConfigId != configId || stack.Count != count || stack.OwnerId != ownerId)
        {
            stack = new ItemStack(itemId, configId, count, ownerId);
        }

        _next[itemId] = stack;

        return stack;
    }

    /// <summary>Call after a read that finished: its stacks are the ones the next read may hand back.</summary>
    public void End()
    {
        (_last, _next) = (_next, _last);
        _next.Clear();
    }
}
