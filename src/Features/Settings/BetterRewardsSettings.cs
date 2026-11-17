using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace BetterRewards.Features.Settings;

/// <summary>
/// BetterRewards configuration. RitsuLib owns the settings UI and persists the
/// local model to a global <c>settings.json</c>. Gameplay reads the static
/// properties, which follow the host snapshot in a multiplayer run and the
/// local settings otherwise.
/// Random relic and random item buttons follow the regular merchant shop for rarity odds
/// and item type ratios of 3 relics / 7 cards / 3 potions. Their gold cost uses a
/// configurable base price that increases by a fixed amount after each purchase.
/// </summary>
internal static class BetterRewardsSettings
{
    public const string ModId = "BetterRewards";
    public const string DataKey = "settings";
    public const string RunDataKey = "settings";
    public const string SettingsFileName = "settings.json";

    internal static readonly string[] ConfigSectionNames =
    [
        "Event",
        "Shop reroll",
        "Random relic",
        "Random item",
    ];

    public static RunSavedData<BetterRewardsSettingsModel> RunSettings = null!;

    public static BetterRewardsSettingsModel Local =>
        ModDataStore.For(ModId).Get<BetterRewardsSettingsModel>(DataKey);

    public static BetterRewardsSettingsModel Current
    {
        get
        {
            if (TryGetMultiplayerRunSettings(out var snapshot))
            {
                return snapshot;
            }

            return Local;
        }
    }

    public static double HpSacrificePercent => Current.HpSacrificePercent;
    public static int SacrificeRepeatCount => Current.SacrificeRepeatCount;
    public static double ScoreToGoldPercent => Current.ScoreToGoldPercent;
    public static int MinPreviousScore => Current.MinPreviousScore;
    public static double ShopRerollBaseCost => Current.ShopRerollBaseCost;
    public static double ShopRerollCostIncrement => Current.ShopRerollCostIncrement;
    public static double RelicBaseCost => Current.RelicBaseCost;
    public static double RelicCostIncrement => Current.RelicCostIncrement;
    public static double RandomItemBaseCost => Current.RandomItemBaseCost;
    public static double RandomItemCostIncrement => Current.RandomItemCostIncrement;

    public static void Register()
    {
        BetterRewardsLocalization.TryMerge();

        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            ModDataStore.For(ModId).Register(
                key: DataKey,
                fileName: SettingsFileName,
                scope: SaveScope.Global,
                defaultFactory: static () => new BetterRewardsSettingsModel(),
                autoCreateIfMissing: true);

            RunSettings = RitsuLibFramework.GetRunSavedDataStore(ModId).Register(
                key: RunDataKey,
                defaultFactory: static () => new BetterRewardsSettingsModel(),
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true,
                });
        }

        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(OnPreparing);
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataLobbyStagingEvent>(OnLobbyStaging);
    }

    private static bool _pageRegistered;

    internal static void RegisterPageIfNeeded()
    {
        if (_pageRegistered)
        {
            return;
        }

        RegisterPage();
        _pageRegistered = true;
    }

    private static void RegisterPage()
    {
        var hpSacrificePercent = BindDouble(static s => s.HpSacrificePercent, static (s, v) => s.HpSacrificePercent = v);
        var sacrificeRepeatCount = BindInt(static s => s.SacrificeRepeatCount, static (s, v) => s.SacrificeRepeatCount = v);
        var scoreToGoldPercent = BindDouble(static s => s.ScoreToGoldPercent, static (s, v) => s.ScoreToGoldPercent = v);
        var minPreviousScore = BindInt(static s => s.MinPreviousScore, static (s, v) => s.MinPreviousScore = v);
        var shopRerollBaseCost = BindDouble(static s => s.ShopRerollBaseCost, static (s, v) => s.ShopRerollBaseCost = v);
        var shopRerollCostIncrement = BindDouble(static s => s.ShopRerollCostIncrement, static (s, v) => s.ShopRerollCostIncrement = v);
        var relicBaseCost = BindDouble(static s => s.RelicBaseCost, static (s, v) => s.RelicBaseCost = v);
        var relicCostIncrement = BindDouble(static s => s.RelicCostIncrement, static (s, v) => s.RelicCostIncrement = v);
        var randomItemBaseCost = BindDouble(static s => s.RandomItemBaseCost, static (s, v) => s.RandomItemBaseCost = v);
        var randomItemCostIncrement = BindDouble(static s => s.RandomItemCostIncrement, static (s, v) => s.RandomItemCostIncrement = v);

        RitsuLibFramework.RegisterModSettings(ModId, page => page
            .WithTitle(Title("BETTERREWARDS.mod_title", "Better Rewards"))
            .WithModDisplayName(Title("BETTERREWARDS.mod_title", "Better Rewards"))
            .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("event", section => section
                .WithTitle(Title("BETTERREWARDS-EVENT.title", "Event"))
                .AddSlider("hp_sacrifice_percent", Title("BETTERREWARDS-HP_SACRIFICE_PERCENT.title", "HP Sacrifice %"), hpSacrificePercent,
                    minValue: 0.0, maxValue: 1.0, step: 0.01,
                    valueFormatter: static v => v.ToString("0.00"),
                    description: Desc("HP_SACRIFICE_PERCENT", "Fraction of your current HP lost per sacrifice (0.0–1.0). Capped so a single choice cannot exceed 1/N of your max HP."))
                .AddIntSlider("sacrifice_repeat_count", Title("BETTERREWARDS-SACRIFICE_REPEAT_COUNT.title", "Sacrifice Repeats"), sacrificeRepeatCount,
                    minValue: 1, maxValue: 10, step: 1,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("SACRIFICE_REPEAT_COUNT", "How many times you may sacrifice HP for gold."))
                .AddSlider("score_to_gold_percent", Title("BETTERREWARDS-SCORE_TO_GOLD_PERCENT.title", "Score → Gold %"), scoreToGoldPercent,
                    minValue: 0.0, maxValue: 1.0, step: 0.01,
                    valueFormatter: static v => v.ToString("0.00"),
                    description: Desc("SCORE_TO_GOLD_PERCENT", "Fraction of your previous run's score converted to gold per sacrifice (e.g. 0.25 = 25% of score per sacrifice)."))
                .AddIntSlider("min_previous_score", Title("BETTERREWARDS-MIN_PREVIOUS_SCORE.title", "Minimum Previous Score"), minPreviousScore,
                    minValue: 1000, maxValue: 10000, step: 50,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("MIN_PREVIOUS_SCORE", "The effective previous-run score is at least this value, guaranteeing a minimum gold opportunity even on the first run.")))
            .AddSection("shop_reroll", section => section
                .WithTitle(Title("BETTERREWARDS-SHOP_REROLL.title", "Shop reroll"))
                .AddSlider("shop_reroll_base_cost", Title("BETTERREWARDS-SHOP_REROLL_BASE_COST.title", "Reroll Base Cost"), shopRerollBaseCost,
                    minValue: 50.0, maxValue: 1000.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("SHOP_REROLL_BASE_COST", "Gold cost for the first shop reroll."))
                .AddSlider("shop_reroll_cost_increment", Title("BETTERREWARDS-SHOP_REROLL_COST_INCREMENT.title", "Reroll Cost Increment"), shopRerollCostIncrement,
                    minValue: 0.0, maxValue: 500.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("SHOP_REROLL_COST_INCREMENT", "Gold added to the reroll cost after each use (e.g. 50 adds 50 gold each time).")))
            .AddSection("random_relic", section => section
                .WithTitle(Title("BETTERREWARDS-RANDOM_RELIC.title", "Random relic"))
                .AddSlider("relic_base_cost", Title("BETTERREWARDS-RELIC_BASE_COST.title", "Random Relic Base Cost"), relicBaseCost,
                    minValue: 50.0, maxValue: 1000.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("RELIC_BASE_COST", "Gold cost for the first random relic purchase."))
                .AddSlider("relic_cost_increment", Title("BETTERREWARDS-RELIC_COST_INCREMENT.title", "Random Relic Cost Increment"), relicCostIncrement,
                    minValue: 0.0, maxValue: 500.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("RELIC_COST_INCREMENT", "Gold added to the relic cost after each purchase (e.g. 50 adds 50 gold each time).")))
            .AddSection("random_item", section => section
                .WithTitle(Title("BETTERREWARDS-RANDOM_ITEM.title", "Random item"))
                .AddSlider("random_item_base_cost", Title("BETTERREWARDS-RANDOM_ITEM_BASE_COST.title", "Random Item Base Cost"), randomItemBaseCost,
                    minValue: 50.0, maxValue: 1000.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("RANDOM_ITEM_BASE_COST", "Gold cost for the first random item purchase."))
                .AddSlider("random_item_cost_increment", Title("BETTERREWARDS-RANDOM_ITEM_COST_INCREMENT.title", "Random Item Cost Increment"), randomItemCostIncrement,
                    minValue: 0.0, maxValue: 500.0, step: 5.0,
                    valueFormatter: static v => v.ToString("0"),
                    description: Desc("RANDOM_ITEM_COST_INCREMENT", "Gold added to the random item cost after each purchase (e.g. 25 adds 25 gold each time)."))));
    }

    private static void OnPreparing(RunSavedDataPreparingEvent evt)
    {
        if (!IsHostEvent(evt))
        {
            return;
        }

        var local = Local;
        RunSettings.Modify(evt.RunState, data => data.CopyFrom(local));
    }

    private static void OnLobbyStaging(RunSavedDataLobbyStagingEvent evt)
    {
        if (!IsHostEvent(evt))
        {
            return;
        }

        if (evt.Reason is not (RunSavedDataLobbyStagingReason.PlayerJoined or RunSavedDataLobbyStagingReason.Committing))
        {
            return;
        }

        var local = Local;
        RunSettings.Lobby.Modify(evt.Lobby, data => data.CopyFrom(local));
    }

    private static bool TryGetMultiplayerRunSettings(out BetterRewardsSettingsModel snapshot)
    {
        snapshot = null!;
        if (RunManager.Instance?.DebugOnlyGetState() is not RunState run)
        {
            return false;
        }

        if (run.Players.Count <= 1)
        {
            return false;
        }

        return RunSettings is not null && RunSettings.TryGet(run, out snapshot);
    }

    private static bool IsHostEvent(object evt)
    {
        var type = evt.GetType();
        foreach (var name in new[] { "IsHost", "IsAuthoritative", "IsServer" })
        {
            var property = type.GetProperty(name);
            if (property?.PropertyType == typeof(bool) && property.GetValue(evt) is bool value)
            {
                return value;
            }
        }

        // Preparing/lobby events are documented as the authoritative write window.
        return true;
    }

    private static ModSettingsValueBinding<BetterRewardsSettingsModel, double> BindDouble(
        Func<BetterRewardsSettingsModel, double> getter,
        Action<BetterRewardsSettingsModel, double> setter)
    {
        return new ModSettingsValueBinding<BetterRewardsSettingsModel, double>(
            ModId, DataKey, SaveScope.Global, getter, setter);
    }

    private static ModSettingsValueBinding<BetterRewardsSettingsModel, int> BindInt(
        Func<BetterRewardsSettingsModel, int> getter,
        Action<BetterRewardsSettingsModel, int> setter)
    {
        return new ModSettingsValueBinding<BetterRewardsSettingsModel, int>(
            ModId, DataKey, SaveScope.Global, getter, setter);
    }

    private static ModSettingsText Title(string key, string fallback) =>
        ModSettingsText.LocString(BetterRewardsLocalization.SettingsTable, key, fallback);

    private static ModSettingsText Desc(string propertySlug, string fallback) =>
        ModSettingsText.LocString(
            BetterRewardsLocalization.SettingsTable,
            $"BETTERREWARDS-{propertySlug}.hover.desc",
            fallback);
}

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
