using BaseLib.Config;

namespace BetterRewards.Features.Settings;

/// <summary>
/// BetterRewards configuration. BaseLib builds the settings UI from these properties,
/// loads them at startup, and saves them to the game's user://mod_configs/BetterRewards.cfg.
/// Gameplay reads the static properties directly.
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

    /// <summary>How many times the sacrifice option may be chosen.</summary>
    [ConfigSlider(1, 999, 1, Format = "{0:0}")]
    public static int SacrificeRepeatCount
    {
        get => _sacrificeRepeatCount;
        set => _sacrificeRepeatCount = Math.Max(1, value);
    }

    /// <summary>Fraction of previous run score converted to gold (0.0–100.0+).</summary>
    [ConfigSlider(0.0, 100.0, 0.05, Format = "{0:0.00}")]
    public static double ScoreToGoldPercent
    {
        get => _scoreToGoldPercent;
        set => _scoreToGoldPercent = Math.Max(0.0, value);
    }

    /// <summary>Minimum effective previous-run score (guarantees at least this much gold opportunity).</summary>
    [ConfigSlider(0, 100000, 50, Format = "{0:0}")]
    public static int MinPreviousScore
    {
        get => _minPreviousScore;
        set => _minPreviousScore = Math.Max(0, value);
    }

    // --- Shop: reroll ---

    [ConfigSection("Shop reroll")]
    [ConfigSlider(0, 9999, 5, Format = "{0:0}")]
    public static double ShopRerollBaseCost
    {
        get => _shopRerollBaseCost;
        set => _shopRerollBaseCost = Math.Max(0.0, value);
    }

    [ConfigSlider(1.0, 5.0, 0.05, Format = "{0:0.00}")]
    public static double ShopRerollCostScaling
    {
        get => _shopRerollCostScaling;
        set => _shopRerollCostScaling = Math.Max(1.0, value);
    }

    // --- Shop: random relic ---

    [ConfigSection("Random relic")]
    [ConfigSlider(0, 9999, 5, Format = "{0:0}")]
    public static double RelicBaseCost
    {
        get => _relicBaseCost;
        set => _relicBaseCost = Math.Max(0.0, value);
    }

    [ConfigSlider(1.0, 5.0, 0.05, Format = "{0:0.00}")]
    public static double RelicCostScaling
    {
        get => _relicCostScaling;
        set => _relicCostScaling = Math.Max(1.0, value);
    }

    [ConfigSection("Random relic weights", CollapsedByDefault = true)]
    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double RelicWeightCommon
    {
        get => _relicWeightCommon;
        set => _relicWeightCommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double RelicWeightUncommon
    {
        get => _relicWeightUncommon;
        set => _relicWeightUncommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double RelicWeightRare
    {
        get => _relicWeightRare;
        set => _relicWeightRare = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double RelicWeightShop
    {
        get => _relicWeightShop;
        set => _relicWeightShop = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double RelicWeightAncient
    {
        get => _relicWeightAncient;
        set => _relicWeightAncient = Math.Max(0.0, value);
    }

    // --- Shop: random item ---

    [ConfigSection("Random item")]
    [ConfigSlider(0, 9999, 5, Format = "{0:0}")]
    public static double RandomItemBaseCost
    {
        get => _randomItemBaseCost;
        set => _randomItemBaseCost = Math.Max(0.0, value);
    }

    [ConfigSlider(1.0, 5.0, 0.05, Format = "{0:0.00}")]
    public static double RandomItemCostScaling
    {
        get => _randomItemCostScaling;
        set => _randomItemCostScaling = Math.Max(1.0, value);
    }

    [ConfigSection("Item type weights", CollapsedByDefault = true)]
    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemWeightRelic
    {
        get => _itemWeightRelic;
        set => _itemWeightRelic = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemWeightCard
    {
        get => _itemWeightCard;
        set => _itemWeightCard = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemWeightPotion
    {
        get => _itemWeightPotion;
        set => _itemWeightPotion = Math.Max(0.0, value);
    }

    [ConfigSection("Card weights", CollapsedByDefault = true)]
    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double CardWeightCommon
    {
        get => _cardWeightCommon;
        set => _cardWeightCommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double CardWeightUncommon
    {
        get => _cardWeightUncommon;
        set => _cardWeightUncommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double CardWeightRare
    {
        get => _cardWeightRare;
        set => _cardWeightRare = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double CardWeightColorless
    {
        get => _cardWeightColorless;
        set => _cardWeightColorless = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double CardWeightCurse
    {
        get => _cardWeightCurse;
        set => _cardWeightCurse = Math.Max(0.0, value);
    }

    [ConfigSection("Random item relic weights", CollapsedByDefault = true)]
    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemRelicWeightCommon
    {
        get => _itemRelicWeightCommon;
        set => _itemRelicWeightCommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemRelicWeightUncommon
    {
        get => _itemRelicWeightUncommon;
        set => _itemRelicWeightUncommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemRelicWeightRare
    {
        get => _itemRelicWeightRare;
        set => _itemRelicWeightRare = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemRelicWeightShop
    {
        get => _itemRelicWeightShop;
        set => _itemRelicWeightShop = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double ItemRelicWeightAncient
    {
        get => _itemRelicWeightAncient;
        set => _itemRelicWeightAncient = Math.Max(0.0, value);
    }

    /// <summary>
    /// Preserved from the previous settings screen. The random-item potion grant still uses
    /// PotionFactory and does not read these weights.
    /// </summary>
    [ConfigSection("Potion weights", CollapsedByDefault = true)]
    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double PotionWeightCommon
    {
        get => _potionWeightCommon;
        set => _potionWeightCommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double PotionWeightUncommon
    {
        get => _potionWeightUncommon;
        set => _potionWeightUncommon = Math.Max(0.0, value);
    }

    [ConfigSlider(0, 1000, 1, Format = "{0:0}")]
    public static double PotionWeightRare
    {
        get => _potionWeightRare;
        set => _potionWeightRare = Math.Max(0.0, value);
    }

    private static double _hpSacrificePercent = 0.20;
    private static int _sacrificeRepeatCount = 4;
    private static double _scoreToGoldPercent = 0.25;
    private static int _minPreviousScore = 1000;

    private static double _shopRerollBaseCost = 150.0;
    private static double _shopRerollCostScaling = 1.10;

    private static double _relicBaseCost = 300.0;
    private static double _relicCostScaling = 1.10;
    private static double _relicWeightCommon = 10.0;
    private static double _relicWeightUncommon = 20.0;
    private static double _relicWeightRare = 35.0;
    private static double _relicWeightShop = 10.0;
    private static double _relicWeightAncient = 25.0;

    private static double _randomItemBaseCost = 75.0;
    private static double _randomItemCostScaling = 1.10;
    private static double _itemWeightRelic = 15.0;
    private static double _itemWeightCard = 50.0;
    private static double _itemWeightPotion = 35.0;
    private static double _cardWeightCommon = 55.0;
    private static double _cardWeightUncommon = 25.0;
    private static double _cardWeightRare = 10.0;
    private static double _cardWeightColorless = 5.0;
    private static double _cardWeightCurse = 5.0;
    private static double _itemRelicWeightCommon = 50.0;
    private static double _itemRelicWeightUncommon = 33.0;
    private static double _itemRelicWeightRare = 17.0;
    private static double _itemRelicWeightShop = 5.0;
    private static double _itemRelicWeightAncient = 2.0;
    private static double _potionWeightCommon = 65.0;
    private static double _potionWeightUncommon = 25.0;
    private static double _potionWeightRare = 10.0;
}
