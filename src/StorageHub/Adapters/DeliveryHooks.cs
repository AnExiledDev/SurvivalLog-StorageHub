using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>A drone that is about to hand over its cargo: where the cargo can land, and what was there before it did.</summary>
internal sealed record Landing(long DroneId, long? HubId, int TripType, IReadOnlyList<ItemStack> Before)
{
    /// <summary>The bags the cargo can land in: the dock hub when it has room, otherwise the drone's own bag.</summary>
    public IReadOnlyList<long> Owners => HubId is { } hub ? new[] { hub, DroneId } : new[] { DroneId };
}

/// <summary>
/// Notices a drone settling its return trip and only records it. The game hands the cargo over inside
/// StrangerTradeActiveManager.SettleDroneReturn (RVA 0x2fc2310), so the hook reads the landing bags just before and
/// DeliveryPutAway does the moving on later frames. Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class DeliveryHooks
{
    private static readonly object Gate = new();
    private static readonly Queue<Landing> Landings = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

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
    private static void BeforeSettle(Furniture drone, DroneTripComponent trip)
    {
        try
        {
            var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

            if (drone == null || items == null) return;

            var hub = StrangerTradeActiveManager.GetDockHub(drone);
            var landing = new Landing(drone.InstanceId, hub?.InstanceId, trip?.TripType ?? -1, Array.Empty<ItemStack>());
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
