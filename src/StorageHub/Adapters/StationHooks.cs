using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using StorageHub.Core;
using Il2CppFridges = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.CookingFridgeInfo>;

namespace StorageHub.Adapters;

/// <summary>
/// Adds a bag tab to the stove and the rat cage for every home storage holding something the station takes. The game
/// builds those tabs from the fridge list its opening action carries (Reducer_Data_Item.RA_GetCookingBagList, RVA
/// 0x285eb10, and RA_GetRatCageBagList, RVA 0x28601b0; both start with plain stores, so they are safe to hook), so
/// the storages are appended to that list before the reducer reads it. The game's own fridge tabs, locks included,
/// are left as they are. Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class StationHooks
{
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;
        Patch(harmony, "RA_GetCookingBagList", nameof(BeforeCooking), "the stove");
        Patch(harmony, "RA_GetRatCageBagList", nameof(BeforeRatCage), "the rat cage");
    }

    private static void Patch(Harmony harmony, string method, string prefix, string station)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(Reducer_Data_Item), method), prefix: new HarmonyMethod(typeof(StationHooks), prefix));
            _log($"{station} gets a bag tab for each home storage holding something it takes");
        }
        catch (Exception ex)
        {
            _log($"could not hook {nameof(Reducer_Data_Item)}.{method}, {station} keeps only the game's own bag tabs: {ex.Message}");
        }
    }

    private static void BeforeCooking(Ac_Item_GetCookingBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithStorageTabs(ac.Fridges, StationKind.Cooking, state, ac.OwnerId, ac.TargetId);
        });

    private static void BeforeRatCage(Ac_Item_GetRatCageBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithStorageTabs(ac.Fridges, StationKind.RatCage, state, ac.OwnerId, ac.TargetId);
        });

    /// <summary>The game's fridge list with a tab added for each other home storage the station has a use for.</summary>
    private static Il2CppFridges? WithStorageTabs(Il2CppFridges? fridges, StationKind kind, State_Data_Item state, long ownerId, long targetId)
    {
        var cache = state?.OwnerCache;
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (state == null || cache == null || agents == null || config == null) return fridges;

        var tabs = fridges ?? new Il2CppFridges();
        var shown = new HashSet<long> { ownerId, targetId };
        var added = 0;

        for (var i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null) shown.Add(tabs[i].OwnerId);
        }

        foreach (var furniture in GameStorages.HomeFurnitures(agents))
        {
            // A drone's bag holds goods it is about to trade away.
            if (shown.Contains(furniture.InstanceId) || StrangerTradeActiveManager.TripOf(furniture) != null) continue;

            if (!HoldsUsable(cache, furniture.InstanceId, kind, state, config)) continue;

            tabs.Add(new CookingFridgeInfo { OwnerId = furniture.InstanceId, FurnitureConfigId = furniture.AgentConfigId, Locked = false });
            shown.Add(furniture.InstanceId);
            added++;
        }

        return added == 0 ? fridges : tabs;
    }

    private static bool HoldsUsable(Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, Data_Item>> cache, long ownerId, StationKind kind, State_Data_Item state, ConfigManager config)
    {
        if (!cache.TryGetValue(ownerId, out var held) || held == null) return false;

        foreach (var item in held.Values)
        {
            // The cache keeps stale entries whose OwnerId points elsewhere; they are not in this storage.
            if (item != null && item.OwnerId == ownerId && item.ItemCount > 0 && StationRules.Takes(kind, item, state, config)) return true;
        }

        return false;
    }

    /// <summary>An exception must never escape into the game's reducer, so a failing hook logs once and leaves the tabs alone.</summary>
    private static void Guard(Action addTabs)
    {
        try
        {
            addTabs();
        }
        catch (Exception ex)
        {
            if (_hasReportedHookFailure) return;

            _hasReportedHookFailure = true;
            _log("adding storage tabs to a station failed, it keeps the game's own tabs (only the first failure is logged): " + ex);
        }
    }
}
