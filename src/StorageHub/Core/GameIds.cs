namespace StorageHub.Core;

/// <summary>Ids from the game's tables that the mod uses. GameIdsTests checks each against the Data Dump.</summary>
public static class GameIds
{
    /// <summary>
    /// The "Conduct Drone trade and retrieve supplies" action (Config_Action 9010), which events 10205, 52780 and 62780
    /// ask for. Its effect is Action/RetrieveDrone, which tells the events this action's own id.
    /// </summary>
    public const int RetrieveSuppliesAction = 9010;
}
