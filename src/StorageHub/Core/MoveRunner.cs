using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

public sealed record MoveReport(int Moved, IReadOnlyList<ItemStack> Unplaced);

public static class MoveRunner
{
    /// <summary>Tries each step's targets in order until the game accepts the stack; a full storage refuses, so the next one gets a turn.</summary>
    public static MoveReport Run(IEnumerable<MoveStep> steps, Func<ItemStack, long, bool> tryMove)
    {
        var moved = 0;
        var unplaced = new List<ItemStack>();

        foreach (var step in steps)
        {
            if (step.Targets.Any(target => tryMove(step.Stack, target))) moved++;
            else unplaced.Add(step.Stack);
        }

        return new MoveReport(moved, unplaced);
    }

    /// <summary>One line for the panel: what moved, and which items had nowhere to go.</summary>
    public static string Describe(MoveReport report, Inventory inventory)
    {
        if (report.Moved == 0 && report.Unplaced.Count == 0) return "Nothing to move.";

        var moved = $"Moved {report.Moved} stack{(report.Moved == 1 ? "" : "s")}.";

        if (report.Unplaced.Count == 0) return moved;

        var names = report.Unplaced.Select(stack => inventory.ItemName(stack.ConfigId)).Distinct().ToList();
        var shown = string.Join(", ", names.Take(5)) + (names.Count > 5 ? $" and {names.Count - 5} more" : "");

        return $"{moved} {report.Unplaced.Count} had nowhere to go: {shown}.";
    }
}
