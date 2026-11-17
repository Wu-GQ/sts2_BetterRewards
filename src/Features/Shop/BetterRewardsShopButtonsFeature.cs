using System.Reflection;
using BetterRewards.Features.Settings;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace BetterRewards.Features.Shop;

/// <summary>
/// Injects three special BetterRewards buttons into the top-left of the merchant screen
/// when a BetterRewards shop visit is active.
/// </summary>
internal static class BetterRewardsShopButtonsFeature
{
    private const string ContainerName = "BetterRewardsShopButtons";
    private const string RerollIconResourceName = "BetterRewards.RerollIconPng";
    private const string RandomRelicIconResourceName = "BetterRewards.RandomRelicIconPng";
    private const string RandomItemIconResourceName = "BetterRewards.RandomItemIconPng";
    private const string LocTableName = "better_rewards_shop";
    private const float ActionIconSize = 80f;
    private static readonly Vector2 ShopNormalScale = Vector2.One;
    private static readonly Vector2 ShopHoverScale = Vector2.One * 1.15f;

    private static Texture2D? _rerollIconTexture;
    private static Texture2D? _randomRelicIconTexture;
    private static Texture2D? _randomItemIconTexture;

    private static MerchantRelicEntry? _preparedRelic;
    private static PreparedItemOffer? _preparedItem;

    private static bool _isActiveShop;
    public static bool IsActiveShop
    {
        get => _isActiveShop;
        set
        {
            _isActiveShop = value;
            if (!value)
            {
                _activeRefreshAction = null;
                ClearPreparedOffers();
            }
        }
    }

    public static void TryAttachButtons(NMerchantInventory merchantInventory)
    {
        if (!IsActiveShop)
        {
            return;
        }

        // Attach to %SlotsContainer so buttons are positioned relative to the merchant rug,
        // not the full-screen inventory control (which is offset off-screen until opened).
        var slotsContainer = merchantInventory.GetNodeOrNull<Control>("%SlotsContainer");
        if (slotsContainer == null)
        {
            Log.Warn("[BetterRewards] %SlotsContainer not found on NMerchantInventory — buttons not attached.");
            return;
        }

        if (slotsContainer.GetNodeOrNull<Control>(ContainerName) != null)
        {
            return;
        }

        var container = new VBoxContainer
        {
            Name = ContainerName,
        };
        container.AddThemeConstantOverride("separation", 12);

        ShopActionUi[]? buttons = null;

        void RefreshLabels()
        {
            if (buttons == null)
            {
                return;
            }

            foreach (var btn in buttons)
            {
                SetCost(btn);
            }
        }

        _activeRefreshAction = RefreshLabels;

        _rerollIconTexture ??= LoadEmbeddedPngTexture(RerollIconResourceName);
        _randomRelicIconTexture ??= LoadEmbeddedPngTexture(RandomRelicIconResourceName);
        _randomItemIconTexture ??= LoadEmbeddedPngTexture(RandomItemIconResourceName);

        EnsureTooltipLocalization();

        var rerollButton = MakeIconAction(
            "reroll",
            _rerollIconTexture,
            () => BetterRewardsShopState.CurrentRerollCost,
            merchantInventory);
        var relicButton = MakeIconAction(
            "random_relic",
            _randomRelicIconTexture,
            () => BetterRewardsShopState.CurrentRelicCost,
            merchantInventory);
        var randomButton = MakeIconAction(
            "random_item",
            _randomItemIconTexture,
            () => BetterRewardsShopState.CurrentRandomItemCost,
            merchantInventory);

        buttons = new[] { rerollButton, relicButton, randomButton };

        rerollButton.OnPressed = () =>
        {
            var player = GetLocalPlayer();
            if (player == null)
            {
                return;
            }

            var cost = BetterRewardsShopState.CurrentRerollCost;
            if (player.Gold < cost)
            {
                return;
            }

            TaskHelper.RunSafely(DoReroll(player, cost, RefreshLabels, merchantInventory));
        };

        relicButton.OnPressed = () =>
        {
            var player = GetLocalPlayer();
            if (player == null)
            {
                return;
            }

            var cost = BetterRewardsShopState.CurrentRelicCost;
            if (player.Gold < cost)
            {
                return;
            }

            TaskHelper.RunSafely(DoRandomRelic(player, RefreshLabels));
        };

        randomButton.OnPressed = () =>
        {
            var player = GetLocalPlayer();
            if (player == null)
            {
                return;
            }

            var cost = BetterRewardsShopState.CurrentRandomItemCost;
            if (player.Gold < cost)
            {
                return;
            }

            TaskHelper.RunSafely(DoRandomItem(player, RefreshLabels));
        };

        container.AddChild(rerollButton.Root);
        container.AddChild(relicButton.Root);
        container.AddChild(randomButton.Root);
        slotsContainer.AddChild(container);

        // Deferred so gold is readable after the node enters the tree.
        // Purchase event subscriptions happen in TrySubscribePurchaseEvents, called from
        // the Initialize patch (after Inventory is populated).
        Callable.From(RefreshLabels).CallDeferred();

        void PositionContainer()
        {
            if (!GodotObject.IsInstanceValid(container) || !GodotObject.IsInstanceValid(slotsContainer))
            {
                return;
            }

            var containerSize = container.GetCombinedMinimumSize();
            var availableSize = slotsContainer.Size;
            if (availableSize.X <= 0f || availableSize.Y <= 0f)
                availableSize = slotsContainer.GetRect().Size;

            // Bottom-left of the merchant window + 5% from the left, 10% up from the bottom.
            var x = availableSize.X * 0.075f;
            var y = availableSize.Y - containerSize.Y - availableSize.Y * 0.175f;

            container.Position = new Vector2(x, MathF.Max(0f, y));
        }

        slotsContainer.Resized += PositionContainer;
        Callable.From(PositionContainer).CallDeferred();
    }

    /// <summary>
    /// Called from the Initialize patch once Inventory is populated.
    /// Subscribes RefreshLabels to every entry's PurchaseCompleted so that
    /// buying anything in the shop (card, relic, potion, card removal, or our
    /// own buttons) immediately updates our price label colors.
    /// </summary>
    public static void TrySubscribePurchaseEvents(NMerchantInventory merchantInventory)
    {
        if (!IsActiveShop) return;

        var slotsContainer = merchantInventory.GetNodeOrNull<Control>("%SlotsContainer");
        if (slotsContainer == null) return;

        var container = slotsContainer.GetNodeOrNull<Control>(ContainerName);
        if (container == null) return;

        // Find the RefreshLabels closure captured in TryAttachButtons by looking for
        // the buttons node — we need to call SetCost on all ShopActionUi children.
        // Simpler: just keep a static registry of active refresh delegates.
        if (_activeRefreshAction == null) return;

        if (merchantInventory.Inventory == null) return;

        foreach (var entry in merchantInventory.Inventory.AllEntries)
        {
            entry.PurchaseCompleted += (_, _) => _activeRefreshAction?.Invoke();
        }
    }

    // Stores the most recent RefreshLabels delegate so the Initialize patch can subscribe it.
    private static Action? _activeRefreshAction;

    private static readonly MethodInfo? _restockAfterPurchase =
        typeof(MerchantEntry).GetMethod("RestockAfterPurchase", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? _setCardRemovalUsed =
        typeof(MerchantCardRemovalEntry).GetProperty("Used", BindingFlags.Public | BindingFlags.Instance)
            ?.GetSetMethod(nonPublic: true);

    // NMerchantCardRemoval._isUnavailable is a private bool that gate-keeps UpdateVisual.
    // Once true it never resets, so we must reflect to clear it before re-enabling the slot.
    private static readonly FieldInfo? _isUnavailableField =
        typeof(NMerchantCardRemoval).GetField("_isUnavailable", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? _animatorField =
        typeof(NMerchantCardRemoval).GetField("_animator", BindingFlags.NonPublic | BindingFlags.Instance);

    private static async Task DoReroll(Player player, int cost, Action refreshLabels, NMerchantInventory nInventory)
    {
        await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
        BetterRewardsShopState.RecordRerollUse();

        var inventory = NMerchantRoom.Instance?.Inventory?.Inventory;
        if (inventory == null)
        {
            Log.Warn("[BetterRewards] DoReroll: MerchantInventory not found.");
            refreshLabels();
            return;
        }

        // Re-populate card entries, then notify UI for each one individually.
        // (CalcCost throws if CreationResult is null, so we must Populate before notifying.)
        foreach (var entry in inventory.CardEntries)
        {
            entry.Populate(inventory);
            entry.OnMerchantInventoryUpdated();
        }

        // Re-populate relic and potion entries (including purchased/cleared slots).
        if (_restockAfterPurchase != null)
        {
            foreach (var entry in inventory.RelicEntries)
            {
                _restockAfterPurchase.Invoke(entry, new object?[] { inventory });
                entry.OnMerchantInventoryUpdated();
            }
            foreach (var entry in inventory.PotionEntries)
            {
                _restockAfterPurchase.Invoke(entry, new object?[] { inventory });
                entry.OnMerchantInventoryUpdated();
            }
        }
        else
        {
            Log.Warn("[BetterRewards] Could not reflect RestockAfterPurchase — relics/potions not rerolled.");
        }

        // Reset card removal — re-enable the service for this rerolled shop visit.
        var cardRemoval = inventory.CardRemovalEntry;
        if (cardRemoval != null)
        {
            _setCardRemovalUsed?.Invoke(cardRemoval, new object[] { false });
            cardRemoval.CalcCost();

            var cardRemovalNode = nInventory.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval");
            if (cardRemovalNode != null)
            {
                _isUnavailableField?.SetValue(cardRemovalNode, false);

                if (_animatorField?.GetValue(cardRemovalNode) is AnimationPlayer animator)
                {
                    animator.CurrentAnimation = "Used";
                    animator.Seek(0.0, true);
                    animator.Stop();
                }

                var hitbox = cardRemovalNode.Hitbox;
                if (hitbox != null)
                {
                    hitbox.MouseFilter = Control.MouseFilterEnum.Stop;
                    cardRemovalNode.FocusMode = Control.FocusModeEnum.All;
                }
            }

            cardRemoval.OnMerchantInventoryUpdated();
        }

        // NMerchantCard.UpdateVisual sets Visible=false on purchase but never re-shows it.
        // After re-populating all entries, force all slots visible again.
        foreach (var slot in nInventory.GetAllSlots())
        {
            if (slot.Entry.IsStocked)
            {
                slot.Visible = true;
            }
        }

        refreshLabels();
        Log.Info("[BetterRewards] Shop rerolled in-place.");
    }

    private static async Task DoRandomRelic(Player player, Action refreshLabels)
    {
        EnsurePreparedRelic(player);
        var entry = _preparedRelic;
        if (entry?.Model == null)
        {
            Log.Warn("[BetterRewards] Random Relic → no prepared relic offer.");
            refreshLabels();
            return;
        }

        var cost = BetterRewardsShopState.CurrentRelicCost;
        if (player.Gold < cost)
        {
            return;
        }

        await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);

        var relic = entry.Model;
        var mutable = relic.IsMutable ? relic : relic.ToMutable();
        Log.Info($"[BetterRewards] Random Relic → {relic.Rarity} relic: {relic.Id} for {cost} gold");
        await RelicCmd.Obtain(mutable, player);

        BetterRewardsShopState.RecordRelicUse();
        _preparedRelic = null;
        EnsurePreparedRelic(player);
        refreshLabels();
    }

    private static async Task DoRandomItem(Player player, Action refreshLabels)
    {
        EnsurePreparedItem(player);
        var offer = _preparedItem;
        if (offer == null || !offer.IsReady)
        {
            Log.Warn("[BetterRewards] Random Item → no prepared item offer.");
            refreshLabels();
            return;
        }

        var cost = BetterRewardsShopState.CurrentRandomItemCost;
        if (player.Gold < cost)
        {
            return;
        }

        Log.Info($"[BetterRewards] Random Item rolled type: {offer.Type}");
        var granted = false;
        try
        {
            switch (offer.Type)
            {
                case RandomItemType.Relic:
                {
                    var relic = offer.Relic!.Model!;
                    var mutable = relic.IsMutable ? relic : relic.ToMutable();
                    Log.Info($"[BetterRewards] Random Item (Relic) → {relic.Rarity}: {relic.Id} for {cost} gold");
                    await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
                    await RelicCmd.Obtain(mutable, player);
                    granted = true;
                    break;
                }
                case RandomItemType.Card:
                {
                    var card = offer.Card!.CreationResult!.Card;
                    Log.Info($"[BetterRewards] Random Item (Card) → {card.Rarity}: {card.Id} for {cost} gold");
                    if (!(await CardPileCmd.Add(card, PileType.Deck)).success)
                    {
                        Log.Warn("[BetterRewards] Random Item (Card) could not be added to the deck.");
                        break;
                    }

                    await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
                    AnimateCardToDeck(card, player);
                    granted = true;
                    break;
                }
                case RandomItemType.Potion:
                {
                    var potion = offer.Potion!.Model!;
                    Log.Info($"[BetterRewards] Random Item (Potion) → {potion.Rarity}: {potion.Id} for {cost} gold");
                    var result = await PotionCmd.TryToProcure(potion, player);
                    if (!result.success)
                    {
                        Log.Warn("[BetterRewards] Random Item (Potion) could not be procured.");
                        break;
                    }

                    await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
                    granted = true;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Random item grant failed: {ex.Message}\n{ex.StackTrace}");
        }

        if (granted)
        {
            BetterRewardsShopState.RecordRandomItemUse();
            _preparedItem = null;
            EnsurePreparedItem(player);
        }

        refreshLabels();
    }
    private static void AnimateCardToDeck(CardModel card, Player player)
    {
        try
        {
            var globalUi = NRun.Instance?.GlobalUi;
            if (globalUi == null) return;

            var cardNode = NCard.Create(card);
            if (cardNode == null) return;
            cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            var trailContainer = globalUi.TopBar.TrailContainer;
            trailContainer.AddChildSafely(cardNode);

            // Start from the merchant inventory region (center of screen is a safe default).
            cardNode.GlobalPosition = new Vector2(globalUi.Size.X * 0.5f, globalUi.Size.Y * 0.5f);

            var vfx = NCardFlyVfx.Create(cardNode, PileType.Deck, isAddingToPile: true, player.Character.TrailPath);
            if (vfx != null)
            {
                trailContainer.AddChildSafely(vfx);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] AnimateCardToDeck failed (non-fatal): {ex.Message}");
        }
    }

    private static Player? GetLocalPlayer()
    {
        // NMerchantRoom.Inventory is NMerchantInventory; its .Inventory is MerchantInventory which holds Player.
        var player = NMerchantRoom.Instance?.Inventory?.Inventory?.Player;
        if (player != null && LocalContext.IsMe(player))
        {
            return player;
        }

        return null;
    }

    private static readonly CardType[] ShopCharacterCardTypes =
    {
        CardType.Attack, CardType.Attack, CardType.Skill, CardType.Skill, CardType.Power
    };

    private static readonly CardRarity[] ShopColorlessCardRarities =
    {
        CardRarity.Uncommon, CardRarity.Rare
    };

    private const int ShopRelicSlotCount = 3;
    private const int ShopCardSlotCount = 7;
    private const int ShopPotionSlotCount = 3;

    internal static void ClearPreparedOffers()
    {
        _preparedRelic = null;
        _preparedItem = null;
    }

    private static void EnsurePreparedRelic(Player player)
    {
        if (_preparedRelic?.IsStocked == true)
        {
            return;
        }

        var rarity = RelicFactory.RollRarity(player.PlayerRng.Shops);
        _preparedRelic = new MerchantRelicEntry(rarity, player);
    }

    private static void EnsurePreparedItem(Player player)
    {
        if (_preparedItem?.IsReady == true)
        {
            return;
        }

        _preparedItem = null;
        for (var attempt = 0; attempt < 5 && _preparedItem == null; attempt++)
        {
            switch (RollItemType(player))
            {
                case RandomItemType.Relic:
                {
                    var rarity = RelicFactory.RollRarity(player.PlayerRng.Shops);
                    var entry = new MerchantRelicEntry(rarity, player);
                    if (entry.IsStocked)
                    {
                        _preparedItem = PreparedItemOffer.FromRelic(entry);
                    }
                    break;
                }
                case RandomItemType.Card:
                {
                    var entry = CreateShopCardEntry(player);
                    if (entry.IsStocked)
                    {
                        _preparedItem = PreparedItemOffer.FromCard(entry);
                    }
                    break;
                }
                case RandomItemType.Potion:
                {
                    var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, player.PlayerRng.Shops);
                    if (potion != null)
                    {
                        _preparedItem = PreparedItemOffer.FromPotion(new MerchantPotionEntry(potion.ToMutable(), player));
                    }
                    break;
                }
            }
        }
    }

    private static MerchantCardEntry CreateShopCardEntry(Player player)
    {
        var rng = player.PlayerRng.Shops;
        var characterSlots = ShopCharacterCardTypes.Length;
        var colorlessSlots = ShopColorlessCardRarities.Length;
        var slot = rng.NextInt(characterSlots + colorlessSlots);
        var inventory = NMerchantRoom.Instance?.Inventory?.Inventory;

        MerchantCardEntry entry;
        if (slot < characterSlots)
        {
            var type = ShopCharacterCardTypes[slot];
            var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
            entry = new MerchantCardEntry(player, pool, type);
            entry.Populate(inventory);
            if (rng.NextInt(characterSlots) == 0)
            {
                entry.SetOnSale();
            }
            Log.Info($"[BetterRewards] Prepared character {type} {entry.CreationResult?.Card.Rarity}: {entry.CreationResult?.Card.Id}");
        }
        else
        {
            var rarity = ShopColorlessCardRarities[slot - characterSlots];
            var pool = ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
            entry = new MerchantCardEntry(player, pool, rarity);
            entry.Populate(inventory);
            Log.Info($"[BetterRewards] Prepared colorless {rarity}: {entry.CreationResult?.Card.Id}");
        }

        return entry;
    }

    private enum RandomItemType { Relic, Card, Potion }

    private static RandomItemType RollItemType(Player player)
    {
        var total = ShopRelicSlotCount + ShopCardSlotCount + ShopPotionSlotCount;
        var roll = player.PlayerRng.Shops.NextInt(total);
        if (roll < ShopRelicSlotCount)
        {
            return RandomItemType.Relic;
        }

        if (roll < ShopRelicSlotCount + ShopCardSlotCount)
        {
            return RandomItemType.Card;
        }

        return RandomItemType.Potion;
    }

    private static void EnsureTooltipLocalization()
    {
        BetterRewardsLocalization.TryMerge();
    }

    private static ShopActionUi MakeIconAction(string id, Texture2D? iconTexture, Func<int> getCost, NMerchantInventory merchantInventory)
    {
        var root = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        root.AddThemeConstantOverride("separation", 4);

        var action = new TextureButton
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(ActionIconSize, ActionIconSize),
            PivotOffset = new Vector2(ActionIconSize * 0.5f, ActionIconSize * 0.5f),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            TextureNormal = iconTexture,
        };

        // Extract gold icon texture and font from a live slot's Cost node, then build
        // our own small HBoxContainer — bypasses all Godot container scaling issues.
        var (goldTexture, costFont) = ExtractCostAssets(merchantInventory);

        var costRow = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        costRow.AddThemeConstantOverride("separation", 3);

        if (goldTexture != null)
        {
            var goldIcon = new TextureRect
            {
                Texture = goldTexture,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                CustomMinimumSize = new Vector2(20f, 20f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            costRow.AddChild(goldIcon);
        }

        var priceLabel = new Label
        {
            Text = getCost().ToString(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (costFont != null)
        {
            priceLabel.AddThemeFontOverride("font", costFont);
        }
        priceLabel.AddThemeFontSizeOverride("font_size", 18);
        costRow.AddChild(priceLabel);

        var ui = new ShopActionUi(root, action, priceLabel, getCost);

        Tween? activeTween = null;
        action.MouseEntered += () =>
        {
            activeTween?.Kill();
            activeTween = action.CreateTween();
            activeTween.TweenProperty(action, "scale", ShopHoverScale, 0.08)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            ShowHoverTip(action, id);
        };
        action.MouseExited += () =>
        {
            activeTween?.Kill();
            activeTween = action.CreateTween();
            activeTween.TweenProperty(action, "scale", ShopNormalScale, 0.15)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Expo);
            HideHoverTip(action);
        };
        action.Pressed += () =>
        {
            ui.OnPressed?.Invoke();
        };

        root.AddChild(action);
        root.AddChild(costRow);

        return ui;
    }

    private static void ShowHoverTip(Control owner, string id)
    {
        NHoverTipSet.Remove(owner);
        var tip = new HoverTip(
            new LocString(LocTableName, $"{id}.title"),
            new LocString(LocTableName, $"{id}.description"));
        var tipSet = NHoverTipSet.CreateAndShow(owner, tip);
        tipSet.GlobalPosition = owner.GlobalPosition + new Vector2(ActionIconSize + 8f, -40f);
    }

    private static void HideHoverTip(Control owner)
    {
        NHoverTipSet.Remove(owner);
    }

    private static (Texture2D? goldTexture, Font? font) ExtractCostAssets(NMerchantInventory merchantInventory)
    {
        Texture2D? goldTexture = null;
        Font? font = null;

        // Look in the card removal node and all slots for the Cost subtree.
        var candidates = new List<Node?> { merchantInventory.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval") };
        candidates.AddRange(merchantInventory.GetAllSlots().Cast<Node?>());

        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;

            // Find first TextureRect anywhere in the subtree as the gold icon.
            if (goldTexture == null)
            {
                goldTexture = FindFirstTextureRect(candidate)?.Texture;
            }

            // Find the MegaLabel for the font.
            if (font == null)
            {
                var megaLabel = FindFirstMegaLabel(candidate);
                if (megaLabel != null)
                {
                    font = megaLabel.GetThemeFont("font");
                }
            }

            if (goldTexture != null && font != null) break;
        }

        return (goldTexture, font);
    }

    private static TextureRect? FindFirstTextureRect(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is TextureRect rect && rect.Texture != null)
            {
                return rect;
            }

            var found = FindFirstTextureRect(child);
            if (found != null) return found;
        }

        return null;
    }

    private static MegaLabel? FindFirstMegaLabel(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MegaLabel label)
            {
                return label;
            }

            var found = FindFirstMegaLabel(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetAllMouseFiltersIgnore(Node node)
    {
        if (node is Control c)
        {
            c.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        foreach (var child in node.GetChildren())
        {
            SetAllMouseFiltersIgnore(child);
        }
    }

    private static void SetCost(ShopActionUi ui)
    {
        var cost = ui.GetCost();
        if (ui.PriceLabel != null)
        {
            ui.PriceLabel.Text = cost.ToString();
            ui.PriceLabel.Modulate = HasEnoughGold(cost) ? StsColors.cream : StsColors.red;
        }
    }

    /// <summary>
    /// Picks a random unlocked Ancient relic the player doesn't already own.
    /// Ancient relics are excluded from the normal grab bag, so we query ModelDb directly.
    /// Returns null if no candidates exist (caller should fall back to Rare).
    /// </summary>
    private static bool HasEnoughGold(int cost)
    {
        var player = GetLocalPlayer();
        return player != null && player.Gold >= cost;
    }

    private static Texture2D? LoadEmbeddedPngTexture(string resourceName)
    {
        try
        {
            var assembly = typeof(BetterRewardsShopButtonsFeature).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Log.Warn($"[BetterRewards] Missing embedded icon resource: {resourceName}");
                return null;
            }

            var bytes = new byte[stream.Length];
            var read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
            {
                Log.Warn($"[BetterRewards] Could not read full icon resource: {resourceName}");
                return null;
            }

            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok)
            {
                Log.Warn($"[BetterRewards] Could not decode icon resource: {resourceName}");
                return null;
            }

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Failed loading icon resource {resourceName}: {ex.Message}");
            return null;
        }
    }

    private sealed class PreparedItemOffer
    {
        private PreparedItemOffer(RandomItemType type, MerchantRelicEntry? relic, MerchantCardEntry? card, MerchantPotionEntry? potion)
        {
            Type = type;
            Relic = relic;
            Card = card;
            Potion = potion;
        }

        public RandomItemType Type { get; }

        public MerchantRelicEntry? Relic { get; }

        public MerchantCardEntry? Card { get; }

        public MerchantPotionEntry? Potion { get; }

        public bool IsReady => Type switch
        {
            RandomItemType.Relic => Relic?.IsStocked == true,
            RandomItemType.Card => Card?.IsStocked == true,
            RandomItemType.Potion => Potion?.IsStocked == true,
            _ => false,
        };

        public int Cost => Type switch
        {
            RandomItemType.Relic => Relic!.Cost,
            RandomItemType.Card => Card!.Cost,
            RandomItemType.Potion => Potion!.Cost,
            _ => 0,
        };

        public static PreparedItemOffer FromRelic(MerchantRelicEntry entry) => new(RandomItemType.Relic, entry, null, null);

        public static PreparedItemOffer FromCard(MerchantCardEntry entry) => new(RandomItemType.Card, null, entry, null);

        public static PreparedItemOffer FromPotion(MerchantPotionEntry entry) => new(RandomItemType.Potion, null, null, entry);
    }

    private sealed class ShopActionUi
    {
        public ShopActionUi(VBoxContainer root, TextureButton actionRoot, Label? priceLabel, Func<int> getCost)
        {
            Root = root;
            ActionRoot = actionRoot;
            PriceLabel = priceLabel;
            GetCost = getCost;
        }

        public VBoxContainer Root { get; }

        public TextureButton ActionRoot { get; }

        public Label? PriceLabel { get; }

        public Func<int> GetCost { get; }

        public Action? OnPressed { get; set; }
    }
}
