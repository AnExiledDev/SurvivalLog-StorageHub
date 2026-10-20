using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class OfferedStacksTests
{
    [Fact]
    public void Offered_stacks_are_read_whether_the_id_is_a_number_or_text()
    {
        var ids = OfferedStacks.Read("[{\"id\":12,\"x\":0,\"y\":1},{\"id\":\"34\",\"x\":2,\"y\":0}]");

        Assert.NotNull(ids);
        Assert.Equal(new[] { 12L, 34L }, ids.OrderBy(id => id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    public void Nothing_offered_frees_every_stack(string? json)
    {
        Assert.Empty(OfferedStacks.Read(json)!);
    }

    [Theory]
    [InlineData("{oops")]
    [InlineData("{\"id\":1}")]
    [InlineData("[{\"x\":1}]")]
    [InlineData("[{\"id\":\"abc\"}]")]
    public void An_offer_list_that_cannot_be_read_frees_no_stack(string json)
    {
        Assert.Null(OfferedStacks.Read(json));
    }
}
