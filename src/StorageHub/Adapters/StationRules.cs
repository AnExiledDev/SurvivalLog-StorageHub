using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// What a station takes, asked of the game's own checks: the stove's ingredient rule, and the rat cage's food rule
/// (spoilage included) or a live mouse. Read only, so the bag tab hooks may call it from inside a reducer.
/// </summary>
internal static class StationRules
{
    private static readonly object Gate = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void UseLog(Action<string> log) => _log = log;

    /// <summary>False when the game's check throws, so a broken check shows nothing extra rather than the wrong items.</summary>
    public static bool Takes(StationKind kind, Data_Item item, State_Data_Item state, ConfigManager config)
    {
        try
        {
            var row = config.Get_Config_Item(item.ItemConfigId);

            if (row == null) return false;

            return kind switch
            {
                StationKind.Cooking => Reducer_Web_Cooking.IsCookableIngredient(row),
                StationKind.RatCage => Reducer_Web_RatCage.CheckFoodAdmissionSilent(item, state) || Reducer_Web_RatCage.IsIntactLiveMouse(item, row),
                StationKind.Inventory => false,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown station"),
            };
        }
        catch (Exception ex)
        {
            lock (Gate)
            {
                if (!_hasReportedFailure) _log($"the game's check of what the {StationView.KindName(kind)} takes failed, treating the item as not taken (reported once): {ex}");

                _hasReportedFailure = true;
            }

            return false;
        }
    }
}
