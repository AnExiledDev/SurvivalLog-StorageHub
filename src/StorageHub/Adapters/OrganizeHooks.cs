using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// Notices the bag screen opening, the game's Sort button and the screen closing, and only records them: these run
/// inside the game's UI reducers, where changing items is unsafe, so BagOrganizer does the work on a later frame.
/// Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class OrganizeHooks
{
    private static readonly object Gate = new();
    private static readonly OrganizeRequests Requests = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;

    public static void Apply(Harmony harmony, bool organizeOnOpen, bool compactSort, Action<string> log)
    {
        _log = log;

        if (organizeOnOpen) Patch(harmony, "RA_OpenUI", nameof(AfterOpen), done: "storages are tidied when opened", consequence: "storages will not be tidied when opened");
        if (compactSort) Patch(harmony, "RA_Sort", nameof(AfterSort), done: "the Sort button packs small items into a square", consequence: "the Sort button stays as the game made it");

        // RA_Close is compiled into Trigger_RA_Close, so a patch on RA_Close itself would never run.
        Patch(harmony, "Trigger_RA_Close", nameof(AfterClose), done: "closing the bag screen is noticed", consequence: "a storage is tidied only on its first opening");
        Patch(harmony, "Trigger_RA_ClearUI", nameof(AfterClose), done: "clearing the bag screen is noticed", consequence: "a storage may not be tidied again after the screen is cleared");
    }

    /// <summary>The bag to tidy this frame, if one is due.</summary>
    public static OrganizeRequest? TakeDue()
    {
        lock (Gate) return Requests.TakeDue();
    }

    private static void Patch(Harmony harmony, string method, string postfix, string done, string consequence)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(Reducer_Web_BackpackUI), method), postfix: new HarmonyMethod(typeof(OrganizeHooks), postfix));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {nameof(Reducer_Web_BackpackUI)}.{method}, {consequence}: {ex.Message}");
        }
    }

    private static void AfterOpen(State_Web_BackpackUI __result) =>
        Record(() =>
        {
            if (__result != null) Requests.Opened(ReadScreen(__result));
        });

    private static void AfterSort(Ac_BackpackUI_Sort ac, State_Web_BackpackUI __result) =>
        Record(() =>
        {
            if (__result != null) Requests.Sorted(ac?.Side, ReadScreen(__result));
        });

    private static void AfterClose() => Record(() => Requests.Closed());

    /// <summary>An exception must never escape into the game's reducer, so a failing hook logs once and records nothing.</summary>
    private static void Record(Action record)
    {
        try
        {
            lock (Gate) record();
        }
        catch (Exception ex)
        {
            if (_hasReportedHookFailure) return;

            _hasReportedHookFailure = true;
            _log("a bag screen hook failed (only the first failure is logged): " + ex);
        }
    }

    private static BagScreen ReadScreen(State_Web_BackpackUI state) =>
        new(state.IsDualBag, state.OwnerId_A, new GridSize(state.BagCols_A, state.BagRows_A), state.OwnerId_B, new GridSize(state.BagCols_B, state.BagRows_B));
}
