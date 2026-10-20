using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using StorageHub.Core;
using UnityEngine;
using PollutedCells = Il2CppSystem.Collections.Generic.HashSet<UnityEngine.Vector2Int>;

namespace StorageHub.Adapters;

/// <summary>
/// The only code that rearranges items inside a bag. Each move is checked before anything is written, every position
/// is read back afterwards, and any surprise puts every item back where it was, so an item is never lost, copied or
/// stacked on another. Mold cells are kept clear, and a bag whose mold cannot be read is not touched. Failures are
/// logged once per bag and reason, never shown to the player.
/// </summary>
internal sealed class BagOrganizer
{
    /// <summary>An item as the game holds it, next to how the planner sees it.</summary>
    private readonly record struct Held(ItemData Data, BagItem Placed);

    private const float SecondsPerHour = 3600f;

    private readonly Func<OrganizeRequest?> _takeDue;
    private readonly Action<string> _log;
    private readonly HashSet<string> _reported = new();
    private readonly HashSet<int> _keptApartLogged = new();
    private PackOptions _options = new();
    private bool _mergeStacks;

    public BagOrganizer(Func<OrganizeRequest?> takeDue, Action<string> log)
    {
        _takeDue = takeDue;
        _log = log;
    }

    /// <summary>Called on the main thread at start and after each change. The next tidy goes by them.</summary>
    public void Use(PackOptions options, bool mergeStacks)
    {
        _options = options;
        _mergeStacks = mergeStacks;
    }

    /// <summary>Called every frame; does nothing unless the bag screen asked for a tidy on an earlier frame.</summary>
    public void Update()
    {
        if (_takeDue() is not { } request) return;

        var world = GameSingleton.Existing<BattleLogicWorld>();
        var items = world?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (world == null || items == null || config == null) return;

        if (WhyLeaveAlone(items, request) is { } reason)
        {
            ReportOnce(request.OwnerId, $"left as it is: {reason}");

            return;
        }

        Organize(world, items, config, request);
    }

    private void Organize(BattleLogicWorld world, ItemManager items, ConfigManager config, OrganizeRequest request)
    {
        if (ReadMold(world, request) is not { } mold) return;

        var merged = _mergeStacks && MergeStacks(world, items, config, request, mold);
        var redrawn = Rearrange(items, config, request, mold);

        if (merged && !redrawn) Redraw(items, request);
    }

    /// <summary>Lays the bag out again; true when it wrote anything, which also redraws the screen.</summary>
    private bool Rearrange(ItemManager items, ConfigManager config, OrganizeRequest request, IReadOnlyCollection<Cell> mold)
    {
        if (ReadBag(items, config, request.OwnerId) is not { } bag) return false;

        var space = new BagSpace(request.ShownGrid, mold);

        switch (Organizer.Plan(space, bag.Select(held => held.Placed).ToList(), _options))
        {
            case OrganizePlan.Move move:
                Apply(items, request, space, bag, move);
                return true;
            case OrganizePlan.NoRoom:
                ReportOnce(request.OwnerId, "left as it is: no layout holds every item");
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Tops up partial stacks of one kind from each other before the layout is planned, so the layout sees the fewer
    /// stacks. Only stacks wholly on the grid and clear of mold take part. True when anything was written, even if it
    /// was then undone.
    /// </summary>
    private bool MergeStacks(BattleLogicWorld world, ItemManager items, ConfigManager config, OrganizeRequest request, IReadOnlyCollection<Cell> mold)
    {
        if (!GamePhase.IsPastPrologue(world)) return false;

        try
        {
            if (ReadStacks(items, config, request, mold) is not { } stacks) return false;

            var steps = StackMerge.Plan(stacks, NowHours(world));

            LogKeptApart(config, StackMerge.WhyKeptApart(stacks, steps));

            if (steps.Count == 0) return false;

            var report = StackMerge.Run(steps, new GameStackCounts(items, request.OwnerId));

            if (report.Failure == null) _log($"organize, bag {request.OwnerId}: {StackMerge.Describe(report)}");
            else ReportOnce(request.OwnerId, StackMerge.Describe(report));

            return report.WroteAnything;
        }
        catch (Exception ex)
        {
            ReportOnce(request.OwnerId, $"merging stacks failed: {ex.Message}");

            return true;
        }
    }

    /// <summary>Says once per kind per session why its stacks stay apart, so a kind that never merges can be traced.</summary>
    private void LogKeptApart(ConfigManager config, IReadOnlyList<(int ConfigId, string Why)> reasons)
    {
        foreach (var (configId, why) in reasons)
        {
            if (!_keptApartLogged.Add(configId)) continue;

            _log($"organize, item {configId} ({config.Get_Config_Item(configId)?.ItemName}): {why}");
        }
    }

    private static List<MergeStack>? ReadStacks(ItemManager items, ConfigManager config, OrganizeRequest request, IReadOnlyCollection<Cell> mold)
    {
        var inBag = items.GetItemDataList(request.OwnerId);

        if (inBag == null) return null;

        var moldy = new HashSet<Cell>(mold);
        var stacks = new List<MergeStack>();

        for (var i = 0; i < inBag.Count; i++)
        {
            var data = inBag[i];

            if (data == null) return null;

            var row = config.Get_Config_Item(data.ItemConfigId);
            var size = data.ItemSize;
            var at = new Cell(data.BagPos.x, data.BagPos.y);

            if (row == null || !request.ShownGrid.Holds(at, size.x, size.y) || request.IsOnOffer(data.InstanceId)) continue;

            var freshness = new StackFreshness(data.StartTime, data.TimeLeft, data.TimeLeftFrac, data.TimeScale, data.UseTimes, data.MaxUseTimes);
            var neverSpoils = MergeRule.IsNeverSpoiling(row.Life);

            stacks.Add(new MergeStack(data.InstanceId, data.ItemConfigId, data.ItemCount, row.StackLimit, freshness, Traits(data).Key(neverSpoils), neverSpoils, Covers(moldy, at, size.x, size.y)));
        }

        return stacks;
    }

    private static ItemTraits Traits(ItemData data) =>
        new(data.Polluted, data.IsMapPreset, data.NoPackage, data.InstanceBurnValue, data.InstanceWeight, Floats(data.InstanceVD), Floats(data.InstanceEffectEnd));

    private static string Floats(Il2CppStructArray<float>? values)
    {
        if (values == null) return "-";

        var text = new string[values.Length];

        for (var i = 0; i < values.Length; i++) text[i] = values[i].ToString("R", CultureInfo.InvariantCulture);

        return string.Join(",", text);
    }

    private static bool Covers(HashSet<Cell> moldy, Cell at, int w, int h)
    {
        for (var y = at.Y; y < at.Y + h; y++)
        {
            for (var x = at.X; x < at.X + w; x++)
            {
                if (moldy.Contains(new Cell(x, y))) return true;
            }
        }

        return false;
    }

    /// <summary>The game clock in hours, as Stacking reads it; null when it cannot be read, which keeps food apart.</summary>
    private static float? NowHours(BattleLogicWorld world)
    {
        var time = world._GameTimeManager;

        return time == null ? null : time.GetTotalSeconds() / SecondsPerHour;
    }

    /// <summary>
    /// Bags the game places by its own rules are never touched, and neither is a bag whose real grid differs from the
    /// one on screen, since a layout for one could hide items outside the other.
    /// </summary>
    private static string? WhyLeaveAlone(ItemManager items, OrganizeRequest request)
    {
        if (HasOwnRules(items, request.OwnerId)) return "it places items by its own rules (rack, cooking, compost, hot pot, synthetic or locked)";

        var size = items.GetOwnerBagSize(request.OwnerId);
        var shown = request.ShownGrid;

        if (shown.Cols <= 0 || shown.Rows <= 0 || size.x != shown.Cols || size.y != shown.Rows) return $"the screen shows a {shown.Cols}x{shown.Rows} grid but the bag is {size.x}x{size.y}";

        return null;
    }

    /// <summary>
    /// Synthetic owners cover the vehicle trunk and boxes, the workbench drawer, the shredder and compressor trays and the
    /// trap store, whose contents the game files by its own rules.
    /// </summary>
    private static bool HasOwnRules(ItemManager items, long ownerId) =>
        items.IsCookingFurnitureBag(ownerId) || items.IsInCompostBin(ownerId) || ItemManager.IsMaterialRackOwner(ownerId)
        || ItemManager.IsSyntheticBagOwner(ownerId) || ItemManager.IsLockedBagOwner(ownerId) || ItemManager.IsHotPotWorkbench(ownerId);

    /// <summary>
    /// The bag's mold cells inside its grid, from the same table the game's PackNewItemAvoidingMold packs around. Null,
    /// after a log line, when they cannot be read: a bag whose mold is unknown is left alone rather than risk packing
    /// food onto it.
    /// </summary>
    private IReadOnlyCollection<Cell>? ReadMold(BattleLogicWorld world, OrganizeRequest request)
    {
        try
        {
            var pollution = world._FoodPollutionManager;

            if (pollution == null) return LeftAloneForMold(request, "the mold manager is missing");

            var polluted = pollution.GetPollutedCells(request.OwnerId);

            if (polluted == null) return Array.Empty<Cell>();

            if (polluted.TryCast<PollutedCells>() is not { } cells) return LeftAloneForMold(request, "the game returned them in a form this version cannot read");

            return MoldInGrid(cells, request.ShownGrid);
        }
        catch (Exception ex)
        {
            return LeftAloneForMold(request, $"reading them failed: {ex.Message}");
        }
    }

    private static List<Cell> MoldInGrid(PollutedCells cells, GridSize grid)
    {
        var mold = new List<Cell>();

        for (var y = 0; y < grid.Rows; y++)
        {
            for (var x = 0; x < grid.Cols; x++)
            {
                if (cells.Contains(new Vector2Int(x, y))) mold.Add(new Cell(x, y));
            }
        }

        return mold;
    }

    private IReadOnlyCollection<Cell>? LeftAloneForMold(OrganizeRequest request, string why)
    {
        ReportOnce(request.OwnerId, $"left as it is: its mold cells are unknown, {why}");

        return null;
    }

    private static List<Held>? ReadBag(ItemManager items, ConfigManager config, long ownerId)
    {
        var inBag = items.GetItemDataList(ownerId);

        if (inBag == null) return null;

        var bag = new List<Held>();

        for (var i = 0; i < inBag.Count; i++)
        {
            var data = inBag[i];

            if (data == null) return null;

            var size = data.ItemSize;
            var at = data.BagPos;
            var category = config.Get_Config_Item(data.ItemConfigId)?.Category ?? 0;

            bag.Add(new Held(data, new BagItem(new PackItem(data.InstanceId, size.x, size.y, data.ItemConfigId, category), new Cell(at.x, at.y))));
        }

        return bag;
    }

    private void Apply(ItemManager items, OrganizeRequest request, BagSpace space, IReadOnlyList<Held> bag, OrganizePlan.Move move)
    {
        var failure = MoveAll(items, bag, move.Positions, space.Grid);

        if (failure == null)
        {
            _log($"organized bag {request.OwnerId} ({request.Reason}, {move.Style} layout, {bag.Count} items)");
        }
        else
        {
            ReportOnce(request.OwnerId, $"{failure}; {PutBack(items, request, space, bag)}");
        }

        Redraw(items, request);
    }

    /// <summary>Undoes a move that went wrong, logs what became of each item saved off the grid, and says how the rest went.</summary>
    private string PutBack(ItemManager items, OrganizeRequest request, BagSpace space, IReadOnlyList<Held> bag)
    {
        var saved = bag.Select(held => held.Placed).ToList();
        var writes = Organizer.PutBack(space, saved, ReadPositions(bag));
        var restoreFailure = MoveAll(items, bag, writes, space.Grid);

        foreach (var placed in saved.Where(placed => !space.Grid.Holds(placed.At, placed.Item.W, placed.Item.H)))
        {
            ReportOnce(request.OwnerId, WhatBecameOf(placed, writes));
        }

        return restoreFailure == null ? "every item saved on the grid was put back" : $"putting the items back also failed: {restoreFailure}";
    }

    private static string WhatBecameOf(BagItem placed, IReadOnlyDictionary<long, Cell> writes)
    {
        var id = placed.Item.Id;
        var saved = $"item {id} was saved off the grid at {placed.At.X},{placed.At.Y}";

        if (!writes.TryGetValue(id, out var target)) return $"{saved}, so it was left where it is rather than written back there";

        return target == placed.At
            ? $"{saved} and no cell was free, so it was handed back to the game, which repairs such a cell by its own rules"
            : $"{saved}, so it was moved to {target.X},{target.Y} out of another item's way rather than written back there";
    }

    /// <summary>Where every item is now, by item id; an item whose cell cannot be read is left out, which counts as unknown.</summary>
    private static Dictionary<long, Cell> ReadPositions(IReadOnlyList<Held> bag)
    {
        var now = new Dictionary<long, Cell>();

        foreach (var held in bag)
        {
            try
            {
                var at = held.Data.BagPos;

                now[held.Placed.Item.Id] = new Cell(at.x, at.y);
            }
            catch (Exception)
            {
                // Left out on purpose: PutBack hands an item it cannot place to the game's repair.
            }
        }

        return now;
    }

    /// <summary>
    /// Writes each listed position without the game's overlap check, since items pass over each other while a whole bag
    /// is rearranged, then reads back every one that lies on the grid. A cell off the grid goes to the game's repair,
    /// which settles the item by its own rules, so it is not read back. Null when the bag ends up exactly as asked.
    /// </summary>
    private static string? MoveAll(ItemManager items, IReadOnlyList<Held> bag, IReadOnlyDictionary<long, Cell> positions, GridSize grid)
    {
        var moves = bag.Where(held => positions.ContainsKey(held.Placed.Item.Id)).ToList();

        try
        {
            foreach (var held in moves)
            {
                var at = positions[held.Placed.Item.Id];

                if (!items.SetBagPosSafe(held.Data, new Vector2Int(at.X, at.Y), false)) return $"the game refused to move item {held.Placed.Item.Id} to {at.X},{at.Y}";
            }

            foreach (var held in moves)
            {
                var item = held.Placed.Item;
                var at = positions[item.Id];

                if (!grid.Holds(at, item.W, item.H)) continue;

                var now = held.Data.BagPos;

                if (now.x != at.X || now.y != at.Y) return $"item {item.Id} is at {now.x},{now.y} instead of {at.X},{at.Y}";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"moving items failed: {ex.Message}";
        }
    }

    /// <summary>Has the screen redraw both bags from the item data, after any write, whether it worked or was undone.</summary>
    private void Redraw(ItemManager items, OrganizeRequest request)
    {
        try
        {
            items.SyncBothBags(request.PartnerId, request.OwnerId);
        }
        catch (Exception ex)
        {
            ReportOnce(request.OwnerId, $"redrawing the bag screen failed: {ex.Message}");
        }
    }

    private void ReportOnce(long ownerId, string message)
    {
        if (_reported.Add($"{ownerId}: {message}")) _log($"organize, bag {ownerId}: {message}");
    }
}
