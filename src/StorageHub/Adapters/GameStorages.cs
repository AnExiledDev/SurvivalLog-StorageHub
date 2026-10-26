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

/// <summary>The item table and world a catalog is built from (by address), its ids, and the entry for each id.</summary>
internal sealed record CatalogSource(long Table, long World, IEnumerable<int> Ids, Func<int, CatalogItem?> EntryOf);

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

    // What the game's tables answer, kept while the tables and the language stay the same: the panel reads the
    // inventory every second it is open, and the names and rows in it never change in between. Shared by every reader
    // (the panel, the drone delivery, the station hooks), all on the main thread.
    private static readonly TableCache<int, Config_Item?> ItemRows = new();
    private static readonly TableCache<int, string> ItemNames = new();
    private static readonly TableCache<int, string?> FurnitureNames = new();
    private static readonly TableCache<int, bool> MaterialRacks = new();

    private static Il2CppSystem.Type? _itemDataType;
    private static Il2CppSystem.Type? _backpackScreenType;
    private static IntPtr _namedWorld;

    private readonly Action<string> _log;
    private readonly Dictionary<long, Furniture> _furnitures = new();
    private readonly Dictionary<long, ShelfClock> _clocks = new();
    private readonly Dictionary<long, float> _daysLeft = new();
    private Il2CppSystem.Collections.Generic.List<DropItem>? _floorFound;
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

        var cache = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(ItemDataType)?.OwnerCache;
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var player = agents?.GetLeadingRole();
        var config = GameSingleton.Existing<ConfigManager>();

        var isChinese = GameLanguage.IsChinese();

        if (cache == null) return new InventoryRead.NotReady(Texts.NoItemData.Pick(isChinese));
        if (world == null || agents == null || player == null || config == null) return new InventoryRead.NotReady(Texts.NoWorld.Pick(isChinese));

        ForgetNamesOfOtherWorld(world);

        var storages = new List<Storage> { new(player.InstanceId, Texts.Bag.Pick(isChinese), StorageKind.Bag) };
        AddHomeStorages(agents, config, isChinese, storages);
        storages.Add(new Storage(WorkbenchDrawerConfig.DrawerOwnerId, Texts.WorkbenchDrawer.Pick(isChinese), StorageKind.Drawer));
        var stacks = ReadStacks(cache, storages, config, NowHours(world));
        var names = new Dictionary<int, string>();

        foreach (var stack in stacks)
        {
            if (!names.ContainsKey(stack.ConfigId)) names[stack.ConfigId] = ItemName(config, stack.ConfigId, isChinese);
        }

        return new InventoryRead.Ready(new Inventory(storages, stacks, names, isChinese));
    }

    /// <summary>
    /// Names are dropped when another world loads, as they always were: a language pack picked in the menu can rename
    /// things without the table or the Chinese setting changing.
    /// </summary>
    private static void ForgetNamesOfOtherWorld(BattleLogicWorld world)
    {
        if (world.Pointer == _namedWorld) return;

        ItemNames.Clear();
        FurnitureNames.Clear();
        _namedWorld = world.Pointer;
    }

    /// <summary>The UI store's item data type, looked up once.</summary>
    public static Il2CppSystem.Type ItemDataType => _itemDataType ??= Il2CppType.Of<State_Data_Item>();

    /// <summary>
    /// The item table's row for an item, or null when it has none. Read straight from the table rather than through
    /// Get_Config_Item, and kept per table, so a row is looked up once a run. A table not loaded yet keeps nothing.
    /// </summary>
    public static Config_Item? ItemRow(ConfigManager config, int configId)
    {
        ItemRows.Use(config.Pointer.ToInt64(), isChinese: false);

        if (ItemRows.TryGet(configId, out var cached)) return cached;

        if (config._Config_Item_Dict is not { } table) return null;

        return ItemRows.Set(configId, table.TryGetValue(configId, out var row) ? row : null);
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
        var held = items.GetItemDataList(ownerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];

            if (!IsHeld(item, ownerId)) continue;

            _clocks[item.InstanceId] = new ShelfClock(item.StartTime, item.TimeLeft, item.PresetShelfScale);
            _daysLeft[item.InstanceId] = FallbackDaysLeft(config, item.ItemConfigId, item.StartTime, item.TimeLeft, item.PresetShelfScale, nowHours);
            stacks.Add(new ItemStack(item.InstanceId, item.ItemConfigId, item.ItemCount, ownerId));
        }

        return stacks;
    }

    /// <summary>The stacks one bag holds, read only: safe to call from a hook, since it keeps nothing and changes nothing.</summary>
    public static IReadOnlyList<ItemStack> ReadHeld(ItemManager items, long ownerId)
    {
        var stacks = new List<ItemStack>();
        AddHeld(items, ownerId, stacks);

        return stacks;
    }

    /// <summary>ReadHeld into a list the caller keeps, for a reader that runs every frame.</summary>
    public static void AddHeld(ItemManager items, long ownerId, List<ItemStack> into)
    {
        var held = items.GetItemDataList(ownerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];

            if (IsHeld(item, ownerId)) into.Add(new ItemStack(item!.InstanceId, item.ItemConfigId, item.ItemCount, ownerId));
        }
    }

    /// <summary>A pooled item can sit in a list it no longer belongs to, the same ghost the UI cache keeps.</summary>
    private static bool IsHeld(ItemData? item, long ownerId) => item != null && item.OwnerId == ownerId && item.ItemCount > 0;

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
            var config = GameSingleton.Existing<ConfigManager>();
            var itemConfig = config == null ? null : ItemRow(config, stack.ConfigId);

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
    /// One unit of a stack. Splitting a stack is Stacking's work, so without it (or when it cannot split this one) the
    /// whole stack moves, as a plain click always did.
    /// </summary>
    public UnitMove MoveOne(ItemStack stack, long targetOwnerId)
    {
        var split = StackingUnits.MoveOne(stack.ItemId, stack.OwnerId, targetOwnerId);

        if (split.HasValue) return split.Value ? UnitMove.One : UnitMove.Refused;

        // Stacking passes on a stack that is no longer where it was read; moving it from there would hang the game.
        if (!ReadOwner(stack.OwnerId).Any(held => held.ItemId == stack.ItemId)) return UnitMove.Refused;

        return Move(stack, targetOwnerId) ? UnitMove.WholeStack : UnitMove.Refused;
    }

    /// <summary>
    /// Everything lying on the floor of the home map, as stacks owned by the floor. Wave loot is left out: the game's
    /// drone collects it and tracks it by id. Call after Read, which clears the spoil clocks this adds to.
    /// </summary>
    public IReadOnlyList<ItemStack> ReadFloor()
    {
        var drops = GameSingleton.Existing<BattleLogicWorld>()?._DropItemManager;

        if (drops == null) return Array.Empty<ItemStack>();

        // One game list, reused by every read and emptied after it, so the floor is not a new game object each second.
        var found = _floorFound ??= new Il2CppSystem.Collections.Generic.List<DropItem>();
        found.Clear();
        drops.GetDropItemsInHome(found);
        var stacks = new List<ItemStack>();
        var config = GameSingleton.Existing<ConfigManager>();
        var nowHours = NowHours(GameSingleton.Existing<BattleLogicWorld>());

        try
        {
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
        }
        finally
        {
            // Emptied so the reused list pins none of the game's drops between reads.
            found.Clear();
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
        var isChinese = GameLanguage.IsChinese();

        return config == null ? Texts.UnnamedItem.Say(isChinese, configId) : ItemName(config, configId, isChinese);
    }

    /// <summary>What the keep list's catalog is read from right now, or null before the game has loaded its tables.</summary>
    public static CatalogSource? CatalogNow(bool isChinese)
    {
        var config = GameSingleton.Existing<ConfigManager>();

        if (config == null) return null;

        var world = GameSingleton.Existing<BattleLogicWorld>();

        if (world != null) ForgetNamesOfOtherWorld(world);

        return new CatalogSource(config.Pointer.ToInt64(), world?.Pointer.ToInt64() ?? 0, CatalogIds(config), configId => CatalogEntry(config, configId, isChinese));
    }

    /// <summary>
    /// Every id in the game's item table (ConfigManager._Config_Item_Dict, which GetAll_Config_Item reads, RVA
    /// 0x2c9f860), walked lazily so the catalog can take a few each frame; empty until the game has loaded its tables.
    /// Pair with <see cref="CatalogEntry"/>.
    /// </summary>
    public static IEnumerable<int> CatalogIds(ConfigManager config)
    {
        if (config._Config_Item_Dict is not { } table) yield break;

        foreach (var id in table.Keys) yield return id;
    }

    /// <summary>
    /// The item as the keep list's search lists it, or null when it has no real name: a name that is still the raw
    /// ItemName key has no translation and is left out. Config_Item.InCodex is not used: CodexManager::IsInCodex (RVA
    /// 0x2fe3050) reads it for the Food and Prey codex pages only, so it says nothing about tools or materials.
    /// </summary>
    public static CatalogItem? CatalogEntry(ConfigManager config, int configId, bool isChinese)
    {
        if (ItemRow(config, configId) is not { } item) return null;

        var name = ItemName(config, configId, isChinese);

        return name != $"Item {item.ID}" && name != item.ItemName ? new CatalogItem(item.ID, name) : null;
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
        var config = GameSingleton.Existing<ConfigManager>();
        var item = config == null ? null : ItemRow(config, configId);

        return NonEmpty(item?.WebIcon) ?? NonEmpty(item?.Icon);
    }

    /// <summary>
    /// Every piece of home furniture with a bag that counts as the player's storage; read only, so a hook may call it.
    /// The neighbor's basket is left out: what sits in it is staged for her or is her gift.
    /// </summary>
    public static List<Furniture> HomeFurnitures(AgentManager agents) => HomeFurnituresAndBasket(agents).Homes;

    /// <summary>
    /// The home storages and the neighbor's basket (if this character has one and it is built) from one walk of the
    /// game's furniture list.
    /// </summary>
    private static (List<Furniture> Homes, Furniture? Basket) HomeFurnituresAndBasket(AgentManager agents)
    {
        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);
        var homes = new List<Furniture>();
        var basketId = BasketConfigId();
        Furniture? basket = null;

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture == null) continue;

            if (IsBasket(furniture, basketId)) basket ??= furniture;
            else if (!ItemManager.IsExcludedFromCostPool(furniture.InstanceId)) homes.Add(furniture);
        }

        return (homes, basket);
    }

    /// <summary>The basket's furniture id for this character: the girl's or the boy's, 0 when there is no neighbor.</summary>
    public static int BasketConfigId() => NeighborRules.GetBasketFurnitureId();

    public static bool IsBasket(Furniture? furniture, int basketId) => basketId > 0 && furniture != null && furniture.AgentConfigId == basketId;

    /// <summary>
    /// Whether a station takes each stack from the last Read, by the game's own checks on the item the UI cache holds.
    /// A stack the cache no longer has is not taken.
    /// </summary>
    public Func<ItemStack, bool> Usable(StationKind kind)
    {
        var state = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(ItemDataType);
        var cache = state?.OwnerCache;
        var config = GameSingleton.Existing<ConfigManager>();

        if (kind == StationKind.Inventory || state == null || cache == null || config == null) return _ => false;

        return stack => cache.TryGetValue(stack.OwnerId, out var held) && held != null && held.TryGetValue(stack.ItemId, out var item) && item != null && StationRules.Takes(kind, item, state, config);
    }

    /// <summary>
    /// The looting screen's bag side tabs, in the game's order: the bag, any car boxes, then the home storages. Empty
    /// when the bag screen is not open beside a storage.
    /// </summary>
    public List<long> ReadLootScreen()
    {
        var tabs = new List<long>();
        var screen = BackpackScreen();

        if (screen == null || !screen.IsDualBag) return tabs;

        for (var i = 0; screen.ATabOwnerIds != null && i < screen.ATabOwnerIds.Count; i++) tabs.Add(screen.ATabOwnerIds[i]);

        return tabs;
    }

    /// <summary>Out on an exploration map rather than at home, where looting keeps the game's own screen.</summary>
    public static bool IsExploring() => VehicleConfig.IsInExploreInstance();

    /// <summary>
    /// The open basket screen's storage tabs, in the game's order, and what she wishes for, read off the screen's own
    /// state. No tabs and no wish when the bag screen is not open on the basket.
    /// </summary>
    public (List<long> Tabs, BasketWish Wish) ReadBasketScreen()
    {
        var tabs = new List<long>();
        var screen = BackpackScreen();

        if (screen == null || !IsBasketScreen(screen)) return (tabs, BasketWish.None);

        for (var i = 0; screen.ATabOwnerIds != null && i < screen.ATabOwnerIds.Count; i++) tabs.Add(screen.ATabOwnerIds[i]);

        return (tabs, WishOf(screen.NeighborWishSnap));
    }

    /// <summary>Whether she wishes for an item, by the item's category in the game's item table.</summary>
    public static Func<int, bool> IsWished(BasketWish wish)
    {
        var config = GameSingleton.Existing<ConfigManager>();

        return configId => config != null && ItemRow(config, configId) is { } row && wish.Wants(row.Category);
    }

    /// <summary>The bag screen's state while it shows.</summary>
    public static State_Web_BackpackUI? BackpackScreen()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = _backpackScreenType ??= Il2CppType.Of<WebUI_BackpackUI>();

        if (ui == null || !ui.IsUIActive(screen)) return null;

        return ui.GetStateByUI(screen)?.TryCast<State_Web_BackpackUI>();
    }

    /// <summary>The bag screen with the neighbor's basket on its right side.</summary>
    public static bool IsBasketScreen(State_Web_BackpackUI screen)
    {
        var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;

        return screen.IsDualBag && agents != null && IsBasket(agents.GetAgent(screen.OwnerId_B)?.TryCast<Furniture>(), BasketConfigId());
    }

    public static BasketWish WishOf(NeighborWishSnapshot? snap)
    {
        if (snap == null) return BasketWish.None;

        var categories = new List<int>();

        for (var i = 0; snap.wishCats != null && i < snap.wishCats.Count; i++) categories.Add(snap.wishCats[i]);

        return new BasketWish(snap.active, snap.wishPending, snap.basketAway, snap.dying, categories, snap.crisisActive ? snap.crisisCat : null);
    }

    /// <summary>Whether the bag screen shows; the game skips its bag request while it does.</summary>
    /// <summary>A save is loaded, so the panel has items to show.</summary>
    public bool IsWorldLoaded() => GameSingleton.Existing<BattleLogicWorld>()?._ItemManager != null;

    public bool IsBackpackShowing() => GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.IsPageActive("Backpack") == true;

    public void CloseBackpack() => Ac_BackpackUI_Close.SendAction();

    /// <summary>The game's own "open a storage beside the bag", as walking up to it does; -1 shows the bag alone.</summary>
    public bool AskForBags(long ownerId)
    {
        var player = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (player == null) return false;

        Ac_Item_GetBagList.SendAction(player.InstanceId, ownerId, false);

        return true;
    }

    /// <summary>Each home storage's name as the Home list shows it, numbered where names repeat.</summary>
    public static Dictionary<long, string> HomeStorageNames(AgentManager agents, ConfigManager config) => HomeStorageNames(HomeFurnitures(agents), config);

    /// <summary>The same names for a furniture list the caller already read.</summary>
    public static Dictionary<long, string> HomeStorageNames(List<Furniture> homes, ConfigManager config)
    {
        var names = DistinctNames(homes, config, GameLanguage.IsChinese());
        var byId = new Dictionary<long, string>();

        for (var i = 0; i < homes.Count; i++) byId[homes[i].InstanceId] = names[i];

        return byId;
    }

    private static IReadOnlyList<string> DistinctNames(List<Furniture> homes, ConfigManager config, bool isChinese)
    {
        var names = new List<string>(homes.Count);

        foreach (var furniture in homes) names.Add(FurnitureName(config, furniture, isChinese));

        return StorageNames.Distinct(names);
    }

    private void AddHomeStorages(AgentManager agents, ConfigManager config, bool isChinese, List<Storage> storages)
    {
        var (homes, basket) = HomeFurnituresAndBasket(agents);
        var names = DistinctNames(homes, config, isChinese);

        for (var i = 0; i < homes.Count; i++)
        {
            _furnitures[homes[i].InstanceId] = homes[i];

            var isRack = IsMaterialRack(config, homes[i]);

            storages.Add(new Storage(homes[i].InstanceId, names[i], StorageKind.Furniture, DroneBayOf(homes[i]), isRack, isRack && IsRobotPutOff(homes[i])));
        }

        if (basket != null) storages.Add(new Storage(basket.InstanceId, FurnitureName(config, basket, isChinese), StorageKind.Basket));
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
        var configId = furniture.AgentConfigId;

        MaterialRacks.Use(config.Pointer.ToInt64(), isChinese: false);

        if (MaterialRacks.TryGet(configId, out var isRack)) return isRack;

        try
        {
            return MaterialRacks.Set(configId, config.IsMaterialRackFurniture(configId));
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
        var row = ItemRow(config, item.ItemConfigId);

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
        var row = config == null ? null : ItemRow(config, configId);

        return row == null ? NeverSpoilsByGame : Freshness.DaysLeft(row.Life, startTime, timeLeft, presetShelfScale, nowHours);
    }

    /// <summary>Resolved once per item per table and language: the view is rebuilt every second and the names do not change in between.</summary>
    private static string ItemName(ConfigManager config, int configId, bool isChinese)
    {
        ItemNames.Use(config.Pointer.ToInt64(), isChinese);

        if (ItemNames.TryGet(configId, out var cached)) return cached;

        var item = ItemRow(config, configId);

        // An id with no row is not kept: the unnamed text is cheap, and the cache stays the size of the real table.
        if (item == null) return Texts.UnnamedItem.Say(isChinese, configId);

        return ItemNames.Set(configId, Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local) ?? NonEmpty(item.ItemName) ?? Texts.UnnamedItem.Say(isChinese, configId));
    }

    /// <summary>A furniture's name by its kind, kept per table and language; a kind with no name falls back to its own instance.</summary>
    private static string FurnitureName(ConfigManager config, Furniture furniture, bool isChinese)
    {
        var configId = furniture.AgentConfigId;

        FurnitureNames.Use(config.Pointer.ToInt64(), isChinese);

        if (!FurnitureNames.TryGet(configId, out var name))
        {
            var key = config._Config_Furniture_Dict is { } table && table.TryGetValue(configId, out var row) ? row?.Name : null;

            name = FurnitureNames.Set(configId, Translate(config, key) ?? NonEmpty(key));
        }

        return name ?? Texts.UnnamedStorage.Say(isChinese, furniture.InstanceId);
    }

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
