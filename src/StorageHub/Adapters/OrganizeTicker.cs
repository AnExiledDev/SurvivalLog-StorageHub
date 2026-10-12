using System;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// The Unity object that hands frames to the bag organizer, the frame after the bag screen asked. Only the first
/// failure is logged and none is rethrown into the game; one that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class OrganizeTicker : MonoBehaviour
{
    internal static BagOrganizer? Bags;
    internal static Action<string> Log = _ => { };
    private static bool _hasReportedFailure;

    public OrganizeTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Bags?.Update();
        }
        catch (Exception ex)
        {
            if (_hasReportedFailure) return;

            _hasReportedFailure = true;
            Log("organize update failed (only the first failure is logged): " + ex);
        }
    }
}
