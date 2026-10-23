using System;
using System.Reflection;

namespace StorageHub.Adapters;

internal enum UnitMove
{
    Refused,
    One,
    WholeStack,
}

/// <summary>
/// Stacking's one-unit move, found by name so Storage Hub runs without Stacking installed. Null means Stacking is
/// absent or skipped the move (a single item, or one it does not split), and the caller moves the stack itself.
/// </summary>
internal static class StackingUnits
{
    private static readonly Lazy<Func<long, long, long, bool?>?> Mover = new(Find);

    /// <summary>Whether part of a stack can be moved at all; without Stacking only whole stacks move.</summary>
    public static bool IsAvailable => Mover.Value != null;

    public static bool? MoveOne(long itemId, long fromOwnerId, long toOwnerId) => Mover.Value?.Invoke(itemId, fromOwnerId, toOwnerId);

    private static Func<long, long, long, bool?>? Find()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var method = assembly.GetType("Stacking.UnitMoves", false)?.GetMethod("MoveOne", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(long), typeof(long), typeof(long) }, null);

            if (method != null && method.ReturnType == typeof(bool?)) return (Func<long, long, long, bool?>)Delegate.CreateDelegate(typeof(Func<long, long, long, bool?>), method);
        }

        return null;
    }
}
