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
        ("combat", "Combat"), ("healing", "Healing"),
        // One card for the KILLS & DROPS theme (docs/Themes.md, David 2026-08-20:
        // "Kills and Drops should be … Kills and Drops"). The Kills card and the
        // "Drops by creature…" menu window are two tabs there now. The KEY stays
        // "kills" — it is in every existing player's SectionOrder, HiddenSections and
        // MiniStats — so there is nothing for a migration to fold and the card keeps
        // whatever slot the player put it in. Only the LABEL moved.
        ("kills", "Kills & Drops"),
        // One card for every quest surface (David, 2026-08-16). It replaced the separate
        // "Sky Quest" and "Epics" cards, which each carried a full tabbed checklist on the
        // widget — a review surface, not a glance one, and now a click away in the Quest
        // Tracker window. AppSettings.MigrateQuestSections folds the two old keys onto it.
        ("quests", "Quests"),
        // One card for the GEAR & LOOT theme (docs/Themes.md, David 2026-08-20). It
        // replaced the separate Loot and Gear cards, which are tabs in that window now;
        // AppSettings.MigrateLootSections folds the old keys onto this one, and
        // LootSurface.AbsorbedCardKeys is the list it reads — so what disappears is
        // spelled ONCE, not again here.
        //
        // A card removed from this catalog must also leave MainWindow.SectionMap, and
        // vice versa: ApplySectionLayout appends every catalog key that is not already in
        // SectionOrder and then looks each one up in the map, so a key in one and not the
        // other throws on STARTUP, for everybody. E2E caught exactly that here.
        ("loot", "Gear & Loot"),
        // Key stays "tracked" — it's persisted in SectionOrder/HiddenSections. Only the
        // label follows the feature's rename from tracked loot to watch rules (#5).
        ("tracked", "Watch"), ("buffs", "Buffs"),
        // One card for every progress surface — the PROGRESS THEME (docs/Themes.md,
        // David ruled themes a direction 2026-08-19). It replaced the separate Money,
        // Motes, Faction and Raids cards, which are now tabs in the Progress window.
        // AppSettings.MigrateProgressSections folds the four old keys onto this one, and
        // ProgressSurface.AbsorbedCardKeys is the list it reads — so what disappears is
        // spelled ONCE, not again here.
        ("progress", "Progress"),
        // MOTES IS A CARD AGAIN, and hidden by default (David, 2026-08-21, answering
        // Scribe's "bring motes back as its own top-level card, behind a setting if
        // needed" and #228's "I simply want to track my mote drops in the main window").
        //
        // It needs no new setting: HiddenSections IS the setting, and Options is where it
        // lives — "cards always show unless you hid them; the switch is in Options" is the
        // rule this app already runs on. AppSettings.MigrateMotesCard hides it ONCE for
        // existing profiles so nobody's widget silently grows a row, and after that the
        // eye in Cards & windows is the whole mechanism.
        //
        // The Progress → Wealth tab keeps its Motes block either way. They read the same
        // numbers; this is a second VIEW of them on the surface a farmer is actually
        // looking at, not a fold being undone.
        //
        // A card in this catalog MUST also be in MainWindow.SectionMap on BOTH widgets, or
        // ApplySectionLayout throws on startup for everybody — the crash the Gear & Loot
        // fold found. Both have it.
        ("motes", "Motes"),
        ("misc", "Travels & Deaths"),
    ];

    /// <summary>The card's icon, by section key — one table both widgets read (Gate 5).
    ///
    /// These were fourteen emoji typed into fourteen places, on the ONE surface that is
    /// always on screen. That made them the highest-value glyphs left in the app: #148 and
    /// #166 exist because emoji failed to render at all under Wine, on the Linux and macOS
    /// builds that are EQBuddy's only uncontested ground, and a card header that renders as
    /// a hollow box is the first thing a player sees.
    ///
    /// Names are <see cref="IconPaths"/> SHAPES rather than card names, so a card can be
    /// renamed or repurposed without stranding an icon called "Kills". Anything unmapped
    /// falls back to a neutral marker rather than throwing — a new card should look plain,
    /// not crash the widget.</summary>
    private static readonly Dictionary<string, string> Icons = new(StringComparer.Ordinal)
    {
        ["combat"] = "Swords",
        ["healing"] = "Heal",
        ["kills"] = "Skull",
        ["loot"] = "Bag",
        ["motes"] = "Sparkle",
        ["quests"] = "Map",
        ["gear"] = "Gear",
        ["tracked"] = "Target",
        ["buffs"] = "Timer",
        ["progress"] = "Chart",
        ["misc"] = "Location",
    };

    public static string Icon(string key) => Icons.GetValueOrDefault(key, "Info");

    /// <summary>
    /// What a THEMED card absorbed, in the words the absorbed cards used to carry.
    ///
    /// This exists because of #219 (typical-usual-chaos). The Progress theme folded the
    /// Motes card into a tab, and he went to Options → Cards & windows to switch it back
    /// on — the one place in the app whose whole job is to list every card. There was no
    /// Motes row, and nothing anywhere on that screen said where it had gone. He filed
    /// "now I can't get it back", and he was right: from where he stood, the feature had
    /// been deleted.
    ///
    /// A fold is invisible by construction — the thing that would tell you about it is
    /// the thing that was removed. So the surviving card carries the names of the cards it
    /// replaced, at the exact screen someone goes to when a card is missing. Keyed by the
    /// SURVIVING card and listing the absorbed ones by their old titles, because those are
    /// the words a player is searching the screen for.
    /// </summary>
    private static readonly Dictionary<string, string[]> AbsorbedTitles = new(StringComparer.Ordinal)
    {
        // 2026-08-16: the Sky Quest and Epics cards became tabs in the Quest Tracker.
        ["quests"] = ["Sky Quest", "Epics"],
        // 2026-08-19: the PROGRESS THEME (docs/Themes.md).
        //
        // MOTES LEFT THIS LIST on 2026-08-21, when it became a card again. The note answers
        // "where did the card I am hunting for go", so naming a card that is two rows above
        // it in the same list is worse than saying nothing — it sends someone looking in the
        // window for a card that is right there. It is still a Wealth-tab block as well;
        // being in two places is not the same as having moved.
        ["progress"] = ["Money", "Faction", "Raids"],
        // 2026-08-20: the GEAR & LOOT theme (docs/Themes.md).
        ["loot"] = ["Gear"],
        // 2026-08-21: the KILLS & DROPS theme. "Drops by creature" was never a CARD —
        // it was an entry in the cog menu — so it is named here anyway, deliberately.
        // The note's job is to answer "where did the thing I used to open go", and a
        // menu entry that disappears is exactly as invisible as a card that does
        // (trap 29 from the other side). Cards & windows is the screen people look at.
        ["kills"] = ["Drops by creature"],
    };

    /// <summary>The one-line "these live in here now" note for a card, or null for a card
    /// that never absorbed anything. Add a line to <see cref="AbsorbedTitles"/> in the
    /// same change as any future fold — it is step 3's other half.</summary>
    public static string? AbsorbedNote(string key)
    {
        if (!AbsorbedTitles.TryGetValue(key, out var titles) || titles.Length == 0) return null;
        // "Gear are tabs in here now" — the sentence was written when every fold absorbed
        // several cards, and the Gear & Loot theme is the first to absorb exactly one.
        // The line's whole job is to be READ by someone hunting for a card that vanished;
        // a player who finds it ungrammatical has been given one more reason to doubt it.
        var verb = titles.Length == 1 ? " is a tab in here now" : " are tabs in here now";
        return string.Join(" · ", titles) + verb;
    }
}

/// <param name="Absorbed">"Money · Motes · Faction · Raids are tabs in here now", or null.
/// The answer to "where did my card go?" at the screen where that question is asked —
/// see <see cref="OverlaySections.AbsorbedNote"/> and #219.</param>
public sealed record OptionsCardRow(string Key, string Title, bool Hidden, string? Absorbed = null);

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
    public bool HideWhenGameNotRunning
    {
        get => _settings.HideWhenGameNotRunning;
        set { _settings.HideWhenGameNotRunning = value; PersistAnd(); }
    }
    public bool HideFromAltTab
    {
        get => _settings.HideFromAltTab;
        set { _settings.HideFromAltTab = value; PersistAnd(); }
    }
    public bool KeepAboveOverlays
    {
        get => _settings.KeepAboveOverlays;
        set { _settings.KeepAboveOverlays = value; PersistAnd(); }
    }
    public bool SpawnChipsGrowUp
    {
        get => _settings.SpawnChipsGrowUp;
        set { _settings.SpawnChipsGrowUp = value; PersistAnd(); }
    }
    public bool MezChipsGrowUp
    {
        get => _settings.MezChipsGrowUp;
        set { _settings.MezChipsGrowUp = value; PersistAnd(); }
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
    /// <summary>First entry of the sound picker: no alert sound at all. The volume slider
    /// floors at 10%, so this is the only way to silence alerts outright — and it's the
    /// honest choice on a platform whose built-in sound files aren't present (Wine ships
    /// none of the Windows Media clips, so every built-in there plays nothing anyway).
    /// Stored as AlertSoundCatalog.OffChoice, which the sound planner already reads as
    /// "play nothing".</summary>
    public const string DisabledSoundChoice = "(Disabled)";
    public const string CustomSoundChoice = "Custom file…";
    public static readonly string[] SoundChoices =
        [DisabledSoundChoice, .. AlertSoundCatalog.Names.Select(n => n == "Ding" ? $"{n} (default)" : n), CustomSoundChoice];

    // Picker layout: 0 = Disabled, 1..Names.Length = built-ins, last = Custom.
    private static bool IsOff(string? choice) =>
        string.Equals((choice ?? "").Trim(), AlertSoundCatalog.OffChoice, StringComparison.OrdinalIgnoreCase);

    /// <summary>Index into SoundChoices for the current setting (0 = Disabled, last = the
    /// custom slot for paths).</summary>
    public int SoundIndex
    {
        get
        {
            if (IsOff(_settings.AlertSound)) return 0;
            var i = Array.IndexOf(AlertSoundCatalog.Names, AlertSoundCatalog.Normalize(_settings.AlertSound));
            return i >= 0 ? i + 1 : AlertSoundCatalog.Names.Length + 1;
        }
    }

    public bool IsDisabledSoundIndex(int index) => index == 0;
    public bool IsCustomSoundIndex(int index) => index >= AlertSoundCatalog.Names.Length + 1;

    public void SelectNamedSound(int index)
    {
        if (IsCustomSoundIndex(index)) return;
        _settings.AlertSound = IsDisabledSoundIndex(index)
            ? AlertSoundCatalog.OffChoice
            : AlertSoundCatalog.Names[index - 1];
        PersistAnd(nameof(SoundFileNote));
    }

    public void SetCustomSound(string path)
    {
        _settings.AlertSound = path;
        PersistAnd(nameof(SoundFileNote));
    }

    public string SoundFileNote =>
        AlertSoundCatalog.IsCustom(_settings.AlertSound) ? $"Custom: {_settings.AlertSound}" : "";

    // ---- spoken alerts ----
    /// <summary>First entry of the voice picker; the rest are installed voice names.</summary>
    public const string DefaultVoiceChoice = "System default";

    /// <summary>Picker entries for the given installed voices. The list is a parameter
    /// (the views pass SpokenAlerts.InstalledVoiceNames()) so this mapping stays
    /// testable without instantiating SAPI.</summary>
    public static string[] VoiceChoices(IReadOnlyList<string> installedVoices) =>
        [DefaultVoiceChoice, .. installedVoices];

    /// <summary>Index into <see cref="VoiceChoices"/> — 0 (default) also for a voice
    /// that's no longer installed, matching the fallback SpokenAlerts applies at speak
    /// time, so the picker never claims a voice the alerts won't actually use.</summary>
    public int VoiceIndex(IReadOnlyList<string> installedVoices)
    {
        for (var i = 0; i < installedVoices.Count; i++)
            if (string.Equals(installedVoices[i], _settings.SpeechVoice, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return 0;
    }

    public void SelectVoice(IReadOnlyList<string> installedVoices, int index)
    {
        _settings.SpeechVoice = index <= 0 || index > installedVoices.Count
            ? "" : installedVoices[index - 1];
        // Applied live — the next alert speaks with the new voice, no restart.
        SpokenAlerts.SetVoice(_settings.SpeechVoice);
        PersistAnd();
    }

    /// <summary>Clamped on read too: a hand-edited settings.json with rate 40 must show
    /// the slider (and speak) at the same +5 it will actually use.</summary>
    public int SpeechRate
    {
        get => Math.Clamp(_settings.SpeechRate, SpokenAlerts.MinRate, SpokenAlerts.MaxRate);
        set
        {
            _settings.SpeechRate = Math.Clamp(value, SpokenAlerts.MinRate, SpokenAlerts.MaxRate);
            SpokenAlerts.SetRate(_settings.SpeechRate);
            PersistAnd(nameof(SpeechRateLabel));
        }
    }
    public string SpeechRateLabel => SpeechRate == 0 ? "normal" : $"{SpeechRate:+0;-0}";

    public int SpeechVolume
    {
        get => Math.Clamp(_settings.SpeechVolume, 0, 100);
        set
        {
            _settings.SpeechVolume = Math.Clamp(value, 0, 100);
            SpokenAlerts.SetVolume(_settings.SpeechVolume);
            PersistAnd(nameof(SpeechVolumeLabel));
        }
    }
    public string SpeechVolumeLabel => $"{SpeechVolume}%";

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

    /// <summary>Move a rule up (-1) or down (+1) in the list — the "manual" watch
    /// sort shows rules exactly in this order (#105, wizen). No-op at the edges.</summary>
    public void MoveRule(TrackedRule rule, int delta)
    {
        var rules = _settings.TrackedRules;
        var at = rules.IndexOf(rule);
        var to = at + delta;
        if (at < 0 || to < 0 || to >= rules.Count) return;
        rules.RemoveAt(at);
        rules.Insert(to, rule);
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
            _settings.HiddenSections.Contains(key),
            OverlaySections.AbsorbedNote(key)))];

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
        // A key this install has never seen slots in at its CATALOG position (after its
        // nearest catalog predecessor the user already has) — appending at the end put
        // every new card below the fold, where "new in this release!" reads as "missing"
        // (David's 1.66 field test: Buffs and Raids were invisible on a tall layout).
        for (var i = 0; i < OverlaySections.Catalog.Length; i++)
        {
            var key = OverlaySections.Catalog[i].Key;
            if (order.Contains(key)) continue;
            var at = order.Count;
            for (var j = i - 1; j >= 0; j--)
            {
                var prev = order.IndexOf(OverlaySections.Catalog[j].Key);
                if (prev >= 0) { at = prev + 1; break; }
            }
            order.Insert(at, key);
        }
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
