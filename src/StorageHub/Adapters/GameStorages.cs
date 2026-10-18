using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using StorageHub.Core;
using Il2CppItems = Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, GameCore.HotUpdate.ReduxUI.Data_Item>>;

namespace StorageHub.Adapters;

internal abstract record InventoryRead
{
    public sealed record Ready(Inventory Inventory) : InventoryRead;

    public sealed record NotReady(string Reason) : InventoryRead;
}

/// <summary>
/// The only code that reads or moves the game's items. Reads go through the UI store's item cache, the same
/// source ItemTotals and BetterCrafting use; moves go through the game's own quick-move.
/// </summary>
internal sealed class GameStorages
{
    private const float SecondsPerHour = 3600f;

    /// <summary>What the game's shelf life read answers for an item that never spoils.</summary>
    private const float NeverSpoilsByGame = -1f;

    /// <summary>What an item's spoil clock reads, for the fridge tag rules.</summary>
    private readonly record struct ShelfClock(float StartTime, float TimeLeft, float PresetShelfScale);

    private readonly Action<string> _log;
    private readonly Dictionary<long, Furniture> _furnitures = new();
    private readonly Dictionary<long, ShelfClock> _clocks = new();
    private readonly Dictionary<int, string> _itemNames = new();
    private readonly Dictionary<long, float> _daysLeft = new();
    private IntPtr _namedWorld;
    private bool _hasReportedRankFailure;
    private bool _hasReportedRackRankFailure;
    private bool _hasReportedRackCheckFailure;
    private bool _hasReportedRobotPutFailure;
    private bool _hasReportedUnpickableDrop;
    private bool _hasReportedShelfLifeFailure;

    public GameStorages(Action<string> log)
    {
        _log = log;
    }

    /// <summary>
    /// Days until each stack read since the last Forget spoils, by item id, as the game's own backpack shows it: -1 for
    /// an item that never spoils or has stopped spoiling.
    /// </summary>
    public IReadOnlyDictionary<long, float> DaysLeft => _daysLeft;

    public InventoryRead Read()
    {
        Forget();

        var cache = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>())?.OwnerCache;
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var player = agents?.GetLeadingRole();
        var config = GameSingleton.Existing<ConfigManager>();

        var isChinese = GameLanguage.IsChinese();

        if (cache == null) return new InventoryRead.NotReady(Texts.NoItemData.Pick(isChinese));
        if (world == null || agents == null || player == null || config == null) return new InventoryRead.NotReady(Texts.NoWorld.Pick(isChinese));

        ForgetNamesOfOtherWorld(world);

        var storages = new List<Storage> { new(player.InstanceId, Texts.Bag.Pick(isChinese), StorageKind.Bag) };
        storages.AddRange(HomeStorages(agents, config));
        storages.Add(new Storage(WorkbenchDrawerConfig.DrawerOwnerId, Texts.WorkbenchDrawer.Pick(isChinese), StorageKind.Drawer));
        var stacks = ReadStacks(cache, storages, config, NowHours(world));
        var names = stacks.Select(stack => stack.ConfigId).Distinct().ToDictionary(id => id, id => ItemName(config, id));

        return new InventoryRead.Ready(new Inventory(storages, stacks, names, isChinese));
    }

    /// <summary>
    /// Lets go of the game's furniture objects once an action is done with them, so a closed panel pins nothing in the
    /// game's heap. Rank needs them only between a Read and the end of the plan built from it.
    /// </summary>
    public void Forget()
    {
        _furnitures.Clear();
        _clocks.Clear();
        _daysLeft.Clear();
    }

    /// <summary>
    /// The stacks one bag holds right now, straight from the game's item manager rather than the UI's cache, which can
    /// lag a frame behind. Their spoil clocks are kept, so Rank can judge them; call after Read, which clears them.
    /// </summary>
    public IReadOnlyList<ItemStack> ReadOwner(long ownerId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var items = world?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (world == null || items == null) return Array.Empty<ItemStack>();

        var nowHours = NowHours(world);
        var stacks = new List<ItemStack>();

        foreach (var item in Held(items, ownerId))
        {
            _clocks[item.InstanceId] = new ShelfClock(item.StartTime, item.TimeLeft, item.PresetShelfScale);
            _daysLeft[item.InstanceId] = FallbackDaysLeft(config, item.ItemConfigId, item.StartTime, item.TimeLeft, item.PresetShelfScale, nowHours);
            stacks.Add(new ItemStack(item.InstanceId, item.ItemConfigId, item.ItemCount, ownerId));
        }

        return stacks;
    }

    /// <summary>The stacks one bag holds, read only: safe to call from a hook, since it keeps nothing and changes nothing.</summary>
    public static IReadOnlyList<ItemStack> ReadHeld(ItemManager items, long ownerId) =>
        Held(items, ownerId).Select(item => new ItemStack(item.InstanceId, item.ItemConfigId, item.ItemCount, ownerId)).ToList();

    private static IEnumerable<ItemData> Held(ItemManager items, long ownerId)
    {
        var held = items.GetItemDataList(ownerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];

            // A pooled item can sit in a list it no longer belongs to, the same ghost the UI cache keeps.
            if (item != null && item.OwnerId == ownerId && item.ItemCount > 0) yield return item;
        }
    }

    /// <summary>What the game says about each stack and storage, read through the storages from the last Read.</summary>
    public PutFacts Facts => new(Rank, RackRank);

    /// <summary>What the storage's tag rule thinks of the stack, or null when the rule does not want it.</summary>
    public PutRank? Rank(ItemStack stack, Storage storage)
    {
        if (!_furnitures.TryGetValue(storage.OwnerId, out var furniture)) return null;

        return RankByGame(stack, "tag rank", ref _hasReportedRankFailure, (itemConfig, daysLeft) =>
        {
            var tags = furniture.GetAgentComponent<FurnitureTagComponent>();

            return tags != null && tags.TryGetPutRank(furniture.AgentConfigId, itemConfig, daysLeft, out var level, out var specificity) ? new PutRank(level, specificity) : null;
        });
    }

    /// <summary>What the game's own rack rule thinks of the stack, the rank its robots use, or null when the rack has no slot for it.</summary>
    public PutRank? RackRank(ItemStack stack, Storage storage)
    {
        if (!_furnitures.TryGetValue(storage.OwnerId, out var furniture)) return null;

        return RankByGame(stack, "material rack rank", ref _hasReportedRackRankFailure, (itemConfig, daysLeft) =>
        {
            var rack = furniture.GetAgentComponent<MaterialRackComponent>();

            return rack != null && rack.TryGetPutRank(furniture.AgentConfigId, itemConfig, daysLeft, out var level, out var specificity) ? new PutRank(level, specificity) : null;
        });
    }

    /// <summary>Runs one of the game's rank checks on the stack's item and its real time left, failing soft to "not wanted".</summary>
    private PutRank? RankByGame(ItemStack stack, string what, ref bool hasReported, Func<Config_Item, float, PutRank?> rank)
    {
        if (!_clocks.TryGetValue(stack.ItemId, out var clock)) return null;

        try
        {
            var itemConfig = GameSingleton.Existing<ConfigManager>()?.Get_Config_Item(stack.ConfigId);

            if (itemConfig == null) return null;

            var daysLeft = Freshness.DaysLeft(itemConfig.Life, clock.StartTime, clock.TimeLeft, clock.PresetShelfScale, Furniture.GetCompressorNowHours());

            return rank(itemConfig, daysLeft);
        }
        catch (Exception ex)
        {
            ReportOnce(what, ref hasReported, ex);

            return null;
        }
    }

    private void ReportOnce(string what, ref bool hasReported, Exception ex)
    {
        if (!hasReported) _log($"{what} failed (reported once): {ex}");

        hasReported = true;
    }

    /// <summary>The game's quick-move of one whole stack. False when the game refuses, usually because the target is full.</summary>
    public bool Move(ItemStack stack, long targetOwnerId)
    {
        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

        if (items == null) return false;

        return items.TryQuickMoveItem(stack.ItemId, stack.OwnerId, targetOwnerId, true, false);
    }

    /// <summary>
    /// Everything lying on the floor of the home map, as stacks owned by the floor. Wave loot is left out: the game's
    /// drone collects it and tracks it by id. Call after Read, which clears the spoil clocks this adds to.
    /// </summary>
    public IReadOnlyList<ItemStack> ReadFloor()
    {
        var drops = GameSingleton.Existing<BattleLogicWorld>()?._DropItemManager;

        if (drops == null) return Array.Empty<ItemStack>();

        var found = new Il2CppSystem.Collections.Generic.List<DropItem>();
        drops.GetDropItemsInHome(found);
        var stacks = new List<ItemStack>();
        var config = GameSingleton.Existing<ConfigManager>();
        var nowHours = NowHours(GameSingleton.Existing<BattleLogicWorld>());

        for (var i = 0; i < found.Count; i++)
        {
            var drop = found[i];

            if (drop == null || !drop.HasItem || drop.IsWaveLoot) continue;

            var item = drop.Item;

            if (PickupId(drops, drop) is not { } pickupId) continue;

            _clocks[pickupId] = new ShelfClock(item.StartTime, item.TimeLeft, item.PresetShelfScale);
            _daysLeft[pickupId] = FallbackDaysLeft(config, item.ItemConfigId, item.StartTime, item.TimeLeft, item.PresetShelfScale, nowHours);
            stacks.Add(new ItemStack(pickupId, item.ItemConfigId, item.ItemCount, Inventory.FloorOwnerId));
        }

        return stacks;
    }

    /// <summary>
    /// The game's own pickup, aimed at a storage instead of the bag, so the item keeps what a hand pickup keeps. True
    /// only when nothing is left on the floor; a storage that takes part of a stack leaves the rest for the next one.
    /// </summary>
    public bool PickUp(ItemStack floorStack, long targetOwnerId)
    {
        var drops = GameSingleton.Existing<BattleLogicWorld>()?._DropItemManager;

        if (drops == null) return false;

        var isAccepted = drops.TryPickupItem(floorStack.ItemId, targetOwnerId, out var picked, false);
        var left = drops.FindDropItem(floorStack.ItemId);
        var remaining = left == null || !left.HasItem ? 0 : left.Item.ItemCount;
        var taken = floorStack.Count - remaining;

        // The floor is the truth: the game's answer only gets logged when it disagrees, since its meaning is not documented.
        if (taken != picked || (!isAccepted && taken != 0)) _log($"PICKUP MISMATCH item {floorStack.ItemId}: taken {taken}, picked {picked}, accepted {isAccepted}");

        return remaining == 0;
    }

    public string NameOf(int configId)
    {
        var config = GameSingleton.Existing<ConfigManager>();

        return config == null ? Texts.UnnamedItem.Say(GameLanguage.IsChinese(), configId) : ItemName(config, configId);
    }

    /// <summary>
    /// Every item in the game's item table (ConfigManager._Config_Item_Dict, which GetAll_Config_Item reads, RVA
    /// 0x2c9f860) that has a real name, for the keep list's search: a name that is still the raw ItemName key has no
    /// translation and is left out. Config_Item.InCodex is not used: CodexManager::IsInCodex (RVA 0x2fe3050) reads it
    /// for the Food and Prey codex pages only, so it says nothing about tools or materials. Empty until the game has
    /// loaded its tables.
    /// </summary>
    public IReadOnlyList<CatalogItem> Catalog()
    {
        var config = GameSingleton.Existing<ConfigManager>();
        var table = config?._Config_Item_Dict;
        var catalog = new List<CatalogItem>();

        if (config == null || table == null) return catalog;

        foreach (var item in table.Values)
        {
            if (item == null) continue;

            var name = ItemName(config, item.ID);

            if (name != $"Item {item.ID}" && name != item.ItemName) catalog.Add(new CatalogItem(item.ID, name));
        }

        return catalog;
    }

    /// <summary>Names are cached for the life of one world; a new world may come with a different language.</summary>
    private void ForgetNamesOfOtherWorld(BattleLogicWorld world)
    {
        if (world.Pointer == _namedWorld) return;

        _itemNames.Clear();
        _namedWorld = world.Pointer;
    }

    /// <summary>The id the game's pickup looks a floor item up by: the drop's own id, or failing that its item's.</summary>
    private long? PickupId(DropItemManager drops, DropItem drop)
    {
        if (IsSameDrop(drops.FindDropItem(drop.InstanceId), drop)) return drop.InstanceId;
        if (IsSameDrop(drops.FindDropItem(drop.Item.InstanceId), drop)) return drop.Item.InstanceId;

        if (!_hasReportedUnpickableDrop) _log($"floor item {drop.InstanceId} (item {drop.Item.InstanceId}) is not found by either id; floor items are skipped (reported once)");

        _hasReportedUnpickableDrop = true;

        return null;
    }

    private static bool IsSameDrop(DropItem? found, DropItem drop) => found != null && found.Pointer == drop.Pointer;

    /// <summary>The icon the item table names for the web UI, falling back to the in-world icon.</summary>
    public string? IconName(int configId)
    {
        var item = GameSingleton.Existing<ConfigManager>()?.Get_Config_Item(configId);

        return NonEmpty(item?.WebIcon) ?? NonEmpty(item?.Icon);
    }

    /// <summary>Every piece of home furniture with a bag that counts as the player's storage; read only, so a hook may call it.</summary>
    public static List<Furniture> HomeFurnitures(AgentManager agents)
    {
        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);
        var homes = new List<Furniture>();

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture != null && !ItemManager.IsExcludedFromCostPool(furniture.InstanceId)) homes.Add(furniture);
        }

        return homes;
    }

    /// <summary>
    /// Whether a station takes each stack from the last Read, by the game's own checks on the item the UI cache holds.
    /// A stack the cache no longer has is not taken.
    /// </summary>
    public Func<ItemStack, bool> Usable(StationKind kind)
    {
        var state = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>());
        var cache = state?.OwnerCache;
        var config = GameSingleton.Existing<ConfigManager>();

        if (kind == StationKind.Inventory || state == null || cache == null || config == null) return _ => false;

        return stack => cache.TryGetValue(stack.OwnerId, out var held) && held != null && held.TryGetValue(stack.ItemId, out var item) && item != null && StationRules.Takes(kind, item, state, config);
    }

    /// <summary>The game's own "open a storage beside the bag", as walking up to it does; -1 shows the bag alone.</summary>
    public bool OpenBesideBag(long ownerId)
    {
        var player = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (player == null) return false;

        Ac_Item_GetBagList.SendAction(player.InstanceId, ownerId, false);

        return true;
    }

    private IEnumerable<Storage> HomeStorages(AgentManager agents, ConfigManager config)
    {
        var homes = HomeFurnitures(agents);
        var names = StorageNames.Distinct(homes.Select(furniture => FurnitureName(config, furniture)).ToList());

        for (var i = 0; i < homes.Count; i++)
        {
            _furnitures[homes[i].InstanceId] = homes[i];

            var isRack = IsMaterialRack(config, homes[i]);

            yield return new Storage(homes[i].InstanceId, names[i], StorageKind.Furniture, DroneBayOf(homes[i]), isRack, isRack && IsRobotPutOff(homes[i]));
        }
    }

    /// <summary>The rack's own switch that stops robots putting items in, failing soft to "on" so the rack stays a target.</summary>
    private bool IsRobotPutOff(Furniture furniture)
    {
        try
        {
            var rack = furniture.GetAgentComponent<MaterialRackComponent>();

            return rack != null && !rack.GetAllowRobotPut();
        }
        catch (Exception ex)
        {
            ReportOnce("rack robot put switch", ref _hasReportedRobotPutFailure, ex);

            return false;
        }
    }

    /// <summary>A material rack by the game's own rack table, so any rack the game adds counts too.</summary>
    private bool IsMaterialRack(ConfigManager config, Furniture furniture)
    {
        try
        {
            return config.IsMaterialRackFurniture(furniture.AgentConfigId);
        }
        catch (Exception ex)
        {
            ReportOnce("material rack check", ref _hasReportedRackCheckFailure, ex);

            return false;
        }
    }

    /// <summary>A drone hub by its config, or a drone by the trip component every drone carries.</summary>
    private static DroneBay DroneBayOf(Furniture furniture)
    {
        if (StrangerTradeActiveManager.IsDroneHub(furniture.AgentConfigId)) return DroneBay.Hub;

        return StrangerTradeActiveManager.TripOf(furniture) != null ? DroneBay.Drone : DroneBay.None;
    }

    private List<ItemStack> ReadStacks(Il2CppItems cache, IEnumerable<Storage> storages, ConfigManager config, float nowHours)
    {
        var stacks = new List<ItemStack>();

        foreach (var storage in storages)
        {
            if (!cache.TryGetValue(storage.OwnerId, out var held) || held == null) continue;

            foreach (var item in held.Values)
            {
                // The cache can keep stale entries whose OwnerId points elsewhere (BetterCrafting calls them ghosts); the game would refuse to move them.
                if (item == null || item.OwnerId != storage.OwnerId) continue;

                _clocks[item.LogicId] = new ShelfClock(item.StartTime, item.TimeLeft, item.PresetShelfScale);
                _daysLeft[item.LogicId] = ShelfLifeDays(config, item, nowHours);
                stacks.Add(new ItemStack(item.LogicId, item.ItemConfigId, item.ItemCount, storage.OwnerId));
            }
        }

        return stacks;
    }

    /// <summary>
    /// The game clock in hours, as the spoil clocks count it. The compressor's whole hours stand in when the time
    /// manager is missing, which only costs the list its minutes.
    /// </summary>
    private static float NowHours(BattleLogicWorld? world)
    {
        var time = world?._GameTimeManager;

        return time != null ? time.GetTotalSeconds() / SecondsPerHour : Furniture.GetCompressorNowHours();
    }

    /// <summary>
    /// The backpack's own shelf life (Reducer_Web_BackpackUI.GetShelfLifeDaysRaw, RVA 0x2880900), so the list agrees
    /// with the game's tooltip, compost freezing and storage shelf factors included. Failing that, the fridge rules' formula.
    /// </summary>
    private float ShelfLifeDays(ConfigManager config, Data_Item item, float nowHours)
    {
        var row = config.Get_Config_Item(item.ItemConfigId);

        if (row == null) return NeverSpoilsByGame;

        try
        {
            return Reducer_Web_BackpackUI.GetShelfLifeDaysRaw(row, item, nowHours);
        }
        catch (Exception ex)
        {
            if (!_hasReportedShelfLifeFailure) _log("reading shelf life from the game failed, working it out instead (reported once): " + ex.Message);

            _hasReportedShelfLifeFailure = true;

            return Freshness.DaysLeft(row.Life, item.StartTime, item.TimeLeft, item.PresetShelfScale, nowHours);
        }
    }

    /// <summary>For items the backpack has no entry for yet (the floor, a bag read straight from the item manager).</summary>
    // TimeScale is left out on purpose: the game's own shelf life read leaves it out too, so this matches its tooltip.
    private static float FallbackDaysLeft(ConfigManager? config, int configId, float startTime, float timeLeft, float presetShelfScale, float nowHours)
    {
        var row = config?.Get_Config_Item(configId);

        return row == null ? NeverSpoilsByGame : Freshness.DaysLeft(row.Life, startTime, timeLeft, presetShelfScale, nowHours);
    }

    /// <summary>Resolved once per item per world: the view is rebuilt every second and the names do not change in between.</summary>
    private string ItemName(ConfigManager config, int configId)
    {
        if (_itemNames.TryGetValue(configId, out var cached)) return cached;

        return _itemNames[configId] = ResolveItemName(config, configId);
    }

    private static string ResolveItemName(ConfigManager config, int configId)
    {
        var item = config.Get_Config_Item(configId);

        if (item == null) return Texts.UnnamedItem.Say(GameLanguage.IsChinese(), configId);

        return Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local) ?? NonEmpty(item.ItemName) ?? Texts.UnnamedItem.Say(GameLanguage.IsChinese(), configId);
    }

    private static string FurnitureName(ConfigManager config, Furniture furniture)
    {
        var name = config.Get_Config_Furniture(furniture.AgentConfigId)?.Name;

        return Translate(config, name) ?? NonEmpty(name) ?? Texts.UnnamedStorage.Say(GameLanguage.IsChinese(), furniture.InstanceId);
    }

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
