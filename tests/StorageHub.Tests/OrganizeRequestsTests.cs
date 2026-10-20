using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

public class OrganizeRequestsTests
{
    private const long PlayerBag = 100;
    private const long Chest = 200;
    private const long OtherChest = 300;
    private static readonly GridSize BagGrid = new(6, 5);
    private static readonly GridSize ChestGrid = new(8, 4);

    private static readonly BagScreen ChestOpen = new(true, PlayerBag, BagGrid, Chest, ChestGrid);
    private static readonly BagScreen BagAlone = new(false, PlayerBag, BagGrid, 0, default);

    [Fact]
    public void Opening_a_storage_tidies_the_storage_and_never_the_players_bag()
    {
        var requests = new OrganizeRequests();

        requests.Opened(ChestOpen);

        Assert.Equal(new OrganizeRequest(Chest, PlayerBag, ChestGrid, OrganizeReason.Opened), NextFrame(requests));
    }

    [Fact]
    public void Nothing_moves_in_the_frame_the_screen_asked_and_a_request_is_handed_out_once()
    {
        var requests = new OrganizeRequests();
        requests.Opened(ChestOpen);

        Assert.Null(requests.TakeDue());
        Assert.NotNull(requests.TakeDue());
        Assert.Null(requests.TakeDue());
    }

    [Fact]
    public void Opening_the_players_bag_on_its_own_tidies_nothing()
    {
        var requests = new OrganizeRequests();

        requests.Opened(BagAlone);

        Assert.Null(NextFrame(requests));
    }

    [Fact]
    public void A_storage_is_tidied_once_per_opening_so_items_moved_by_hand_stay_put()
    {
        var requests = new OrganizeRequests();
        requests.Opened(ChestOpen);
        NextFrame(requests);

        requests.Opened(ChestOpen);

        Assert.Null(NextFrame(requests));
    }

    [Fact]
    public void Closing_and_opening_the_storage_again_tidies_it_again()
    {
        var requests = new OrganizeRequests();
        requests.Opened(ChestOpen);
        NextFrame(requests);

        requests.Closed();
        requests.Opened(ChestOpen);

        Assert.Equal(Chest, NextFrame(requests)?.OwnerId);
    }

    [Fact]
    public void Switching_to_another_storage_tidies_that_one()
    {
        var requests = new OrganizeRequests();
        requests.Opened(ChestOpen);
        NextFrame(requests);

        requests.Opened(ChestOpen with { OwnerB = OtherChest });

        Assert.Equal(OtherChest, NextFrame(requests)?.OwnerId);
    }

    [Fact]
    public void Closing_before_the_next_frame_cancels_the_tidy()
    {
        var requests = new OrganizeRequests();
        requests.Opened(ChestOpen);
        requests.TakeDue();

        requests.Closed();

        Assert.Null(NextFrame(requests));
    }

    [Fact]
    public void Sort_on_the_storage_side_repacks_the_storage()
    {
        var requests = new OrganizeRequests();

        requests.Sorted("B", ChestOpen);

        Assert.Equal(new OrganizeRequest(Chest, PlayerBag, ChestGrid, OrganizeReason.Sorted), NextFrame(requests));
    }

    [Fact]
    public void Sort_on_the_bag_side_repacks_the_players_bag_and_redraws_the_storage_beside_it()
    {
        var requests = new OrganizeRequests();

        requests.Sorted("A", ChestOpen);

        Assert.Equal(new OrganizeRequest(PlayerBag, Chest, BagGrid, OrganizeReason.Sorted), NextFrame(requests));
    }

    [Fact]
    public void Sort_with_only_the_players_bag_open_repacks_the_bag_on_its_own()
    {
        var requests = new OrganizeRequests();

        requests.Sorted("A", BagAlone);

        Assert.Equal(new OrganizeRequest(PlayerBag, PlayerBag, BagGrid, OrganizeReason.Sorted), NextFrame(requests));
    }

    [Fact]
    public void Sort_on_the_storage_side_with_no_storage_open_does_nothing_as_in_the_game()
    {
        var requests = new OrganizeRequests();

        requests.Sorted("B", BagAlone);

        Assert.Null(NextFrame(requests));
    }

    [Fact]
    public void Any_side_other_than_the_storage_sorts_the_bag_as_the_game_does()
    {
        var requests = new OrganizeRequests();

        requests.Sorted(null, ChestOpen);

        Assert.Equal(PlayerBag, NextFrame(requests)?.OwnerId);
    }

    [Fact]
    public void The_trade_sort_button_tidies_the_bag_shown_and_keeps_offered_stacks_whole()
    {
        var requests = new OrganizeRequests();

        requests.TradeSorted(PlayerBag, BagGrid, new HashSet<long> { 7, 9 });

        var request = NextFrame(requests);

        Assert.NotNull(request);
        Assert.Equal((PlayerBag, PlayerBag, BagGrid, OrganizeReason.Sorted), (request.OwnerId, request.PartnerId, request.ShownGrid, request.Reason));
        Assert.True(request.IsOnOffer(9));
        Assert.False(request.IsOnOffer(8));
    }

    [Fact]
    public void A_trade_sort_with_no_bag_shown_tidies_nothing()
    {
        var requests = new OrganizeRequests();

        requests.TradeSorted(0, BagGrid, new HashSet<long>());

        Assert.Null(NextFrame(requests));
    }

    [Fact]
    public void Outside_the_trade_window_no_stack_counts_as_offered()
    {
        var requests = new OrganizeRequests();

        requests.Sorted(null, BagAlone);

        Assert.False(NextFrame(requests)?.IsOnOffer(PlayerBag));
    }

    /// <summary>Runs the two frame ticks it takes for a request to come due and returns what the second one hands out.</summary>
    private static OrganizeRequest? NextFrame(OrganizeRequests requests)
    {
        requests.TakeDue();

        return requests.TakeDue();
    }
}
