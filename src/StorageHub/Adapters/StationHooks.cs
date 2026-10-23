using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using StorageHub.Core;
using Il2CppCabinets = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.ToolCabinetInfo>;
using Il2CppFridges = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.CookingFridgeInfo>;
using Il2CppTradeContainers = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.TradeContainerInfo>;

namespace StorageHub.Adapters;

/// <summary>
/// Adds a bag tab to a station for every home storage holding something the station takes, next to the game's own
/// tabs, whose locks are left as they are. The game builds a station's tabs from the storage list its opening action
/// carries, so the storages are appended to that list before the reducer reads it: the fridge list at the stove
/// (Reducer_Data_Item.RA_GetCookingBagList, RVA 0x29F54B0), the rat cage (RA_GetRatCageBagList, 0x29F6B50), the brewing
/// barrel (RA_GetBrewBagList, 0x29F4870) and the dye vat (RA_GetDyeBagList, 0x29F5E20), and the tool cabinet list at
/// the shredder (RA_GetShredderBagList, 0x29F6F40) and the ration press (RA_GetCompressorBagList, 0x29F4C50). The drone
/// trade asks ItemManager.CollectTradeContainers (RVA 0x26C3D80) for its tabs, so that list gets them on the way out,
/// which also lets an aid drop take items from those storages. The workbench's reducer has no body of its own to hook,
/// so its tool cabinet list is extended in Ac_Item_GetHandMadeBagList.SendAction (RVA 0x297E8B0) instead; the game fills
/// a recipe from every tab it lists, so a home storage tab there also feeds crafting. Every target starts with plain
/// stores, so all are safe to hook. Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class StationHooks
{
    private static Action<string> _log = _ => { };
    private static bool _hasReportedHookFailure;
    private static bool _isOn;
    private static bool _isHooked;

    /// <summary>
    /// Called on the main thread at start and after each change. The hooks go in the first time the tabs are turned on;
    /// turned off, they leave every station's list as the game made it from the next opening on.
    /// </summary>
    public static void Use(Harmony harmony, bool isOn, Action<string> log)
    {
        _log = log;
        _isOn = isOn;

        if (!isOn || _isHooked) return;

        _isHooked = true;

        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetCookingBagList", nameof(BeforeCooking), null, "the stove");
        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetRatCageBagList", nameof(BeforeRatCage), null, "the rat cage");
        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetBrewBagList", nameof(BeforeBrew), null, "the brewing barrel");
        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetDyeBagList", nameof(BeforeDye), null, "the dye vat");
        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetShredderBagList", nameof(BeforeShredder), null, "the shredder");
        Patch(harmony, typeof(Reducer_Data_Item), "RA_GetCompressorBagList", nameof(BeforeCompressor), null, "the ration press");
        Patch(harmony, typeof(ItemManager), nameof(ItemManager.CollectTradeContainers), null, nameof(AfterTradeContainers), "the drone trade");
        Patch(harmony, typeof(Ac_Item_GetHandMadeBagList), nameof(Ac_Item_GetHandMadeBagList.SendAction), nameof(BeforeWorkbench), null, "the workbench");
        Patch(harmony, typeof(Reducer_Web_BackpackUI), "BuildATabs", null, nameof(AfterBuildATabs), "the looting screen");
    }

    private static void Patch(Harmony harmony, Type type, string method, string? prefix, string? postfix, string station)
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method),
                prefix: prefix == null ? null : new HarmonyMethod(typeof(StationHooks), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(StationHooks), postfix));
            _log($"{station} gets a bag tab for each home storage holding something it takes");
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, {station} keeps only the game's own bag tabs: {ex.Message}");
        }
    }

    private static void BeforeCooking(Ac_Item_GetCookingBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithFridgeTabs(ac.Fridges, StationKind.Cooking, state, ac.OwnerId, ac.TargetId);
        });

    private static void BeforeRatCage(Ac_Item_GetRatCageBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithFridgeTabs(ac.Fridges, StationKind.RatCage, state, ac.OwnerId, ac.TargetId);
        });

    private static void BeforeBrew(Ac_Item_GetBrewBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithFridgeTabs(ac.Fridges, StationKind.Brew, state, ac.OwnerId, ac.TargetId);
        });

    private static void BeforeDye(Ac_Item_GetDyeBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac != null) ac.Fridges = WithFridgeTabs(ac.Fridges, StationKind.Dye, state, ac.OwnerId, ac.TargetId);
        });

    private static void BeforeShredder(Ac_Item_GetShredderBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac == null) return;

            var shown = new HashSet<long> { ac.OwnerId, ac.TargetId, ac.TrayOwnerId };

            AddOwners(shown, ac.Cabinets);
            ac.Cabinets = WithCabinetTabs(ac.Cabinets, StoragesToAdd(StationKind.Shredder, state, shown));
        });

    // The press lists fridges as well; a storage goes in with the cabinets, as a fridge tab carries the freezer's rules.
    private static void BeforeCompressor(Ac_Item_GetCompressorBagList ac, State_Data_Item state) =>
        Guard(() =>
        {
            if (ac == null) return;

            var shown = new HashSet<long> { ac.OwnerId, ac.TargetId, ac.TrayOwnerId };

            AddOwners(shown, ac.Cabinets);
            AddOwners(shown, ac.Fridges);
            ac.Cabinets = WithCabinetTabs(ac.Cabinets, StoragesToAdd(StationKind.Compressor, state, shown));
        });

    // Parameters by position: OwnerId, TargetId and Cabinets of the static SendAction.
    private static void BeforeWorkbench(long __0, long __1, ref Il2CppCabinets __5)
    {
        var cabinets = __5;

        Guard(() =>
        {
            var state = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>());

            if (state == null) return;

            var shown = new HashSet<long> { __0, __1 };

            AddOwners(shown, cabinets);
            cabinets = WithCabinetTabs(cabinets, StoragesToAdd(StationKind.Workbench, state, shown));
        });

        __5 = cabinets;
    }

    private static void AfterTradeContainers(long droneOwnerId, ref Il2CppTradeContainers __result)
    {
        var containers = __result;

        Guard(() =>
        {
            var state = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>());
            var shown = new HashSet<long> { droneOwnerId };

            if (containers != null)
            {
                for (var i = 0; i < containers.Count; i++)
                {
                    if (containers[i] != null) shown.Add(containers[i].OwnerId);
                }
            }

            var storages = state == null ? new List<HomeStorage>() : StoragesToAdd(StationKind.Trade, state, shown);

            if (storages.Count == 0) return;

            containers ??= new Il2CppTradeContainers();

            foreach (var storage in storages) containers.Add(new TradeContainerInfo { OwnerId = storage.OwnerId, FurnitureConfigId = storage.ConfigId, IsFridge = false, Pinned = false });
        });

        __result = containers;
    }

    /// <summary>
    /// The looting screen shows the bag beside an opened storage, the basket or the trunk, and the bag side's tabs are
    /// the game's own, as at a car's door boxes. At home every other home storage becomes one more tab there, so drag
    /// and drop, quick transfer and the switch keys work on it as on the bag; out exploring the tabs stay the game's.
    /// The game builds the tabs once per opening (Reducer_Web_BackpackUI.BuildATabs, RVA 0x2A5FE50, called only from
    /// RA_OpenUI), so they are appended right after. The game names every tab it does not know "Backpack", so each
    /// storage tab is given the name the Home list shows.
    /// </summary>
    private static void AfterBuildATabs(State_Web_BackpackUI state, State_Data_Item itemState) =>
        Guard(() =>
        {
            var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;

            if (state?.ATabOwnerIds == null || state.ATabsJson == null || itemState == null || agents == null) return;

            var shown = new List<long> { state.OwnerId_B };

            for (var i = 0; i < state.ATabOwnerIds.Count; i++) shown.Add(state.ATabOwnerIds[i]);

            var homes = GameStorages.HomeFurnitures(agents).Select(furniture => new LootTabs.Place(furniture.InstanceId, StrangerTradeActiveManager.TripOf(furniture) != null));
            var added = LootTabs.ToAdd(state.IsDualBag, GameStorages.IsExploring(), homes, shown);

            if (added.Count == 0) return;

            foreach (var ownerId in added) state.ATabOwnerIds.Add(ownerId);

            state.ATabsJson.Value = BagTabNames.Rename(Reducer_Web_BackpackUI.BuildATabsJson(state, itemState), StorageTabNames(state));
        });

    /// <summary>Each tab showing a home storage, by its place in the tab list, with the name the Home list gives it.</summary>
    private static Dictionary<int, string> StorageTabNames(State_Web_BackpackUI state)
    {
        var byIndex = new Dictionary<int, string>();
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (agents == null || config == null) return byIndex;

        var names = GameStorages.HomeStorageNames(agents, config);

        for (var i = 0; i < state.ATabOwnerIds.Count; i++)
        {
            if (names.TryGetValue(state.ATabOwnerIds[i], out var name)) byIndex[i] = name;
        }

        return byIndex;
    }

    /// <summary>The game's fridge list with a tab added for each other home storage the station has a use for.</summary>
    private static Il2CppFridges? WithFridgeTabs(Il2CppFridges? fridges, StationKind kind, State_Data_Item state, long ownerId, long targetId)
    {
        var shown = new HashSet<long> { ownerId, targetId };

        AddOwners(shown, fridges);

        var storages = StoragesToAdd(kind, state, shown);

        if (storages.Count == 0) return fridges;

        var tabs = fridges ?? new Il2CppFridges();

        foreach (var storage in storages) tabs.Add(new CookingFridgeInfo { OwnerId = storage.OwnerId, FurnitureConfigId = storage.ConfigId, Locked = false });

        return tabs;
    }

    private static Il2CppCabinets? WithCabinetTabs(Il2CppCabinets? cabinets, List<HomeStorage> storages)
    {
        if (storages.Count == 0) return cabinets;

        var tabs = cabinets ?? new Il2CppCabinets();

        foreach (var storage in storages) tabs.Add(new ToolCabinetInfo { OwnerId = storage.OwnerId, FurnitureConfigId = storage.ConfigId });

        return tabs;
    }

    private static void AddOwners(HashSet<long> shown, Il2CppFridges? fridges)
    {
        if (fridges == null) return;

        for (var i = 0; i < fridges.Count; i++)
        {
            if (fridges[i] != null) shown.Add(fridges[i].OwnerId);
        }
    }

    private static void AddOwners(HashSet<long> shown, Il2CppCabinets? cabinets)
    {
        if (cabinets == null) return;

        for (var i = 0; i < cabinets.Count; i++)
        {
            if (cabinets[i] != null) shown.Add(cabinets[i].OwnerId);
        }
    }

    private readonly record struct HomeStorage(long OwnerId, int ConfigId);

    /// <summary>
    /// Every home storage not already shown that holds something the station takes, in the game's own order.
    /// </summary>
    private static List<HomeStorage> StoragesToAdd(StationKind kind, State_Data_Item state, HashSet<long> shown)
    {
        var storages = new List<HomeStorage>();
        var cache = state?.OwnerCache;
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (state == null || cache == null || agents == null || config == null) return storages;

        foreach (var furniture in GameStorages.HomeFurnitures(agents))
        {
            // A drone's bag holds goods it is about to trade away.
            if (shown.Contains(furniture.InstanceId) || StrangerTradeActiveManager.TripOf(furniture) != null) continue;

            if (!HoldsUsable(cache, furniture.InstanceId, kind, state, config)) continue;

            storages.Add(new HomeStorage(furniture.InstanceId, furniture.AgentConfigId));
            shown.Add(furniture.InstanceId);
        }

        return storages;
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

    /// <summary>An exception must never escape into the game's code, so a failing hook logs once and leaves the tabs alone.</summary>
    private static void Guard(Action addTabs)
    {
        if (!_isOn) return;

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
