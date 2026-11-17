using BetterRewards.Features.Settings;

namespace BetterRewards.Features.Shop;

/// <summary>
/// Per-run state for the special shop buttons: tracks current costs (which increase by a fixed amount each use).
/// Reset when a new BetterRewards shop visit starts.
/// </summary>
internal static class BetterRewardsShopState
{
    public static int RerollUseCount { get; private set; }
    public static int RelicUseCount { get; private set; }
    public static int RandomItemUseCount { get; private set; }

    public static int CurrentRerollCost => ComputeCost(BetterRewardsSettings.ShopRerollBaseCost, BetterRewardsSettings.ShopRerollCostIncrement, RerollUseCount);
    public static int CurrentRelicCost => ComputeCost(BetterRewardsSettings.RelicBaseCost, BetterRewardsSettings.RelicCostIncrement, RelicUseCount);
    public static int CurrentRandomItemCost => ComputeCost(BetterRewardsSettings.RandomItemBaseCost, BetterRewardsSettings.RandomItemCostIncrement, RandomItemUseCount);

    public static void Reset()
    {
        RerollUseCount = 0;
        RelicUseCount = 0;
        RandomItemUseCount = 0;
        BetterRewardsShopButtonsFeature.ClearPreparedOffers();
    }

    public static void RecordRerollUse() => RerollUseCount++;
    public static void RecordRelicUse() => RelicUseCount++;
    public static void RecordRandomItemUse() => RandomItemUseCount++;

    private static int ComputeCost(double baseCost, double increment, int uses)
    {
        // cost = baseCost + increment * uses, rounded to nearest int
        return (int)Math.Round(baseCost + increment * uses);
    }
}
