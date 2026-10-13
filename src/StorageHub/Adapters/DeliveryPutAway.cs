using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// Puts a drone's delivery away into the tagged storages, the same way the bag's put-away does, a few frames after the
/// drone settles: the hook only records the landing, and the cargo is moved here, outside the game's own settle. What
/// no storage takes stays where the game put it, and a drone left empty has its retrieve finished for it. One line per
/// delivery goes to the log and the toast.
/// </summary>
internal sealed class DeliveryPutAway
{
    /// <summary>Long enough for a cargo handed over across several frames, short enough that a missed one is let go.</summary>
    private const float LimitSeconds = 5f;

    private readonly Func<Landing?> _takeNext;
    private readonly GameStorages _game;
    private readonly DroneRetrieves _retrieves;
    private readonly WebPage _page;
    private readonly Action<string> _log;

    private List<Landing> _waiting = new();
    private LandingGroup? _group;
    private float _startedAt;
    private IReadOnlyList<ItemStack> _seenLastTime = Array.Empty<ItemStack>();

    public DeliveryPutAway(Func<Landing?> takeNext, GameStorages game, DroneRetrieves retrieves, WebPage page, Action<string> log)
    {
        _takeNext = takeNext;
        _game = game;
        _retrieves = retrieves;
        _page = page;
        _log = log;
    }

    /// <summary>
    /// Called every frame; watches one group of landings at a time until its cargo has settled, then puts it away. A
    /// landing that shares a bag with the watched group joins it, so one hub is never watched twice.
    /// </summary>
    public void Update()
    {
        TakeWaiting();

        if ((_group ?? StartNext()) is not { } watched) return;

        var world = GameSingleton.Existing<BattleLogicWorld>();
        var items = world?._ItemManager;

        if (items == null || !GamePhase.IsPastPrologue(world))
        {
            Finish();

            return;
        }

        var (group, waiting) = Delivery.Absorb(watched, _waiting);
        _group = group;
        _waiting = waiting.ToList();
        var seenNow = Delivery.Delivered(group.Before, group.Owners.SelectMany(owner => GameStorages.ReadHeld(items, owner)).ToList());

        switch (Delivery.Check(_seenLastTime, seenNow, Time.unscaledTime - _startedAt, LimitSeconds))
        {
            case DeliveryCheck.Wait:
                _seenLastTime = seenNow;
                break;
            case DeliveryCheck.NothingLanded:
                _log($"{Drones(group)}: nothing new arrived");
                Finish();
                break;
            case DeliveryCheck.Landed:
                PutAwaySafely(group);
                Finish();
                break;
            default:
                throw new InvalidOperationException("unknown delivery check");
        }
    }

    private void TakeWaiting()
    {
        while (_takeNext() is { } landing) _waiting.Add(landing);
    }

    private LandingGroup? StartNext()
    {
        if (_waiting.Count == 0) return null;

        _group = new LandingGroup(new[] { _waiting[0] });
        _waiting.RemoveAt(0);
        _startedAt = Time.unscaledTime;
        _seenLastTime = Array.Empty<ItemStack>();

        return _group;
    }

    private void Finish()
    {
        _group = null;
        _seenLastTime = Array.Empty<ItemStack>();
    }

    private static string Drones(LandingGroup group) =>
        string.Join(", ", group.Landings.Select(landing => $"drone {landing.DroneId} (trip type {landing.TripType})"));

    private void PutAwaySafely(LandingGroup group)
    {
        try
        {
            var line = PutAway(group);
            _log($"{Drones(group)}: {line}");
            _page.Status(line);
        }
        catch (Exception ex)
        {
            _log($"putting away the delivery of {Drones(group)} failed, it stays where it landed: {ex}");
        }
        finally
        {
            FinishEmptiedRetrieves(group);
            _game.Forget();
        }
    }

    /// <summary>The landing bags are read again here, through the same reader that ranks them, so the plan sees this frame's counts.</summary>
    private string PutAway(LandingGroup group)
    {
        if (_game.Read() is not InventoryRead.Ready ready) return "Drone delivery: storages could not be read, everything stayed where it landed.";

        var landed = group.Owners.SelectMany(owner => _game.ReadOwner(owner)).ToList();
        var delivered = Delivery.Delivered(group.Before, landed);
        var inventory = Named(ready.Inventory, group, delivered);
        var steps = Delivery.Plan(inventory, delivered, group.Owners, _game.Rank);

        return Delivery.Describe(Delivery.Place(steps, _game.Move), inventory);
    }

    /// <summary>
    /// The game only offers a retrieve while the drone holds its cargo in its own bag, so once the put-away has moved all
    /// of it out, the retrieve is finished for it. Runs after the put-away even when it failed partway, since what it did
    /// move stays moved, and reads the drones' bags as the moves left them. A failure here leaves the retrieve offered.
    /// </summary>
    private void FinishEmptiedRetrieves(LandingGroup group)
    {
        try
        {
            if (GameSingleton.Existing<BattleLogicWorld>()?._ItemManager is not { } items) return;

            var dronesHold = group.Landings.SelectMany(landing => GameStorages.ReadHeld(items, landing.DroneId)).ToList();

            foreach (var drone in Delivery.RetrievesToFinish(group, dronesHold, _retrieves.IsAwaiting)) _retrieves.Finish(drone);

            foreach (var drone in Delivery.RetrievesDone(group, _retrieves.IsAwaiting)) _retrieves.CountRetrieve(drone);
        }
        catch (Exception ex)
        {
            _log($"could not check whether {Drones(group)} were left empty, their retrieve stays: {ex}");
        }
    }

    /// <summary>The landing bags and delivered kinds get names, so the line can say what stayed where.</summary>
    private Inventory Named(Inventory inventory, LandingGroup group, IEnumerable<ItemStack> delivered)
    {
        var storages = inventory.Storages.ToList();
        var names = inventory.ItemNames.ToDictionary(entry => entry.Key, entry => entry.Value);

        foreach (var landing in group.Landings)
        {
            if (landing.HubId is { } hub && storages.All(storage => storage.OwnerId != hub)) storages.Add(new Storage(hub, "Drone hub", StorageKind.Furniture, DroneBay.Hub));
            if (storages.All(storage => storage.OwnerId != landing.DroneId)) storages.Add(new Storage(landing.DroneId, "Drone", StorageKind.Furniture, DroneBay.Drone));
        }

        foreach (var stack in delivered) names.TryAdd(stack.ConfigId, _game.NameOf(stack.ConfigId));

        return inventory with { Storages = storages, ItemNames = names };
    }
}
