using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class KeepListKeeperTests
{
    private static readonly DateTime Noon = new(2026, 10, 12, 12, 0, 5);

    private static KeepListKeeper Keeper(FakeKeepStore store) => new(store, () => Noon, _ => { });

    private static string Saved(KeepList list) => KeepListJson.Write(list);

    [Fact]
    public void With_no_saved_list_yet_the_list_starts_empty_and_saves()
    {
        var store = new FakeKeepStore(new StoredText.Missing());
        var keeper = Keeper(store);

        var edit = keeper.Change(list => list.With(Apple, 5));

        Assert.Equal(KeepEdit.Saved, edit);
        Assert.Equal(5, keeper.List.AmountOf(Apple));
        Assert.Equal(Saved(KeepList.Empty.With(Apple, 5)), store.Text);
    }

    [Fact]
    public void A_saved_list_is_read_and_an_edit_keeps_what_was_on_it()
    {
        var store = new FakeKeepStore(new StoredText.Found(Saved(KeepList.Empty.With(Apple, 5))));
        var keeper = Keeper(store);

        keeper.Change(list => list.With(Rope, 2));

        Assert.Equal(Saved(KeepList.Empty.With(Apple, 5).With(Rope, 2)), store.Text);
    }

    [Fact]
    public void A_file_that_is_not_a_keep_list_is_copied_aside_under_a_timestamped_name_before_the_list_starts_empty()
    {
        var store = new FakeKeepStore(new StoredText.Found("not json"));
        var keeper = Keeper(store);

        Assert.Equal(new[] { ".broken-20261012-120005" }, store.CopiedAside);
        Assert.True(keeper.IsRead);
        Assert.Empty(keeper.List.Amounts);
        Assert.Equal(KeepEdit.Saved, keeper.Change(list => list.With(Apple, 5)));
    }

    [Fact]
    public void A_file_that_is_not_a_keep_list_and_cannot_be_copied_aside_is_never_written_over()
    {
        var store = new FakeKeepStore(new StoredText.Found("not json")) { IsCopyFailing = true };
        var keeper = Keeper(store);

        var edit = keeper.Change(list => list.With(Apple, 5));

        Assert.Equal(KeepEdit.NotRead, edit);
        Assert.Equal("not json", store.Text);
    }

    [Fact]
    public void A_list_that_could_not_be_read_refuses_every_change_so_the_saved_one_is_not_overwritten()
    {
        var saved = Saved(KeepList.Empty.With(Apple, 5));
        var store = new FakeKeepStore(new StoredText.Failed("the file is in use"), saved);
        var keeper = Keeper(store);

        var edit = keeper.Change(list => list.With(Rope, 2));

        Assert.Equal(KeepEdit.NotRead, edit);
        Assert.False(keeper.IsRead);
        Assert.Equal(saved, store.Text);
        Assert.Empty(store.CopiedAside);
    }

    [Fact]
    public void A_read_that_gets_through_later_lets_the_change_apply_on_top_of_the_saved_list()
    {
        var store = new FakeKeepStore(new StoredText.Failed("the file is in use"), Saved(KeepList.Empty.With(Apple, 5)));
        var keeper = Keeper(store);
        store.Next = new StoredText.Found(Saved(KeepList.Empty.With(Apple, 5)));

        var edit = keeper.Change(list => list.With(Rope, 2));

        Assert.Equal(KeepEdit.Saved, edit);
        Assert.Equal(Saved(KeepList.Empty.With(Apple, 5).With(Rope, 2)), store.Text);
    }

    [Fact]
    public void A_list_already_read_is_not_read_again()
    {
        var store = new FakeKeepStore(new StoredText.Found(Saved(KeepList.Empty.With(Apple, 5))));
        var keeper = Keeper(store);
        store.Next = new StoredText.Failed("the file is in use");

        Assert.True(keeper.TryRead());
        Assert.Equal(5, keeper.List.AmountOf(Apple));
    }

    [Fact]
    public void A_change_that_could_not_be_saved_leaves_the_list_in_memory_as_it_was()
    {
        var store = new FakeKeepStore(new StoredText.Found(Saved(KeepList.Empty.With(Apple, 5)))) { IsWriteFailing = true };
        var keeper = Keeper(store);

        var edit = keeper.Change(list => list.Without(Apple).With(Rope, 2));

        Assert.Equal(KeepEdit.NotSaved, edit);
        Assert.Equal(5, keeper.List.AmountOf(Apple));
        Assert.False(keeper.List.Has(Rope));
    }
}

/// <summary>A keep list store in memory: Next is what the next read finds, Text is what the last write left.</summary>
public sealed class FakeKeepStore : IKeepListStore
{
    public FakeKeepStore(StoredText next, string? text = null)
    {
        Next = next;
        Text = text ?? (next is StoredText.Found found ? found.Text : null);
    }

    public StoredText Next { get; set; }

    public string? Text { get; private set; }

    public List<string> CopiedAside { get; } = new();

    public bool IsCopyFailing { get; init; }

    public bool IsWriteFailing { get; init; }

    public string Name => "storagehub.keep.json";

    public StoredText Read() => Next;

    public bool TryCopyAside(string suffix, out string why)
    {
        why = IsCopyFailing ? "the disk is full" : "";

        if (IsCopyFailing) return false;

        CopiedAside.Add(suffix);

        return true;
    }

    public bool TryWrite(string text, out string why)
    {
        why = IsWriteFailing ? "the disk is full" : "";

        if (IsWriteFailing) return false;

        Text = text;

        return true;
    }
}
