using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

/// <summary>Which rows the basket list marks as wanted: only what the game's own wish strip would show her asking for.</summary>
public class BasketWishTests
{
    private const int Food = BasketWish.FoodCategory;
    private const int Tools = 5;
    private const int Cloth = 7;
    private const int Medicine = 9;

    private static BasketWish Today(bool isPending = false, bool isAway = false, bool isDying = false, int? crisis = null) =>
        new(true, isPending, isAway, isDying, new[] { Tools, Cloth }, crisis);

    [Fact]
    public void Todays_categories_are_wanted_and_others_are_not()
    {
        Assert.True(Today().Wants(Tools));
        Assert.True(Today().Wants(Cloth));
        Assert.False(Today().Wants(Medicine));
    }

    [Fact]
    public void A_crisis_adds_its_category()
    {
        Assert.True(Today(crisis: Medicine).Wants(Medicine));
    }

    [Fact]
    public void While_she_is_dying_only_food_is_wanted()
    {
        Assert.True(Today(isDying: true).Wants(Food));
        Assert.False(Today(isDying: true).Wants(Tools));
    }

    [Fact]
    public void Nothing_is_wanted_while_the_wish_is_not_shown_yet_the_basket_is_away_or_there_is_no_rescue()
    {
        Assert.False(Today(isPending: true).Wants(Tools));
        Assert.False(Today(isAway: true).Wants(Tools));
        Assert.False(BasketWish.None.Wants(Food));
        Assert.False((Today() with { IsActive = false }).Wants(Tools));
    }
}
