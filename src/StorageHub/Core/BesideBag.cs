using System;

namespace StorageHub.Core;

/// <summary>Where an Open from the home storage window is on its way to the bag screen.</summary>
public abstract record BesideBagStep
{
    private BesideBagStep()
    {
    }

    public sealed record Idle : BesideBagStep;

    public sealed record WaitingForClose(long OwnerId, int FramesLeft) : BesideBagStep;
}

public enum BesideBagCommand
{
    None,
    CloseBackpack,
    AskForBags,
    ReportStuck,
}

/// <param name="OwnerId">The storage to show beside the bag, or -1 for the bag alone; read for AskForBags.</param>
public sealed record BesideBagTick(BesideBagStep Next, BesideBagCommand Command, long OwnerId);

/// <summary>
/// Shows a storage beside the bag, one frame at a time. The bag screen binds its state once when it opens, so a new
/// pair of bags only shows on a fresh open, and the game's bag request is skipped while the screen still shows. An
/// open screen is closed first, and the bags are asked for once it is down. The wait is bounded, so an Open can never
/// hang.
/// </summary>
public static class BesideBag
{
    public const int CloseFrameLimit = 60;

    public static BesideBagTick Start(long ownerId, bool isBackpackShowing) => isBackpackShowing
        ? new BesideBagTick(new BesideBagStep.WaitingForClose(ownerId, CloseFrameLimit), BesideBagCommand.CloseBackpack, ownerId)
        : Done(BesideBagCommand.AskForBags, ownerId);

    public static BesideBagTick Advance(BesideBagStep step, bool isBackpackShowing) => step switch
    {
        BesideBagStep.Idle => Done(BesideBagCommand.None, -1),
        BesideBagStep.WaitingForClose waiting => AwaitClose(waiting, isBackpackShowing),
        _ => throw new InvalidOperationException("unhandled beside-bag step " + step),
    };

    private static BesideBagTick AwaitClose(BesideBagStep.WaitingForClose waiting, bool isBackpackShowing)
    {
        if (!isBackpackShowing) return Done(BesideBagCommand.AskForBags, waiting.OwnerId);
        if (waiting.FramesLeft <= 1) return Done(BesideBagCommand.ReportStuck, waiting.OwnerId);

        return new BesideBagTick(waiting with { FramesLeft = waiting.FramesLeft - 1 }, BesideBagCommand.None, waiting.OwnerId);
    }

    private static BesideBagTick Done(BesideBagCommand command, long ownerId) => new(new BesideBagStep.Idle(), command, ownerId);
}
