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
    /// <summary>What an item's spoil clock reads, for the fridge tag rules.</summary>
    private readonly record struct ShelfClock(float StartTime, float TimeLeft, float PresetShelfScale);

    private readonly Action<string> _log;
    private readonly Dictionary<long, Furniture> _furnitures = new();
    private readonly Dictionary<long, ShelfClock> _clocks = new();
    private readonly Dictionary<int, string> _itemNames = new();
    private IntPtr _namedWorld;
    private bool _hasReportedRankFailure;
    private bool _hasReportedUnpickableDrop;

    public GameStorages(Action<string> log)
    {
        _log = log;
    }

    public InventoryRead Read()
    {
        Forget();

        var cache = GameSingleton.Existing<ReduxUISystem>()?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>())?.OwnerCache;
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var agents = world?._AgentManager;
        var player = agents?.GetLeadingRole();
        var config = GameSingleton.Existing<ConfigManager>();

        if (cache == null) return new InventoryRead.NotReady("no item data yet (load a save first)");
        if (world == null || agents == null || player == null || config == null) return new InventoryRead.NotReady("the game world is not loaded");

        ForgetNamesOfOtherWorld(world);

        var storages = new List<Storage> { new(player.InstanceId, "Bag", StorageKind.Bag) };
        storages.AddRange(HomeStorages(agents, config));
        storages.Add(new Storage(WorkbenchDrawerConfig.DrawerOwnerId, "Workbench drawer", StorageKind.Drawer));
        var stacks = ReadStacks(cache, storages);
        var names = stacks.Select(stack => stack.ConfigId).Distinct().ToDictionary(id => id, id => ItemName(config, id));

        return new InventoryRead.Ready(new Inventory(storages, stacks, names));
    }

    /// <summary>
    /// Lets go of the game's furniture objects once an action is done with them, so a closed panel pins nothing in the
    /// game's heap. Rank needs them only between a Read and the end of the plan built from it.
    /// </summary>
    public void Forget()
    {
        _furnitures.Clear();
        _clocks.Clear();
    }

    /// <summary>What the storage's tag rule thinks of the stack, or null when the rule does not want it.</summary>
    public PutRank? Rank(ItemStack stack, Storage storage)
    {
        if (!_furnitures.TryGetValue(storage.OwnerId, out var furniture) || !_clocks.TryGetValue(stack.ItemId, out var clock)) return null;

        try
        {
            var tags = furniture.GetAgentComponent<FurnitureTagComponent>();
            var itemConfig = GameSingleton.Existing<ConfigManager>()?.Get_Config_Item(stack.ConfigId);

            if (tags == null || itemConfig == null) return null;

            var daysLeft = Freshness.DaysLeft(itemConfig.Life, clock.StartTime, clock.TimeLeft, clock.PresetShelfScale, Furniture.GetCompressorNowHours());
            return tags.TryGetPutRank(furniture.AgentConfigId, itemConfig, daysLeft, out var level, out var specificity) ? new PutRank(level, specificity) : null;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRankFailure) _log("tag rank failed (reported once): " + ex);

            _hasReportedRankFailure = true;

            return null;
        }
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

        for (var i = 0; i < found.Count; i++)
        {
            var drop = found[i];

            if (drop == null || !drop.HasItem || drop.IsWaveLoot) continue;

            var item = drop.Item;

            if (PickupId(drops, drop) is not { } pickupId) continue;

            _clocks[pickupId] = new ShelfClock(item.StartTime, item.TimeLeft, item.PresetShelfScale);
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

        return config == null ? $"Item {configId}" : ItemName(config, configId);
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

    private IEnumerable<Storage> HomeStorages(AgentManager agents, ConfigManager config)
    {
        var found = agents.GetFurnituresWithBag(agents.GetHomeMapId(), true, true);
        var homes = new List<Furniture>();

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture != null && !ItemManager.IsExcludedFromCostPool(furniture.InstanceId)) homes.Add(furniture);
        }

        var names = StorageNames.Distinct(homes.Select(furniture => FurnitureName(config, furniture)).ToList());

        for (var i = 0; i < homes.Count; i++)
        {
            _furnitures[homes[i].InstanceId] = homes[i];

            yield return new Storage(homes[i].InstanceId, names[i], StorageKind.Furniture);
        }
    }

    private List<ItemStack> ReadStacks(Il2CppItems cache, IEnumerable<Storage> storages)
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
                stacks.Add(new ItemStack(item.LogicId, item.ItemConfigId, item.ItemCount, storage.OwnerId));
            }
        }

        return stacks;
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

        if (item == null) return $"Item {configId}";

        return Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local) ?? NonEmpty(item.ItemName) ?? $"Item {configId}";
    }

    private static string FurnitureName(ConfigManager config, Furniture furniture)
    {
        var name = config.Get_Config_Furniture(furniture.AgentConfigId)?.Name;

        return Translate(config, name) ?? NonEmpty(name) ?? $"Storage {furniture.InstanceId}";
    }

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
