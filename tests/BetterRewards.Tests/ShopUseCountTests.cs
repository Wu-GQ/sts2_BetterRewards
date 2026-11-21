using BetterRewards.Features.Shop;
using Xunit;

namespace BetterRewards.Tests;

/// <summary>
/// Regression tests for the co-op desync that prompted this refactor.
/// <para>
/// In a co-op run every player owns a separate merchant inventory, built from their own
/// character's card pool and rolled from their own <c>PlayerRng.Shops</c>. Prices are part of
/// that per-player state. These tests lock down the invariant that one player's button presses
/// never move another player's price, which a single global counter used to violate.
/// </para>
/// </summary>
public sealed class ShopUseCountTests
{
    private const ulong PlayerA = 1001UL;
    private const ulong PlayerB = 1002UL;

    [Fact]
    public void Recording_a_use_only_advances_that_players_counts()
    {
        var counts = new BetterRewardsShopUseCount();

        counts.RecordReroll(PlayerA);

        Assert.Equal((1, 0, 0), counts.Get(PlayerA));
        Assert.Equal((0, 0, 0), counts.Get(PlayerB));
    }

    [Fact]
    public void One_players_presses_never_move_another_players_price()
    {
        var counts = new BetterRewardsShopUseCount();
        const double baseCost = 150.0;
        const double increment = 50.0;

        var bBefore = BetterRewardsShopUseCount.ComputeCost(baseCost, increment, counts.Get(PlayerB).Reroll);

        for (var i = 0; i < 5; i++)
        {
            counts.RecordReroll(PlayerA);
        }

        var bAfter = BetterRewardsShopUseCount.ComputeCost(baseCost, increment, counts.Get(PlayerB).Reroll);
        var aAfter = BetterRewardsShopUseCount.ComputeCost(baseCost, increment, counts.Get(PlayerA).Reroll);

        Assert.Equal(bBefore, bAfter);
        Assert.Equal(400, aAfter);
    }

    [Fact]
    public void Apply_overwrites_exactly_one_players_counts()
    {
        var counts = new BetterRewardsShopUseCount();
        counts.RecordReroll(PlayerB);

        counts.Apply(PlayerA, reroll: 3, relic: 2, randomItem: 1);

        Assert.Equal((3, 2, 1), counts.Get(PlayerA));
        Assert.Equal((1, 0, 0), counts.Get(PlayerB));
    }

    [Fact]
    public void Replaying_a_full_ladder_reproduces_the_actors_price()
    {
        var actor = new BetterRewardsShopUseCount();
        actor.RecordReroll(PlayerA);

        var (reroll, relic, randomItem) = actor.Get(PlayerA);

        // A peer receiving only the counts must land on the same price the actor charged.
        var peer = new BetterRewardsShopUseCount();
        peer.Apply(PlayerA, reroll, relic, randomItem);

        Assert.Equal(actor.Get(PlayerA), peer.Get(PlayerA));
    }

    [Fact]
    public void Apply_clamps_negative_counts_from_a_malformed_message()
    {
        var counts = new BetterRewardsShopUseCount();

        counts.Apply(PlayerA, reroll: -5, relic: -1, randomItem: -2);

        Assert.Equal((0, 0, 0), counts.Get(PlayerA));
    }

    [Fact]
    public void Reset_clears_every_players_counts()
    {
        var counts = new BetterRewardsShopUseCount();
        counts.RecordReroll(PlayerA);
        counts.RecordRelic(PlayerB);

        counts.Reset();

        Assert.Equal((0, 0, 0), counts.Get(PlayerA));
        Assert.Equal((0, 0, 0), counts.Get(PlayerB));
    }

    [Fact]
    public void Cost_ladder_follows_base_plus_increment_per_use()
    {
        Assert.Equal(150, BetterRewardsShopUseCount.ComputeCost(150.0, 50.0, 0));
        Assert.Equal(200, BetterRewardsShopUseCount.ComputeCost(150.0, 50.0, 1));
        Assert.Equal(400, BetterRewardsShopUseCount.ComputeCost(150.0, 50.0, 5));
    }

    [Fact]
    public void Cost_ladder_with_a_zero_increment_is_flat()
    {
        Assert.Equal(75, BetterRewardsShopUseCount.ComputeCost(75.0, 0.0, 9));
    }

    [Fact]
    public void Cost_ladder_rounds_to_the_nearest_gold()
    {
        // Math.Round is banker's rounding, matching the original price code: 162.5 -> 162.
        Assert.Equal(162, BetterRewardsShopUseCount.ComputeCost(150.0, 12.5, 1));
        Assert.Equal(163, BetterRewardsShopUseCount.ComputeCost(150.0, 13.0, 1));
    }

    [Fact]
    public void Unknown_players_read_as_zero_without_being_created()
    {
        var counts = new BetterRewardsShopUseCount();

        Assert.Equal((0, 0, 0), counts.Get(9999UL));

        // Recording for that player afterwards counts from zero, not from a half-created entry.
        counts.RecordRelic(9999UL);
        Assert.Equal((0, 1, 0), counts.Get(9999UL));
    }
}
