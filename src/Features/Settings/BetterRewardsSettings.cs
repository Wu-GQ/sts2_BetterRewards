using BaseLib.Config;

namespace BetterRewards.Features.Settings;

/// <summary>
/// BetterRewards configuration. BaseLib builds the settings UI from these properties,
/// loads them at startup, and saves them to the game's user://mod_configs/BetterRewards.cfg.
/// Gameplay reads the static properties directly.
/// Random relic and random item buttons follow the regular merchant shop: rarity odds,
/// item type ratios of 3 relics / 7 cards / 3 potions, and official shop prices.
/// </summary>
[ConfigHoverTipsByDefault]
internal sealed class BetterRewardsSettings : SimpleModConfig
{
    public const string ConfigModId = "BetterRewards";

    public static void Register()
    {
        BetterRewardsLocalization.TryMerge();
        ModConfigRegistry.Register(ConfigModId, new BetterRewardsSettings());
    }

    // --- HP sacrifice event ---

    /// <summary>Fraction of current HP lost per sacrifice choice (0.0–1.0).</summary>
    [ConfigSection("Event")]
    [ConfigSlider(0.0, 1.0, 0.01, Format = "{0:0.00}")]
    public static double HpSacrificePercent
    {
        get => _hpSacrificePercent;
        set => _hpSacrificePercent = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>How many times the sacrifice option may be chosen (1–10).</summary>
    [ConfigSlider(1, 10, 1, Format = "{0:0}")]
    public static int SacrificeRepeatCount
    {
        get => _sacrificeRepeatCount;
        set => _sacrificeRepeatCount = Math.Clamp(value, 1, 10);
    }

    /// <summary>Fraction of previous run score converted to gold (0.0–1.0).</summary>
    [ConfigSlider(0.0, 1.0, 0.01, Format = "{0:0.00}")]
    public static double ScoreToGoldPercent
    {
        get => _scoreToGoldPercent;
        set => _scoreToGoldPercent = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Minimum effective previous-run score (guarantees at least this much gold opportunity).</summary>
    [ConfigSlider(1000, 10000, 50, Format = "{0:0}")]
    public static int MinPreviousScore
    {
        get => _minPreviousScore;
        set => _minPreviousScore = Math.Clamp(value, 1000, 10000);
    }

    // --- Shop: reroll ---

    [ConfigSection("Shop reroll")]
    [ConfigSlider(50, 1000, 5, Format = "{0:0}")]
    public static double ShopRerollBaseCost
    {
        get => _shopRerollBaseCost;
        set => _shopRerollBaseCost = Math.Clamp(value, 50.0, 1000.0);
    }

    [ConfigSlider(1.0, 5.0, 0.05, Format = "{0:0.00}")]
    public static double ShopRerollCostScaling
    {
        get => _shopRerollCostScaling;
        set => _shopRerollCostScaling = Math.Max(1.0, value);
    }

    private static double _hpSacrificePercent = 0.20;
    private static int _sacrificeRepeatCount = 4;
    private static double _scoreToGoldPercent = 0.25;
    private static int _minPreviousScore = 1000;

    private static double _shopRerollBaseCost = 150.0;
    private static double _shopRerollCostScaling = 1.10;
}
