using StorageHub.Core;
using Xunit;
using static StorageHub.Tests.Fixtures;

namespace StorageHub.Tests;

public class MergeRuleTests
{
    private const float Now = 100f;

    [Fact]
    public void Food_spoiling_within_the_hour_of_each_other_may_share_a_stack()
    {
        Assert.True(MergeRule.CanMerge(Fresh(130.4f), Fresh(130f), Now, neverSpoils: false));
    }

    [Fact]
    public void Food_more_than_an_hour_apart_keeps_its_own_stack_so_old_food_never_hides_in_new()
    {
        Assert.False(MergeRule.CanMerge(Fresh(124f), Fresh(130f), Now, neverSpoils: false));
    }

    [Fact]
    public void Food_is_never_merged_while_the_game_clock_is_unknown()
    {
        Assert.False(MergeRule.CanMerge(Fresh(130f), Fresh(130f), nowHours: null, neverSpoils: false));
    }

    [Fact]
    public void Food_that_never_spoils_does_not_merge_with_food_that_does()
    {
        Assert.False(MergeRule.CanMerge(Fresh(130f), Lasting(), Now, neverSpoils: false));
        Assert.True(MergeRule.CanMerge(Lasting(), Lasting(), Now, neverSpoils: false));
    }

    [Fact]
    public void Food_whose_clock_is_stopped_only_merges_with_food_stopped_at_the_same_point()
    {
        var frozen = Fresh(130f) with { TimeScale = 0f };

        Assert.True(MergeRule.CanMerge(frozen, frozen with { TimeLeftFrac = 0.5f }, Now, neverSpoils: false));
        Assert.False(MergeRule.CanMerge(frozen, Fresh(130f), Now, neverSpoils: false));
        Assert.False(MergeRule.CanMerge(frozen, frozen with { TimeLeft = 40 }, Now, neverSpoils: false));
    }

    [Fact]
    public void Items_with_several_uses_left_merge_only_with_the_same_uses_left()
    {
        var full = Fresh(130f) with { UseTimes = 3, MaxUseTimes = 3 };

        Assert.True(MergeRule.CanMerge(full, full, Now, neverSpoils: false));
        Assert.False(MergeRule.CanMerge(full, full with { UseTimes = 2 }, Now, neverSpoils: false));
    }

    [Fact]
    public void Never_spoiling_seeds_merge_whatever_their_clocks_say_and_even_when_the_clock_is_unknown()
    {
        Assert.True(MergeRule.CanMerge(Fresh(10f), Fresh(500f), nowHours: null, neverSpoils: true));
        Assert.True(MergeRule.IsNeverSpoiling(MergeRule.SeedCategory, life: 0));
        Assert.False(MergeRule.IsNeverSpoiling(MergeRule.SeedCategory, life: 48));
        Assert.False(MergeRule.IsNeverSpoiling(category: 2, life: 0));
    }

    internal static StackFreshness Fresh(float expiresAtHour) => new(0, (int)expiresAtHour, expiresAtHour - (int)expiresAtHour, 1f, 1, 1);

    internal static StackFreshness Lasting() => new(0, 0, 0f, 1f, 1, 1);
}

public class StackMergePlanTests
{
    private const float Now = 100f;

    [Fact]
    public void Partial_stacks_of_one_kind_fill_the_fullest_first_and_the_emptiest_disappears()
    {
        var bag = new FakeBag(Food(1, Apple, 7), Food(2, Apple, 5), Food(3, Apple, 4));

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag);

        Assert.Null(report.Failure);
        Assert.Equal(new Dictionary<long, int> { [1] = 10, [2] = 6 }, bag.Counts);
    }

    [Fact]
    public void Full_stacks_and_stacks_of_unstackable_kinds_are_left_out()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 10), Food(2, Apple, 3), Food(3, Rope, 1, cap: 1), Food(4, Rope, 1, cap: 1) }, Now);

        Assert.Empty(steps);
    }

    [Fact]
    public void A_stack_on_a_mold_cell_is_never_touched()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 4), Food(2, Apple, 3) with { TouchesMold = true } }, Now);

        Assert.Empty(steps);
    }

    [Fact]
    public void Stacks_whose_other_traits_differ_stay_apart()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 4), Food(2, Apple, 3) with { Traits = "polluted" } }, Now);

        Assert.Empty(steps);
    }

    [Fact]
    public void Food_too_far_apart_in_freshness_stays_apart_while_matching_food_merges()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 4), Food(2, Apple, 3) with { Freshness = MergeRuleTests.Fresh(160f) }, Food(3, Apple, 2) }, Now);

        Assert.Equal(new[] { (3L, 1L, 2) }, steps.Select(step => (step.SourceId, step.TargetId, step.Units)));
    }

    [Fact]
    public void Different_kinds_never_pour_into_each_other()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 4), Food(2, RedBerry, 3) }, Now);

        Assert.Empty(steps);
    }

    [Fact]
    public void Units_only_move_one_way_a_stack_that_gave_is_never_filled_and_one_that_was_filled_never_gives()
    {
        var steps = StackMerge.Plan(new[] { Food(1, Apple, 6), Food(2, Apple, 6), Food(3, Apple, 6), Food(4, Apple, 6) }, Now);
        var sources = steps.Select(step => step.SourceId).ToHashSet();
        var targets = steps.Select(step => step.TargetId).ToHashSet();

        Assert.NotEmpty(steps);
        Assert.Empty(sources.Intersect(targets));
    }

    internal static MergeStack Food(long id, int configId, int count, int cap = 10) =>
        new(id, configId, count, cap, MergeRuleTests.Fresh(130f), "", NeverSpoils: false, TouchesMold: false);
}

public class StackMergeRunTests
{
    private const float Now = 100f;

    /// <summary>Every fault one write can show: the game threw before writing, wrote and then threw, or said yes and wrote nothing.</summary>
    public static TheoryData<int, Fault> EveryWriteFailingEachWay()
    {
        var cases = new TheoryData<int, Fault>();
        var writes = CleanRunWrites();

        for (var write = 1; write <= writes; write++)
        {
            foreach (var fault in new[] { Fault.Refuses, Fault.LandsThenThrows, Fault.SaysYesWritesNothing }) cases.Add(write, fault);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryWriteFailingEachWay))]
    public void A_failure_at_any_write_loses_no_unit_and_fills_no_stack_past_its_cap(int failingWrite, Fault fault)
    {
        var bag = Mixed();
        var before = bag.TotalsByKind();

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag.FailingAt(failingWrite, fault));

        Assert.Equal(before, bag.TotalsByKind());
        Assert.All(bag.Counts, stack => Assert.InRange(stack.Value, 1, bag.CapOf(stack.Key)));
        Assert.DoesNotContain(report.Outcomes, outcome => outcome is MergeOutcome.Broken);
    }

    [Theory]
    [MemberData(nameof(EveryWriteFailingEachWay))]
    public void A_failed_step_is_undone_and_stops_the_run(int failingWrite, Fault fault)
    {
        var bag = Mixed();

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag.FailingAt(failingWrite, fault));

        if (report.Failure is not { } failed) return;

        Assert.Same(failed, report.Outcomes[^1]);
        Assert.Equal(failed.Step.SourceBefore, bag.CountOf(failed.Step.SourceId));
        Assert.Equal(failed.Step.TargetBefore, bag.CountOf(failed.Step.TargetId));
    }

    [Fact]
    public void When_the_game_refuses_to_lower_the_source_the_raised_target_is_lowered_back()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 5));

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag.FailingAt(2, Fault.Refuses));

        Assert.IsType<MergeOutcome.RolledBack>(report.Failure);
        Assert.Equal(new Dictionary<long, int> { [1] = 7, [2] = 5 }, bag.Counts);
    }

    [Fact]
    public void A_whole_source_that_is_gone_after_a_throw_counts_as_merged_because_its_units_are_in_the_target()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 2));

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag.FailingAt(2, Fault.LandsThenThrows));

        Assert.Null(report.Failure);
        Assert.Equal(new Dictionary<long, int> { [1] = 9 }, bag.Counts);
    }

    [Fact]
    public void When_putting_a_step_back_also_fails_the_run_says_so_with_the_counts_it_found()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 5));

        var report = StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag.FailingFrom(2, Fault.Refuses));

        var broken = Assert.IsType<MergeOutcome.Broken>(report.Failure);
        Assert.Contains("stack 1 holds 10 (was 7)", broken.Why);
        Assert.Contains("putting them back failed", StackMerge.Describe(report));
    }

    [Fact]
    public void A_step_planned_against_a_bag_that_has_since_changed_is_skipped_without_writing()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 5));
        var steps = StackMerge.Plan(bag.Stacks, Now);

        bag.Counts[2] = 4;
        var report = StackMerge.Run(steps, bag);

        Assert.IsType<MergeOutcome.Skipped>(Assert.Single(report.Outcomes));
        Assert.False(report.WroteAnything);
        Assert.Equal(new Dictionary<long, int> { [1] = 7, [2] = 4 }, bag.Counts);
    }

    [Fact]
    public void A_step_that_would_fill_past_the_cap_is_never_written()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 5));
        var overfull = new MergeStep(2, 1, Apple, Units: 5, SourceBefore: 5, TargetBefore: 7, Cap: 10);

        var report = StackMerge.Run(new[] { overfull }, bag);

        Assert.IsType<MergeOutcome.Skipped>(Assert.Single(report.Outcomes));
        Assert.Equal(new Dictionary<long, int> { [1] = 7, [2] = 5 }, bag.Counts);
    }

    [Fact]
    public void The_log_line_counts_the_merges()
    {
        var bag = new FakeBag(StackMergePlanTests.Food(1, Apple, 7), StackMergePlanTests.Food(2, Apple, 2), StackMergePlanTests.Food(3, RedBerry, 1), StackMergePlanTests.Food(4, RedBerry, 1));

        Assert.Equal("merged 2 stacks", StackMerge.Describe(StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag)));
    }

    /// <summary>Two kinds with several partial stacks each, plus stacks that must stay out: one full, one on mold, one tainted.</summary>
    private static FakeBag Mixed() => new(
        StackMergePlanTests.Food(1, Apple, 7),
        StackMergePlanTests.Food(2, Apple, 5),
        StackMergePlanTests.Food(3, Apple, 4),
        StackMergePlanTests.Food(4, Apple, 10),
        StackMergePlanTests.Food(5, Apple, 2) with { TouchesMold = true },
        StackMergePlanTests.Food(6, Apple, 3) with { Traits = "polluted" },
        StackMergePlanTests.Food(7, RedBerry, 1, cap: 5),
        StackMergePlanTests.Food(8, RedBerry, 3, cap: 5),
        StackMergePlanTests.Food(9, RedBerry, 4, cap: 5));

    private static int CleanRunWrites()
    {
        var bag = Mixed();

        StackMerge.Run(StackMerge.Plan(bag.Stacks, Now), bag);

        return bag.Writes;
    }
}

public enum Fault
{
    Refuses,
    LandsThenThrows,
    SaysYesWritesNothing,
}

/// <summary>
/// A bag's counts the way the game keeps them: a count that falls to 0 removes the stack. Writes can be made to fail at
/// one numbered write, or at every write from one on.
/// </summary>
internal sealed class FakeBag : IStackCounts
{
    private readonly Dictionary<long, int> _caps;
    private readonly Dictionary<long, int> _kinds;
    private int _failAt = int.MaxValue;
    private bool _staysBroken;
    private Fault _fault;

    public FakeBag(params MergeStack[] stacks)
    {
        Stacks = stacks;
        Counts = stacks.ToDictionary(stack => stack.Id, stack => stack.Count);
        _caps = stacks.ToDictionary(stack => stack.Id, stack => stack.Cap);
        _kinds = stacks.ToDictionary(stack => stack.Id, stack => stack.ConfigId);
    }

    public IReadOnlyList<MergeStack> Stacks { get; }

    public Dictionary<long, int> Counts { get; }

    public int Writes { get; private set; }

    public FakeBag FailingAt(int write, Fault fault)
    {
        _failAt = write;
        _fault = fault;

        return this;
    }

    public FakeBag FailingFrom(int write, Fault fault)
    {
        _staysBroken = true;

        return FailingAt(write, fault);
    }

    public int CapOf(long stackId) => _caps[stackId];

    public Dictionary<int, int> TotalsByKind() =>
        Counts.GroupBy(stack => _kinds[stack.Key]).ToDictionary(kind => kind.Key, kind => kind.Sum(stack => stack.Value));

    public int? CountOf(long stackId) => Counts.TryGetValue(stackId, out var count) ? count : null;

    public bool TryIncrease(long stackId, int units) => Write(stackId, () => Counts[stackId] += units);

    public bool TryDecrease(long stackId, int units) => Write(stackId, () =>
    {
        Counts[stackId] -= units;

        if (Counts[stackId] <= 0) Counts.Remove(stackId);
    });

    public bool TryRemove(long stackId) => Write(stackId, () => Counts.Remove(stackId));

    private bool Write(long stackId, Action apply)
    {
        Writes++;

        if (!Counts.ContainsKey(stackId)) return false;

        var fails = Writes == _failAt || (_staysBroken && Writes >= _failAt);

        if (!fails)
        {
            apply();

            return true;
        }

        switch (_fault)
        {
            case Fault.Refuses:
                return false;
            case Fault.LandsThenThrows:
                apply();
                return false;
            case Fault.SaysYesWritesNothing:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(_fault), _fault, "unknown fault");
        }
    }
}
