using BetterRewards.Features.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace BetterRewards.Core;

public static class ModBootstrap
{
    private const string HarmonyId = "betterrewards.harmony";
    private const string BuildMarker = "2026-11-15-baselib";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Log.Info($"[BetterRewards] Mod loaded. build={BuildMarker}");

        BetterRewardsSettings.Register();

        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
    }
}
