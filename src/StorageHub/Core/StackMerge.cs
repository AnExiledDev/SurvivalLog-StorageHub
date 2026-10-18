using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// What a stack would lose by merging: how long the item has before it spoils, and the uses it holds. A copy of
/// Stacking's Freshness record (sl-stacking, src/Stacking/Core/Freshness.cs), so the two mods agree on which food may
/// share a stack and Storage Hub still works without Stacking installed.
/// </summary>
public readonly record struct StackFreshness(int StartTime, int TimeLeft, float TimeLeftFrac, float TimeScale, int UseTimes, int MaxUseTimes)
{
    /// <summary>The game gives an item that never spoils no time left on its clock.</summary>
    public bool Spoils => TimeLeft != 0;

    /// <summary>The hour on the game clock at which it spoils at its current rate.</summary>
    public float ExpiresAt => StartTime + TimeLeft + TimeLeftFrac;

    public bool HoldsSeveralUses => UseTimes > 1 || MaxUseTimes > 1;
}

/// <summary>
/// Stacking's rule for which stacks may become one, mirrored from StackIdentity in sl-stacking
/// src/Stacking/Core/Freshness.cs (CanShareStack, CanJoin, HaveSameUses, ShelfHoursLeft) and the never-spoiling exception
/// in src/Stacking/Core/StackLimits.cs (NeverSpoiling). Keep the two in step: a change there is a change here.
/// </summary>
public static class MergeRule
{
    /// <summary>
    /// Shelf life closer than this counts as the same. Adding an item to a bag drops the fraction of an hour the game
    /// keeps on a stack, so two halves of one batch can differ by up to an hour.
    /// </summary>
    public const float ToleranceHours = 1f;

    /// <summary>A kind whose Life is 0 or less never spoils, in any category, so its stacks have no freshness to lose.</summary>
    public static bool IsNeverSpoiling(int life) => life <= 0;

    public static bool CanShareStack(StackFreshness incoming, StackFreshness stack, float nowHours)
    {
        if (!HaveSameUses(incoming, stack) || incoming.Spoils != stack.Spoils) return false;

        if (!incoming.Spoils) return true;

        if (incoming.TimeScale <= 0f || stack.TimeScale <= 0f)
        {
            return incoming.TimeScale == stack.TimeScale && Math.Abs(incoming.ExpiresAt - stack.ExpiresAt) <= ToleranceHours;
        }

        return Math.Abs(ShelfHoursLeft(incoming, nowHours) - ShelfHoursLeft(stack, nowHours)) <= ToleranceHours;
    }

    /// <summary>
    /// Whether one stack may pour into another. A never-spoiling kind only needs matching uses, whatever hour each stack
    /// was picked up; anything else needs the game clock to be known and the freshness rule to hold.
    /// </summary>
    public static bool CanMerge(StackFreshness source, StackFreshness target, float? nowHours, bool neverSpoils)
    {
        if (neverSpoils) return HaveSameUses(source, target);

        return nowHours is { } hours && CanShareStack(source, target, hours);
    }

    /// <summary>A single-use item reads 0 or 1 depending on which game path made it; only items with several uses carry information here.</summary>
    private static bool HaveSameUses(StackFreshness a, StackFreshness b)
    {
        if (!a.HoldsSeveralUses && !b.HoldsSeveralUses) return true;

        return a.UseTimes == b.UseTimes && a.MaxUseTimes == b.MaxUseTimes;
    }

    /// <summary>Clock hours to expiry times the spoil rate where the item sits, as the game's own portion merge measures it.</summary>
    private static float ShelfHoursLeft(StackFreshness item, float nowHours) => (item.ExpiresAt - nowHours) * item.TimeScale;
}

/// <summary>
/// One stack in a bag as the merge planner sees it. Cap is the game's stack limit for the kind. Traits carries every
/// per-item value the freshness rule does not cover (see ItemTraits.Key); two stacks only merge when their traits match exactly. TouchesMold is true when any cell under the
/// stack is moldy, and such a stack is never touched, the same way the tidy keeps mold cells to the game.
/// </summary>
public sealed record MergeStack(long Id, int ConfigId, int Count, int Cap, StackFreshness Freshness, string Traits, bool NeverSpoils, bool TouchesMold);

/// <summary>
/// Pour Units from the source stack into the target. The counts each stack must hold just before the step are part of
/// it, so a step planned against a bag that has since changed is skipped instead of written. EmptiesSource means the
/// whole source goes, and the game removes it rather than leaving a stack of 0.
/// </summary>
public sealed record MergeStep(long SourceId, long TargetId, int ConfigId, int Units, int SourceBefore, int TargetBefore, int Cap)
{
    public bool EmptiesSource => Units == SourceBefore;
}

/// <summary>
/// What reading one stack's count found. Gone and Unreadable must stay apart: a stack that is gone has given up its
/// units, while one that could not be read may still hold them.
/// </summary>
public abstract record StackRead
{
    public sealed record Count(int Units) : StackRead;

    /// <summary>The stack is no longer in the bag.</summary>
    public sealed record Gone : StackRead;

    /// <summary>The read itself failed, so the stack may or may not still be there.</summary>
    public sealed record Unreadable(string Why) : StackRead;
}

/// <summary>The game's item counts in one bag. Every write reports false when it threw; the runner reads every count back regardless.</summary>
public interface IStackCounts
{
    StackRead CountOf(long stackId);

    bool TryIncrease(long stackId, int units);

    bool TryDecrease(long stackId, int units);

    bool TryRemove(long stackId);
}

public abstract record MergeOutcome(MergeStep Step)
{
    public sealed record Merged(MergeStep Step) : MergeOutcome(Step);

    /// <summary>The bag no longer matched the plan, so nothing was written.</summary>
    public sealed record Skipped(MergeStep Step, string Why) : MergeOutcome(Step);

    /// <summary>A write went wrong and both stacks were put back to their counts before the step.</summary>
    public sealed record RolledBack(MergeStep Step, string Why) : MergeOutcome(Step);

    /// <summary>A write went wrong and putting it back did too. Why names both stacks' counts as last read.</summary>
    public sealed record Broken(MergeStep Step, string Why) : MergeOutcome(Step);
}

public sealed record MergeReport(IReadOnlyList<MergeOutcome> Outcomes)
{
    public int Merged => Outcomes.Count(outcome => outcome is MergeOutcome.Merged);

    /// <summary>True when anything was written, even if it was then undone, so the bag screen must be redrawn.</summary>
    public bool WroteAnything => Outcomes.Any(outcome => outcome is not MergeOutcome.Skipped);

    /// <summary>The first outcome that stopped the run, if one did.</summary>
    public MergeOutcome? Failure => Outcomes.FirstOrDefault(outcome => outcome is MergeOutcome.RolledBack or MergeOutcome.Broken);
}

public static class StackMerge
{
    /// <summary>
    /// Fills the fullest stacks of each kind from the emptiest. Only stacks below their cap, off mold, with the same
    /// traits and passing the freshness rule pour into each other, and no stack is filled past its cap. A stack that has
    /// given is never filled and one that has been filled never gives, so units only ever move one way.
    /// </summary>
    public static IReadOnlyList<MergeStep> Plan(IReadOnlyList<MergeStack> stacks, float? nowHours)
    {
        var steps = new List<MergeStep>();

        foreach (var kind in stacks.Where(CanTakePart).GroupBy(stack => (stack.ConfigId, stack.Traits)))
        {
            steps.AddRange(PlanKind(kind.OrderByDescending(stack => stack.Count).ThenBy(stack => stack.Id).ToList(), nowHours));
        }

        return steps;
    }

    /// <summary>
    /// For each kind with two or more stacks that could take part, why some of them stay apart after the plan: the
    /// traits that differ, or, when the traits match, that their freshness or uses do. Kinds the plan joins fully are
    /// left out. Each line starts with the kind's config id, so the caller can log a kind once.
    /// </summary>
    public static IReadOnlyList<(int ConfigId, string Why)> WhyKeptApart(IReadOnlyList<MergeStack> stacks, IReadOnlyList<MergeStep> steps)
    {
        var count = stacks.ToDictionary(stack => stack.Id, stack => stack.Count);

        foreach (var step in steps)
        {
            count[step.SourceId] -= step.Units;
            count[step.TargetId] += step.Units;
        }

        var reasons = new List<(int, string)>();

        foreach (var kind in stacks.Where(CanTakePart).GroupBy(stack => stack.ConfigId))
        {
            var partial = kind.Where(stack => count[stack.Id] > 0 && count[stack.Id] < stack.Cap).ToList();

            if (partial.Count < 2) continue;

            var differing = ItemTraits.Differing(partial.Select(stack => stack.Traits));
            var why = differing.Count > 0 ? $"{string.Join(", ", differing)} differ" : "freshness or uses differ";

            reasons.Add((kind.Key, $"{partial.Count} part stacks stay apart: {why}"));
        }

        return reasons;
    }

    /// <summary>
    /// Runs the steps in order. Each is checked against the counts the plan expects, then the target is raised, then the
    /// source is lowered or removed, and every count is read back after each write. A failure undoes that step and
    /// stops the run, so at most one step is ever in doubt.
    /// </summary>
    public static MergeReport Run(IReadOnlyList<MergeStep> steps, IStackCounts counts)
    {
        var outcomes = new List<MergeOutcome>();

        foreach (var step in steps)
        {
            var outcome = RunStep(step, counts);

            outcomes.Add(outcome);

            if (outcome is MergeOutcome.RolledBack or MergeOutcome.Broken) break;
        }

        return new MergeReport(outcomes);
    }

    /// <summary>One line for the log: how many merges landed and, if one failed, what happened.</summary>
    public static string Describe(MergeReport report)
    {
        var merged = report.Merged == 1 ? "merged 1 stack" : $"merged {report.Merged} stacks";
        var skipped = report.Outcomes.Count(outcome => outcome is MergeOutcome.Skipped);
        var line = skipped == 0 ? merged : $"{merged}, skipped {skipped} the bag no longer matched";

        return report.Failure switch
        {
            MergeOutcome.RolledBack failed => $"{line}; stopped: {failed.Why}, both stacks put back",
            MergeOutcome.Broken failed => $"{line}; stopped: {failed.Why}, and putting them back failed",
            _ => line,
        };
    }

    private static bool CanTakePart(MergeStack stack) => !stack.TouchesMold && stack.Cap > 1 && stack.Count > 0 && stack.Count < stack.Cap;

    private static IEnumerable<MergeStep> PlanKind(IReadOnlyList<MergeStack> fullestFirst, float? nowHours)
    {
        var count = fullestFirst.ToDictionary(stack => stack.Id, stack => stack.Count);
        var gave = new HashSet<long>();
        var took = new HashSet<long>();

        foreach (var source in fullestFirst.Reverse())
        {
            if (took.Contains(source.Id)) continue;

            foreach (var target in fullestFirst)
            {
                if (count[source.Id] == 0) break;

                if (target.Id == source.Id || gave.Contains(target.Id) || !MergeRule.CanMerge(source.Freshness, target.Freshness, nowHours, source.NeverSpoils)) continue;

                var room = Math.Min(target.Cap, source.Cap) - count[target.Id];

                if (room <= 0) continue;

                var units = Math.Min(room, count[source.Id]);

                yield return new MergeStep(source.Id, target.Id, source.ConfigId, units, count[source.Id], count[target.Id], target.Cap);

                count[source.Id] -= units;
                count[target.Id] += units;
                gave.Add(source.Id);
                took.Add(target.Id);
            }
        }
    }

    private static MergeOutcome RunStep(MergeStep step, IStackCounts counts)
    {
        if (WhyStale(step, counts) is { } stale) return new MergeOutcome.Skipped(step, stale);

        var raised = counts.TryIncrease(step.TargetId, step.Units);

        if (!raised || !Holds(counts, step.TargetId, step.TargetBefore + step.Units))
        {
            return Undo(step, counts, $"raising stack {step.TargetId} by {step.Units} did not land");
        }

        return step.EmptiesSource ? RemoveSource(step, counts) : LowerSource(step, counts);
    }

    private static string? WhyStale(MergeStep step, IStackCounts counts)
    {
        var source = counts.CountOf(step.SourceId);
        var target = counts.CountOf(step.TargetId);

        if (!IsCount(source, step.SourceBefore) || !IsCount(target, step.TargetBefore)) return $"stack {step.SourceId} holds {Show(source)} and {step.TargetId} holds {Show(target)}, the plan expected {step.SourceBefore} and {step.TargetBefore}";

        if (step.Units <= 0 || step.Units > step.SourceBefore || step.TargetBefore + step.Units > step.Cap) return $"pouring {step.Units} would break a stack limit of {step.Cap}";

        return null;
    }

    /// <summary>
    /// Removing a whole source: once it is gone its units are in the target, whatever the call reported. A source that
    /// cannot be read may still hold its units, so that is undone like any other failure.
    /// </summary>
    private static MergeOutcome RemoveSource(MergeStep step, IStackCounts counts)
    {
        counts.TryRemove(step.SourceId);

        return counts.CountOf(step.SourceId) is StackRead.Gone
            ? new MergeOutcome.Merged(step)
            : Undo(step, counts, $"removing stack {step.SourceId} did not land");
    }

    private static MergeOutcome LowerSource(MergeStep step, IStackCounts counts)
    {
        var lowered = counts.TryDecrease(step.SourceId, step.Units);

        return lowered && Holds(counts, step.SourceId, step.SourceBefore - step.Units)
            ? new MergeOutcome.Merged(step)
            : Undo(step, counts, $"lowering stack {step.SourceId} by {step.Units} did not land");
    }

    /// <summary>
    /// Puts both stacks back to their counts before the step, then reads them to say whether that worked. A source that
    /// cannot be read may already have given its units to the target, so nothing is written then: lowering the target
    /// could lose those units, and a duplicate is the lesser harm.
    /// </summary>
    private static MergeOutcome Undo(MergeStep step, IStackCounts counts, string why)
    {
        if (counts.CountOf(step.SourceId) is StackRead.Unreadable) return Broken(step, counts, $"{why}, and stack {step.SourceId} could not be read so nothing was put back");

        var sourceBack = RestoreTo(step.SourceId, step.SourceBefore, counts);
        var targetBack = RestoreTo(step.TargetId, step.TargetBefore, counts);

        if (sourceBack && targetBack) return new MergeOutcome.RolledBack(step, why);

        return Broken(step, counts, why);
    }

    private static MergeOutcome.Broken Broken(MergeStep step, IStackCounts counts, string why) =>
        new(step, $"{why}; stack {step.SourceId} now holds {Show(counts.CountOf(step.SourceId))} (was {step.SourceBefore}), stack {step.TargetId} holds {Show(counts.CountOf(step.TargetId))} (was {step.TargetBefore})");

    private static bool RestoreTo(long stackId, int wanted, IStackCounts counts)
    {
        if (counts.CountOf(stackId) is not StackRead.Count { Units: var now }) return false;

        if (now < wanted) counts.TryIncrease(stackId, wanted - now);
        else if (now > wanted) counts.TryDecrease(stackId, now - wanted);

        return Holds(counts, stackId, wanted);
    }

    private static bool Holds(IStackCounts counts, long stackId, int units) => IsCount(counts.CountOf(stackId), units);

    private static bool IsCount(StackRead read, int units) => read is StackRead.Count count && count.Units == units;

    private static string Show(StackRead read) => read switch
    {
        StackRead.Count count => count.Units.ToString(CultureInfo.InvariantCulture),
        StackRead.Gone => "nothing",
        StackRead.Unreadable unreadable => $"unreadable ({unreadable.Why})",
        _ => throw new InvalidOperationException("unknown stack read"),
    };
}
