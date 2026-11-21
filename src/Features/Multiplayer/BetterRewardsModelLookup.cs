using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace BetterRewards.Features.Multiplayer;

/// <summary>
/// Resolves the model ids carried by BetterRewards net messages back into live game models.
/// <para>
/// Ids are the wire format on purpose: they are stable across peers regardless of load order,
/// and resolving by id is what lets a replay remove a relic from the grab bags by identity
/// instead of trying to reproduce another peer's random draws.
/// </para>
/// </summary>
internal static class BetterRewardsModelLookup
{
    /// <summary>Mutable relic, ready for <c>RelicCmd.Obtain</c>.</summary>
    public static RelicModel? FindRelic(string id) => FindCanonicalRelic(id)?.ToMutable();

    /// <summary>Canonical relic, used to remove an entry from a grab bag by identity.</summary>
    public static RelicModel? FindCanonicalRelic(string id) => FindCanonical<RelicModel>(id);

    /// <summary>Mutable potion, ready for <c>PotionCmd.TryToProcure</c>.</summary>
    public static PotionModel? FindPotion(string id)
    {
        var canonical = FindCanonical<PotionModel>(id);
        return canonical?.ToMutable();
    }

    /// <summary>Canonical card, ready for <c>RunState.CreateCard</c>.</summary>
    public static CardModel? FindCanonicalCard(string id) => FindCanonical<CardModel>(id);

    /// <summary>True when the id names a card, used to tell the three random-item kinds apart.</summary>
    public static bool IsCard(string id) => FindCanonical<CardModel>(id) != null;

    /// <summary>True when the id names a potion.</summary>
    public static bool IsPotion(string id) => FindCanonical<PotionModel>(id) != null;

    /// <summary>
    /// Creates the card inside the buyer's run state and moves it into their deck.
    /// <para>
    /// <c>RunState.CreateCard</c> needs the canonical model; <c>CardPileCmd.Add</c> then throws
    /// unless the card is already registered with the run state, which is why creation comes
    /// first. The upgrade flag travels on the wire because the shop's upgrade roll is part of
    /// what the buyer paid for.
    /// </para>
    /// </summary>
    public static async Task<bool> AddCardToDeck(string rawId, Player player, bool upgraded)
    {
        var canonical = FindCanonicalCard(rawId);
        if (canonical == null)
        {
            Log.Warn($"[BetterRewards] Could not resolve granted card {rawId} for replay.");
            return false;
        }

        var card = player.RunState.CreateCard(canonical, player);
        if (upgraded && card.IsUpgradable)
        {
            CardCmd.Upgrade(card);
        }

        var result = await CardPileCmd.Add(card, PileType.Deck);
        if (!result.success)
        {
            Log.Warn($"[BetterRewards] Could not move granted card {rawId} into the deck.");
        }

        return result.success;
    }

    private static T? FindCanonical<T>(string rawId) where T : AbstractModel
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return null;
        }

        ModelId modelId;
        try
        {
            modelId = ModelId.Deserialize(rawId);
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Malformed model id '{rawId}': {ex.Message}");
            return null;
        }

        // ModelDb.GetByIdOrNull<T> does an unguarded (T) cast, so asking for the wrong model
        // type throws instead of returning null. Probing a card id as a relic (and vice versa)
        // is exactly what the random-item replay does, so the cast failure is expected input
        // here rather than an error.
        T? canonical;
        try
        {
            canonical = ModelDb.GetByIdOrNull<T>(modelId);
        }
        catch (InvalidCastException)
        {
            return null;
        }

        if (canonical == null)
        {
            return null;
        }

        return canonical.IsMutable ? null : canonical;
    }
}
