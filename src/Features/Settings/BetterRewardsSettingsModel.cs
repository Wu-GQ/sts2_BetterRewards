namespace BetterRewards.Features.Settings;

/// <summary>
/// The persisted BetterRewards settings model.
/// <para>
/// Deliberately dependency-free: no Godot, sts2 or RitsuLib types are referenced here, so the
/// defaults and the clamp ranges can be exercised by the pure-logic test suite. The values below
/// are the shipped defaults and must not change when settings wiring changes.
/// </para>
/// </summary>
internal sealed class BetterRewardsSettingsModel
{
    public double HpSacrificePercent { get; set; } = 0.20;
    public int SacrificeRepeatCount { get; set; } = 4;
    public double ScoreToGoldPercent { get; set; } = 0.25;
    public int MinPreviousScore { get; set; } = 1000;
    public double ShopRerollBaseCost { get; set; } = 150.0;
    public double ShopRerollCostIncrement { get; set; } = 50.0;
    public double RelicBaseCost { get; set; } = 300.0;
    public double RelicCostIncrement { get; set; } = 50.0;
    public double RandomItemBaseCost { get; set; } = 75.0;
    public double RandomItemCostIncrement { get; set; } = 25.0;

    public void CopyFrom(BetterRewardsSettingsModel source)
    {
        HpSacrificePercent = source.HpSacrificePercent;
        SacrificeRepeatCount = source.SacrificeRepeatCount;
        ScoreToGoldPercent = source.ScoreToGoldPercent;
        MinPreviousScore = source.MinPreviousScore;
        ShopRerollBaseCost = source.ShopRerollBaseCost;
        ShopRerollCostIncrement = source.ShopRerollCostIncrement;
        RelicBaseCost = source.RelicBaseCost;
        RelicCostIncrement = source.RelicCostIncrement;
        RandomItemBaseCost = source.RandomItemBaseCost;
        RandomItemCostIncrement = source.RandomItemCostIncrement;
        Clamp();
    }

    public void Clamp()
    {
        HpSacrificePercent = Math.Clamp(HpSacrificePercent, 0.0, 1.0);
        SacrificeRepeatCount = Math.Clamp(SacrificeRepeatCount, 1, 10);
        ScoreToGoldPercent = Math.Clamp(ScoreToGoldPercent, 0.0, 1.0);
        MinPreviousScore = Math.Clamp(MinPreviousScore, 1000, 10000);
        ShopRerollBaseCost = Math.Clamp(ShopRerollBaseCost, 50.0, 1000.0);
        ShopRerollCostIncrement = Math.Clamp(ShopRerollCostIncrement, 0.0, 500.0);
        RelicBaseCost = Math.Clamp(RelicBaseCost, 50.0, 1000.0);
        RelicCostIncrement = Math.Clamp(RelicCostIncrement, 0.0, 500.0);
        RandomItemBaseCost = Math.Clamp(RandomItemBaseCost, 50.0, 1000.0);
        RandomItemCostIncrement = Math.Clamp(RandomItemCostIncrement, 0.0, 500.0);
    }
}
