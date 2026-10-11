using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class FreshnessTests
{
    [Fact]
    public void An_item_without_a_life_never_spoils()
    {
        Assert.Equal(Freshness.NeverSpoils, Freshness.DaysLeft(life: 0, startTime: 0, timeLeft: 0, presetShelfScale: 1, nowHours: 500));
    }

    [Fact]
    public void Days_left_are_the_hours_left_stretched_by_the_shelf_factor()
    {
        // Started at hour 100 with 48 hours on the clock, now hour 124: 24 hours left, doubled by a fridge.
        Assert.Equal(2f, Freshness.DaysLeft(life: 2, startTime: 100, timeLeft: 48, presetShelfScale: 2, nowHours: 124));
    }

    [Fact]
    public void A_missing_shelf_factor_counts_as_one_and_spoiled_food_has_zero_days()
    {
        Assert.Equal(1f, Freshness.DaysLeft(2, 0, 24, presetShelfScale: 0, nowHours: 0));
        Assert.Equal(0f, Freshness.DaysLeft(2, 0, 24, 1, nowHours: 100));
    }
}

public class StorageNamesTests
{
    [Fact]
    public void Repeated_storage_names_are_numbered_so_they_can_be_told_apart()
    {
        Assert.Equal(new[] { "Wooden Box", "Fridge", "Wooden Box 2", "Wooden Box 3" }, StorageNames.Distinct(new[] { "Wooden Box", "Fridge", "Wooden Box", "Wooden Box" }));
    }
}
