using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StorageHub.Adapters;

/// <summary>
/// Finishes a drone's retrieve once a delivery put-away has emptied its bag, so the drone stops offering to retrieve
/// nothing and drops its marker. The steps are the game's own Retrieve supplies (RetrieveDroneCargo) without opening
/// the drone's bag, plus the hub panel refresh its automatic unload sends. Never throws into the game: a failure is
/// logged and the drone is left as it was.
/// </summary>
internal sealed class DroneRetrieves
{
    /// <summary>
    /// The tag the game's retrieve sets. Its only reader drops the highlight from the drone button on furniture other than
    /// the hub (Furniture.BuildSingleBtnData); the hub's own buttons follow the trip, which ResetTrip clears.
    /// </summary>
    private const string CargoRetrievedTag = "StrangerTradeCargoRetrieved";

    /// <summary>The game's "Conduct Drone trade and retrieve supplies" action (Config_Action 9010), which events 10205, 52780 and 62780 ask for.</summary>
    private const int RetrieveSuppliesActionId = 9010;

    private readonly Action<string> _log;

    public DroneRetrieves(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Whether the drone is back, its cargo settled into its own bag, and waits for the player to retrieve it.</summary>
    public bool IsAwaiting(long droneId)
    {
        try
        {
            return GameSingleton.Existing<BattleLogicWorld>()?._StrangerTradeActiveManager is { IsLocked: false } trade && trade.IsDroneReturned(droneId);
        }
        catch (Exception ex)
        {
            _log($"could not tell whether drone {droneId} waits for a retrieve, it is left as it is: {ex.Message}");

            return false;
        }
    }

    /// <summary>
    /// Whether the drone surely no longer waits for a retrieve. Unlike IsAwaiting, a failed or locked read answers no, so
    /// a drone the player may still retrieve is never counted twice.
    /// </summary>
    public bool IsSurelyRetrieved(long droneId)
    {
        try
        {
            return GameSingleton.Existing<BattleLogicWorld>()?._StrangerTradeActiveManager is { IsLocked: false } trade && !trade.IsDroneReturned(droneId);
        }
        catch (Exception ex)
        {
            _log($"could not tell whether drone {droneId} was retrieved, it does not count toward events: {ex.Message}");

            return false;
        }
    }

    /// <summary>Finishes the retrieve of a drone that is back and waiting for one; any other drone is left alone.</summary>
    public void Finish(long droneId)
    {
        try
        {
            if (TryFinish(droneId)) _log($"drone {droneId} is empty, its retrieve is done");
        }
        catch (Exception ex)
        {
            _log($"finishing the retrieve of drone {droneId} failed, it may still offer one: {ex}");
        }
    }

    private bool TryFinish(long droneId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var trade = world?._StrangerTradeActiveManager;
        var drone = StrangerTradeActiveManager.GetDroneById(droneId);
        var trip = drone == null ? null : StrangerTradeActiveManager.TripOf(drone);

        if (world == null || trade == null || trade.IsLocked || drone == null || trip == null || !trade.IsDroneReturned(droneId)) return false;

        trade.RecordHubUnloaded(trip.HubLogId, true);
        trip.ResetTrip();
        trade.SyncBusyTags();

        if (world._TagManager is { } tags) StrangerTradeActiveManager.SetTag(tags, CargoRetrievedTag, true);

        HideDroneCue(drone, droneId);
        world._PhoneSMSManager?.TryRestoreRosterCue(drone);
        trade.PushDroneHubUpdate();

        return true;
    }

    /// <summary>
    /// Tells the game's events that this drone's supplies were retrieved, as its own Retrieve supplies does
    /// (AE_RetrieveDroneCargo calls DynamicEventManager.OnActionCompleted). Without it an event such as "Conduct Drone
    /// trade and retrieve supplies" can never finish once cargo is unloaded for the player. With no such event pending the
    /// game counts nothing.
    /// </summary>
    public void CountRetrieve(long droneId)
    {
        try
        {
            if (GameSingleton.Existing<BattleLogicWorld>()?._DynamicEventManager is not { } events) return;

            events.OnActionCompleted(RetrieveSuppliesActionId);
            _log($"drone {droneId}: reported its retrieve (action {RetrieveSuppliesActionId}) to events");
        }
        catch (Exception ex)
        {
            _log($"counting the retrieve of drone {droneId} toward events failed, an event asking for one may stay open: {ex.Message}");
        }
    }

    /// <summary>
    /// Kept in the game's order, before the roster cue comes back, since that cue goes to the same drone, but caught on its
    /// own so a failure here cannot skip the roster cue or the hub refresh.
    /// </summary>
    private void HideDroneCue(Furniture drone, long droneId)
    {
        try
        {
            EventDispatcherTools.NotifyInstance<string>(drone.ShowInstanceId, EnumNotify_Furniture.FurnitureHideDirectCue, GameKey.StrangerTrade_DroneCue);
        }
        catch (Exception ex)
        {
            _log($"hiding the cue over drone {droneId} failed, it may stay shown:{ex.Message}");
        }
    }
}
