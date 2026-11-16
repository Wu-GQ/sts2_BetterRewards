using BetterRewards.Features.Settings;

namespace BetterRewards.Features.Shop;

/// <summary>
/// Per-run state for the special shop buttons. Reroll cost scales up each use.
/// Random relic and random item charge official merchant prices instead of a scaling fee.
/// Reset when a new BetterRewards shop visit starts.
/// </summary>
internal static class BetterRewardsShopState
{
    public static int RerollUseCount { get; private set; }

    public static int CurrentRerollCost => ComputeCost(BetterRewardsSettings.ShopRerollBaseCost, BetterRewardsSettings.ShopRerollCostScaling, RerollUseCount);

    public static void Reset()
    {
        RerollUseCount = 0;
        BetterRewardsShopButtonsFeature.ClearPreparedOffers();
    }

    public static void RecordRerollUse() => RerollUseCount++;

    private static int ComputeCost(double baseCost, double scaling, int uses)
    {
        // cost = baseCost * scaling^uses, rounded to nearest int
        return (int)Math.Round(baseCost * Math.Pow(scaling, uses));
    }
}
