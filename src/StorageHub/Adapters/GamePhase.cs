using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StorageHub.Adapters;

/// <summary>Whether a loaded game is past the prologue, where the new automatic work is allowed to run.</summary>
internal static class GamePhase
{
    public static bool IsPastPrologue(BattleLogicWorld? world) => world?._GameTimeManager?.WorldStateType == WorldStateType.PostDisaster;
}
