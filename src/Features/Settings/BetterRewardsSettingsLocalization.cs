using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace BetterRewards.Features.Settings;

/// <summary>
/// Injects settings labels into the game's settings_ui table. BaseLib looks those keys up
/// when it builds the config screen. The mod does not ship them in a Godot pack, matching
/// how event text is registered in code.
/// </summary>
internal static class BetterRewardsSettingsLocalization
{
    internal const string ResourceName = "BetterRewards.SettingsUiLocalization";

    private static bool _logged;

    public static void TryMerge()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Log.Warn("[BetterRewards] Embedded settings localization resource is missing.");
                return;
            }

            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            LocManager.Instance.GetTable("settings_ui").MergeWith(entries);
            if (!_logged)
            {
                _logged = true;
                Log.Info($"[BetterRewards] Merged {entries.Count} settings_ui strings for BaseLib config labels.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Settings localization is not ready yet. {ex.Message}");
        }
    }
}

/// <summary>
/// Localization tables are not always ready during mod init. Merge again when the main menu
/// is up, before the player can open BaseLib's config screen.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class BetterRewardsSettingsLocalizationPatch
{
    public static void Postfix()
    {
        BetterRewardsSettingsLocalization.TryMerge();
    }
}
