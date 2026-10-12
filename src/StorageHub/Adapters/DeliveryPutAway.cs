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
/// no storage takes stays where the game put it. One line per delivery goes to the log and the toast.
/// </summary>
internal sealed class DeliveryPutAway
{
    /// <summary>Long enough for a cargo handed over across several frames, short enough that a missed one is let go.</summary>
    private const float LimitSeconds = 5f;

    private readonly Func<Landing?> _takeNext;
    private readonly GameStorages _game;
    private readonly WebPage _page;
    private readonly Action<string> _log;

    private Landing? _landing;
    private float _startedAt;
    private IReadOnlyList<ItemStack> _seenLastTime = Array.Empty<ItemStack>();

    public DeliveryPutAway(Func<Landing?> takeNext, GameStorages game, WebPage page, Action<string> log)
    {
        _takeNext = takeNext;
        _game = game;
        _page = page;
        _log = log;
    }

    /// <summary>Called every frame; looks at one landing at a time until its cargo has settled, then puts it away.</summary>
    public void Update()
    {
        if ((_landing ?? StartNext()) is not { } landing) return;

        var world = GameSingleton.Existing<BattleLogicWorld>();
        var items = world?._ItemManager;

        if (items == null || !GamePhase.IsPastPrologue(world))
        {
            Finish();

            return;
        }

        var seenNow = Delivery.Delivered(landing.Before, landing.Owners.SelectMany(owner => GameStorages.ReadHeld(items, owner)).ToList());

        switch (Delivery.Check(_seenLastTime, seenNow, Time.unscaledTime - _startedAt, LimitSeconds))
        {
            case DeliveryCheck.Wait:
                _seenLastTime = seenNow;
                break;
            case DeliveryCheck.NothingLanded:
                _log($"drone {landing.DroneId}, trip type {landing.TripType}: nothing new arrived");
                Finish();
                break;
            case DeliveryCheck.Landed:
                PutAwaySafely(landing);
                Finish();
                break;
            default:
                throw new InvalidOperationException("unknown delivery check");
        }
    }

    private Landing? StartNext()
    {
        _landing = _takeNext();
        _startedAt = Time.unscaledTime;
        _seenLastTime = Array.Empty<ItemStack>();

        return _landing;
    }

    private void Finish()
    {
        _landing = null;
        _seenLastTime = Array.Empty<ItemStack>();
    }

    private void PutAwaySafely(Landing landing)
    {
        try
        {
            var line = PutAway(landing);
            _log($"drone {landing.DroneId}, trip type {landing.TripType}: {line}");
            _page.Status(line);
        }
        catch (Exception ex)
        {
            _log($"putting away drone {landing.DroneId}'s delivery failed, it stays where it landed: {ex}");
        }
        finally
        {
            _game.Forget();
        }
    }

    /// <summary>The landing bags are read again here, through the same reader that ranks them, so the plan sees this frame's counts.</summary>
    private string PutAway(Landing landing)
    {
        if (_game.Read() is not InventoryRead.Ready ready) return "Drone delivery: storages could not be read, everything stayed where it landed.";

        var landed = landing.Owners.SelectMany(owner => _game.ReadOwner(owner)).ToList();
        var delivered = Delivery.Delivered(landing.Before, landed);
        var inventory = Named(ready.Inventory, landing, delivered);
        var steps = Delivery.Plan(inventory, delivered, landing.Owners, _game.Rank);

        return Delivery.Describe(Delivery.Place(steps, _game.Move), inventory);
    }

    /// <summary>The landing bags and delivered kinds get names, so the line can say what stayed where.</summary>
    private Inventory Named(Inventory inventory, Landing landing, IEnumerable<ItemStack> delivered)
    {
        var storages = inventory.Storages.ToList();
        var names = inventory.ItemNames.ToDictionary(entry => entry.Key, entry => entry.Value);

        if (landing.HubId is { } hub && storages.All(storage => storage.OwnerId != hub)) storages.Add(new Storage(hub, "Drone hub", StorageKind.Furniture));
        if (storages.All(storage => storage.OwnerId != landing.DroneId)) storages.Add(new Storage(landing.DroneId, "Drone", StorageKind.Furniture));

        foreach (var stack in delivered) names.TryAdd(stack.ConfigId, _game.NameOf(stack.ConfigId));

        return inventory with { Storages = storages, ItemNames = names };
    }
}
