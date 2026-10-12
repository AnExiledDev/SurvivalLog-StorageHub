using System;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// The Unity object that hands frames to the delivery put-away. Only the first failure is logged and none is rethrown
/// into the game; one that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class DeliveryTicker : MonoBehaviour
{
    internal static DeliveryPutAway? Deliveries;
    internal static Action<string> Log = _ => { };
    private static bool _hasReportedFailure;

    public DeliveryTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Deliveries?.Update();
        }
        catch (Exception ex)
        {
            if (_hasReportedFailure) return;

            _hasReportedFailure = true;
            Log("delivery update failed (only the first failure is logged): " + ex);
        }
    }
}
