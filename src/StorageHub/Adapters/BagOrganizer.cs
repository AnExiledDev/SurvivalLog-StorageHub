using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// The only code that rearranges items inside a bag. Each move is checked before anything is written, every position
/// is read back afterwards, and any surprise puts every item back where it was, so an item is never lost, copied or
/// stacked on another. Failures are logged once per bag and reason, never shown to the player.
/// </summary>
internal sealed class BagOrganizer
{
    /// <summary>An item as the game holds it, next to how the planner sees it.</summary>
    private readonly record struct Held(ItemData Data, BagItem Placed);

    private readonly Func<OrganizeRequest?> _takeDue;
    private readonly PackOptions _options;
    private readonly Action<string> _log;
    private readonly HashSet<string> _reported = new();

    public BagOrganizer(Func<OrganizeRequest?> takeDue, PackOptions options, Action<string> log)
    {
        _takeDue = takeDue;
        _options = options;
        _log = log;
    }

    /// <summary>Called every frame; does nothing unless the bag screen asked for a tidy on an earlier frame.</summary>
    public void Update()
    {
        if (_takeDue() is not { } request) return;

        var items = GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;
        var config = GameSingleton.Existing<ConfigManager>();

        if (items == null || config == null) return;

        if (WhyLeaveAlone(items, request) is { } reason)
        {
            ReportOnce(request.OwnerId, $"left as it is: {reason}");

            return;
        }

        Organize(items, config, request);
    }

    private void Organize(ItemManager items, ConfigManager config, OrganizeRequest request)
    {
        if (ReadBag(items, config, request.OwnerId) is not { } bag) return;

        switch (Organizer.Plan(request.ShownGrid, bag.Select(held => held.Placed).ToList(), _options))
        {
            case OrganizePlan.Move move:
                Apply(items, request, bag, move);
                break;
            case OrganizePlan.NoRoom:
                ReportOnce(request.OwnerId, "left as it is: no layout holds every item");
                break;
            case OrganizePlan.AlreadyTidy:
                break;
        }
    }

    /// <summary>
    /// Bags the game places by its own rules are never touched, and neither is a bag whose real grid differs from the
    /// one on screen, since a layout for one could hide items outside the other.
    /// </summary>
    private static string? WhyLeaveAlone(ItemManager items, OrganizeRequest request)
    {
        if (HasOwnRules(items, request.OwnerId)) return "it places items by its own rules (rack, cooking, compost, hot pot, robot mold, synthetic or locked)";

        var size = items.GetOwnerBagSize(request.OwnerId);
        var shown = request.ShownGrid;

        if (shown.Cols <= 0 || shown.Rows <= 0 || size.x != shown.Cols || size.y != shown.Rows) return $"the screen shows a {shown.Cols}x{shown.Rows} grid but the bag is {size.x}x{size.y}";

        return null;
    }

    private static bool HasOwnRules(ItemManager items, long ownerId) =>
        items.IsCookingFurnitureBag(ownerId) || items.IsInCompostBin(ownerId) || ItemManager.IsMaterialRackOwner(ownerId)
        || ItemManager.IsSyntheticBagOwner(ownerId) || ItemManager.IsLockedBagOwner(ownerId) || ItemManager.IsHotPotWorkbench(ownerId)
        || ItemManager.GetRobotMoldCells(ownerId) is { Count: > 0 };

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

    private void Apply(ItemManager items, OrganizeRequest request, IReadOnlyList<Held> bag, OrganizePlan.Move move)
    {
        var failure = MoveAll(items, bag, move.Positions);

        if (failure == null)
        {
            _log($"organized bag {request.OwnerId} ({request.Reason}, {move.Style} layout, {bag.Count} items)");
        }
        else
        {
            var restoreFailure = MoveAll(items, bag, bag.ToDictionary(held => held.Placed.Item.Id, held => held.Placed.At));

            ReportOnce(request.OwnerId, restoreFailure == null ? $"{failure}; every item was put back" : $"{failure}; putting the items back also failed: {restoreFailure}");
        }

        Redraw(items, request);
    }

    /// <summary>
    /// Writes every position without the game's overlap check, since items pass over each other while a whole bag
    /// is rearranged, then reads every one back. Null when the bag ends up exactly as asked.
    /// </summary>
    private static string? MoveAll(ItemManager items, IReadOnlyList<Held> bag, IReadOnlyDictionary<long, Cell> positions)
    {
        try
        {
            foreach (var held in bag)
            {
                var at = positions[held.Placed.Item.Id];

                if (!items.SetBagPosSafe(held.Data, new Vector2Int(at.X, at.Y), false)) return $"the game refused to move item {held.Placed.Item.Id} to {at.X},{at.Y}";
            }

            foreach (var held in bag)
            {
                var at = positions[held.Placed.Item.Id];
                var now = held.Data.BagPos;

                if (now.x != at.X || now.y != at.Y) return $"item {held.Placed.Item.Id} is at {now.x},{now.y} instead of {at.X},{at.Y}";
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
