namespace BetterRewards.Features.Settings;

/// <summary>
/// One resettable setting: its stable UI entry id, its value kind and its shipped default.
/// </summary>
internal readonly record struct BetterRewardsSettingDefault(string Id, string Kind, double Value);

/// <summary>
/// Single source of truth for the ten resettable settings.
/// <para>
/// Defaults are read from a freshly constructed <see cref="BetterRewardsSettingsModel"/> so the
/// numbers are never duplicated here. Any change to the model's initializers is picked up
/// automatically, and <c>SettingsDefaultsTests</c> fails if this table and the model disagree.
/// </para>
/// </summary>
internal static class BetterRewardsSettingDefaults
{
    public static IReadOnlyList<BetterRewardsSettingDefault> Entries { get; } = Build();

    private static BetterRewardsSettingDefault[] Build()
    {
        var model = new BetterRewardsSettingsModel();
        return
        [
            new("hp_sacrifice_percent", "double", model.HpSacrificePercent),
            new("sacrifice_repeat_count", "int", model.SacrificeRepeatCount),
            new("score_to_gold_percent", "double", model.ScoreToGoldPercent),
            new("min_previous_score", "int", model.MinPreviousScore),
            new("shop_reroll_base_cost", "double", model.ShopRerollBaseCost),
            new("shop_reroll_cost_increment", "double", model.ShopRerollCostIncrement),
            new("relic_base_cost", "double", model.RelicBaseCost),
            new("relic_cost_increment", "double", model.RelicCostIncrement),
            new("random_item_base_cost", "double", model.RandomItemBaseCost),
            new("random_item_cost_increment", "double", model.RandomItemCostIncrement),
        ];
    }

    public static double Double(string id) => Lookup(id).Value;

    public static int Int(string id) => (int)Lookup(id).Value;

    public static string KindOf(string id) => Lookup(id).Kind;

    private static BetterRewardsSettingDefault Lookup(string id)
    {
        foreach (var entry in Entries)
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                return entry;

        throw new KeyNotFoundException($"Unknown BetterRewards setting id '{id}'.");
    }

    /// <summary>
    /// Writes every shipped default back onto <paramref name="target"/>.
    /// Values come from a fresh model, so this restores exactly what a new install produces.
    /// </summary>
    public static void Apply(BetterRewardsSettingsModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var defaults = new BetterRewardsSettingsModel();
        target.HpSacrificePercent = defaults.HpSacrificePercent;
        target.SacrificeRepeatCount = defaults.SacrificeRepeatCount;
        target.ScoreToGoldPercent = defaults.ScoreToGoldPercent;
        target.MinPreviousScore = defaults.MinPreviousScore;
        target.ShopRerollBaseCost = defaults.ShopRerollBaseCost;
        target.ShopRerollCostIncrement = defaults.ShopRerollCostIncrement;
        target.RelicBaseCost = defaults.RelicBaseCost;
        target.RelicCostIncrement = defaults.RelicCostIncrement;
        target.RandomItemBaseCost = defaults.RandomItemBaseCost;
        target.RandomItemCostIncrement = defaults.RandomItemCostIncrement;
        target.Clamp();
    }
}
