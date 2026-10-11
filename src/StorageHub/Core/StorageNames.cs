using System.Collections.Generic;
using System.Linq;

namespace StorageHub.Core;

public static class StorageNames
{
    /// <summary>Numbers repeated names ("Wooden Box", "Wooden Box 2") so the panel can tell five identical boxes apart.</summary>
    public static IReadOnlyList<string> Distinct(IReadOnlyList<string> names)
    {
        var seen = new Dictionary<string, int>();

        return names
            .Select(name =>
            {
                seen[name] = seen.GetValueOrDefault(name) + 1;

                return seen[name] == 1 ? name : $"{name} {seen[name]}";
            })
            .ToList();
    }
}
