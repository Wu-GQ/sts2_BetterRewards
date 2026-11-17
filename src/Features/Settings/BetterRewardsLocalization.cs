using System.Globalization;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace BetterRewards.Features.Settings;

/// <summary>
/// Loads BetterRewards strings for the current game language and merges them into
/// LocManager tables. English is the fallback; other official language tables overlay it when present.
/// Dynamic values (scores, HP, gold) stay in LocString variables so translations can use SmartFormat.
/// </summary>
internal static class BetterRewardsLocalization
{
    internal const string EventsTable = "events";
    internal const string SettingsTable = "settings_ui";
    internal const string ShopTable = "better_rewards_shop";

    private static readonly string[] Tables = [EventsTable, SettingsTable, ShopTable];
    private static readonly object Sync = new();
    private static bool _subscribed;
    private static string? _appliedLanguage;

    public static void TryMerge()
    {
        try
        {
            if (LocManager.Instance == null)
            {
                Log.Warn("[BetterRewards] Localization is not ready yet. LocManager is not available.");
                return;
            }

            ApplyLanguage(ResolveLanguage(LocManager.Instance.Language));
            SubscribeIfNeeded();
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] Localization is not ready yet. {ex.Message}");
        }
    }

    public static LocString EventString(string key, params (string Name, object Value)[] variables)
    {
        TryMerge();
        return Create(EventsTable, key, variables);
    }

    public static LocString Create(string table, string key, params (string Name, object Value)[] variables)
    {
        var loc = new LocString(table, key);
        foreach (var (name, value) in variables)
        {
            switch (value)
            {
                case string text:
                    loc.Add(name, text);
                    break;
                case bool flag:
                    loc.Add(name, flag);
                    break;
                case IConvertible:
                    loc.Add(name, Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    loc.AddObj(name, value);
                    break;
            }
        }

        return loc;
    }

    private static string ResolveLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "eng";
        }

        var code = language.Trim().ToLowerInvariant().Replace('-', '_');
        return code switch
        {
            "zhs" or "zh" or "zh_cn" or "zh_hans" or "zh_sg" or "chs" or "cn" => "zhs",
            "zht" or "zh_tw" or "zh_hk" or "zh_mo" or "zh_hant" or "cht" or "tw" => "zht",
            "esp" or "es_419" or "es_mx" or "es_la" or "es_latam" => "esp",
            "spa" or "es" or "es_es" => "spa",
            "ptb" or "pt" or "pt_br" or "por" => "ptb",
            "fra" or "fr" or "fre" => "fra",
            "deu" or "de" or "ger" => "deu",
            "ita" or "it" => "ita",
            "jpn" or "ja" or "jp" => "jpn",
            "kor" or "ko" or "kr" => "kor",
            "pol" or "pl" => "pol",
            "rus" or "ru" => "rus",
            "tha" or "th" => "tha",
            "tur" or "tr" => "tur",
            "ind" or "id" or "idn" => "ind",
            "ara" or "ar" => "ara",
            "eng" or "en" or "en_us" or "en_gb" => "eng",
            _ => code
        };
    }

    private static void ApplyLanguage(string language)
    {
        lock (Sync)
        {
            foreach (var table in Tables)
            {
                var entries = LoadTable(table, language);
                if (entries.Count == 0)
                {
                    continue;
                }

                MergeTable(table, entries);
            }

            if (!string.Equals(_appliedLanguage, language, StringComparison.Ordinal))
            {
                Log.Info($"[BetterRewards] Applied {language} localization tables.");
            }

            _appliedLanguage = language;
        }
    }

    private static Dictionary<string, string> LoadTable(string table, string language)
    {
        var entries = LoadEmbeddedTable("eng", table);
        if (!string.Equals(language, "eng", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (key, value) in LoadEmbeddedTable(language, table))
            {
                entries[key] = value;
            }
        }

        return entries;
    }

    private static Dictionary<string, string> LoadEmbeddedTable(string language, string table)
    {
        var resourceName = $"BetterRewards.Loc.{language}.{table}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    }

    private static void MergeTable(string tableName, Dictionary<string, string> entries)
    {
        var manager = LocManager.Instance;
        try
        {
            manager.GetTable(tableName).MergeWith(entries);
            return;
        }
        catch (LocException)
        {
            // Custom tables such as better_rewards_shop are not created by the game.
        }

        var tablesField = typeof(LocManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Instance);
        if (tablesField?.GetValue(manager) is Dictionary<string, LocTable> tables)
        {
            if (!tables.ContainsKey(tableName))
            {
                tables[tableName] = new LocTable(tableName, new Dictionary<string, string>());
            }

            tables[tableName].MergeWith(entries);
            return;
        }

        Log.Warn($"[BetterRewards] Could not access LocManager._tables — {tableName} was not merged.");
    }

    private static void SubscribeIfNeeded()
    {
        if (_subscribed || LocManager.Instance == null)
        {
            return;
        }

        LocManager.Instance.SubscribeToLocaleChange(TryMerge);
        _subscribed = true;
    }
}

/// <summary>
/// Localization tables are not always ready during mod init. Merge again when the main menu
/// is up, before the player can open BaseLib's config screen.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class BetterRewardsSettingsLocalizationPatch
{
    public static void Postfix()
    {
        BetterRewardsLocalization.TryMerge();
    }
}
