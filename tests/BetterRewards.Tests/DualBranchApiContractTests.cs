using System.Text.RegularExpressions;
using Xunit;

namespace BetterRewards.Tests;

/// <summary>
/// STATIC CONTRACT TESTS for the one-assembly/two-branches rule.
///
/// BetterRewards ships a single DLL for both the `public` branch (0.111.x) and `public-beta`
/// (0.113.x). It is compiled against the oldest of the two, so every member whose signature
/// changed in between must be reached through <c>Sts2ApiCompat</c> reflection; a direct call
/// binds to the 0.111 signature and throws <c>MissingMethodException</c> at runtime on beta.
///
/// This is not hypothetical. <c>MerchantInventory.AllEntries</c> is
/// <c>IEnumerable&lt;MerchantEntry&gt;</c> on 0.111 and <c>List&lt;MerchantEntry&gt;</c> on
/// 0.113. A property getter's return type is part of its metadata signature, so a direct call
/// compiled against 0.111 threw on beta and took the whole shop-action publish with it: the
/// host rerolled, paid, and then never told anyone, so teammates kept the pre-reroll stock.
///
/// These tests read repository source text and need no game assemblies, so they run in CI.
/// They are a cheap net, not a proof: the authoritative check is
/// <c>scripts/tools/check_game_api_bindings</c>, which resolves every game MemberRef by name
/// and signature against each real <c>sts2.dll</c>.
/// </summary>
public sealed class DualBranchApiContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BetterRewards.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the BetterRewards repository root.");
    }

    /// <summary>
    /// Mod sources with comments removed, so prose that mentions an API is not mistaken for a
    /// call to it. The compat layer itself is excluded: it is the one place allowed to touch
    /// these members directly.
    /// </summary>
    private static IEnumerable<(string Name, string Code)> ModSources()
    {
        var srcDir = Path.Combine(RepoRoot, "src");
        var compat = Path.Combine(srcDir, "Core", "Sts2ApiCompat.cs");

        foreach (var path in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(path, compat, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return (Path.GetFileName(path), StripComments(File.ReadAllText(path)));
        }
    }

    private static string StripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\n]*", " ");
    }

    private static List<string> FindOffenders(string pattern, Func<string, bool> fileIsRelevant)
    {
        var offenders = new List<string>();

        foreach (var (name, code) in ModSources())
        {
            if (!fileIsRelevant(code))
                continue;

            foreach (Match match in Regex.Matches(code, pattern))
            {
                var line = code[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{name}:{line}");
            }
        }

        return offenders;
    }

    [Fact]
    public void AllEntries_is_only_read_through_the_compat_layer()
    {
        // MerchantInventory.AllEntries changed return type between branches, so even the
        // receiving side of the assignment matters. Matches `.AllEntries` on any receiver.
        var offenders = FindOffenders(@"\.\s*AllEntries\b", _ => true);

        Assert.True(offenders.Count == 0,
            "Direct MerchantInventory.AllEntries access found (use Sts2ApiCompat.GetAllEntries): " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void MerchantEntry_id_is_only_read_through_the_compat_layer()
    {
        // MerchantEntry.Id is uint? on 0.113 and absent on 0.111, so it goes through
        // Sts2ApiCompat.TryGetEntryId. Scoped to files that deal in merchant types, because
        // other records in this mod legitimately have their own `entry.Id` members, and model
        // ids (`card.Id`, `relic.Id`) are a different thing entirely.
        var offenders = FindOffenders(
            @"\bentry\s*\.\s*Id\b",
            code => code.Contains("MerchantEntry", StringComparison.Ordinal)
                 || code.Contains("MerchantInventory", StringComparison.Ordinal));

        Assert.True(offenders.Count == 0,
            "Direct MerchantEntry.Id access found (use Sts2ApiCompat.TryGetEntryId): " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void The_compat_layer_still_exposes_both_accessors()
    {
        var compat = File.ReadAllText(Path.Combine(RepoRoot, "src", "Core", "Sts2ApiCompat.cs"));

        // The rules above are only safe while these exist.
        Assert.Contains("public static IReadOnlyList<MerchantEntry> GetAllEntries(", compat, StringComparison.Ordinal);
        Assert.Contains("public static uint? TryGetEntryId(", compat, StringComparison.Ordinal);

        // GetAllEntries must degrade to an empty list rather than propagate. It feeds the wire
        // snapshot, and a throw there aborts the whole publish, which is the bug being guarded.
        Assert.Contains("return [];", compat, StringComparison.Ordinal);
    }

    [Fact]
    public void Shop_sync_captures_slots_before_publishing_so_the_log_can_report_them()
    {
        // The reroll used to inline CaptureSlots as a PublishAction argument, so a throw inside
        // it aborted the publish with nothing but a stack trace. Hoisting it lets the completion
        // line state how many slots actually went out, which is what distinguishes "sent the
        // new stock" from "sent nothing" in a player's godot.log.
        var source = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "Features", "Shop", "BetterRewardsShopButtonsFeature.cs"));

        Assert.Contains("var slots = BetterRewardsShopSyncService.CaptureSlots(inventory);", source,
            StringComparison.Ordinal);
        Assert.Contains("Slots = slots,", source, StringComparison.Ordinal);
        Assert.Contains("slots carried", source, StringComparison.Ordinal);
    }
}
