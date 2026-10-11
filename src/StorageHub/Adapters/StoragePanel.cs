using System;
using System.Collections.Generic;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// The Unity object that hands frames to the session. Each distinct failure is logged once, never rethrown into the
/// game; a failure that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class StoragePanel : MonoBehaviour
{
    private const int MaxReportedFailures = 20;

    internal static HubSession? Session;
    internal static Action<string> Log = _ => { };
    private static readonly HashSet<string> ReportedFailures = new();

    public StoragePanel(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Session?.Update();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void ReportFailure(Exception ex)
    {
        if (ReportedFailures.Count >= MaxReportedFailures || !ReportedFailures.Add(ex.GetType().FullName + ": " + ex.Message)) return;

        Log("update failed (each distinct failure reported once): " + ex);

        if (ReportedFailures.Count == MaxReportedFailures) Log($"{MaxReportedFailures} distinct update failures reported; further ones are not logged");
    }
}
