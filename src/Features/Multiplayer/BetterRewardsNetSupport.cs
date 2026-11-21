using System.Globalization;
using System.Reflection;
using BetterRewards.Features.Shop;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace BetterRewards.Features.Multiplayer;

internal static class BetterRewardsNetSupport
{
    public static INetGameService? TryGetNetService() => RunManager.Instance?.NetService;

    public static bool IsConnectedMultiplayer(INetGameService? netService = null)
    {
        netService ??= TryGetNetService();
        return netService != null && netService.IsConnected && netService.Type != NetGameType.Singleplayer;
    }

    public static bool IsHost(INetGameService? netService = null)
    {
        netService ??= TryGetNetService();
        return netService != null && netService.IsConnected && netService.Type == NetGameType.Host;
    }

    public static bool IsMultiplayerRun(IRunState? run = null)
    {
        if (IsConnectedMultiplayer())
        {
            return true;
        }

        run ??= TryGetRunState();
        return run != null && run.Players.Count > 1;
    }

    public static IRunState? TryGetRunState()
    {
        if (RunManager.Instance?.DebugOnlyGetState() is RunState debugState)
        {
            return debugState;
        }

        var instance = RunManager.Instance;
        if (instance != null)
        {
            foreach (var name in new[] { "State", "CurrentState", "RunState", "ActiveState", "ActiveRun" })
            {
                var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(instance) is IRunState runState)
                {
                    return runState;
                }
            }
        }

        var nRun = NRun.Instance;
        if (nRun != null)
        {
            var property = nRun.GetType().GetProperty("RunState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(nRun) is IRunState fromNode)
            {
                return fromNode;
            }
        }

        return null;
    }

    public static Player? FindPlayer(ulong netId)
    {
        var run = TryGetRunState();
        return run?.Players.FirstOrDefault(player => player.NetId == netId);
    }

    /// <summary>
    /// The local player, for code paths that run before a room node can name them.
    /// </summary>
    public static Player? FindLocalPlayer()
    {
        var localId = LocalContext.NetId;
        return localId.HasValue ? FindPlayer(localId.Value) : null;
    }

    /// <summary>
    /// Runs an async action on the Godot main thread.
    /// <para>
    /// The deferred callback must not return <see cref="Task"/>. Godot's
    /// <c>Callable.From</c> trampoline tries to convert a lambda's return value into a
    /// <c>Variant</c>, and <c>Task</c> has no Variant conversion, so the call throws
    /// <c>InvalidOperationException</c> before the work ever runs. The async body is therefore
    /// started inside a void lambda.
    /// </para>
    /// </summary>
    public static void RunSafelyOnMainThread(Func<Task> action)
    {
        Callable.From(() =>
        {
            try
            {
                TaskHelper.RunSafely(action());
            }
            catch (Exception ex)
            {
                Log.Warn($"[BetterRewards] Deferred multiplayer apply failed: {ex}");
            }
        }).CallDeferred();
    }

    public static void WriteDouble(PacketWriter writer, double value) =>
        writer.WriteString(value.ToString("G17", CultureInfo.InvariantCulture));

    public static double ReadDouble(PacketReader reader)
    {
        var raw = reader.ReadString();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }
}
