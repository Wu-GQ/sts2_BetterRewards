using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BetterRewards.Core;

/// <summary>
/// Bridges the merchant API changes between game builds so one assembly serves both the
/// public branch and public-beta.
/// <para>
/// The build targets the oldest supported game (0.111.x), so every call here must compile
/// against 0.111 and work on 0.113 through reflection. 0.113 changed four things this mod
/// touches:
/// </para>
/// <list type="bullet">
/// <item><c>MerchantEntry.Id</c> was added, plus a sequential assignment in <c>Populate</c>.
///      Slot order is identical in both builds, so the ordinal into <c>AllEntries</c> is used
///      instead and no reflection is needed for it.</item>
/// <item><c>MerchantCardEntry</c> gained an <c>inventory</c> constructor parameter, and
///      <c>Populate(MerchantInventory?)</c> replaced the parameterless <c>Populate()</c>.</item>
/// <item><c>MerchantSynchronizer</c> took over per-player shop ownership from
///      <c>MerchantRoom.Inventories</c>.</item>
/// </list>
/// </summary>
internal static class Sts2ApiCompat
{
    // Static field initializers run in declaration order, so the type must be resolved before
    // the boolean that reports whether it exists.
    private static readonly PropertyInfo? MerchantSynchronizerProperty =
        typeof(RunManager).GetProperty("MerchantSynchronizer", BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo? GetInventoryForMethod =
        MerchantSynchronizerProperty?.PropertyType.GetMethod("GetInventoryFor", BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    /// True when the running game exposes <c>MerchantSynchronizer</c> (0.113+). False on
    /// 0.111.x, where the room owns the inventories directly.
    /// </summary>
    public static readonly bool HasMerchantSynchronizer = MerchantSynchronizerProperty != null;

    private static readonly PropertyInfo? RoomInventoriesProperty =
        typeof(MerchantRoom).GetProperty("Inventories", BindingFlags.Public | BindingFlags.Instance);

    // 0.113: MerchantCardEntry(Player, IEnumerable<CardModel>, CardType|CardRarity)
    private static readonly ConstructorInfo? CardEntryByTypeCtor =
        typeof(MerchantCardEntry).GetConstructor([typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType)]);

    private static readonly ConstructorInfo? CardEntryByRarityCtor =
        typeof(MerchantCardEntry).GetConstructor([typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardRarity)]);

    // 0.111: MerchantCardEntry(Player, MerchantInventory, IEnumerable<CardModel>, CardType|CardRarity)
    private static readonly ConstructorInfo? CardEntryByTypeWithInventoryCtor = typeof(MerchantCardEntry)
        .GetConstructor([typeof(Player), typeof(MerchantInventory), typeof(IEnumerable<CardModel>), typeof(CardType)]);

    private static readonly ConstructorInfo? CardEntryByRarityWithInventoryCtor = typeof(MerchantCardEntry)
        .GetConstructor([typeof(Player), typeof(MerchantInventory), typeof(IEnumerable<CardModel>), typeof(CardRarity)]);

    // 0.113 takes the inventory here; 0.111 reads it from the constructor field and takes nothing.
    private static readonly MethodInfo? PopulateWithInventoryMethod =
        typeof(MerchantCardEntry).GetMethod("Populate", BindingFlags.Public | BindingFlags.Instance, null,
            [typeof(MerchantInventory)], null);

    private static readonly MethodInfo? PopulateParameterlessMethod =
        typeof(MerchantCardEntry).GetMethod("Populate", BindingFlags.Public | BindingFlags.Instance, null,
            Type.EmptyTypes, null);

    /// <summary>
    /// Creates a shop card entry for either game build. The caller must still populate it with
    /// <see cref="PopulateCardEntry"/>.
    /// </summary>
    public static MerchantCardEntry CreateCardEntry(Player player, MerchantInventory? inventory,
        IEnumerable<CardModel> pool, CardType type)
    {
        if (CardEntryByTypeCtor != null)
        {
            return (MerchantCardEntry)CardEntryByTypeCtor.Invoke([player, pool, type]);
        }

        if (CardEntryByTypeWithInventoryCtor != null)
        {
            return (MerchantCardEntry)CardEntryByTypeWithInventoryCtor.Invoke([player, inventory, pool, type]);
        }

        throw new MissingMethodException(
            typeof(MerchantCardEntry).FullName, "No CardType constructor matches this game build.");
    }

    /// <inheritdoc cref="CreateCardEntry"/>
    public static MerchantCardEntry CreateCardEntry(Player player, MerchantInventory? inventory,
        IEnumerable<CardModel> pool, CardRarity rarity)
    {
        if (CardEntryByRarityCtor != null)
        {
            return (MerchantCardEntry)CardEntryByRarityCtor.Invoke([player, pool, rarity]);
        }

        if (CardEntryByRarityWithInventoryCtor != null)
        {
            return (MerchantCardEntry)CardEntryByRarityWithInventoryCtor.Invoke([player, inventory, pool, rarity]);
        }

        throw new MissingMethodException(
            typeof(MerchantCardEntry).FullName, "No CardRarity constructor matches this game build.");
    }

    /// <summary>
    /// Rolls a shop card entry's offer on either game build.
    /// </summary>
    public static void PopulateCardEntry(MerchantCardEntry entry, MerchantInventory? inventory)
    {
        if (PopulateWithInventoryMethod != null)
        {
            PopulateWithInventoryMethod.Invoke(entry, [inventory]);
            return;
        }

        if (PopulateParameterlessMethod != null)
        {
            PopulateParameterlessMethod.Invoke(entry, null);
            return;
        }

        throw new MissingMethodException(typeof(MerchantCardEntry).FullName, "No Populate overload matches this game build.");
    }

    /// <summary>
    /// One-line probe for the startup log: which game build this assembly resolved, and which
    /// merchant path it will therefore take. Without it, a silently-failing reflection lookup
    /// on one branch would only show up as a missing shop button much later.
    /// </summary>
    public static string DescribeResolvedGameBuild()
    {
        var build = HasMerchantSynchronizer ? "MerchantSynchronizer (0.113+)" : "MerchantRoom.Inventories (0.111.x)";

        var missing = new List<string>();
        if (MerchantSynchronizerProperty == null && RoomInventoriesProperty == null)
        {
            missing.Add("shop lookup");
        }

        if (CardEntryByTypeCtor == null && CardEntryByTypeWithInventoryCtor == null)
        {
            missing.Add("card ctor (type)");
        }

        if (CardEntryByRarityCtor == null && CardEntryByRarityWithInventoryCtor == null)
        {
            missing.Add("card ctor (rarity)");
        }

        if (PopulateWithInventoryMethod == null && PopulateParameterlessMethod == null)
        {
            missing.Add("card Populate");
        }

        return missing.Count == 0
            ? $"merchant API: {build}."
            : $"merchant API: {build}, but these lookups did not resolve: {string.Join(", ", missing)}.";
    }

    /// <summary>
    /// Finds <em>this peer's copy</em> of another player's shop.
    /// <para>
    /// 0.113+ keeps every player's inventory on <c>RunManager.MerchantSynchronizer</c>. 0.111.x
    /// keeps them on <c>MerchantRoom.Inventories</c>, indexed by player slot. Both are keyed by
    /// the same slot index. Returns null when no merchant is active on this peer, which is the
    /// normal case once the room has been left.
    /// </para>
    /// </summary>
    public static MerchantInventory? FindInventoryFor(Player player)
    {
        if (HasMerchantSynchronizer)
        {
            var synchronizer = MerchantSynchronizerProperty?.GetValue(RunManager.Instance);
            if (synchronizer == null || GetInventoryForMethod == null)
            {
                return null;
            }

            return GetInventoryForMethod.Invoke(synchronizer, [player]) as MerchantInventory;
        }

        if (player.RunState.CurrentRoom is not MerchantRoom room || RoomInventoriesProperty == null)
        {
            return null;
        }

        if (RoomInventoriesProperty.GetValue(room) is not System.Collections.IList inventories)
        {
            return null;
        }

        var slot = player.RunState.GetPlayerSlotIndex(player);
        if (slot < 0 || slot >= inventories.Count)
        {
            Log.Warn($"[BetterRewards] Player slot {slot} is outside MerchantRoom.Inventories " +
                     $"(count {inventories.Count}).");
            return null;
        }

        return inventories[slot] as MerchantInventory;
    }
}
