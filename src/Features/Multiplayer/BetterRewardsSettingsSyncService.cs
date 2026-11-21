using BetterRewards.Features.Settings;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace BetterRewards.Features.Multiplayer;

internal struct BetterRewardsSettingsMessage : INetMessage, IPacketSerializable
{
    public double HpSacrificePercent;
    public int SacrificeRepeatCount;
    public double ScoreToGoldPercent;
    public int MinPreviousScore;
    public double ShopRerollBaseCost;
    public double ShopRerollCostIncrement;
    public double RelicBaseCost;
    public double RelicCostIncrement;
    public double RandomItemBaseCost;
    public double RandomItemCostIncrement;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        BetterRewardsNetSupport.WriteDouble(writer, HpSacrificePercent);
        writer.WriteInt(SacrificeRepeatCount);
        BetterRewardsNetSupport.WriteDouble(writer, ScoreToGoldPercent);
        writer.WriteInt(MinPreviousScore);
        BetterRewardsNetSupport.WriteDouble(writer, ShopRerollBaseCost);
        BetterRewardsNetSupport.WriteDouble(writer, ShopRerollCostIncrement);
        BetterRewardsNetSupport.WriteDouble(writer, RelicBaseCost);
        BetterRewardsNetSupport.WriteDouble(writer, RelicCostIncrement);
        BetterRewardsNetSupport.WriteDouble(writer, RandomItemBaseCost);
        BetterRewardsNetSupport.WriteDouble(writer, RandomItemCostIncrement);
    }

    public void Deserialize(PacketReader reader)
    {
        HpSacrificePercent = BetterRewardsNetSupport.ReadDouble(reader);
        SacrificeRepeatCount = reader.ReadInt();
        ScoreToGoldPercent = BetterRewardsNetSupport.ReadDouble(reader);
        MinPreviousScore = reader.ReadInt();
        ShopRerollBaseCost = BetterRewardsNetSupport.ReadDouble(reader);
        ShopRerollCostIncrement = BetterRewardsNetSupport.ReadDouble(reader);
        RelicBaseCost = BetterRewardsNetSupport.ReadDouble(reader);
        RelicCostIncrement = BetterRewardsNetSupport.ReadDouble(reader);
        RandomItemBaseCost = BetterRewardsNetSupport.ReadDouble(reader);
        RandomItemCostIncrement = BetterRewardsNetSupport.ReadDouble(reader);
    }

    public static BetterRewardsSettingsMessage FromModel(BetterRewardsSettingsModel model)
    {
        return new BetterRewardsSettingsMessage
        {
            HpSacrificePercent = model.HpSacrificePercent,
            SacrificeRepeatCount = model.SacrificeRepeatCount,
            ScoreToGoldPercent = model.ScoreToGoldPercent,
            MinPreviousScore = model.MinPreviousScore,
            ShopRerollBaseCost = model.ShopRerollBaseCost,
            ShopRerollCostIncrement = model.ShopRerollCostIncrement,
            RelicBaseCost = model.RelicBaseCost,
            RelicCostIncrement = model.RelicCostIncrement,
            RandomItemBaseCost = model.RandomItemBaseCost,
            RandomItemCostIncrement = model.RandomItemCostIncrement,
        };
    }

    public BetterRewardsSettingsModel ToModel()
    {
        var model = new BetterRewardsSettingsModel
        {
            HpSacrificePercent = HpSacrificePercent,
            SacrificeRepeatCount = SacrificeRepeatCount,
            ScoreToGoldPercent = ScoreToGoldPercent,
            MinPreviousScore = MinPreviousScore,
            ShopRerollBaseCost = ShopRerollBaseCost,
            ShopRerollCostIncrement = ShopRerollCostIncrement,
            RelicBaseCost = RelicBaseCost,
            RelicCostIncrement = RelicCostIncrement,
            RandomItemBaseCost = RandomItemBaseCost,
            RandomItemCostIncrement = RandomItemCostIncrement,
        };
        model.Clamp();
        return model;
    }
}

internal static class BetterRewardsSettingsSyncService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<BetterRewardsSettingsMessage>> Handlers = [];
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> DisconnectHandlers = [];
    private static BetterRewardsSettingsModel? _cached;

    public static void EnsureAttached(INetGameService netService, string source)
    {
        lock (Sync)
        {
            if (Handlers.ContainsKey(netService))
            {
                return;
            }

            MessageHandlerDelegate<BetterRewardsSettingsMessage> handler = (message, senderId) =>
                HandleMessage(netService, message, senderId);
            Action<NetErrorInfo> disconnectHandler = _ => OnDisconnected(netService);

            Handlers[netService] = handler;
            DisconnectHandlers[netService] = disconnectHandler;

            netService.RegisterMessageHandler(handler);
            netService.Disconnected += disconnectHandler;
            Log.Info($"[BetterRewards] Settings sync attached ({source}).");
        }
    }

    public static bool TryGetCached(out BetterRewardsSettingsModel snapshot)
    {
        lock (Sync)
        {
            if (_cached != null)
            {
                snapshot = _cached;
                return true;
            }
        }

        snapshot = null!;
        return false;
    }

    public static void CacheAndBroadcast(BetterRewardsSettingsModel model, string source)
    {
        var copy = Clone(model);
        lock (Sync)
        {
            _cached = copy;
        }

        var netService = BetterRewardsNetSupport.TryGetNetService();
        if (!BetterRewardsNetSupport.IsHost(netService) || netService == null)
        {
            return;
        }

        EnsureAttached(netService, source);
        netService.SendMessage(BetterRewardsSettingsMessage.FromModel(copy));
        Log.Info($"[BetterRewards] Host settings broadcast ({source}).");
    }

    public static void SendSnapshotTo(INetGameService netService, ulong targetPlayerId, string source)
    {
        if (!BetterRewardsNetSupport.IsHost(netService))
        {
            return;
        }

        BetterRewardsSettingsModel model;
        lock (Sync)
        {
            model = Clone(_cached ?? BetterRewardsSettings.Local);
            _cached = model;
        }

        EnsureAttached(netService, source);
        netService.SendMessage(BetterRewardsSettingsMessage.FromModel(model), targetPlayerId);
        Log.Info($"[BetterRewards] Host settings snapshot sent to {targetPlayerId} ({source}).");
    }

    public static void RefreshFromHostLocal(string source)
    {
        if (!BetterRewardsNetSupport.IsHost())
        {
            return;
        }

        CacheAndBroadcast(BetterRewardsSettings.Local, source);
    }

    private static void HandleMessage(
        INetGameService netService,
        BetterRewardsSettingsMessage message,
        ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        var model = message.ToModel();
        lock (Sync)
        {
            _cached = model;
        }

        Log.Info($"[BetterRewards] Applied host settings snapshot from {senderId}.");
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

            if (Handlers.Count == 0)
            {
                _cached = null;
            }
        }
    }

    private static BetterRewardsSettingsModel Clone(BetterRewardsSettingsModel source)
    {
        var copy = new BetterRewardsSettingsModel();
        copy.CopyFrom(source);
        return copy;
    }
}
