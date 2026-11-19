using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace BetterRewards.Tests;

/// <summary>
/// STATIC CONTRACT TESTS.
///
/// These read repository source text and localization JSON. They do not compile or run
/// the mod, do not load sts2.dll, Godot or RitsuLib, and are not integration tests.
/// They exist to catch wiring regressions that the pure-logic suite cannot see:
/// a binding losing its default-value wrapper, the confirm dialog being bypassed,
/// or one language dropping a key.
/// </summary>
public sealed class SettingsWiringContractTests
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

    private static string ReadSource(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine([RepoRoot, .. relativeParts]));

    private static string SettingsSource =>
        ReadSource("src", "Features", "Settings", "BetterRewardsSettings.cs");

    private static string LocalizationSource =>
        ReadSource("src", "Features", "Settings", "BetterRewardsLocalization.cs");

    private static readonly string[] Languages =
    [
        "ara", "deu", "eng", "esp", "fra", "ind", "ita", "jpn",
        "kor", "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht",
    ];

    private static string SettingsUiPath(string language) =>
        Path.Combine(RepoRoot, "localization", language, "settings_ui.json");

    [Fact]
    public void Every_slider_binding_goes_through_a_default_capable_factory()
    {
        var source = SettingsSource;

        // Every AddSlider/AddIntSlider call must pass a variable produced by BindDouble/BindInt,
        // never an inline raw ModSettingsValueBinding.
        var sliderCalls = Regex.Matches(source, @"\.Add(Slider|IntSlider)\(");
        Assert.True(sliderCalls.Count >= 10, $"Expected at least 10 slider calls, found {sliderCalls.Count}.");

        var bindVariables = Regex.Matches(source, @"var\s+(\w+)\s*=\s*Bind(Double|Int)\(")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(10, bindVariables.Count);

        foreach (var variable in bindVariables)
            Assert.Contains(variable, source, StringComparison.Ordinal);

        // No raw binding constructions may survive outside the two factories.
        var rawConstructions = Regex.Matches(source,
            @"new\s+ModSettingsValueBinding<BetterRewardsSettingsModel");
        Assert.Equal(2, rawConstructions.Count);
    }

    [Fact]
    public void Both_binding_factories_wrap_with_WithDefault_and_derive_defaults_from_a_fresh_model()
    {
        var source = SettingsSource;

        var doubleFactory = ExtractMethodBody(source, "BindDouble");
        var intFactory = ExtractMethodBody(source, "BindInt");

        foreach (var factory in new[] { doubleFactory, intFactory })
        {
            Assert.Contains("ModSettingsBindings.WithDefault(", factory, StringComparison.Ordinal);
            // Defaults come from a newly constructed model, so the numbers are never duplicated
            // as literals next to the sliders.
            Assert.Contains("new BetterRewardsSettingsModel()", factory, StringComparison.Ordinal);
            Assert.DoesNotContain("0.20", factory, StringComparison.Ordinal);
            Assert.DoesNotContain("1000", factory, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Returns the body of the named method declaration by walking braces, so assertions cannot
    /// leak into neighbouring members or into call sites that merely mention the method name.
    /// </summary>
    private static string ExtractMethodBody(string source, string methodName)
    {
        var pattern = $"{methodName}(";
        var searchFrom = 0;
        while (true)
        {
            var found = source.IndexOf(pattern, searchFrom, StringComparison.Ordinal);
            Assert.True(found >= 0, $"Method {methodName} not found.");

            // Walk back to the start of the line and require a declaration keyword.
            var lineStart = source.LastIndexOf('\n', found) + 1;
            var declaration = source[lineStart..found];
            if (declaration.Contains("private", StringComparison.Ordinal) ||
                declaration.Contains("public", StringComparison.Ordinal) ||
                declaration.Contains("internal", StringComparison.Ordinal))
            {
                var open = source.IndexOf('{', found);
                Assert.True(open >= 0, $"Method {methodName} has no body.");

                var depth = 0;
                for (var i = open; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                            return source[open..(i + 1)];
                    }
                }

                throw new InvalidOperationException($"Unbalanced braces in {methodName}.");
            }

            searchFrom = found + pattern.Length;
        }
    }

    [Fact]
    public void Binding_factory_return_types_expose_the_default_value_interface()
    {
        var source = SettingsSource;

        Assert.Matches(
            @"private\s+static\s+IModSettingsValueBinding<double>\s+BindDouble\(",
            source);
        Assert.Matches(
            @"private\s+static\s+IModSettingsValueBinding<int>\s+BindInt\(",
            source);
    }

    [Fact]
    public void Reset_button_is_registered_with_the_framework_button_builder()
    {
        var source = SettingsSource;

        Assert.Contains("AddButton(", source, StringComparison.Ordinal);
        Assert.Contains("ModSettingsButtonTone.Danger", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_button_uses_the_native_confirmation_dialog_with_a_danger_confirm()
    {
        var source = SettingsSource;

        Assert.Contains("ModSettingsUiFactory.ShowStyledConfirm(", source, StringComparison.Ordinal);
        Assert.Contains("confirmIsDanger: true", source, StringComparison.Ordinal);

        // The reset must be reachable only from the confirm callback, never before it.
        var confirmIndex = source.IndexOf("ModSettingsUiFactory.ShowStyledConfirm(", StringComparison.Ordinal);
        var resetIndex = source.IndexOf("ResetAllLocalSettings", StringComparison.Ordinal);
        Assert.True(resetIndex > confirmIndex,
            "The reset call must appear inside the confirmation dialog wiring, after ShowStyledConfirm.");
    }

    [Fact]
    public void Confirmed_reset_saves_immediately_and_refreshes_the_ui()
    {
        var source = SettingsSource;

        Assert.Contains("SaveLocalModel(", source, StringComparison.Ordinal);
        Assert.Contains("RequestRefreshAfterDataModelBatchChange(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_model_lives_in_its_own_game_free_file()
    {
        var modelPath = Path.Combine(RepoRoot, "src", "Features", "Settings", "BetterRewardsSettingsModel.cs");
        Assert.True(File.Exists(modelPath));

        var modelSource = File.ReadAllText(modelPath);
        Assert.DoesNotContain("using MegaCrit", modelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using STS2RitsuLib", modelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("using Godot", modelSource, StringComparison.Ordinal);

        // The moved model must not still be declared inside the RitsuLib-coupled file.
        Assert.DoesNotContain("class BetterRewardsSettingsModel", SettingsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_registration_timing_after_main_menu_is_untouched()
    {
        // Guard the ObjectDisposedException fix: the page must still be registered from the
        // main-menu readiness patch, not during mod bootstrap or localization loading.
        var localization = LocalizationSource;

        Assert.Contains("[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]", localization,
            StringComparison.Ordinal);
        Assert.Contains("BetterRewardsSettings.RegisterPageIfNeeded();", localization, StringComparison.Ordinal);

        var bootstrap = ReadSource("src", "Core", "ModBootstrap.cs");
        Assert.DoesNotContain("RegisterPageIfNeeded", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterModSettings", bootstrap, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_touches_only_the_global_local_settings_store()
    {
        var source = SettingsSource;

        // The reset wiring must never write the per-run snapshot. Snapshot writes live only in
        // the pre-existing lifecycle handlers, which this feature does not touch.
        var confirmWiring = ExtractMethodBody(source, "ConfirmAndResetAllSettings");
        Assert.DoesNotContain("RunSettings", confirmWiring, StringComparison.Ordinal);

        var storeAdapter = ExtractMethodBody(source, "UpdateLocalModel")
                           + ExtractMethodBody(source, "SaveLocalModel");
        Assert.Contains("ModDataStore.For(ModId)", storeAdapter, StringComparison.Ordinal);
        Assert.Contains("DataKey", storeAdapter, StringComparison.Ordinal);
        Assert.Contains("SaveScope.Global", SettingsSource, StringComparison.Ordinal);

        // The coordinator itself only knows about the local model.
        var coordinator = ReadSource("src", "Features", "Settings", "BetterRewardsResetCoordinator.cs");
        Assert.DoesNotContain("RunSettings", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("History", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("BetterRewardsShopState", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void All_seventeen_languages_share_the_same_settings_ui_keys()
    {
        var keySets = Languages.ToDictionary(
            language => language,
            language => ReadKeys(SettingsUiPath(language)),
            StringComparer.Ordinal);

        var reference = keySets["eng"];
        foreach (var (language, keys) in keySets)
            Assert.Equal(reference.OrderBy(k => k, StringComparer.Ordinal),
                keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_language_defines_all_eight_reset_strings()
    {
        string[] required =
        [
            "BETTERREWARDS-RESET.title",
            "BETTERREWARDS-RESET_ALL.title",
            "BETTERREWARDS-RESET_ALL.button",
            "BETTERREWARDS-RESET_ALL.hover.desc",
            "BETTERREWARDS-RESET_CONFIRM.title",
            "BETTERREWARDS-RESET_CONFIRM.button",
            "BETTERREWARDS-RESET_CONFIRM.hover.desc",
            "BETTERREWARDS-RESET_CANCEL.button",
        ];

        foreach (var language in Languages)
        {
            var keys = ReadKeys(SettingsUiPath(language));
            foreach (var key in required)
                Assert.True(keys.Contains(key), $"{language}/settings_ui.json is missing {key}");
        }
    }

    [Fact]
    public void Reset_strings_are_wired_through_the_settings_table()
    {
        var source = SettingsSource;

        Assert.Contains("BETTERREWARDS-RESET.title", source, StringComparison.Ordinal);
        Assert.Contains("BETTERREWARDS-RESET_ALL.button", source, StringComparison.Ordinal);

        // They must use the existing settings_ui table helpers so the 17 languages apply.
        Assert.Contains("BetterRewardsLocalization.SettingsTable", source, StringComparison.Ordinal);
    }


    [Fact]
    public void Page_description_is_localized_instead_of_the_manifest_fallback()
    {
        var source = SettingsSource;
        Assert.Contains("WithDescription(", source, StringComparison.Ordinal);
        Assert.Contains("BETTERREWARDS.mod_desc", source, StringComparison.Ordinal);
        Assert.Contains("BetterRewardsLocalization.SettingsTable", source, StringComparison.Ordinal);

        foreach (var language in Languages)
        {
            var keys = ReadKeys(SettingsUiPath(language));
            Assert.True(keys.Contains("BETTERREWARDS.mod_desc"),
                $"{language}/settings_ui.json is missing BETTERREWARDS.mod_desc");
        }
    }

    [Fact]
    public void Player_facing_settings_copy_does_not_mention_settings_json()
    {
        foreach (var language in Languages)
        {
            using var stream = File.OpenRead(SettingsUiPath(language));
            using var document = JsonDocument.Parse(stream);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.GetString() ?? "";
                Assert.DoesNotContain("settings.json", value, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("setting.json", value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static HashSet<string> ReadKeys(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
