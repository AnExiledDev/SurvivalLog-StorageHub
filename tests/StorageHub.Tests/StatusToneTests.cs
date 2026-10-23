using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class StatusToneTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_move_that_left_items_behind_reads_as_a_warning_not_a_success(bool isChinese)
    {
        var line = Texts.Sentences(isChinese, Texts.Moved.Say(isChinese, 4), Texts.Nowhere.Say(isChinese, 2, "Red Apple"));

        Assert.Equal("warn", StatusTone.Of(line));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_move_with_nothing_left_behind_reads_as_a_success(bool isChinese)
    {
        Assert.Equal("ok", StatusTone.Of(Texts.Moved.Say(isChinese, 1)));
        Assert.Equal("ok", StatusTone.Of(Texts.Moved.Say(isChinese, 7)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_failure_reads_as_bad_even_beside_a_success(bool isChinese)
    {
        Assert.Equal("bad", StatusTone.Of(Texts.Failed.Pick(isChinese)));
        Assert.Equal("bad", StatusTone.Of(Texts.Sentences(isChinese, Texts.KeepNotRead.Pick(isChinese), Texts.NothingPutAway.Pick(isChinese))));
        Assert.Equal("bad", StatusTone.Of(Texts.NotAvailable.Say(isChinese, Texts.NoWorld.Pick(isChinese))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_line_that_reports_nothing_done_stays_plain(bool isChinese)
    {
        Assert.Equal("", StatusTone.Of(Texts.NothingToMove.Pick(isChinese)));
        Assert.Equal("", StatusTone.Of(Texts.NothingOnFloor.Pick(isChinese)));
        Assert.Equal("", StatusTone.Of(""));
    }
}
