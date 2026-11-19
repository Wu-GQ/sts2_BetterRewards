using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.addons.mega_text;

namespace BetterRewards.Features.Settings;

/// <summary>
/// The official mod details card reads the unlocalized manifest description.
/// After the game fills that card, swap in the existing settings_ui translation.
/// </summary>
[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
internal static class BetterRewardsModInfoLocalizationPatch
{
    internal const string FallbackDescription =
        "Adds an opening-ancient bargain where you can sacrifice HP for gold, then shop in a special store with reroll, random relics, and random items.";

    public static void Postfix(NModInfoContainer __instance, Mod mod)
    {
        try
        {
            if (!string.Equals(mod.manifest?.id, "BetterRewards", StringComparison.Ordinal))
            {
                return;
            }

            var label = __instance.GetNodeOrNull<MegaRichTextLabel>("ModDescription");
            if (label == null)
            {
                return;
            }

            var localized = ResolveDescription();
            var manifestDescription = mod.manifest?.description ?? "No description";
            if (label.Text.Contains(manifestDescription, StringComparison.Ordinal))
            {
                label.Text = label.Text.Replace(manifestDescription, localized, StringComparison.Ordinal);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed to localize the mod details description: {ex.Message}");
        }
    }

    internal static string ResolveDescription()
    {
        BetterRewardsLocalization.TryMerge();
        try
        {
            var text = new LocString(BetterRewardsLocalization.SettingsTable, "BETTERREWARDS.mod_desc")
                .GetFormattedText();
            if (string.IsNullOrWhiteSpace(text) ||
                text.Contains("BETTERREWARDS.mod_desc", StringComparison.Ordinal))
            {
                return FallbackDescription;
            }

            return text;
        }
        catch
        {
            return FallbackDescription;
        }
    }
}
