using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// Notices a drone settling its return trip and only records it. The game hands the cargo over inside
/// StrangerTradeActiveManager.SettleDroneReturn (RVA 0x2fc2310), so the hook reads the landing bags just before and
/// DeliveryPutAway does the moving on later frames. The game also settles trips that bring nothing, so a landing is
/// recorded only when Delivery.HandsOverCargo says cargo is coming. Static because a Harmony patch has nowhere else to
/// look.
/// </summary>
internal static class DeliveryHooks
{
    private static readonly object Gate = new();
    private static readonly Queue<Landing> Landings = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;
    private static bool _isOn;
    private static bool _isHooked;

    /// <summary>
    /// Called on the main thread at start and after each change. The hook goes in the first time the put-away is turned
    /// on; turned off, it records no more landings, and one already recorded is still put away.
    /// </summary>
    public static void Use(Harmony harmony, bool isOn, Action<string> log)
    {
        _log = log;
        _isOn = isOn;

        if (!isOn || _isHooked) return;

        _isHooked = true;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(StrangerTradeActiveManager), nameof(StrangerTradeActiveManager.SettleDroneReturn)), prefix: new HarmonyMethod(typeof(DeliveryHooks), nameof(BeforeSettle)));
            _log("drone deliveries are put away");
        }
        catch (Exception ex)
        {
            _log($"could not hook {nameof(StrangerTradeActiveManager)}.{nameof(StrangerTradeActiveManager.SettleDroneReturn)}, drone deliveries stay where they land: {ex.Message}");
        }
    }

    /// <summary>The oldest landing not yet handled, if any.</summary>
    public static Landing? TakeNext()
    {
        lock (Gate) return Landings.Count == 0 ? null : Landings.Dequeue();
    }

    /// <summary>Reads only, and an exception never escapes into the game's settle: a failing hook logs once and records nothing.</summary>
    private static void BeforeSettle(StrangerTradeActiveManager __instance, Furniture drone, DroneTripComponent trip)
    {
        if (!_isOn) return;

        try
        {
            var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

            if (__instance == null || drone == null || trip == null || items == null) return;
            if (!Delivery.HandsOverCargo(new TripAtSettle(__instance.IsLocked, trip.IsBusy(), trip.IsCharity, !string.IsNullOrEmpty(trip.CargoJson)))) return;

            var hub = StrangerTradeActiveManager.GetDockHub(drone);
            var landing = new Landing(drone.InstanceId, hub?.InstanceId, trip.TripType, Array.Empty<ItemStack>());
            landing = landing with { Before = landing.Owners.SelectMany(owner => GameStorages.ReadHeld(items, owner)).ToList() };

            lock (Gate) Landings.Enqueue(landing);
        }
        catch (Exception ex)
        {
            if (_hasReportedHookFailure) return;

            _hasReportedHookFailure = true;
            _log("the drone landing hook failed (only the first failure is logged): " + ex);
        }
    }
}
