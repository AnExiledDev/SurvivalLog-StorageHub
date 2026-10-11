using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class CommandBatchTests
{
    [Fact]
    public void A_double_clicked_move_runs_once_in_a_frame()
    {
        var batch = CommandBatch.WithoutRepeatedMoves(new PageCommand[] { new PageCommand.PutAwayBag(), new PageCommand.PutAwayBag(), new PageCommand.Take(1, 200), new PageCommand.Take(1, 200) });

        Assert.Equal(new PageCommand[] { new PageCommand.PutAwayBag(), new PageCommand.Take(1, 200) }, batch);
    }

    [Fact]
    public void Different_moves_in_the_same_frame_all_run_in_order()
    {
        var commands = new PageCommand[] { new PageCommand.Take(1, 200), new PageCommand.Take(1, 300), new PageCommand.PutAway(1), new PageCommand.PutAwayFloor() };

        Assert.Equal(commands, CommandBatch.WithoutRepeatedMoves(commands));
    }

    [Fact]
    public void Focus_changes_are_never_dropped_so_the_last_one_wins()
    {
        var commands = new PageCommand[] { new PageCommand.Focus(true), new PageCommand.Focus(false), new PageCommand.Focus(true) };

        Assert.Equal(commands, CommandBatch.WithoutRepeatedMoves(commands));
    }
}
