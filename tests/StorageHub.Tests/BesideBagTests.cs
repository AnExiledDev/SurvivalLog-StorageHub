using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class BesideBagTests
{
    private const long Fridge = 1001;

    [Fact]
    public void With_the_bag_screen_down_the_bags_are_asked_for_at_once()
    {
        var tick = BesideBag.Start(Fridge, isBackpackShowing: false);

        Assert.Equal(BesideBagCommand.AskForBags, tick.Command);
        Assert.Equal(Fridge, tick.OwnerId);
        Assert.IsType<BesideBagStep.Idle>(tick.Next);
    }

    [Fact]
    public void An_open_bag_screen_is_closed_before_the_bags_are_asked_for()
    {
        var start = BesideBag.Start(Fridge, isBackpackShowing: true);
        var stillOpen = BesideBag.Advance(start.Next, isBackpackShowing: true);
        var closed = BesideBag.Advance(stillOpen.Next, isBackpackShowing: false);

        Assert.Equal(BesideBagCommand.CloseBackpack, start.Command);
        Assert.Equal(BesideBagCommand.None, stillOpen.Command);
        Assert.Equal(BesideBagCommand.AskForBags, closed.Command);
        Assert.Equal(Fridge, closed.OwnerId);
        Assert.IsType<BesideBagStep.Idle>(closed.Next);
    }

    [Fact]
    public void Closing_the_storage_reopens_the_bag_alone()
    {
        var start = BesideBag.Start(-1, isBackpackShowing: true);
        var closed = BesideBag.Advance(start.Next, isBackpackShowing: false);

        Assert.Equal(-1, closed.OwnerId);
        Assert.Equal(BesideBagCommand.AskForBags, closed.Command);
    }

    [Fact]
    public void A_bag_screen_that_never_closes_is_given_up_and_reported()
    {
        var step = BesideBag.Start(Fridge, isBackpackShowing: true).Next;
        var command = BesideBagCommand.None;

        for (var frame = 0; frame < BesideBag.CloseFrameLimit && command == BesideBagCommand.None; frame++)
        {
            var tick = BesideBag.Advance(step, isBackpackShowing: true);
            step = tick.Next;
            command = tick.Command;
        }

        Assert.Equal(BesideBagCommand.ReportStuck, command);
        Assert.IsType<BesideBagStep.Idle>(step);
    }

    [Fact]
    public void Nothing_is_sent_while_no_open_is_under_way()
    {
        var tick = BesideBag.Advance(new BesideBagStep.Idle(), isBackpackShowing: true);

        Assert.Equal(BesideBagCommand.None, tick.Command);
    }
}
