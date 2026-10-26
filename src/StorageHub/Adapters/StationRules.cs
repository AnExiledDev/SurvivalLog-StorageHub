using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// What a station takes, asked of the game's own checks: the stove's ingredient rule, the rat cage's food rule
/// (spoilage included) or a live mouse, the brewing and dyeing ingredient flags, the shredder's list, the ration press's
/// food rule, the drone trade's value, and the workbench's recipe materials. Read only, so the bag tab hooks may call it from inside a reducer.
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
            var row = GameStorages.ItemRow(config, item.ItemConfigId);

            if (row == null) return false;

            return kind switch
            {
                StationKind.Cooking => Reducer_Web_Cooking.IsCookableIngredient(row),
                StationKind.RatCage => Reducer_Web_RatCage.CheckFoodAdmissionSilent(item, state) || Reducer_Web_RatCage.IsIntactLiveMouse(item, row),
                StationKind.Brew => row.CanBrew,
                StationKind.Dye => config.IsPlantProduct(item.ItemConfigId),
                StationKind.Shredder => config.IsShredableItem(item.ItemConfigId),
                StationKind.Compressor => IsPressable(row, item.ItemConfigId),
                StationKind.Trade => row.TradeValue > 0,
                StationKind.Workbench => RecipeMaterials(config).Contains(item.ItemConfigId),
                StationKind.Basket => !NeighborRules.IsBasketForbidItem(item.ItemConfigId),
                StationKind.Storage => true,
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

    private static HashSet<int>? _recipeMaterials;
    private static ConfigManager? _recipeMaterialsSource;

    /// <summary>
    /// Every item some usable recipe asks for. The recipe table never changes while the game runs, so it is read once
    /// per config instance and kept.
    /// </summary>
    private static HashSet<int> RecipeMaterials(ConfigManager config)
    {
        lock (Gate)
        {
            if (_recipeMaterials != null && ReferenceEquals(_recipeMaterialsSource, config)) return _recipeMaterials;

            var materials = new HashSet<int>();
            var recipes = config._Config_ProductionList_Dict;

            if (recipes != null)
            {
                foreach (var recipe in recipes.Values)
                {
                    if (recipe == null || !recipe.IsUseable || recipe.MaterialList == null) continue;

                    for (var i = 0; i < recipe.MaterialList.Count; i++) materials.Add(recipe.MaterialList[i]);
                }
            }

            _recipeMaterials = materials;
            _recipeMaterialsSource = config;

            return materials;
        }
    }

    /// <summary>
    /// The ration press's own refusal checks (Reducer_Web_Compressor's deny text): food only, neither of the two liquid
    /// sub-categories, nothing the press made itself, and nothing on its banned list. Spoiled food is left for the
    /// press to refuse, as spoilage changes while the tab is open.
    /// </summary>
    private static bool IsPressable(Config_Item row, int configId) =>
        row.Category == FoodCategory
        && row.SubCategory != LiquidSubCategory
        && row.SubCategory != OtherLiquidSubCategory
        && !CompressorConfig.IsCompressProduct(configId)
        && !CompressorConfig.IsBannedItem(configId);

    private const int FoodCategory = 1;
    private const int LiquidSubCategory = 9;
    private const int OtherLiquidSubCategory = 10;
}
