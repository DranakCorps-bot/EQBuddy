using System.ComponentModel;
using System.Runtime.CompilerServices;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>The alert sounds both UIs offer. File names are Windows Media files —
/// the WPF app plays them directly; other platforms map or substitute.</summary>
public static class AlertSoundCatalog
{
    public static readonly (string Name, string WindowsMediaFile)[] Sounds =
    [
        ("Ding", "Windows Ding.wav"),
        ("Notify", "Windows Notify.wav"),
        ("Chimes", "chimes.wav"),
        ("Chord", "chord.wav"),
        ("Tada", "tada.wav"),
        ("Exclamation", "Windows Exclamation.wav"),
        ("Alarm", "Alarm01.wav"),
    ];

    public static readonly string[] Names = [.. Sounds.Select(s => s.Name)];

    /// <summary>Maps legacy SystemSounds values from early builds onto the palette;
    /// anything else (a named entry or a custom file path) passes through.</summary>
    public static string Normalize(string choice) => choice switch
    {
        "Asterisk" or "" => "Ding",
        "Beep" => "Chord",
        "Hand" => "Chimes",
        "Question" => "Notify",
        _ => choice,
    };

    public static bool IsCustom(string choice) => Array.IndexOf(Names, Normalize(choice)) < 0;

    /// <summary>
    /// The sound a rule should actually play. A rule with its own
    /// <see cref="TrackedRule.AlertSoundName"/> wins; otherwise it inherits the shared
    /// choice. Returns null when the rule is muted, so callers play nothing at all
    /// rather than falling back to a default.
    /// </summary>
    public static string? Resolve(TrackedRule rule, string sharedChoice) =>
        !rule.AlertSound ? null
        : rule.AlertSoundName.Length > 0 ? Normalize(rule.AlertSoundName)
        : Normalize(sharedChoice);

    /// <summary>Per-rule picker entries, in order. Index 0 mutes the rule, index 1
    /// inherits the shared choice, then the built-ins, then "Custom…".</summary>
    public const string OffChoice = "Off";
    public const string InheritChoice = "Default";
    public const string CustomChoice = "Custom…";

    public static string[] RuleChoices => [OffChoice, InheritChoice, .. Names, CustomChoice];

    /// <summary>Which entry of <see cref="RuleChoices"/> a rule currently sits on.</summary>
    public static int RuleChoiceIndex(TrackedRule rule)
    {
        if (!rule.AlertSound) return 0;
        if (rule.AlertSoundName.Length == 0) return 1;
        var named = Array.IndexOf(Names, Normalize(rule.AlertSoundName));
        return named >= 0 ? named + 2 : RuleChoices.Length - 1;   // custom path
    }

    /// <summary>Apply a picker selection to a rule. Returns true when the caller still
    /// needs to ask the user for a custom file (the "Custom…" entry).</summary>
    public static bool ApplyRuleChoice(TrackedRule rule, int index)
    {
        if (index <= 0) { rule.AlertSound = false; return false; }
        rule.AlertSound = true;
        if (index == 1) { rule.AlertSoundName = ""; return false; }
        var namedIndex = index - 2;
        if (namedIndex < Names.Length) { rule.AlertSoundName = Names[namedIndex]; return false; }
        return true;   // "Custom…" — the view owns the file dialog
    }
}

/// <summary>Color themes both UIs offer, keyed the same as the WPF app's
/// Themes/*.xaml palette dictionaries so <see cref="AppSettings.Theme"/> round-trips
/// between the two without translation.</summary>
public static class ThemeCatalog
{
    public static readonly (string Key, string Label)[] Themes =
    [
        ("ParchmentBrass", "Parchment & Brass"),
        ("BlueGrey", "Blue Grey"),
        ("Turquoise", "Turquoise"),
        ("Redish", "Redish"),
        ("Grey", "Grey"),
        ("Solarized", "Solarized"),
        ("SolarizedDark", "Solarized Dark"),
        ("HighContrast", "High Contrast"),
        // Not "Custom…": the alert-sound pickers use that exact label, and anything
        // that filters combo items by AlertSoundCatalog.CustomChoice would match this
        // dropdown too (the Avalonia render test does).
        (CustomTheme.Key, "Custom colors…"),
    ];

    public static readonly string[] Labels = [.. Themes.Select(t => t.Label)];

    /// <summary>Index of a theme key in <see cref="Themes"/>/<see cref="Labels"/>;
    /// falls back to 0 for an unrecognized key (e.g. an older settings.json).</summary>
    public static int IndexOf(string key)
    {
        var i = Array.FindIndex(Themes, t => t.Key == key);
        return i >= 0 ? i : 0;
    }
}

/// <summary>The overlay cards, in default order — shared by both UIs' layout and
/// Options card editors.</summary>
public static class OverlaySections
{
    public static readonly (string Key, string Title)[] Catalog =
    [
        ("combat", "Combat"), ("healing", "Healing"), ("kills", "Kills"), ("loot", "Loot"),
        ("motes", "Motes"),
        ("sky", "Sky Quest"),
        // Key stays "tracked" — it's persisted in SectionOrder/HiddenSections. Only the
        // label follows the feature's rename from tracked loot to watch rules (#5).
        ("tracked", "Watch"), ("money", "Money"), ("progress", "Progress"),
        ("faction", "Faction"), ("misc", "Travels & Deaths"),
    ];
}

public sealed record OptionsCardRow(string Key, string Title, bool Hidden);

/// <summary>
/// Framework-neutral Options logic: every mapping, mutation, and derived label the
/// Options window needs. Views only build controls, forward input here, and apply
/// visual side effects (scale/opacity/layout) to their own windows.
/// </summary>
public sealed class OptionsViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly Action _persist;

    public OptionsViewModel(AppSettings settings, Action persist)
    {
        _settings = settings;
        _persist = persist;
        NormalizeSectionOrder();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AppSettings Settings => _settings;
    public void Persist() => _persist();

    // ---- sliders ----
    public double UiScale
    {
        get => _settings.UiScale;
        set { _settings.UiScale = Math.Clamp(value, 0.5, 2.0); Changed(); Changed(nameof(ScaleLabel)); }
    }
    public string ScaleLabel => $"{_settings.UiScale * 100:0}%";

    public double ChipScale
    {
        get => _settings.ChipScale;
        set { _settings.ChipScale = Math.Clamp(value, 0.5, 2.0); Changed(); Changed(nameof(ChipScaleLabel)); }
    }
    public string ChipScaleLabel => $"{_settings.ChipScale * 100:0}%";

    public double Opacity
    {
        get => _settings.Opacity;
        set { _settings.Opacity = Math.Clamp(value, 0.3, 1.0); Changed(); Changed(nameof(OpacityLabel)); }
    }
    public string OpacityLabel => $"{_settings.Opacity * 100:0}%";

    public double BackgroundOpacity
    {
        get => _settings.BackgroundOpacity;
        set { _settings.BackgroundOpacity = Math.Clamp(value, 0.15, 1.0); Changed(); Changed(nameof(BackgroundOpacityLabel)); }
    }
    public string BackgroundOpacityLabel => $"{_settings.BackgroundOpacity * 100:0}%";

    // ---- theme ----
    /// <summary>Display labels for the theme picker, in <see cref="ThemeCatalog"/> order.</summary>
    public static readonly string[] ThemeLabels = ThemeCatalog.Labels;

    /// <summary>Index into <see cref="ThemeLabels"/>. The view applies the visual side
    /// effect (swapping the live resource dictionary) after setting this.</summary>
    public int ThemeIndex
    {
        get => ThemeCatalog.IndexOf(_settings.Theme);
        set { _settings.Theme = ThemeCatalog.Themes[value].Key; PersistAnd(); }
    }

    // ---- toggles ----
    public bool ArchiveLogs
    {
        get => _settings.ArchiveLogs;
        set { _settings.ArchiveLogs = value; PersistAnd(); }
    }

    public bool TruncateLogs
    {
        get => _settings.TruncateLogs;
        set { _settings.TruncateLogs = value; PersistAnd(); }
    }
    public bool PinWatchChips
    {
        get => _settings.PinWatchChips;
        set { _settings.PinWatchChips = value; PersistAnd(); }
    }
    public bool ShowTutorial
    {
        get => _settings.ShowTutorial;
        set { _settings.ShowTutorial = value; PersistAnd(); }
    }
    public bool ShowTargetDrops
    {
        get => _settings.ShowTargetDrops;
        set { _settings.ShowTargetDrops = value; PersistAnd(); }
    }
    public bool HideWhenGameUnfocused
    {
        get => _settings.HideWhenGameUnfocused;
        set { _settings.HideWhenGameUnfocused = value; PersistAnd(); }
    }
    /// <summary>0 = wiki base. Clamped to a sane band — a typo of 90000 would turn the
    /// estimate into an absurdity that outlives the typo.</summary>
    public int RegenPerTickOverride
    {
        get => _settings.RegenPerTickOverride;
        set { _settings.RegenPerTickOverride = Math.Clamp(value, 0, 5000); PersistAnd(); }
    }

    // ---- recent-rate window ----
    public static readonly string[] WindowChoices = ["5 min", "15 min", "30 min"];
    public int RecentWindowIndex
    {
        get => _settings.RecentWindowMinutes switch { 5 => 0, 30 => 2, _ => 1 };
        set { _settings.RecentWindowMinutes = value switch { 0 => 5, 2 => 30, _ => 15 }; PersistAnd(); }
    }

    // ---- alert sound ----
    public const string CustomSoundChoice = "Custom file…";
    public static readonly string[] SoundChoices =
        [.. AlertSoundCatalog.Names.Select(n => n == "Ding" ? $"{n} (default)" : n), CustomSoundChoice];

    /// <summary>Index into SoundChoices for the current setting (the custom slot for paths).</summary>
    public int SoundIndex
    {
        get
        {
            var i = Array.IndexOf(AlertSoundCatalog.Names, AlertSoundCatalog.Normalize(_settings.AlertSound));
            return i >= 0 ? i : AlertSoundCatalog.Names.Length;
        }
    }

    public bool IsCustomSoundIndex(int index) => index >= AlertSoundCatalog.Names.Length;

    public void SelectNamedSound(int index)
    {
        if (IsCustomSoundIndex(index)) return;
        _settings.AlertSound = AlertSoundCatalog.Names[index];
        PersistAnd(nameof(SoundFileNote));
    }

    public void SetCustomSound(string path)
    {
        _settings.AlertSound = path;
        PersistAnd(nameof(SoundFileNote));
    }

    public string SoundFileNote =>
        AlertSoundCatalog.IsCustom(_settings.AlertSound) ? $"Custom: {_settings.AlertSound}" : "";

    /// <summary>Options window width (the user drags its right edge). Clamping to the
    /// window's own Min/Max stays in the view — Core has no notion of chrome.</summary>
    public double OptionsWidth
    {
        get => _settings.OptionsWidth;
        set { _settings.OptionsWidth = value; PersistAnd(); }
    }

    // ---- watch rules ----
    /// <summary>Dropdown labels, in WatchKind order — both UIs map the selected index
    /// straight back to the enum value, so this must stay aligned with the enum.</summary>
    public static readonly string[] KindNames =
    [
        "Loot",
        "Kill",
        "Skill-up",
        "Death",
        "Milestone",
        "Spell fade",
        "Log text",
    ];

    /// <summary>Labels for the SpellFade class picker, in SpellFilter order. Kept short
    /// on purpose — this combo shares a rule row with the alert toggles, and the help
    /// text above the list spells out what "Any CC" covers.</summary>
    public static readonly string[] SpellFilterNames =
    [
        "By name…",
        "Any spell",
        "Any CC",
        "Charm",
        "Mez",
        "Root",
        "Lull",
        "Stun",
        "HoT",
        "Buff",
    ];
    public IReadOnlyList<TrackedRule> Rules => _settings.TrackedRules;

    public TrackedRule AddRule()
    {
        var rule = new TrackedRule { Name = "", Pattern = "" };
        _settings.TrackedRules.Add(rule);
        PersistAnd(nameof(Rules));
        return rule;
    }

    public void RemoveRule(TrackedRule rule)
    {
        _settings.TrackedRules.Remove(rule);
        PersistAnd(nameof(Rules));
    }

    /// <summary>Append rules decoded from a share string — WatchRuleShare already
    /// rebuilt them with fresh ids and sanitized fields; this just lands them.</summary>
    public void ImportRules(IEnumerable<TrackedRule> rules)
    {
        _settings.TrackedRules.AddRange(rules);
        PersistAnd(nameof(Rules));
    }

    // ---- overlay cards ----
    public IReadOnlyList<OptionsCardRow> Cards =>
        [.. _settings.SectionOrder.Select(key => new OptionsCardRow(
            key,
            OverlaySections.Catalog.First(c => c.Key == key).Title,
            _settings.HiddenSections.Contains(key)))];

    public void MoveCard(string key, int delta)
    {
        var order = _settings.SectionOrder;
        var index = order.IndexOf(key);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= order.Count) return;
        (order[index], order[target]) = (order[target], order[index]);
        PersistAnd(nameof(Cards));
    }

    public void ToggleCard(string key)
    {
        if (!_settings.HiddenSections.Remove(key))
            _settings.HiddenSections.Add(key);
        PersistAnd(nameof(Cards));
    }

    private void NormalizeSectionOrder()
    {
        var order = _settings.SectionOrder.Where(k => OverlaySections.Catalog.Any(c => c.Key == k)).ToList();
        foreach (var (key, _) in OverlaySections.Catalog)
            if (!order.Contains(key)) order.Add(key);
        _settings.SectionOrder = order;
    }

    // ---- hotkeys ----
    private void PersistAnd(string? alsoNotify = null, [CallerMemberName] string? propertyName = null)
    {
        _persist();
        Changed(propertyName);
        if (alsoNotify is not null) Changed(alsoNotify);
    }

    private void Changed([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
