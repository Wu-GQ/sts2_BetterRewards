using BetterRewards.Features.Settings;

namespace BetterRewards.Features.Shop;

/// <summary>
/// Per-run state for the special shop buttons: the number of times each action has been used,
/// which drives that player's current price.
/// <para>
/// Counts live in <see cref="BetterRewardsShopUseCount"/>, keyed by player net id. Every
/// player has their own merchant inventory in a co-op run — it is built from
/// <c>player.Character.CardPool</c> and rolled from <c>player.PlayerRng.Shops</c> — so prices
/// are per-player too. A single global counter would make one player's reroll silently
/// change the price on everybody else's buttons.
/// </para>
/// </summary>
internal static class BetterRewardsShopState
{
    private static readonly BetterRewardsShopUseCount Counts = new();

    /// <summary>Net id whose prices the local button labels display.</summary>
    public static ulong LocalPlayerId { get; set; }

    public static int RerollUseCount => Counts.Get(LocalPlayerId).Reroll;

    public static int RelicUseCount => Counts.Get(LocalPlayerId).Relic;

    public static int RandomItemUseCount => Counts.Get(LocalPlayerId).RandomItem;

    public static int CurrentRerollCost => ComputeCost(
        BetterRewardsSettings.ShopRerollBaseCost,
        BetterRewardsSettings.ShopRerollCostIncrement,
        RerollUseCount);

    public static int CurrentRelicCost => ComputeCost(
        BetterRewardsSettings.RelicBaseCost,
        BetterRewardsSettings.RelicCostIncrement,
        RelicUseCount);

    public static int CurrentRandomItemCost => ComputeCost(
        BetterRewardsSettings.RandomItemBaseCost,
        BetterRewardsSettings.RandomItemCostIncrement,
        RandomItemUseCount);

    /// <summary>
    /// Clears every player's counts. Called when a special shop visit begins, because use
    /// counts are scoped to a single visit.
    /// </summary>
    public static void Reset() => Counts.Reset();

    public static void RecordRerollUse(ulong netId) => Counts.RecordReroll(netId);

    public static void RecordRelicUse(ulong netId) => Counts.RecordRelic(netId);

    public static void RecordRandomItemUse(ulong netId) => Counts.RecordRandomItem(netId);

    /// <summary>
    /// Overwrites one player's counts with the authoritative set carried by their action
    /// message, so every peer agrees on that player's price ladder.
    /// </summary>
    public static void ApplyUseCounts(ulong netId, int rerollUseCount, int relicUseCount, int randomItemUseCount) =>
        Counts.Apply(netId, rerollUseCount, relicUseCount, randomItemUseCount);

    public static void GetUseCounts(ulong netId, out int rerollUseCount, out int relicUseCount, out int randomItemUseCount)
    {
        var (reroll, relic, randomItem) = Counts.Get(netId);
        rerollUseCount = reroll;
        relicUseCount = relic;
        randomItemUseCount = randomItem;
    }

    private static int ComputeCost(double baseCost, double increment, int uses) =>
        BetterRewardsShopUseCount.ComputeCost(baseCost, increment, uses);
}
