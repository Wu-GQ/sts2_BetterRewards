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
/// against 0.111 and work on 0.113 through reflection. 0.113 changed five things this mod
/// touches:
/// </para>
/// <list type="bullet">
/// <item><c>MerchantInventory.AllEntries</c> changed from <c>IEnumerable&lt;MerchantEntry&gt;</c>
///      to <c>List&lt;MerchantEntry&gt;</c>. A property getter's return type is part of its
///      metadata signature, so a call compiled against 0.111 binds to
///      <c>get_AllEntries() : IEnumerable&lt;...&gt;</c> and throws
///      <c>MissingMethodException</c> on 0.113. It must be reached through
///      <see cref="GetAllEntries"/>, never by a direct call.</item>
/// <item><c>MerchantEntry.Id</c> was added, plus a sequential assignment in <c>Setup</c>. Read
///      it through <see cref="TryGetEntryId"/>.</item>
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
    /// <c>MerchantInventory.AllEntries</c>. Reflected because its return type differs per build
    /// (<c>List&lt;MerchantEntry&gt;</c> on 0.113, <c>IEnumerable&lt;MerchantEntry&gt;</c> on
    /// 0.111), and a getter's return type is part of the method signature.
    /// </summary>
    private static readonly PropertyInfo? AllEntriesProperty =
        typeof(MerchantInventory).GetProperty("AllEntries", BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    /// <c>MerchantEntry.Id</c> (0.113+). Absent on 0.111.x, where the ordinal into
    /// <c>AllEntries</c> is the only identity a shelf item has.
    /// </summary>
    private static readonly PropertyInfo? EntryIdProperty =
        typeof(MerchantEntry).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

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
    /// Enumerates a shop's entries on either game build.
    /// <para>
    /// This is the only permitted way to read <c>AllEntries</c>. A direct call compiles against
    /// the 0.111 signature (<c>IEnumerable&lt;MerchantEntry&gt;</c>) and throws
    /// <c>MissingMethodException</c> on 0.113, where the property returns
    /// <c>List&lt;MerchantEntry&gt;</c>. That failure is not cosmetic: the reroll builds its wire
    /// snapshot from this list, so a throw here aborts the publish and teammates keep seeing the
    /// pre-reroll shelves.
    /// </para>
    /// <para>
    /// Never throws. A lookup that did not resolve, or a getter that fails, yields an empty list
    /// and a warning: degrading to "no slots carried" still lets the rest of the action replicate,
    /// which is strictly better than losing the whole message.
    /// </para>
    /// </summary>
    public static IReadOnlyList<MerchantEntry> GetAllEntries(MerchantInventory inventory)
    {
        if (AllEntriesProperty == null)
        {
            Log.Warn("[BetterRewards] MerchantInventory.AllEntries did not resolve on this game build; " +
                     "shop sync will carry no slots.");
            return [];
        }

        try
        {
            // List<T> and IEnumerable<T> both assign to this, so one path serves both builds.
            return AllEntriesProperty.GetValue(inventory) is IEnumerable<MerchantEntry> entries
                ? entries as IReadOnlyList<MerchantEntry> ?? entries.ToList()
                : [];
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Could not enumerate the merchant inventory: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// The engine's own identity for a shelf item, or null on a build that has no
    /// <c>MerchantEntry.Id</c>.
    /// <para>
    /// 0.113 replays a purchase as "player X bought entry N" and resolves N with
    /// <c>AllEntries.FirstOrDefault(e =&gt; e.Id == N)</c>, so a snapshot slot must carry this
    /// exact value. 0.111 has no such property and no peer ever re-reads another player's
    /// shelves, so the caller falls back to the ordinal there.
    /// </para>
    /// </summary>
    public static uint? TryGetEntryId(MerchantEntry entry)
    {
        if (EntryIdProperty == null)
        {
            return null;
        }

        try
        {
            // The property is uint?; a boxed value unboxes to uint, and an unset one to null.
            return EntryIdProperty.GetValue(entry) is uint id ? id : null;
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Could not read MerchantEntry.Id: {ex.Message}");
            return null;
        }
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

        // These two are the ones that fail silently today: without them the reroll builds an
        // empty snapshot and stops publishing, and the only trace is a MissingMethodException
        // at the moment a player presses the button.
        if (AllEntriesProperty == null)
        {
            missing.Add("entry list");
        }

        if (EntryIdProperty == null && HasMerchantSynchronizer)
        {
            // 0.113 replays a purchase by id, so a snapshot is useless without it. 0.111 has no
            // id and no purchase replay, and keys snapshots by ordinal, so its absence is normal
            // there and must not be reported.
            missing.Add("entry id");
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
