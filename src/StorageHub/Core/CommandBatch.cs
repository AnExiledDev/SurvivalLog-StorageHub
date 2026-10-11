using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// The commands one frame should run. A double click queues the same move twice before the first one runs; only
/// moves are collapsed, because a focus that flips on, off and on again must still end up on.
/// </summary>
public static class CommandBatch
{
    public static IReadOnlyList<PageCommand> WithoutRepeatedMoves(IEnumerable<PageCommand> commands)
    {
        var seenMoves = new HashSet<PageCommand>();

        return commands.Where(command => !IsMove(command) || seenMoves.Add(command)).ToList();
    }

    private static bool IsMove(PageCommand command) =>
        command is PageCommand.Take or PageCommand.TakeAll or PageCommand.PutAway or PageCommand.PutAwayBag or PageCommand.PutAwayFloor;
}
