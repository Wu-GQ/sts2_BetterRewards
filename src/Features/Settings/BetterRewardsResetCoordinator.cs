namespace BetterRewards.Features.Settings;

/// <summary>
/// The minimal port the reset needs from the settings store.
/// <para>
/// Two operations only: rewrite the local model, then persist it. Keeping the surface this small
/// is what makes the reset testable without the game, and it also documents the scope by
/// construction: no run snapshot, no other mod's data, no purchase counters.
/// </para>
/// </summary>
internal interface IBetterRewardsSettingsStore
{
    /// <summary>Mutates the mod's own global local settings model in place.</summary>
    void UpdateLocalModel(Action<BetterRewardsSettingsModel> mutate);

    /// <summary>Writes the local settings model to disk immediately.</summary>
    void SaveLocalModel();
}

/// <summary>
/// Restores the mod's local settings to their shipped defaults.
/// <para>
/// Scope is intentionally narrow: only the ten BetterRewards settings in the global
/// <c>settings.json</c>. It does not touch another mod's configuration, run history or scores,
/// save files, shop purchase counters, or the in-progress multiplayer run snapshot.
/// </para>
/// </summary>
internal static class BetterRewardsResetCoordinator
{
    /// <summary>
    /// Rewrites every setting to its default and saves immediately so the result survives a
    /// crash or a settings menu that is closed straight afterwards.
    /// </summary>
    public static void ResetAllLocalSettings(IBetterRewardsSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        store.UpdateLocalModel(BetterRewardsSettingDefaults.Apply);
        store.SaveLocalModel();
    }
}
