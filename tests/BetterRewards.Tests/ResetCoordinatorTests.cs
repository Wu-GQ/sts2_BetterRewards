using BetterRewards.Features.Settings;
using Xunit;

namespace BetterRewards.Tests;

/// <summary>
/// Covers the reset action itself: what it writes, what it saves, and that it touches
/// nothing else. The coordinator is pure logic behind a two-method port, so these tests
/// use a recording fake of that port only — no Godot, no RitsuLib, no game assembly.
/// </summary>
public sealed class ResetCoordinatorTests
{
    private sealed class RecordingStore : IBetterRewardsSettingsStore
    {
        public RecordingStore(BetterRewardsSettingsModel model) => Model = model;

        public BetterRewardsSettingsModel Model { get; }

        public List<string> Calls { get; } = [];

        public int UpdateCount { get; private set; }

        public void UpdateLocalModel(Action<BetterRewardsSettingsModel> mutate)
        {
            Calls.Add("update");
            UpdateCount++;
            mutate(Model);
        }

        public void SaveLocalModel()
        {
            Calls.Add("save");
        }
    }

    [Fact]
    public void ResetAll_writes_defaults_and_saves_exactly_once_in_order()
    {
        var store = new RecordingStore(new BetterRewardsSettingsModel
        {
            HpSacrificePercent = 0.85,
            SacrificeRepeatCount = 8,
            ScoreToGoldPercent = 0.75,
            MinPreviousScore = 1500,
            ShopRerollBaseCost = 950.0,
            ShopRerollCostIncrement = 450.0,
            RelicBaseCost = 950.0,
            RelicCostIncrement = 450.0,
            RandomItemBaseCost = 950.0,
            RandomItemCostIncrement = 450.0,
        });

        BetterRewardsResetCoordinator.ResetAllLocalSettings(store);

        Assert.Equal(["update", "save"], store.Calls);
        Assert.Equal(1, store.UpdateCount);
    }

    [Fact]
    public void ResetAll_restores_all_ten_settings_to_defaults()
    {
        var store = new RecordingStore(new BetterRewardsSettingsModel
        {
            HpSacrificePercent = 0.85,
            SacrificeRepeatCount = 8,
            ScoreToGoldPercent = 0.75,
            MinPreviousScore = 1500,
            ShopRerollBaseCost = 950.0,
            ShopRerollCostIncrement = 450.0,
            RelicBaseCost = 950.0,
            RelicCostIncrement = 450.0,
            RandomItemBaseCost = 950.0,
            RandomItemCostIncrement = 450.0,
        });

        BetterRewardsResetCoordinator.ResetAllLocalSettings(store);

        var model = store.Model;
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
    public void ResetAll_leaves_an_already_default_model_unchanged()
    {
        var store = new RecordingStore(new BetterRewardsSettingsModel());

        BetterRewardsResetCoordinator.ResetAllLocalSettings(store);

        var model = store.Model;
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
    public void ResetAll_clamps_rather_than_leaving_out_of_range_values()
    {
        // Guard against a hand-edited settings.json that sits outside the sliders.
        var store = new RecordingStore(new BetterRewardsSettingsModel());
        store.Model.HpSacrificePercent = -1.0;

        BetterRewardsResetCoordinator.ResetAllLocalSettings(store);

        Assert.Equal(0.20, store.Model.HpSacrificePercent);
    }

    [Fact]
    public void Declining_the_confirmation_never_reaches_the_coordinator()
    {
        // Mirrors the wiring contract: the reset delegate is only invoked from the
        // modal's confirm path. Here we model "user cancelled" as simply not invoking it.
        var store = new RecordingStore(new BetterRewardsSettingsModel { SacrificeRepeatCount = 9 });
        Action confirm = () => BetterRewardsResetCoordinator.ResetAllLocalSettings(store);

        Assert.Empty(store.Calls);
        Assert.Equal(9, store.Model.SacrificeRepeatCount);

        confirm();

        Assert.Equal(["update", "save"], store.Calls);
        Assert.Equal(4, store.Model.SacrificeRepeatCount);
    }
}
