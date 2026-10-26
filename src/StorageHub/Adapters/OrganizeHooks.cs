using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// Notices the bag screen opening, the game's Sort button there and in the trade window, and the screen closing, and only records them: these run
/// inside the game's UI reducers, where changing items is unsafe, so BagOrganizer does the work on a later frame.
/// Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class OrganizeHooks
{
    private static readonly object Gate = new();
    private static readonly OrganizeRequests Requests = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;
    private static bool _isOrganizeOnOpen;
    private static bool _isCompactSort;
    private static bool _isOpenHooked;
    private static bool _isSortHooked;
    private static bool _isCloseHooked;

    /// <summary>
    /// Called on the main thread at start and after each change. Each hook goes in the first time its setting is turned
    /// on and stays in; turned off, it records nothing, so the game's opening and its Sort button work as they always did.
    /// </summary>
    public static void Use(Harmony harmony, bool organizeOnOpen, bool compactSort, Action<string> log)
    {
        _log = log;
        _isOrganizeOnOpen = organizeOnOpen;
        _isCompactSort = compactSort;

        if (organizeOnOpen && !_isOpenHooked)
        {
            _isOpenHooked = true;

            Patch(harmony, "RA_OpenUI", nameof(AfterOpen), done: "storages are tidied when opened", consequence: "storages will not be tidied when opened");
        }

        if (compactSort && !_isSortHooked)
        {
            _isSortHooked = true;

            Patch(harmony, "RA_Sort", nameof(AfterSort), done: "the Sort button packs small items into a square", consequence: "the Sort button stays as the game made it");
            Patch(harmony, typeof(Reducer_Web_TradeUI), "RA_Sort", nameof(AfterTradeSort), done: "the trade window's Sort button tidies the bag too", consequence: "the trade window's Sort button stays as the game made it");
        }

        if ((!organizeOnOpen && !compactSort) || _isCloseHooked) return;

        _isCloseHooked = true;

        // RA_Close is compiled into Trigger_RA_Close, so a patch on RA_Close itself would never run.
        Patch(harmony, "Trigger_RA_Close", nameof(AfterClose), done: "closing the bag screen is noticed", consequence: "a storage is tidied only on its first opening");
        Patch(harmony, "Trigger_RA_ClearUI", nameof(AfterClose), done: "clearing the bag screen is noticed", consequence: "a storage may not be tidied again after the screen is cleared");
    }

    /// <summary>The bag to tidy this frame, if one is due.</summary>
    public static OrganizeRequest? TakeDue()
    {
        lock (Gate) return Requests.TakeDue();
    }

    private static void Patch(Harmony harmony, string method, string postfix, string done, string consequence) =>
        Patch(harmony, typeof(Reducer_Web_BackpackUI), method, postfix, done, consequence);

    private static void Patch(Harmony harmony, Type reducer, string method, string postfix, string done, string consequence)
    {
        try
        {
            harmony.Patch(AccessTools.Method(reducer, method), postfix: new HarmonyMethod(typeof(OrganizeHooks), postfix));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {reducer.Name}.{method}, {consequence}: {ex.Message}");
        }
    }

    // Each hook body is a plain try with no lambda, so a hook whose setting is off returns before anything is allocated.
    private static void AfterOpen(State_Web_BackpackUI __result)
    {
        if (!_isOrganizeOnOpen || __result == null) return;

        try
        {
            var screen = ReadScreen(__result);

            lock (Gate) Requests.Opened(screen);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void AfterSort(Ac_BackpackUI_Sort ac, State_Web_BackpackUI __result)
    {
        if (!_isCompactSort || __result == null) return;

        try
        {
            var screen = ReadScreen(__result);

            lock (Gate) Requests.Sorted(ac?.Side, screen);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    /// <summary>The trade window shows one bag tab at a time; an offer list that cannot be read leaves it as the game sorted it.</summary>
    private static void AfterTradeSort(State_Web_TradeUI __result)
    {
        if (!_isCompactSort || __result == null) return;

        try
        {
            if (OfferedStacks.Read(__result.GiveJson) is not { } onOffer)
            {
                _log("the trade window's offer list could not be read, so its Sort button only sorts the game's way");

                return;
            }

            var bag = new GridSize(__result.BagW, __result.BagH);

            lock (Gate) Requests.TradeSorted(__result.BagOwnerId, bag, onOffer);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void AfterClose()
    {
        try
        {
            lock (Gate) Requests.Closed();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    /// <summary>An exception must never escape into the game's reducer, so a failing hook logs once and records nothing.</summary>
    private static void ReportFailure(Exception ex)
    {
        if (_hasReportedHookFailure) return;

        _hasReportedHookFailure = true;
        _log("a bag screen hook failed (only the first failure is logged): " + ex);
    }

    private static BagScreen ReadScreen(State_Web_BackpackUI state) =>
        new(state.IsDualBag, state.OwnerId_A, new GridSize(state.BagCols_A, state.BagRows_A), state.OwnerId_B, new GridSize(state.BagCols_B, state.BagRows_B));
}
