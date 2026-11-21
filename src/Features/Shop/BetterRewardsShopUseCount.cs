namespace BetterRewards.Features.Shop;

/// <summary>
/// Per-player special-shop use counts and the price ladder derived from them.
/// <para>
/// This type is deliberately free of any game, Godot or settings dependency so the
/// multiplayer invariant can be unit tested: <em>one player's button presses must never
/// change another player's price.</em> Every player owns a separate merchant inventory in
/// co-op, and prices are part of that, so the counts are bucketed by player net id rather
/// than kept in a single global counter.
/// </para>
/// </summary>
internal sealed class BetterRewardsShopUseCount
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, Counts> _byPlayer = [];

    private sealed class Counts
    {
        public int Reroll;
        public int Relic;
        public int RandomItem;
    }

    /// <summary>Reads one player's counts; unknown players read as all zeroes.</summary>
    public (int Reroll, int Relic, int RandomItem) Get(ulong netId)
    {
        lock (_sync)
        {
            if (_byPlayer.TryGetValue(netId, out var counts))
            {
                return (counts.Reroll, counts.Relic, counts.RandomItem);
            }
        }

        return (0, 0, 0);
    }

    public void RecordReroll(ulong netId) => Mutate(netId, counts => counts.Reroll++);

    public void RecordRelic(ulong netId) => Mutate(netId, counts => counts.Relic++);

    public void RecordRandomItem(ulong netId) => Mutate(netId, counts => counts.RandomItem++);

    /// <summary>Overwrites one player's counts with an authoritative set from the acting peer.</summary>
    public void Apply(ulong netId, int reroll, int relic, int randomItem)
    {
        lock (_sync)
        {
            _byPlayer[netId] = new Counts
            {
                Reroll = Math.Max(0, reroll),
                Relic = Math.Max(0, relic),
                RandomItem = Math.Max(0, randomItem),
            };
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _byPlayer.Clear();
        }
    }

    /// <summary>
    /// Prices climb by a fixed increment per use: <c>base + increment * uses</c>, rounded.
    /// </summary>
    public static int ComputeCost(double baseCost, double increment, int uses) =>
        (int)Math.Round(baseCost + increment * Math.Max(0, uses));

    private void Mutate(ulong netId, Action<Counts> mutate)
    {
        lock (_sync)
        {
            if (!_byPlayer.TryGetValue(netId, out var counts))
            {
                counts = new Counts();
                _byPlayer[netId] = counts;
            }

            mutate(counts);
        }
    }
}
