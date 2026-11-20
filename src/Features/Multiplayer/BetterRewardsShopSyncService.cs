using System.Reflection;
using BetterRewards.Features.Shop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BetterRewards.Features.Multiplayer;

internal enum BetterRewardsShopSlotKind
{
    Card = 0,
    Relic = 1,
    Potion = 2,
    CardRemoval = 3,
}

internal enum BetterRewardsShopGrantKind
{
    Relic = 0,
    Card = 1,
    Potion = 2,
}

internal struct BetterRewardsShopSlotSnapshot
{
    public uint EntryId;
    public BetterRewardsShopSlotKind Kind;
    public string ModelId;
    public int Cost;
    public bool IsStocked;
    public bool IsOnSale;
    public bool CardRemovalUsed;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt((int)EntryId);
        writer.WriteInt((int)Kind);
        writer.WriteString(ModelId ?? string.Empty);
        writer.WriteInt(Cost);
        BetterRewardsNetSupport.WriteBool(writer, IsStocked);
        BetterRewardsNetSupport.WriteBool(writer, IsOnSale);
        BetterRewardsNetSupport.WriteBool(writer, CardRemovalUsed);
    }

    public void Deserialize(PacketReader reader)
    {
        EntryId = (uint)reader.ReadInt();
        Kind = (BetterRewardsShopSlotKind)reader.ReadInt();
        ModelId = reader.ReadString();
        Cost = reader.ReadInt();
        IsStocked = BetterRewardsNetSupport.ReadBool(reader);
        IsOnSale = BetterRewardsNetSupport.ReadBool(reader);
        CardRemovalUsed = BetterRewardsNetSupport.ReadBool(reader);
    }
}

internal struct BetterRewardsShopRerollMessage : INetMessage, IPacketSerializable
{
    public ulong ActorNetId;
    public int PaidCost;
    public int RerollUseCount;
    public int RelicUseCount;
    public int RandomItemUseCount;
    public BetterRewardsShopSlotSnapshot[] Slots;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        BetterRewardsNetSupport.WriteULong(writer, ActorNetId);
        writer.WriteInt(PaidCost);
        writer.WriteInt(RerollUseCount);
        writer.WriteInt(RelicUseCount);
        writer.WriteInt(RandomItemUseCount);
        var slots = Slots ?? [];
        writer.WriteInt(slots.Length);
        foreach (var slot in slots)
        {
            slot.Serialize(writer);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        ActorNetId = BetterRewardsNetSupport.ReadULong(reader);
        PaidCost = reader.ReadInt();
        RerollUseCount = reader.ReadInt();
        RelicUseCount = reader.ReadInt();
        RandomItemUseCount = reader.ReadInt();
        var count = Math.Max(0, reader.ReadInt());
        Slots = new BetterRewardsShopSlotSnapshot[count];
        for (var i = 0; i < count; i++)
        {
            var slot = new BetterRewardsShopSlotSnapshot();
            slot.Deserialize(reader);
            Slots[i] = slot;
        }
    }
}

internal struct BetterRewardsShopGrantMessage : INetMessage, IPacketSerializable
{
    public ulong BuyerNetId;
    public BetterRewardsShopGrantKind Kind;
    public string ModelId;
    public int PaidCost;
    public int RerollUseCount;
    public int RelicUseCount;
    public int RandomItemUseCount;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        BetterRewardsNetSupport.WriteULong(writer, BuyerNetId);
        writer.WriteInt((int)Kind);
        writer.WriteString(ModelId ?? string.Empty);
        writer.WriteInt(PaidCost);
        writer.WriteInt(RerollUseCount);
        writer.WriteInt(RelicUseCount);
        writer.WriteInt(RandomItemUseCount);
    }

    public void Deserialize(PacketReader reader)
    {
        BuyerNetId = BetterRewardsNetSupport.ReadULong(reader);
        Kind = (BetterRewardsShopGrantKind)reader.ReadInt();
        ModelId = reader.ReadString();
        PaidCost = reader.ReadInt();
        RerollUseCount = reader.ReadInt();
        RelicUseCount = reader.ReadInt();
        RandomItemUseCount = reader.ReadInt();
    }
}

internal static class BetterRewardsShopSyncService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<BetterRewardsShopRerollMessage>> RerollHandlers = [];
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<BetterRewardsShopGrantMessage>> GrantHandlers = [];
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> DisconnectHandlers = [];
    private static readonly FieldInfo? CostField =
        typeof(MerchantEntry).GetField("_cost", BindingFlags.Instance | BindingFlags.NonPublic);

    private static BetterRewardsShopRerollMessage? _pendingReroll;

    public static void EnsureAttached(INetGameService netService, string source)
    {
        lock (Sync)
        {
            if (RerollHandlers.ContainsKey(netService))
            {
                return;
            }

            MessageHandlerDelegate<BetterRewardsShopRerollMessage> rerollHandler = (message, senderId) =>
                HandleRerollMessage(netService, message, senderId);
            MessageHandlerDelegate<BetterRewardsShopGrantMessage> grantHandler = (message, senderId) =>
                HandleGrantMessage(netService, message, senderId);
            Action<NetErrorInfo> disconnectHandler = _ => OnDisconnected(netService);

            RerollHandlers[netService] = rerollHandler;
            GrantHandlers[netService] = grantHandler;
            DisconnectHandlers[netService] = disconnectHandler;

            netService.RegisterMessageHandler(rerollHandler);
            netService.RegisterMessageHandler(grantHandler);
            netService.Disconnected += disconnectHandler;
            Log.Info($"[BetterRewards] Shop sync attached ({source}).");
        }
    }

    public static void BroadcastReroll(Player actor, MerchantInventory inventory, int paidCost)
    {
        var netService = BetterRewardsNetSupport.TryGetNetService();
        if (!BetterRewardsNetSupport.IsConnectedMultiplayer(netService) || netService == null)
        {
            return;
        }

        EnsureAttached(netService, "BroadcastReroll");
        var message = new BetterRewardsShopRerollMessage
        {
            ActorNetId = actor.NetId,
            PaidCost = paidCost,
            RerollUseCount = BetterRewardsShopState.RerollUseCount,
            RelicUseCount = BetterRewardsShopState.RelicUseCount,
            RandomItemUseCount = BetterRewardsShopState.RandomItemUseCount,
            Slots = CaptureSlots(inventory),
        };
        netService.SendMessage(message);
        Log.Info($"[BetterRewards] Broadcast shop reroll ({message.Slots.Length} slots, paid {paidCost}).");
    }

    public static void BroadcastGrant(Player buyer, BetterRewardsShopGrantKind kind, string modelId, int paidCost)
    {
        var netService = BetterRewardsNetSupport.TryGetNetService();
        if (!BetterRewardsNetSupport.IsConnectedMultiplayer(netService) || netService == null)
        {
            return;
        }

        EnsureAttached(netService, "BroadcastGrant");
        netService.SendMessage(new BetterRewardsShopGrantMessage
        {
            BuyerNetId = buyer.NetId,
            Kind = kind,
            ModelId = modelId ?? string.Empty,
            PaidCost = paidCost,
            RerollUseCount = BetterRewardsShopState.RerollUseCount,
            RelicUseCount = BetterRewardsShopState.RelicUseCount,
            RandomItemUseCount = BetterRewardsShopState.RandomItemUseCount,
        });
        Log.Info($"[BetterRewards] Broadcast shop grant {kind} {modelId} for {paidCost} gold.");
    }

    public static void TryApplyPendingReroll(NMerchantInventory nInventory)
    {
        BetterRewardsShopRerollMessage message;
        lock (Sync)
        {
            if (_pendingReroll is not { } pending)
            {
                return;
            }

            message = pending;
        }

        ApplyReroll(message, nInventory, deductGold: false);
    }

    private static void HandleRerollMessage(
        INetGameService netService,
        BetterRewardsShopRerollMessage message,
        ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        BetterRewardsNetSupport.RunOnMainThread(() =>
        {
            var nInventory = NMerchantRoom.Instance?.Inventory;
            ApplyReroll(message, nInventory, deductGold: true);
        });
    }

    private static void HandleGrantMessage(
        INetGameService netService,
        BetterRewardsShopGrantMessage message,
        ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        BetterRewardsNetSupport.RunSafelyOnMainThread(() => ApplyGrant(message));
    }

    private static void ApplyReroll(BetterRewardsShopRerollMessage message, NMerchantInventory? nInventory, bool deductGold)
    {
        BetterRewardsShopState.ApplyUseCounts(message.RerollUseCount, message.RelicUseCount, message.RandomItemUseCount);

        if (deductGold && message.PaidCost > 0)
        {
            var actor = BetterRewardsNetSupport.FindPlayer(message.ActorNetId);
            if (actor != null)
            {
                TaskHelper.RunSafely(PlayerCmd.LoseGold(message.PaidCost, actor, GoldLossType.Spent));
            }
        }

        var inventory = nInventory?.Inventory ?? NMerchantRoom.Instance?.Inventory?.Inventory;
        if (inventory == null)
        {
            lock (Sync)
            {
                _pendingReroll = message;
            }

            Log.Info("[BetterRewards] Cached shop reroll snapshot until the merchant inventory is ready.");
            return;
        }

        ApplySlots(inventory, message.Slots ?? []);
        lock (Sync)
        {
            _pendingReroll = null;
        }

        if (nInventory != null)
        {
            BetterRewardsShopButtonsFeature.RefreshAfterRemoteInventoryChange(nInventory);
        }
        else
        {
            BetterRewardsShopButtonsFeature.RefreshActiveLabels();
        }

        Log.Info($"[BetterRewards] Applied remote shop reroll ({message.Slots?.Length ?? 0} slots).");
    }

    private static async Task ApplyGrant(BetterRewardsShopGrantMessage message)
    {
        BetterRewardsShopState.ApplyUseCounts(message.RerollUseCount, message.RelicUseCount, message.RandomItemUseCount);

        var buyer = BetterRewardsNetSupport.FindPlayer(message.BuyerNetId);
        if (buyer == null)
        {
            Log.Warn($"[BetterRewards] Shop grant buyer {message.BuyerNetId} not found.");
            BetterRewardsShopButtonsFeature.RefreshActiveLabels();
            return;
        }

        try
        {
            switch (message.Kind)
            {
                case BetterRewardsShopGrantKind.Relic:
                {
                    var relic = BetterRewardsModelLookup.FindRelic(message.ModelId, buyer);
                    if (relic == null)
                    {
                        Log.Warn($"[BetterRewards] Could not resolve relic {message.ModelId} for remote grant.");
                        break;
                    }

                    if (message.PaidCost > 0)
                    {
                        await PlayerCmd.LoseGold(message.PaidCost, buyer, GoldLossType.Spent);
                    }

                    var mutable = relic.IsMutable ? relic : relic.ToMutable();
                    await RelicCmd.Obtain(mutable, buyer);
                    break;
                }
                case BetterRewardsShopGrantKind.Card:
                {
                    var card = BetterRewardsModelLookup.FindCard(message.ModelId, buyer);
                    if (card == null)
                    {
                        Log.Warn($"[BetterRewards] Could not resolve card {message.ModelId} for remote grant.");
                        break;
                    }

                    if (!await BetterRewardsModelLookup.AddCardToDeck(card, buyer))
                    {
                        Log.Warn($"[BetterRewards] Remote card grant could not be added to the deck: {message.ModelId}");
                        break;
                    }

                    if (message.PaidCost > 0)
                    {
                        await PlayerCmd.LoseGold(message.PaidCost, buyer, GoldLossType.Spent);
                    }

                    BetterRewardsShopButtonsFeature.AnimateRemoteCardToDeck(card, buyer);
                    break;
                }
                case BetterRewardsShopGrantKind.Potion:
                {
                    var potion = BetterRewardsModelLookup.FindPotion(message.ModelId, buyer);
                    if (potion == null)
                    {
                        Log.Warn($"[BetterRewards] Could not resolve potion {message.ModelId} for remote grant.");
                        break;
                    }

                    var result = await PotionCmd.TryToProcure(potion, buyer);
                    if (!result.success)
                    {
                        Log.Warn($"[BetterRewards] Remote potion grant could not be procured: {message.ModelId}");
                        break;
                    }

                    if (message.PaidCost > 0)
                    {
                        await PlayerCmd.LoseGold(message.PaidCost, buyer, GoldLossType.Spent);
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Remote shop grant failed: {ex}");
        }

        BetterRewardsShopButtonsFeature.RefreshActiveLabels();
    }

    private static BetterRewardsShopSlotSnapshot[] CaptureSlots(MerchantInventory inventory)
    {
        var slots = new List<BetterRewardsShopSlotSnapshot>();
        foreach (var entry in inventory.AllEntries)
        {
            if (entry.Id is not uint id)
            {
                continue;
            }

            switch (entry)
            {
                case MerchantCardEntry card:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Card,
                        ModelId = card.CreationResult?.Card.Id.ToString() ?? string.Empty,
                        Cost = ReadBaseCost(card),
                        IsStocked = card.IsStocked,
                        IsOnSale = card.IsOnSale,
                    });
                    break;
                case MerchantRelicEntry relic:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Relic,
                        ModelId = relic.Model?.Id.ToString() ?? string.Empty,
                        Cost = ReadBaseCost(relic),
                        IsStocked = relic.IsStocked,
                    });
                    break;
                case MerchantPotionEntry potion:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.Potion,
                        ModelId = potion.Model?.Id.ToString() ?? string.Empty,
                        Cost = ReadBaseCost(potion),
                        IsStocked = potion.IsStocked,
                    });
                    break;
                case MerchantCardRemovalEntry removal:
                    slots.Add(new BetterRewardsShopSlotSnapshot
                    {
                        EntryId = id,
                        Kind = BetterRewardsShopSlotKind.CardRemoval,
                        ModelId = string.Empty,
                        Cost = ReadBaseCost(removal),
                        IsStocked = removal.IsStocked,
                        CardRemovalUsed = IsCardRemovalUsed(removal),
                    });
                    break;
            }
        }

        return slots.ToArray();
    }

    private static void ApplySlots(MerchantInventory inventory, IReadOnlyList<BetterRewardsShopSlotSnapshot> slots)
    {
        var byId = inventory.AllEntries
            .Where(entry => entry.Id != null)
            .GroupBy(entry => entry.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var slot in slots)
        {
            if (!byId.TryGetValue(slot.EntryId, out var entry))
            {
                Log.Warn($"[BetterRewards] Reroll snapshot entry {slot.EntryId} was not found.");
                continue;
            }

            try
            {
                switch (entry)
                {
                    case MerchantCardEntry card:
                        ApplyCard(card, slot, inventory.Player);
                        break;
                    case MerchantRelicEntry relic:
                        ApplyRelic(relic, slot, inventory.Player);
                        break;
                    case MerchantPotionEntry potion:
                        ApplyPotion(potion, slot, inventory.Player);
                        break;
                    case MerchantCardRemovalEntry removal:
                        ApplyCardRemoval(removal, slot);
                        break;
                }

                entry.OnMerchantInventoryUpdated();
            }
            catch (Exception ex)
            {
                Log.Warn($"[BetterRewards] Failed applying reroll slot {slot.EntryId}: {ex.Message}");
            }
        }
    }

    private static void ApplyCard(MerchantCardEntry entry, BetterRewardsShopSlotSnapshot slot, Player player)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            SetProperty(entry, "CreationResult", null);
            SetProperty(entry, "IsOnSale", false);
            return;
        }

        var card = BetterRewardsModelLookup.FindCard(slot.ModelId, player);
        var creation = card == null ? null : BetterRewardsModelLookup.CreateCardCreationResult(card);
        if (creation == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop card {slot.ModelId}.");
            return;
        }

        SetProperty(entry, "CreationResult", creation);
        SetProperty(entry, "IsOnSale", slot.IsOnSale);
        WriteBaseCost(entry, slot.Cost);
    }

    private static void ApplyRelic(MerchantRelicEntry entry, BetterRewardsShopSlotSnapshot slot, Player player)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            SetProperty(entry, "Model", null);
            return;
        }

        var relic = BetterRewardsModelLookup.FindRelic(slot.ModelId, player);
        if (relic == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop relic {slot.ModelId}.");
            return;
        }

        var mutable = relic.IsMutable ? relic : relic.ToMutable();
        SetProperty(entry, "Model", mutable);
        WriteBaseCost(entry, slot.Cost);
    }

    private static void ApplyPotion(MerchantPotionEntry entry, BetterRewardsShopSlotSnapshot slot, Player player)
    {
        if (!slot.IsStocked || string.IsNullOrWhiteSpace(slot.ModelId))
        {
            SetProperty(entry, "Model", null);
            return;
        }

        var potion = BetterRewardsModelLookup.FindPotion(slot.ModelId, player);
        if (potion == null)
        {
            Log.Warn($"[BetterRewards] Could not rebuild shop potion {slot.ModelId}.");
            return;
        }

        SetProperty(entry, "Model", potion);
        WriteBaseCost(entry, slot.Cost);
    }

    private static void ApplyCardRemoval(MerchantCardRemovalEntry entry, BetterRewardsShopSlotSnapshot slot)
    {
        BetterRewardsShopButtonsFeature.SetCardRemovalUsed(entry, slot.CardRemovalUsed);
        WriteBaseCost(entry, slot.Cost);
    }

    private static bool IsCardRemovalUsed(MerchantCardRemovalEntry entry)
    {
        var property = typeof(MerchantCardRemovalEntry).GetProperty("Used", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(entry) is true;
    }

    private static int ReadBaseCost(MerchantEntry entry)
    {
        return CostField?.GetValue(entry) is int cost ? cost : entry.Cost;
    }

    private static void WriteBaseCost(MerchantEntry entry, int cost)
    {
        CostField?.SetValue(entry, cost);
    }

    private static void SetProperty(object target, string name, object? value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var setter = property?.GetSetMethod(nonPublic: true);
        if (setter == null)
        {
            var backing = target.GetType().GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backing?.SetValue(target, value);
            return;
        }

        setter.Invoke(target, [value]);
    }

    private static void OnDisconnected(INetGameService netService)
    {
        lock (Sync)
        {
            if (RerollHandlers.TryGetValue(netService, out var rerollHandler))
            {
                netService.UnregisterMessageHandler(rerollHandler);
                RerollHandlers.Remove(netService);
            }

            if (GrantHandlers.TryGetValue(netService, out var grantHandler))
            {
                netService.UnregisterMessageHandler(grantHandler);
                GrantHandlers.Remove(netService);
            }

            if (DisconnectHandlers.TryGetValue(netService, out var disconnectHandler))
            {
                netService.Disconnected -= disconnectHandler;
                DisconnectHandlers.Remove(netService);
            }

            if (RerollHandlers.Count == 0)
            {
                _pendingReroll = null;
            }
        }
    }
}
