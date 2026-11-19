using BetterRewards.Features.Settings;
using Xunit;

namespace BetterRewards.Tests;

/// <summary>
/// Locks the ten BetterRewards setting defaults and their clamp ranges.
/// These values are the shipped behaviour: the reset feature must restore exactly
/// these numbers and must never widen or narrow a slider range.
/// </summary>
public sealed class SettingsDefaultsTests
{
    [Fact]
    public void Model_initializes_every_setting_to_its_shipped_default()
    {
        var model = new BetterRewardsSettingsModel();

        Assert.Equal(0.20, model.HpSacrificePercent);
        Assert.Equal(4, model.SacrificeRepeatCount);
        Assert.Equal(0.25, model.ScoreToGoldPercent);
        Assert.Equal(1000, model.MinPreviousScore);
        Assert.Equal(150.0, model.ShopRerollBaseCost);
        Assert.Equal(50.0, model.ShopRerollCostIncrement);
        Assert.Equal(300.0, model.RelicBaseCost);
        Assert.Equal(50.0, model.RelicCostIncrement);
        Assert.Equal(75.0, model.RandomItemBaseCost);
        Assert.Equal(25.0, model.RandomItemCostIncrement);
    }

    [Fact]
    public void Defaults_table_lists_exactly_ten_unique_setting_ids()
    {
        var ids = BetterRewardsSettingDefaults.Entries.Select(entry => entry.Id).ToArray();

        Assert.Equal(10, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Defaults_table_ids_match_the_settings_ui_entry_ids()
    {
        string[] expected =
        [
            "hp_sacrifice_percent",
            "sacrifice_repeat_count",
            "score_to_gold_percent",
            "min_previous_score",
            "shop_reroll_base_cost",
            "shop_reroll_cost_increment",
            "relic_base_cost",
            "relic_cost_increment",
            "random_item_base_cost",
            "random_item_cost_increment",
        ];

        Assert.Equal(expected, BetterRewardsSettingDefaults.Entries.Select(entry => entry.Id).ToArray());
    }

    [Fact]
    public void Defaults_table_matches_the_model_initializers()
    {
        var model = new BetterRewardsSettingsModel();

        Assert.Equal(model.HpSacrificePercent, BetterRewardsSettingDefaults.Double("hp_sacrifice_percent"));
        Assert.Equal(model.SacrificeRepeatCount, BetterRewardsSettingDefaults.Int("sacrifice_repeat_count"));
        Assert.Equal(model.ScoreToGoldPercent, BetterRewardsSettingDefaults.Double("score_to_gold_percent"));
        Assert.Equal(model.MinPreviousScore, BetterRewardsSettingDefaults.Int("min_previous_score"));
        Assert.Equal(model.ShopRerollBaseCost, BetterRewardsSettingDefaults.Double("shop_reroll_base_cost"));
        Assert.Equal(model.ShopRerollCostIncrement, BetterRewardsSettingDefaults.Double("shop_reroll_cost_increment"));
        Assert.Equal(model.RelicBaseCost, BetterRewardsSettingDefaults.Double("relic_base_cost"));
        Assert.Equal(model.RelicCostIncrement, BetterRewardsSettingDefaults.Double("relic_cost_increment"));
        Assert.Equal(model.RandomItemBaseCost, BetterRewardsSettingDefaults.Double("random_item_base_cost"));
        Assert.Equal(model.RandomItemCostIncrement, BetterRewardsSettingDefaults.Double("random_item_cost_increment"));
    }

    [Fact]
    public void Defaults_table_kinds_match_the_setting_types()
    {
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("hp_sacrifice_percent"));
        Assert.Equal("int", BetterRewardsSettingDefaults.KindOf("sacrifice_repeat_count"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("score_to_gold_percent"));
        Assert.Equal("int", BetterRewardsSettingDefaults.KindOf("min_previous_score"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("shop_reroll_base_cost"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("shop_reroll_cost_increment"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("relic_base_cost"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("relic_cost_increment"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("random_item_base_cost"));
        Assert.Equal("double", BetterRewardsSettingDefaults.KindOf("random_item_cost_increment"));
    }

    [Fact]
    public void Apply_restores_all_ten_settings_from_a_modified_model()
    {
        var model = new BetterRewardsSettingsModel
        {
            HpSacrificePercent = 0.9,
            SacrificeRepeatCount = 9,
            ScoreToGoldPercent = 0.9,
            MinPreviousScore = 9000,
            ShopRerollBaseCost = 900.0,
            ShopRerollCostIncrement = 400.0,
            RelicBaseCost = 900.0,
            RelicCostIncrement = 400.0,
            RandomItemBaseCost = 900.0,
            RandomItemCostIncrement = 400.0,
        };

        BetterRewardsSettingDefaults.Apply(model);

        Assert.Equal(0.20, model.HpSacrificePercent);
        Assert.Equal(4, model.SacrificeRepeatCount);
        Assert.Equal(0.25, model.ScoreToGoldPercent);
        Assert.Equal(1000, model.MinPreviousScore);
        Assert.Equal(150.0, model.ShopRerollBaseCost);
        Assert.Equal(50.0, model.ShopRerollCostIncrement);
        Assert.Equal(300.0, model.RelicBaseCost);
        Assert.Equal(50.0, model.RelicCostIncrement);
        Assert.Equal(75.0, model.RandomItemBaseCost);
        Assert.Equal(25.0, model.RandomItemCostIncrement);
    }

    [Fact]
    public void Clamp_range_for_each_setting_is_unchanged_from_the_shipped_build()
    {
        // Lower bounds: a value below the floor must clamp up to the shipped minimum.
        var low = new BetterRewardsSettingsModel
        {
            HpSacrificePercent = -5.0,
            SacrificeRepeatCount = -5,
            ScoreToGoldPercent = -5.0,
            MinPreviousScore = 0,
            ShopRerollBaseCost = 0.0,
            ShopRerollCostIncrement = -50.0,
            RelicBaseCost = 0.0,
            RelicCostIncrement = -50.0,
            RandomItemBaseCost = 0.0,
            RandomItemCostIncrement = -50.0,
        };
        low.Clamp();

        Assert.Equal(0.0, low.HpSacrificePercent);
        Assert.Equal(1, low.SacrificeRepeatCount);
        Assert.Equal(0.0, low.ScoreToGoldPercent);
        Assert.Equal(1000, low.MinPreviousScore);
        Assert.Equal(50.0, low.ShopRerollBaseCost);
        Assert.Equal(0.0, low.ShopRerollCostIncrement);
        Assert.Equal(50.0, low.RelicBaseCost);
        Assert.Equal(0.0, low.RelicCostIncrement);
        Assert.Equal(50.0, low.RandomItemBaseCost);
        Assert.Equal(0.0, low.RandomItemCostIncrement);

        // Upper bounds: a value above the ceiling must clamp down to the shipped maximum.
        var high = new BetterRewardsSettingsModel
        {
            HpSacrificePercent = 5.0,
            SacrificeRepeatCount = 99,
            ScoreToGoldPercent = 5.0,
            MinPreviousScore = 999999,
            ShopRerollBaseCost = 99999.0,
            ShopRerollCostIncrement = 99999.0,
            RelicBaseCost = 99999.0,
            RelicCostIncrement = 99999.0,
            RandomItemBaseCost = 99999.0,
            RandomItemCostIncrement = 99999.0,
        };
        high.Clamp();

        Assert.Equal(1.0, high.HpSacrificePercent);
        Assert.Equal(10, high.SacrificeRepeatCount);
        Assert.Equal(1.0, high.ScoreToGoldPercent);
        Assert.Equal(10000, high.MinPreviousScore);
        Assert.Equal(1000.0, high.ShopRerollBaseCost);
        Assert.Equal(500.0, high.ShopRerollCostIncrement);
        Assert.Equal(1000.0, high.RelicBaseCost);
        Assert.Equal(500.0, high.RelicCostIncrement);
        Assert.Equal(1000.0, high.RandomItemBaseCost);
        Assert.Equal(500.0, high.RandomItemCostIncrement);
    }

    [Fact]
    public void Defaults_are_inside_their_clamp_range()
    {
        var model = new BetterRewardsSettingsModel();
        model.Clamp();

        Assert.Equal(0.20, model.HpSacrificePercent);
        Assert.Equal(4, model.SacrificeRepeatCount);
        Assert.Equal(0.25, model.ScoreToGoldPercent);
        Assert.Equal(1000, model.MinPreviousScore);
        Assert.Equal(150.0, model.ShopRerollBaseCost);
        Assert.Equal(50.0, model.ShopRerollCostIncrement);
        Assert.Equal(300.0, model.RelicBaseCost);
        Assert.Equal(50.0, model.RelicCostIncrement);
        Assert.Equal(75.0, model.RandomItemBaseCost);
        Assert.Equal(25.0, model.RandomItemCostIncrement);
    }

    [Fact]
    public void CopyFrom_still_copies_everyfield_and_keeps_the_clamp()
    {
        var source = new BetterRewardsSettingsModel { SacrificeRepeatCount = 7, HpSacrificePercent = 0.6 };
        var target = new BetterRewardsSettingsModel();

        target.CopyFrom(source);

        Assert.Equal(0.6, target.HpSacrificePercent);
        Assert.Equal(7, target.SacrificeRepeatCount);
    }
}
