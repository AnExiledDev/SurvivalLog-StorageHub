using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StorageHub.Adapters;

/// <summary>
/// Finishes a drone's retrieve once a delivery put-away has emptied its bag, so the drone stops offering to retrieve
/// nothing and drops its marker. The steps are the game's own Retrieve supplies (RetrieveDroneCargo) without opening
/// the drone's bag, plus the hub panel refresh its automatic unload sends. Never throws into the game: a failure is
/// logged and the drone is left offering its retrieve, as the game left it.
/// </summary>
internal sealed class DroneRetrieves
{
    /// <summary>The tag the game's retrieve sets, which stops the hub's button from asking for a retrieve.</summary>
    private const string CargoRetrievedTag = "StrangerTradeCargoRetrieved";

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

    private static bool TryFinish(long droneId)
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var trade = world?._StrangerTradeActiveManager;
        var drone = StrangerTradeActiveManager.GetDroneById(droneId);
        var trip = drone == null ? null : StrangerTradeActiveManager.TripOf(drone);

        if (world == null || trade == null || trade.IsLocked || drone == null || trip == null) return false;

        trade.RecordHubUnloaded(trip.HubLogId, true);
        trip.ResetTrip();
        trade.SyncBusyTags();

        if (world._TagManager is { } tags) StrangerTradeActiveManager.SetTag(tags, CargoRetrievedTag, true);

        EventDispatcherTools.NotifyInstance<string>(drone.ShowInstanceId, EnumNotify_Furniture.FurnitureHideDirectCue, GameKey.StrangerTrade_DroneCue);
        world._PhoneSMSManager?.TryRestoreRosterCue(drone);
        trade.PushDroneHubUpdate();

        return true;
    }
}
