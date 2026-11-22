using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace BetterRewards.Features.Shop;

/// <summary>
/// OBSERVATION ONLY. Logs the timing of the three special-shop buttons so a real double-press
/// can be recognised in a player's <c>godot.log</c> instead of guessed at.
/// <para>
/// This deliberately does not block, debounce or reorder anything. The buttons are gated only by
/// the gold check in their press handler, and <c>PlayerCmd.LoseGold</c> settles synchronously, so
/// a second press normally already sees the reduced gold and is rejected. The remaining window is
/// the genuinely asynchronous part of an action (obtaining the relic, adding the card, procuring
/// the potion): during it, a second press can start while the first is still running.
/// </para>
/// <para>
/// What the log is meant to answer, if a report ever arrives:
/// </para>
/// <list type="bullet">
/// <item><c>Rapid re-press</c> — two presses inside <see cref="RapidPressWindowMs"/>. Includes
///      the gold at each press, so "rejected by the gold check" is distinguishable from
///      "charged twice".</item>
/// <item><c>overlapping</c> — the second action actually began before the first finished, which
///      is the only state that can double-spend the shop RNG or the relic grab bags.</item>
/// <item><c>spent != expected</c> — the action's own gold delta disagreed with the price it
///      quoted. That is the double-charge signature; it should never appear.</item>
/// </list>
/// </summary>
internal static class ShopActionProbe
{
    /// <summary>
    /// Two presses of the same button closer than this are reported. One second is long enough
    /// to catch a double-click yet short enough that deliberate repeat buying stays quiet.
    /// </summary>
    private const long RapidPressWindowMs = 1500;

    private static readonly object Sync = new();

    /// <summary>Last press time per action, in <see cref="Environment.TickCount64"/> ms.</summary>
    private static readonly Dictionary<string, long> LastPressMs = [];

    /// <summary>Actions currently running per action name.</summary>
    private static readonly Dictionary<string, int> InFlight = [];

    /// <summary>
    /// Records a button press, before the gold check, so presses that are rejected for lack of
    /// gold still leave a trace. Only rapid repeats are logged to keep a normal shop visit quiet.
    /// </summary>
    public static void NotePress(string action, Player player, int cost)
    {
        var now = Environment.TickCount64;

        long previous;
        bool isRepeat;
        lock (Sync)
        {
            isRepeat = LastPressMs.TryGetValue(action, out previous) && now - previous < RapidPressWindowMs;
            LastPressMs[action] = now;
        }

        if (!isRepeat)
        {
            return;
        }

        Log.Warn($"[BetterRewards] Rapid re-press on '{action}' {now - previous} ms after the previous press " +
                 $"(gold={player.Gold}, cost={cost}, inFlight={ReadInFlight(action)}). " +
                 "Watch the following lines: a second charge is the thing this probe exists to catch.");
    }

    /// <summary>
    /// Marks an action as running until the returned scope is disposed. Use with <c>using var</c>
    /// so every early return and exception still closes the scope and logs its end line.
    /// </summary>
    public static Scope Begin(string action, Player player, int cost)
    {
        return new Scope(action, player, cost);
    }

    private static int ReadInFlight(string action)
    {
        lock (Sync)
        {
            return InFlight.TryGetValue(action, out var count) ? count : 0;
        }
    }

    internal sealed class Scope : IDisposable
    {
        private readonly string _action;
        private readonly Player _player;
        private readonly int _cost;
        private readonly int _goldBefore;
        private readonly long _startMs;
        private bool _disposed;

        internal Scope(string action, Player player, int cost)
        {
            _action = action;
            _player = player;
            _cost = cost;
            _goldBefore = player.Gold;
            _startMs = Environment.TickCount64;

            int running;
            lock (Sync)
            {
                running = InFlight.TryGetValue(action, out var count) ? count + 1 : 1;
                InFlight[action] = running;
            }

            // running > 1 is the case worth reading: the previous action of this kind had not
            // finished, so both are now drawing from the same shop RNG and grab bags.
            Log.Info($"[BetterRewards] {action} start (cost={cost}, gold={_goldBefore}, " +
                     $"inFlight={running}{(running > 1 ? " OVERLAPPING" : string.Empty)}).");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            int remaining;
            lock (Sync)
            {
                remaining = InFlight.TryGetValue(_action, out var count) ? Math.Max(0, count - 1) : 0;
                InFlight[_action] = remaining;
            }

            var elapsed = Environment.TickCount64 - _startMs;
            var goldAfter = _player.Gold;
            var spent = _goldBefore - goldAfter;

            Log.Info($"[BetterRewards] {_action} end after {elapsed} ms " +
                     $"(gold {_goldBefore}->{goldAfter}, spent {spent}, quoted {_cost}, inFlight={remaining}).");

            // spent == 0 is the action's own no-op path: the gold check rejected it, or the roll
            // produced nothing to sell. spent == quoted is the expected single charge. Anything
            // else means either two charges landed in this window, or another gold source moved
            // during the action; both deserve a line in the log rather than a silent pass.
            if (spent != 0 && spent != _cost)
            {
                Log.Warn($"[BetterRewards] {_action} spent {spent} but quoted {_cost}. " +
                         "spent == 2 x quoted is a double charge; any other value means another " +
                         "gold change overlapped this action.");
            }
        }
    }
}
