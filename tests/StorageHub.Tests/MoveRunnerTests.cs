using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class MoveRunnerTests
{
    /// <summary>A game that refuses moves into full storages and records where each stack ended up.</summary>
    private sealed class FakeGame
    {
        public HashSet<long> Full { get; } = new();
        public Dictionary<long, long> Placed { get; } = new();

        public bool TryMove(ItemStack stack, long target)
        {
            if (Full.Contains(target)) return false;

            Placed[stack.ItemId] = target;

            return true;
        }
    }

    [Fact]
    public void A_full_storage_passes_the_stack_on_to_the_next_best_one()
    {
        var game = new FakeGame { Full = { Fridge.OwnerId } };
        var apple = Stack(1, Apple, 1, Bag);

        var report = MoveRunner.Run(new[] { new MoveStep(apple, new[] { Fridge.OwnerId, Box.OwnerId }) }, game.TryMove);

        Assert.Equal(1, report.Moved);
        Assert.Equal(Box.OwnerId, game.Placed[apple.ItemId]);
    }

    [Fact]
    public void A_stack_every_storage_refuses_or_none_wants_stays_put_and_is_reported()
    {
        var game = new FakeGame { Full = { Fridge.OwnerId } };
        var apple = Stack(1, Apple, 1, Bag);
        var rope = Stack(2, Rope, 1, Bag);

        var report = MoveRunner.Run(new[] { new MoveStep(apple, new[] { Fridge.OwnerId }), new MoveStep(rope, Array.Empty<long>()) }, game.TryMove);

        Assert.Equal(0, report.Moved);
        Assert.Equal(new[] { apple, rope }, report.Unplaced);
        Assert.Empty(game.Placed);
    }

    [Fact]
    public void The_summary_names_what_had_nowhere_to_go_once_each()
    {
        var home = Home();
        var report = new MoveReport(3, new[] { Stack(1, Rope, 1, Bag), Stack(2, Rope, 1, Bag), Stack(3, Apple, 1, Bag) });

        Assert.Equal("Moved 3 stacks. 3 had nowhere to go: Rope, Red Apple.", MoveRunner.Describe(report, home));
        Assert.Equal("Moved 1 stack.", MoveRunner.Describe(new MoveReport(1, Array.Empty<ItemStack>()), home));
        Assert.Equal("Nothing to move.", MoveRunner.Describe(new MoveReport(0, Array.Empty<ItemStack>()), home));
    }
}
