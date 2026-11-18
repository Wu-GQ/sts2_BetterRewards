using BetterRewards.Features.Settings;
using BetterRewards.Features.Shop;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Collections.Generic;
using System.Reflection;

namespace BetterRewards.Features.Event;

/// <summary>
/// Appends the bargain to the opening ancient only.
/// AncientEventModel.GenerateInitialOptionsWrapper is sealed and is the path every ancient uses,
/// including one another mod substitutes for Neow. Priority.Last appends after other postfixes.
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
[HarmonyPriority(Priority.Last)]
internal static class OpeningAncientOptionsPatch
{
    // Cached reflection accessor for EventModel.SetEventState (protected)
    private static readonly MethodInfo? _setEventState = typeof(EventModel)
        .GetMethod("SetEventState", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPostfix]
    public static void Postfix(AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        try
        {
            var owner = __instance.Owner;
            var run = owner?.RunState;
            if (owner == null || run == null || !IsOpeningAncientVisit(run))
            {
                return;
            }

            if (!Hook.ShouldAllowAncient(run, owner, __instance))
            {
                return;
            }

            if (__result.Any(option => option.TextKey == "BETTERREWARDS.NEOW"))
            {
                return;
            }

            BetterRewardsOpeningShopTransitionState.ResetForOpeningAncient();

            var options = new List<EventOption>(__result)
            {
                BuildBargainOption(__instance)
            };
            __result = options;
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed to inject opening ancient option: {ex.Message}");
        }
    }

    internal static bool IsOpeningAncientVisit(IRunState run)
    {
        if (!run.CurrentMapCoord.HasValue)
        {
            return false;
        }

        var start = run.Map.StartingMapPoint.coord;
        var here = run.CurrentMapCoord.Value;
        return OpeningAncientRules.IsOpeningPosition(
            run.CurrentActIndex,
            run.ExtraFields.StartedWithNeow,
            here.col,
            here.row,
            start.col,
            start.row);
    }

    internal static bool IsCurrentOpeningAncientRoom(IRunState? run)
    {
        if (run == null || run.CurrentRoom is not EventRoom { CanonicalEvent: AncientEventModel })
        {
            return false;
        }

        return IsOpeningAncientVisit(run);
    }

    private static EventOption BuildBargainOption(EventModel ancient)
    {
        var previousScore    = PreviousRunScoreService.GetPreviousRunScore(ancient.Owner?.RunState);
        var hPct             = BetterRewardsSettings.HpSacrificePercent;
        var n                = BetterRewardsSettings.SacrificeRepeatCount;
        var sPct             = BetterRewardsSettings.ScoreToGoldPercent;
        var effectiveHPct    = Math.Min(hPct, 1.0 / n);
        var goldPerSacrifice = (int)Math.Floor(previousScore * sPct);

        var hpPercent = (int)(effectiveHPct * 100);
        var descriptionKey = previousScore > 0
            ? "BETTERREWARDS.NEOW.description"
            : "BETTERREWARDS.NEOW.description.no_score";

        return new EventOption(
            ancient,
            () => OnBargainChosen(ancient, effectiveHPct, n, goldPerSacrifice, sacrificesDone: 0),
            BetterRewardsLocalization.EventString("BETTERREWARDS.NEOW.title"),
            BetterRewardsLocalization.EventString(
                descriptionKey,
                ("previousScore", previousScore),
                ("n", n),
                ("hpPercent", hpPercent),
                ("goldPerSacrifice", goldPerSacrifice)),
            "BETTERREWARDS.NEOW",
            Array.Empty<IHoverTip>());
    }

    private static Task OnBargainChosen(EventModel ancient, double hPct, int maxRepeats, int goldPerSacrifice, int sacrificesDone)
    {
        var owner = ancient.Owner;
        if (owner == null)
        {
            return Task.CompletedTask;
        }

        BetterRewardsShopState.Reset();
        ShowSacrificeState(ancient, owner, hPct, maxRepeats, goldPerSacrifice, sacrificesDone);
        return Task.CompletedTask;
    }

    private static void ShowSacrificeState(EventModel ancient, MegaCrit.Sts2.Core.Entities.Players.Player owner, double hPct, int maxRepeats, int goldPerSacrifice, int sacrificesDone)
    {
        var hpCost   = (int)Math.Floor(owner.Creature.MaxHp * hPct);
        var wouldDie = hpCost >= owner.Creature.CurrentHp;
        var canMore  = sacrificesDone < maxRepeats;

        var hpPercent = (int)(hPct * 100);

        // Use the LocString-based EventOption constructor directly to avoid the string-key
        // overload's call to GetOptionTitle/GetOptionDescription, which returns null for keys
        // not in the original event data file and then crashes in AddDetailsTo(null).
        var options = new List<EventOption>();

        if (canMore && !wouldDie)
        {
            options.Add(new EventOption(
                ancient,
                async () =>
                {
                    var cost = (int)Math.Floor(owner.Creature.MaxHp * hPct);
                    await CreatureCmd.SetCurrentHp(owner.Creature, owner.Creature.CurrentHp - cost);
                    await PlayerCmd.GainGold(goldPerSacrifice, owner);
                    ShowSacrificeState(ancient, owner, hPct, maxRepeats, goldPerSacrifice, sacrificesDone + 1);
                },
                BetterRewardsLocalization.EventString("BETTERREWARDS.SACRIFICE.do.title", ("hpCost", hpCost), ("goldPerSacrifice", goldPerSacrifice), ("sacrificeIndex", sacrificesDone + 1), ("maxRepeats", maxRepeats)),
                BetterRewardsLocalization.EventString("BETTERREWARDS.SACRIFICE.do.desc", ("hpCost", hpCost), ("goldPerSacrifice", goldPerSacrifice), ("sacrificeIndex", sacrificesDone + 1), ("maxRepeats", maxRepeats)),
                "BETTERREWARDS.SACRIFICE.do",
                Array.Empty<IHoverTip>()));
        }
        else if (canMore)
        {
            // Grayed out — null OnChosen makes IsLocked = true
            options.Add(new EventOption(
                ancient,
                null,
                BetterRewardsLocalization.EventString("BETTERREWARDS.SACRIFICE.locked.title", ("hpPercent", hpPercent)),
                BetterRewardsLocalization.EventString("BETTERREWARDS.SACRIFICE.locked.desc"),
                "BETTERREWARDS.SACRIFICE.locked",
                Array.Empty<IHoverTip>()));
        }

        options.Add(new EventOption(
            ancient,
            async () =>
            {
                BetterRewardsOpeningShopTransitionState.MarkMerchantPending(owner.NetId);
                ShowMerchantPendingState(ancient);
                await TryEnterPendingMerchantRoom("Enter shop option");
            },
            BetterRewardsLocalization.EventString("BETTERREWARDS.ENTER_SHOP.title"),
            BetterRewardsLocalization.EventString("BETTERREWARDS.ENTER_SHOP.desc"),
            "BETTERREWARDS.ENTER_SHOP",
            Array.Empty<IHoverTip>()));

        CallSetEventState(ancient, BetterRewardsLocalization.EventString("BETTERREWARDS.SACRIFICE.state.desc", ("sacrificesDone", sacrificesDone), ("maxRepeats", maxRepeats), ("currentHp", owner.Creature.CurrentHp), ("maxHp", owner.Creature.MaxHp), ("goldPerSacrifice", goldPerSacrifice)), options);
    }

    private static void ShowMerchantPendingState(EventModel ancient)
    {
        NMapScreen.Instance?.SetTravelEnabled(enabled: false);

        CallSetEventState(
            ancient,
            BetterRewardsLocalization.EventString("BETTERREWARDS.SHOP_PENDING.desc"),
            new[]
            {
                new EventOption(
                    ancient,
                    null,
                    BetterRewardsLocalization.EventString("BETTERREWARDS.SHOP_PENDING.title"),
                    BetterRewardsLocalization.EventString("BETTERREWARDS.SHOP_PENDING.desc"),
                    "BETTERREWARDS.SHOP_PENDING",
                    Array.Empty<IHoverTip>())
            });
    }

    private static void CallSetEventState(EventModel ancient, LocString description, IEnumerable<EventOption> options)
    {
        if (_setEventState == null)
        {
            Log.Error("[BetterRewards] Could not find EventModel.SetEventState via reflection.");
            return;
        }

        _setEventState.Invoke(ancient, new object[] { description, options });
    }

    internal static async Task TryEnterPendingMerchantRoom(string source)
    {
        var runManager = RunManager.Instance;
        var runState = runManager.DebugOnlyGetState();
        if (!IsCurrentOpeningAncientRoom(runState))
        {
            return;
        }

        var synchronizer = runManager.EventSynchronizer;
        if (synchronizer == null || synchronizer.Events.Count == 0 || synchronizer.Events.Any(@event => !BetterRewardsOpeningShopTransitionState.IsEventReadyForMerchantTransition(@event)))
        {
            return;
        }

        if (!BetterRewardsOpeningShopTransitionState.TryBeginMerchantTransition())
        {
            return;
        }

        try
        {
            BetterRewardsShopButtonsFeature.IsActiveShop = true;
            Log.Info($"[BetterRewards] Entering special merchant room after the opening ancient from '{source}'.");
            await runManager.EnterRoom(new MerchantRoom());
        }
        catch (Exception ex)
        {
            BetterRewardsShopButtonsFeature.IsActiveShop = false;
            BetterRewardsOpeningShopTransitionState.CancelMerchantTransition();
            Log.Warn($"[BetterRewards] Failed to enter special merchant room: {ex.Message}");
        }
    }
}

internal static class BetterRewardsOpeningShopTransitionState
{
    private static readonly HashSet<ulong> PendingMerchantPlayers = [];
    private static bool _merchantTransitionStarted;

    public static void ResetForOpeningAncient()
    {
        PendingMerchantPlayers.Clear();
        _merchantTransitionStarted = false;
        BetterRewardsShopButtonsFeature.IsActiveShop = false;
    }

    public static void MarkMerchantPending(ulong playerId)
    {
        if (playerId == 0)
        {
            return;
        }

        PendingMerchantPlayers.Add(playerId);
        NMapScreen.Instance?.SetTravelEnabled(enabled: false);
        Log.Info($"[BetterRewards] Special merchant room queued after the opening ancient for player {playerId}.");
    }

    public static bool TryBeginMerchantTransition()
    {
        if (PendingMerchantPlayers.Count == 0 || _merchantTransitionStarted)
        {
            return false;
        }

        _merchantTransitionStarted = true;
        NMapScreen.Instance?.SetTravelEnabled(enabled: false);
        return true;
    }

    public static void CancelMerchantTransition()
    {
        _merchantTransitionStarted = false;
    }

    public static void ClearAfterMerchantExit()
    {
        PendingMerchantPlayers.Clear();
        _merchantTransitionStarted = false;
        BetterRewardsShopButtonsFeature.IsActiveShop = false;
    }

    public static bool ShouldBlockProceed()
    {
        return PendingMerchantPlayers.Count > 0
            && OpeningAncientOptionsPatch.IsCurrentOpeningAncientRoom(RunManager.Instance?.DebugOnlyGetState());
    }

    public static bool IsEventReadyForMerchantTransition(MegaCrit.Sts2.Core.Models.EventModel eventModel)
    {
        if (eventModel.IsFinished)
        {
            return true;
        }

        return eventModel.Owner != null && PendingMerchantPlayers.Contains(eventModel.Owner.NetId);
    }
}

/// <summary>
/// Patches NMerchantInventory._Ready to inject the BetterRewards special buttons.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory._Ready))]
internal static class NMerchantInventoryReadyPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMerchantInventory __instance)
    {
        try
        {
            BetterRewardsShopButtonsFeature.TryAttachButtons(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed to attach shop buttons: {ex.Message}");
        }
    }
}

/// <summary>
/// Patches NMerchantInventory.Initialize to subscribe RefreshLabels to all
/// PurchaseCompleted events. Initialize runs after _Ready and is when Inventory
/// is first populated, so this is the earliest safe point to access AllEntries.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class NMerchantInventoryInitializePatch
{
    [HarmonyPostfix]
    public static void Postfix(NMerchantInventory __instance)
    {
        try
        {
            BetterRewardsShopButtonsFeature.TrySubscribePurchaseEvents(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed to subscribe purchase events: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(EventRoom), "OnEventStateChanged")]
internal static class EventRoomOpeningAncientTransitionPatch
{
    [HarmonyPostfix]
    public static void Postfix(EventRoom __instance)
    {
        if (__instance.CanonicalEvent is not AncientEventModel)
        {
            return;
        }

        TaskHelper.RunSafely(OpeningAncientOptionsPatch.TryEnterPendingMerchantRoom("EventRoom.OnEventStateChanged"));
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
internal static class BetterRewardsBlockProceedWhileMerchantPendingPatch
{
    [HarmonyPrefix]
    public static bool Prefix(EventOption option)
    {
        if (!option.IsProceed || !BetterRewardsOpeningShopTransitionState.ShouldBlockProceed())
        {
            return true;
        }

        NMapScreen.Instance?.SetTravelEnabled(enabled: false);
        return false;
    }
}

[HarmonyPatch(typeof(MerchantRoom), nameof(MerchantRoom.Exit))]
internal static class BetterRewardsMerchantRoomExitPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BetterRewardsOpeningShopTransitionState.ClearAfterMerchantExit();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class BetterRewardsResetSinglePlayerRunPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BetterRewardsOpeningShopTransitionState.ResetForOpeningAncient();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class BetterRewardsResetSavedSinglePlayerRunPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BetterRewardsOpeningShopTransitionState.ResetForOpeningAncient();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class BetterRewardsResetNewMultiplayerRunPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BetterRewardsOpeningShopTransitionState.ResetForOpeningAncient();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class BetterRewardsResetSavedMultiplayerRunPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BetterRewardsOpeningShopTransitionState.ResetForOpeningAncient();
    }
}


/// <summary>
/// Shows an embedded icon on BetterRewards event options through the relic icon slot.
/// That slot, including %Outline, is what CznStyleUI styles. These are textures only:
/// no relic model is created, so nothing is added to the compendium or the player's relic list.
/// </summary>
[HarmonyPatch(typeof(NEventOptionButton), "_Ready")]
internal static class NEventOptionButtonReadyPatch
{
    private const string FallbackShopIconPath = "res://images/packed/common_ui/map/icons/map_shop.png";
    private const string NeowIconResourceName = "BetterRewards.MerchantTradeContractPng";
    private const string SacrificeIconResourceName = "BetterRewards.LifeForGoldHeartPng";
    private const string EnterShopIconResourceName = "BetterRewards.EnterShopDoorPng";
    private const string WaitMerchantIconResourceName = "BetterRewards.WaitMerchantHourglassPng";

    private static readonly Dictionary<string, Texture2D?> CustomIcons = new();

    [HarmonyPostfix]
    public static void Postfix(NEventOptionButton __instance)
    {
        try
        {
            var option = __instance.Option;
            if (option?.TextKey is not string textKey ||
                !textKey.StartsWith("BETTERREWARDS.", StringComparison.Ordinal))
            {
                return;
            }

            var icon = __instance.GetNodeOrNull<TextureRect>("%RelicIcon");
            if (icon == null)
            {
                return;
            }

            var texture = TextureFor(textKey);
            if (texture == null)
            {
                return;
            }

            icon.SetTexture(texture);
            var outline = icon.GetNodeOrNull<TextureRect>("%Outline");
            outline?.SetTexture(texture);
            icon.Visible = true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed to apply event option icon: {ex.Message}");
        }
    }

    private static Texture2D? TextureFor(string textKey)
    {
        var resourceName = textKey switch
        {
            "BETTERREWARDS.NEOW" => NeowIconResourceName,
            "BETTERREWARDS.SACRIFICE.do" or "BETTERREWARDS.SACRIFICE.locked" => SacrificeIconResourceName,
            "BETTERREWARDS.ENTER_SHOP" => EnterShopIconResourceName,
            "BETTERREWARDS.SHOP_PENDING" => WaitMerchantIconResourceName,
            // Any other BetterRewards option keeps the vanilla shop map icon.
            _ => null
        };

        if (resourceName == null)
        {
            return ResourceLoader.Load<Texture2D>(FallbackShopIconPath, cacheMode: ResourceLoader.CacheMode.Reuse);
        }

        if (CustomIcons.TryGetValue(resourceName, out var cached))
        {
            return cached ?? ResourceLoader.Load<Texture2D>(FallbackShopIconPath, cacheMode: ResourceLoader.CacheMode.Reuse);
        }

        var texture = LoadEmbeddedPngTexture(resourceName);
        CustomIcons[resourceName] = texture;
        return texture ?? ResourceLoader.Load<Texture2D>(FallbackShopIconPath, cacheMode: ResourceLoader.CacheMode.Reuse);
    }

    private static Texture2D? LoadEmbeddedPngTexture(string resourceName)
    {
        try
        {
            var assembly = typeof(NEventOptionButtonReadyPatch).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Log.Warn($"[BetterRewards] Missing embedded event icon resource: {resourceName}");
                return null;
            }

            var bytes = new byte[stream.Length];
            var read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
            {
                Log.Warn($"[BetterRewards] Could not read full event icon resource: {resourceName}");
                return null;
            }

            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok)
            {
                Log.Warn($"[BetterRewards] Could not decode event icon resource: {resourceName}");
                return null;
            }

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed loading event icon resource {resourceName}: {ex.Message}");
            return null;
        }
    }
}
