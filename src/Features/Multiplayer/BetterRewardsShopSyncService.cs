using System.Reflection;
using BetterRewards.Features.Shop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BetterRewards.Features.Multiplayer;

/// <summary>
/// What a special-shop button does.
/// </summary>
internal enum BetterRewardsShopActionKind
{
    Reroll = 0,
    RandomRelic = 1,
    RandomItem = 2,
}

/// <summary>
/// A relic that a special-shop action took out of the acting player's grab bags.
/// <para>
/// Both the reroll and the two random-buy buttons pull relics with
/// <c>RelicFactory.PullNextRelicFromBack</c>, which removes the relic from
/// <c>player.RelicGrabBag</c> and from <c>runState.SharedRelicGrabBag</c>. The engine
/// checksums the per-player bag, and peers cannot reproduce the removal by re-running the
/// draws (their shop RNG streams are per-player), so the ids travel on the wire and each peer
/// removes exactly those.
/// </para>
/// </summary>
internal struct BetterRewardsPulledRelic
{
    public string ModelId;

    public void Serialize(PacketWriter writer) => writer.WriteString(ModelId ?? string.Empty);

    public void Deserialize(PacketReader reader) => ModelId = reader.ReadString();
}

internal enum BetterRewardsShopSlotKind
{
    Card = 0,
    Relic = 1,
    Potion = 2,
    CardRemoval = 3,
}

/// <summary>
/// One slot of the acting player's rerolled shelves.
/// <para>
/// The engine keeps a copy of <em>every</em> player's <see cref="MerchantInventory"/> on every
/// peer, and a purchase is replicated as "player X bought entry N" and re-played against that
/// copy. If a reroll only changed the acting peer's copy, the other peers would re-play the
/// purchase against stale stock and hand the buyer the pre-reroll item. So the rerolled stock
/// is sent explicitly and applied to the actor's own inventory copy on each peer.
/// </para>
/// </summary>
internal struct BetterRewardsShopSlotSnapshot
{
    public uint EntryId;
    public BetterRewardsShopSlotKind Kind;
    public string ModelId;
    public int BaseCost;
    public bool IsStocked;
    public bool IsOnSale;
    public bool CardUpgraded;
    public bool CardRemovalUsed;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(EntryId, 8);
        writer.WriteInt((int)Kind);
        writer.WriteString(ModelId ?? string.Empty);
        writer.WriteInt(BaseCost);
        writer.WriteBool(IsStocked);
        writer.WriteBool(IsOnSale);
        writer.WriteBool(CardUpgraded);
        writer.WriteBool(CardRemovalUsed);
    }

    public void Deserialize(PacketReader reader)
    {
        EntryId = reader.ReadUInt(8);
        Kind = (BetterRewardsShopSlotKind)reader.ReadInt();
        ModelId = reader.ReadString();
        BaseCost = reader.ReadInt();
        IsStocked = reader.ReadBool();
        IsOnSale = reader.ReadBool();
        CardUpgraded = reader.ReadBool();
        CardRemovalUsed = reader.ReadBool();
    }
}

/// <summary>
/// Announcement that one player used a special-shop button.
/// <para>
/// The message never describes the <em>receiving</em> player's shop. A reroll's stock is
/// carried only so every peer can keep its copy of the <em>acting</em> player's shop in step
/// with the acting peer, which is what the engine's purchase replay depends on.
/// </para>
/// </summary>
internal struct BetterRewardsShopActionMessage : INetMessage, IPacketSerializable
{
    public ulong ActorNetId;
    public BetterRewardsShopActionKind Kind;
    public int PaidCost;

    /// <summary>Model paid out by a random relic or random item purchase; empty for a reroll.</summary>
    public string GrantedModelId;

    /// <summary>True when a granted card should be upgraded (the shop's upgrade roll).</summary>
    public bool GrantedCardUpgraded;

    public int RerollUseCount;
    public int RelicUseCount;
    public int RandomItemUseCount;

    /// <summary>Relics this action pulled out of the actor's grab bags.</summary>
    public BetterRewardsPulledRelic[] PulledRelics;

    /// <summary>Rerolled stock for the actor's own shop copy; empty for the buy buttons.</summary>
    public BetterRewardsShopSlotSnapshot[] Slots;

    /// <summary>
    /// The actor's shop RNG after the action.
    /// <para>
    /// Every action draws from <c>player.PlayerRng.Shops</c>, and the engine strips that stream
    /// from the divergence checksum, so a desync here is silent today but corrupts the next
    /// act's shop. Shipping the state keeps every peer's copy of the actor's stream aligned.
    /// </para>
    /// </summary>
    public bool HasShopsRng;
    public SerializableRng ShopsRng;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(ActorNetId);
        writer.WriteInt((int)Kind);
        writer.WriteInt(PaidCost);
        writer.WriteString(GrantedModelId ?? string.Empty);
        writer.WriteBool(GrantedCardUpgraded);
        writer.WriteInt(RerollUseCount);
        writer.WriteInt(RelicUseCount);
        writer.WriteInt(RandomItemUseCount);

        var pulled = PulledRelics ?? [];
        writer.WriteInt(pulled.Length);
        foreach (var relic in pulled)
        {
            relic.Serialize(writer);
        }

        var slots = Slots ?? [];
        writer.WriteInt(slots.Length);
        foreach (var slot in slots)
        {
            slot.Serialize(writer);
        }

        writer.WriteBool(HasShopsRng);
        if (HasShopsRng)
        {
            writer.Write(ShopsRng);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        ActorNetId = reader.ReadULong();
        Kind = (BetterRewardsShopActionKind)reader.ReadInt();
        PaidCost = reader.ReadInt();
        GrantedModelId = reader.ReadString();
        GrantedCardUpgraded = reader.ReadBool();
        RerollUseCount = reader.ReadInt();
        RelicUseCount = reader.ReadInt();
        RandomItemUseCount = reader.ReadInt();

        var pulledCount = Math.Max(0, reader.ReadInt());
        PulledRelics = new BetterRewardsPulledRelic[pulledCount];
        for (var i = 0; i < pulledCount; i++)
        {
            var relic = new BetterRewardsPulledRelic();
            relic.Deserialize(reader);
            PulledRelics[i] = relic;
        }

        var slotCount = Math.Max(0, reader.ReadInt());
        Slots = new BetterRewardsShopSlotSnapshot[slotCount];
        for (var i = 0; i < slotCount; i++)
        {
            var slot = new BetterRewardsShopSlotSnapshot();
            slot.Deserialize(reader);
            Slots[i] = slot;
        }

        HasShopsRng = reader.ReadBool();
        if (HasShopsRng)
        {
            ShopsRng = reader.Read<SerializableRng>();
        }
    }
}

/// <summary>
/// Replicates special-shop actions so every peer's run state, and every peer's copy of the
/// acting player's shop, stays identical.
/// <para>
/// Replay never touches the receiving player's own shelves: a reroll by another player must not
/// change what you see in your shop.
/// </para>
/// </summary>
internal static class BetterRewardsShopSyncService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<BetterRewardsShopActionMessage>> Handlers = [];
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> DisconnectHandlers = [];

    private static readonly FieldInfo? CostField =
        typeof(MerchantEntry).GetField("_cost", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? CreationResultField =
        typeof(MerchantCardEntry).GetField("<CreationResult>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? IsOnSaleField =
        typeof(MerchantCardEntry).GetField("<IsOnSale>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? RelicModelField =
        typeof(MerchantRelicEntry).GetField("<Model>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? PotionModelField =
        typeof(MerchantPotionEntry).GetField("<Model>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo? CardRemovalUsedSetter =
        typeof(MerchantCardRemovalEntry).GetProperty("Used", BindingFlags.Public | BindingFlags.Instance)
            ?.GetSetMethod(nonPublic: true);

    private static readonly MethodInfo? RestockAfterPurchase =
        typeof(MerchantEntry).GetMethod("RestockAfterPurchase", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void EnsureAttached(INetGameService netService, string source)
    {
        lock (Sync)
        {
            if (Handlers.ContainsKey(netService))
            {
                return;
            }

            MessageHandlerDelegate<BetterRewardsShopActionMessage> handler = (message, senderId) =>
                HandleActionMessage(netService, message, senderId);
            Action<NetErrorInfo> disconnectHandler = _ => OnDisconnected(netService);

            Handlers[netService] = handler;
            DisconnectHandlers[netService] = disconnectHandler;

            netService.RegisterMessageHandler(handler);
            netService.Disconnected += disconnectHandler;
            Log.Info($"[BetterRewards] Shop action sync attached ({source}).");
        }
    }

    /// <summary>
    /// Publishes an action performed locally. The local run state has already been mutated by
    /// the caller; this only informs the other peers.
    /// </summary>
    public static void PublishAction(BetterRewardsShopActionMessage message, Player actor)
    {
        // Capture the actor's shop RNG after the action so peers can realign their copy.
        try
        {
            message.ShopsRng = actor.PlayerRng.Shops.ToSerializable();
            message.HasShopsRng = true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Could not serialize shop RNG: {ex.Message}");
            message.HasShopsRng = false;
        }

        var netService = BetterRewardsNetSupport.TryGetNetService();
        if (netService == null || !BetterRewardsNetSupport.IsConnectedMultiplayer(netService))
        {
            return;
        }

        EnsureAttached(netService, "PublishAction");
        netService.SendMessage(message);

        var role = netService.Type == NetGameType.Host ? "host" : "client";
        Log.Info($"[BetterRewards] Published shop action {message.Kind} by {message.ActorNetId} " +
                 $"as {role} (paid {message.PaidCost}, {message.PulledRelics?.Length ?? 0} relics pulled, " +
                 $"{message.Slots?.Length ?? 0} slots, rng={message.HasShopsRng}).");
    }

    /// <summary>
    /// Reads the acting player's current shelves into wire form.
    /// </summary>
    public static BetterRewardsShopSlotSnapshot[] CaptureSlots(MerchantInventory inventory)
    {
        var slots = new List<BetterRewardsShopSlotSnapshot>();
        foreach (var entry in inventory.AllEntries)
        {
            if (entry.Id is not uint id)
            {
                continue;
            }

            var baseCost = CostField?.GetValue(entry) is int cost ? cost : entry.Cost;

            switch (entry)
            {
                case MerchantCardEntry card:
                {
                    var cardModel = card.CreationResult?.Card;
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Card,
                        ModelId = cardModel?.Id.ToString() ?? string.Empty,
                        BaseCost = baseCost,
                        IsStocked = card.IsStocked,
                        IsOnSale = card.IsOnSale,
                        CardUpgraded = cardModel?.IsUpgraded ?? false,
                    });
                    break;
                }
                case MerchantRelicEntry relic:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Relic,
                        ModelId = relic.Model?.Id.ToString() ?? string.Empty,
                        BaseCost = baseCost,
                        IsStocked = relic.IsStocked,
                    });
                    break;
                case MerchantPotionEntry potion:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Potion,
                        ModelId = potion.Model?.Id.ToString() ?? string.Empty,
                        BaseCost = baseCost,
                        IsStocked = potion.IsStocked,
                    });
                    break;
                case MerchantCardRemovalEntry removal:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.CardRemoval,
                        BaseCost = baseCost,
                        IsStocked = removal.IsStocked,
                        CardRemovalUsed = removal.Used,
                    });
                    break;
            }
        }

        return slots.ToArray();
    }

    private static void HandleActionMessage(
        INetGameService netService,
        BetterRewardsShopActionMessage message,
        ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        Log.Info($"[BetterRewards] Received shop action {message.Kind} from {message.ActorNetId} " +
                 $"(paid {message.PaidCost}, {message.PulledRelics?.Length ?? 0} relics pulled, " +
                 $"{message.Slots?.Length ?? 0} slots); replaying.");

        BetterRewardsNetSupport.RunSafelyOnMainThread(() => ApplyAction(message));
    }

    /// <summary>
    /// Replays another player's action against that player's own state.
    /// </summary>
    public static async Task ApplyAction(BetterRewardsShopActionMessage message)
    {
        var actor = BetterRewardsNetSupport.FindPlayer(message.ActorNetId);
        if (actor == null)
        {
            Log.Warn($"[BetterRewards] Shop action actor {message.ActorNetId} not found; skipping replay.");
            return;
        }

        // Use counts are per player, so this only moves the actor's price ladder.
        BetterRewardsShopState.ApplyUseCounts(
            message.ActorNetId,
            message.RerollUseCount,
            message.RelicUseCount,
            message.RandomItemUseCount);

        RealignShopRng(actor, message);
        ApplyPulledRelics(actor, message.PulledRelics);
        ApplySlotsToActorInventory(actor, message);

        // The remaining steps mutate shared run state, which only exists while a run is live.
        if (RunManager.Instance?.DebugOnlyGetState() == null)
        {
            Log.Warn($"[BetterRewards] Applied shop action {message.Kind} from {message.ActorNetId} " +
                     "without run-state effects: no active run state on this peer.");
            return;
        }

        if (message.PaidCost > 0)
        {
            await PlayerCmd.LoseGold(message.PaidCost, actor, GoldLossType.Spent);
        }

        switch (message.Kind)
        {
            case BetterRewardsShopActionKind.RandomRelic:
                await GrantRelic(actor, message.GrantedModelId);
                break;
            case BetterRewardsShopActionKind.RandomItem:
                await GrantItem(actor, message.GrantedModelId, message.GrantedCardUpgraded);
                break;
        }

        // Confirmation line for the success path. Without it the only evidence of a replay is
        // the absence of warnings, which cannot distinguish "applied" from "silently skipped".
        Log.Info($"[BetterRewards] Applied shop action {message.Kind} from {message.ActorNetId} " +
                 $"(paid {message.PaidCost}, granted '{message.GrantedModelId}', " +
                 $"upgraded={message.GrantedCardUpgraded}, actorGold={actor.Gold}).");
    }

    /// <summary>
    /// Loads the actor's post-action shop RNG so this peer's copy of that stream stays in phase.
    /// </summary>
    private static void RealignShopRng(Player actor, BetterRewardsShopActionMessage message)
    {
        if (!message.HasShopsRng)
        {
            Log.Warn($"[BetterRewards] Shop RNG not carried for {message.ActorNetId}'s action; " +
                     "their next shop roll may drift.");
            return;
        }

        try
        {
            actor.PlayerRng.Shops.LoadFromSerializable(message.ShopsRng);
            Log.Info($"[BetterRewards] Realigned shop RNG for {message.ActorNetId} (counter={message.ShopsRng.counter}).");
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Could not realign shop RNG for {message.ActorNetId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes the relics an action consumed from the actor's bags on this peer.
    /// <para>
    /// Removal is by id rather than by replaying the draws, so it is idempotent and does not
    /// depend on this peer's shop RNG being in phase with the actor's. Both bags are cleared
    /// because <c>RelicFactory.PullNextRelicFromBack</c> removes the relic from each of them.
    /// Missing ids are ignored: the engine may already have pruned a relic that became
    /// disallowed.
    /// </para>
    /// </summary>
    private static void ApplyPulledRelics(Player actor, IReadOnlyList<BetterRewardsPulledRelic>? pulled)
    {
        if (pulled == null || pulled.Count == 0)
        {
            return;
        }

        var removed = 0;
        foreach (var entry in pulled)
        {
            if (string.IsNullOrWhiteSpace(entry.ModelId))
            {
                continue;
            }

            var canonical = BetterRewardsModelLookup.FindCanonicalRelic(entry.ModelId);
            if (canonical == null)
            {
                Log.Warn($"[BetterRewards] Pulled relic {entry.ModelId} could not be resolved for the grab-bag removal.");
                continue;
            }

            actor.RelicGrabBag.Remove(canonical);
            actor.RunState.SharedRelicGrabBag.Remove(canonical);
            removed++;
        }

        Log.Info($"[BetterRewards] Removed {removed}/{pulled.Count} pulled relics from {actor.NetId}'s grab bags.");
    }

    /// <summary>
    /// Applies a rerolled stock snapshot to <em>this peer's copy of the acting player's</em>
    /// inventory — never to the receiving player's own shelves.
    /// <para>
    /// This is what the engine's purchase replay reads from. Leaving it stale is what made a
    /// teammate hand over the pre-reroll item when the actor bought slot N afterwards.
    /// </para>
    /// </summary>
    private static void ApplySlotsToActorInventory(Player actor, BetterRewardsShopActionMessage message)
    {
        var slots = message.Slots;
        if (slots == null || slots.Length == 0)
        {
            return;
        }

        MerchantInventory inventory;
        try
        {
            var synchronizer = RunManager.Instance?.MerchantSynchronizer;
            if (synchronizer == null)
            {
                return;
            }

            inventory = synchronizer.GetInventoryFor(actor);
        }
        catch (Exception ex)
        {
            // The merchant can already have been left on this peer, in which case the engine
            // has dropped the inventories and there is nothing to keep in step.
            Log.Warn($"[BetterRewards] Could not reach the merchant inventory for {message.ActorNetId}: {ex.Message}");
            return;
        }

        var byId = new Dictionary<uint, MerchantEntry>();
        foreach (var entry in inventory.AllEntries)
        {
            if (entry.Id is uint id)
            {
                byId[id] = entry;
            }
        }

        var applied = 0;
        var missing = 0;
        foreach (var slot in slots)
        {
            if (!byId.TryGetValue(slot.EntryId, out var entry))
            {
                Log.Warn($"[BetterRewards] Reroll snapshot entry {slot.EntryId} not present in the actor's inventory.");
                missing++;
                continue;
            }

            try
            {
                switch (entry)
                {
                    case MerchantCardEntry card:
                        ApplyCardSlot(card, slot, actor);
                        break;
                    case MerchantRelicEntry relic:
                        ApplyRelicSlot(relic, slot);
                        break;
                    case MerchantPotionEntry potion:
                        ApplyPotionSlot(potion, slot);
                        break;
                    case MerchantCardRemovalEntry removal:
                        ApplyCardRemovalSlot(removal, slot);
                        break;
                }

                entry.OnMerchantInventoryUpdated();
                applied++;
            }
            catch (Exception ex)
            {
                Log.Warn($"[BetterRewards] Failed applying reroll slot {slot.EntryId}: {ex.Message}");
            }
        }

        // This is the line that proves a reroll actually reached this peer's copy of the actor's
        // shop. A purchase replayed afterwards reads from exactly these slots.
        Log.Info($"[BetterRewards] Applied reroll snapshot to {message.ActorNetId}'s inventory copy " +
                 $"(applied {applied}/{slots.Length} slots, missing {missing}).");
    }

    private static void ApplyCardSlot(MerchantCardEntry entry, BetterRewardsShopSlotSnapshot slot, Player actor)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            CreationResultField?.SetValue(entry, null);
            IsOnSaleField?.SetValue(entry, false);
            WriteBaseCost(entry, slot.BaseCost);
            return;
        }

        var canonical = BetterRewardsModelLookup.FindCanonicalCard(slot.ModelId);
        if (canonical == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop card {slot.ModelId}.");
            return;
        }

        // Creating through the run state registers the card there, exactly as the acting peer's
        // own populate step did, so both peers hold the same set of shop cards.
        var card = actor.RunState.CreateCard(canonical, actor);
        if (slot.CardUpgraded && card.IsUpgradable && !card.IsUpgraded)
        {
            CardCmd.Upgrade(card);
        }

        CreationResultField?.SetValue(entry, new CardCreationResult(card));
        IsOnSaleField?.SetValue(entry, slot.IsOnSale);
        WriteBaseCost(entry, slot.BaseCost);
    }

    private static void ApplyRelicSlot(MerchantRelicEntry entry, BetterRewardsShopSlotSnapshot slot)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            RelicModelField?.SetValue(entry, null);
            WriteBaseCost(entry, slot.BaseCost);
            return;
        }

        var relic = BetterRewardsModelLookup.FindRelic(slot.ModelId);
        if (relic == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop relic {slot.ModelId}.");
            return;
        }

        RelicModelField?.SetValue(entry, relic);
        WriteBaseCost(entry, slot.BaseCost);
    }

    private static void ApplyPotionSlot(MerchantPotionEntry entry, BetterRewardsShopSlotSnapshot slot)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            PotionModelField?.SetValue(entry, null);
            WriteBaseCost(entry, slot.BaseCost);
            return;
        }

        var potion = BetterRewardsModelLookup.FindPotion(slot.ModelId);
        if (potion == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop potion {slot.ModelId}.");
            return;
        }

        PotionModelField?.SetValue(entry, potion);
        WriteBaseCost(entry, slot.BaseCost);
    }

    private static void ApplyCardRemovalSlot(MerchantCardRemovalEntry entry, BetterRewardsShopSlotSnapshot slot)
    {
        CardRemovalUsedSetter?.Invoke(entry, [slot.CardRemovalUsed]);
        WriteBaseCost(entry, slot.BaseCost);
    }

    private static void WriteBaseCost(MerchantEntry entry, int cost) => CostField?.SetValue(entry, cost);

    private static async Task GrantRelic(Player actor, string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        var relic = BetterRewardsModelLookup.FindRelic(modelId);
        if (relic == null)
        {
            Log.Warn($"[BetterRewards] Could not resolve granted relic {modelId} for replay.");
            return;
        }

        await RelicCmd.Obtain(relic, actor);
        Log.Info($"[BetterRewards] Granted relic {modelId} to {actor.NetId} (now {actor.Relics.Count} relics).");
    }

    /// <summary>
    /// Replays a random-item payout. The wire only carries the model id, so the kind is inferred
    /// from which model database actually contains it; that keeps the message small and means a
    /// card, relic and potion can never be confused with each other.
    /// </summary>
    private static async Task GrantItem(Player actor, string modelId, bool cardUpgraded)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        if (BetterRewardsModelLookup.IsCard(modelId))
        {
            var added = await BetterRewardsModelLookup.AddCardToDeck(modelId, actor, cardUpgraded);
            Log.Info($"[BetterRewards] Granted card {modelId} to {actor.NetId} " +
                     $"(upgraded={cardUpgraded}, added={added}, deck={actor.Deck.Cards.Count}).");
            return;
        }

        if (BetterRewardsModelLookup.IsPotion(modelId))
        {
            var potion = BetterRewardsModelLookup.FindPotion(modelId);
            if (potion == null)
            {
                Log.Warn($"[BetterRewards] Could not resolve granted potion {modelId} for replay.");
                return;
            }

            var result = await PotionCmd.TryToProcure(potion, actor);
            Log.Info($"[BetterRewards] Granted potion {modelId} to {actor.NetId} (success={result.success}).");
            return;
        }

        await GrantRelic(actor, modelId);
    }

    /// <summary>
    /// Restocks one of the acting player's own merchant slots after a reroll.
    /// <para>
    /// Only ever called on the peer that pressed the button, for that peer's own inventory. The
    /// resulting stock then travels to the other peers as a snapshot, which they apply to their
    /// copy of this player's inventory.
    /// </para>
    /// </summary>
    public static void RestockOwnEntry(MerchantEntry entry, MerchantInventory inventory)
    {
        if (RestockAfterPurchase == null)
        {
            Log.Warn("[BetterRewards] Could not reflect RestockAfterPurchase; a slot was not rerolled.");
            return;
        }

        RestockAfterPurchase.Invoke(entry, [inventory]);
        entry.OnMerchantInventoryUpdated();
    }

    private static void OnDisconnected(INetGameService netService)
    {
        lock (Sync)
        {
            if (Handlers.TryGetValue(netService, out var handler))
            {
                netService.UnregisterMessageHandler(handler);
                Handlers.Remove(netService);
            }

            if (DisconnectHandlers.TryGetValue(netService, out var disconnectHandler))
            {
                netService.Disconnected -= disconnectHandler;
                DisconnectHandlers.Remove(netService);
            }
        }
    }
}
