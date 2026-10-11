using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class StatusLineTests
{
    private const string NotReady = "Not available right now: the game world is not loaded";

    [Fact]
    public void A_not_ready_line_is_sent_once_not_on_every_refresh()
    {
        var line = new StatusLine();

        Assert.Equal(NotReady, line.Refreshed(NotReady));
        Assert.Null(line.Refreshed(NotReady));
        Assert.Null(line.Refreshed(NotReady));
    }

    [Fact]
    public void The_not_ready_line_is_cleared_once_the_inventory_can_be_read_again()
    {
        var line = new StatusLine();
        line.Refreshed(NotReady);

        Assert.Equal("", line.Refreshed(null));
        Assert.Null(line.Refreshed(null));
    }

    [Fact]
    public void A_refresh_never_wipes_an_actions_result()
    {
        var line = new StatusLine();
        line.Result("Moved 3 stacks.");

        Assert.Null(line.Refreshed(null));
    }

    [Fact]
    public void A_repeated_result_is_still_sent_because_the_page_unlocks_its_buttons_on_it()
    {
        var line = new StatusLine();

        Assert.Equal("Moved 1 stack.", line.Result("Moved 1 stack."));
        Assert.Equal("Moved 1 stack.", line.Result("Moved 1 stack."));
    }

    [Fact]
    public void An_action_that_reported_not_ready_is_cleared_when_the_inventory_comes_back()
    {
        var line = new StatusLine();
        line.Result(NotReady);

        Assert.Null(line.Refreshed(NotReady));
        Assert.Equal("", line.Refreshed(null));
    }

    [Fact]
    public void A_freshly_drawn_panel_is_told_it_is_not_ready_even_if_it_was_before()
    {
        var line = new StatusLine();
        line.Refreshed(NotReady);

        line.Cleared();

        Assert.Equal(NotReady, line.Refreshed(NotReady));
    }
}
