using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The Behavior block, host-neutral** — everything Settings knows about how EQBuddy acts
/// rather than how it looks: EQBuddy Mobile pairing and its sounds switch, the three
/// hide-when rules and the Alt+Tab note, keep-above-overlays, the global hotkey rows, the
/// regen-per-tick override, auto-empty and its archive, the launch tutorial, and the perf
/// readout.
///
/// **Blocks, not tabs, are the unit that moves** (Fable's SR series; <see cref="SettingsAlertsView"/>
/// is the precedent, and <see cref="SettingsLookView"/> is this block's twin). It builds its own
/// controls, carries its own visibility and spacing (trap 15), and knows nothing about the
/// window it hangs in — so <c>OptionsWindow</c> keeps its five tabs while the Evolved shell's
/// Settings room composes the SAME block. **Each host constructs its own instance** (trap 45),
/// and **both hosts wrap one <see cref="AppSettings"/>** (trap 13) — the block never loads
/// settings for itself, because a second snapshot clobbers the first one wholesale (#169).
///
/// **EQBuddy Mobile's pairing panel moves WITH this block, and the title-bar 📱 button is
/// untouched.** CLAUDE.md's own carve-out — *"Settings live in Options — except EQBuddy Mobile,
/// which David wanted as its own title-bar button"* — makes that button the standing second
/// door, and Settings becoming the only path in would violate the rule by omission rather than
/// by edit. The panel says so in its own helper line, which is why the line names the button.
///
/// **The one thing this block cannot own on its own: the KEY that a hotkey recorder captures.**
/// Rebuilding the rows on every click means the recording button is a new control with no
/// focus, so the press arrives at the WINDOW rather than anywhere inside this panel — which is
/// why <see cref="HandleRecordingKey"/> is a method a host forwards its <c>PreviewKeyDown</c>
/// to rather than a handler attached in here. The DECISION is the block's; the routing is the
/// host's, exactly like the window chrome the lift left behind. A host that forgets to forward
/// gets a recorder that never records, so <c>SettingsLookBehaviorBlockTests</c> asserts the
/// forward on the host as well as the method here.
///
/// **The vocabulary sweep ran here** (§4 of `docs/BEVEL-v2-staging-critique.md`, Helm-signed;
/// Bevel's I-11 §5 named the hits in advance). A block serving two hosts has ONE string set and
/// it must pass in shell scope: the three "hide the widget" labels, the alt-tab note's aside
/// about chips and breakout windows, and the keep-above paragraph's two "widget"s are EQBuddy's
/// own name now. <see cref="AltTabPolicy.TaskbarWarning"/> was reworded at its source in
/// UI.Shared for the same reason — a shared const this block prints is a string the block shows,
/// wherever it is declared.
///
/// **The prose-to-hover pass reached this block in Pass 2** (Bevel's faces, Helm-signed
/// 2026-09-08; <see cref="SettingsProsePolicy"/> is the arithmetic). This is the heaviest tab
/// in Settings and it lost the most: EIGHT explanations moved onto an ⓘ beside the thing they
/// explain — <see cref="HideUnfocusedBlurb"/>, <see cref="KeepAboveBlurb"/>,
/// <see cref="HotkeysBlurb"/>, <see cref="RegenOverrideBlurb"/>,
/// <see cref="AutoEmptyBlurb"/>, <see cref="ArchiveBlurb"/>,
/// <see cref="PerfReadoutBlurb"/> and <see cref="SetupReadout.BehaviorNote"/>. Not one word
/// was rewritten; the consts above are the strings that shipped, hanging somewhere else.
///
/// **THREE paragraphs deliberately did NOT move, and they are this block's judgement.** Each
/// is a row with its reason in `SettingsProsePass2Tests` so a later pass cannot "finish the
/// job" silently:
///
/// <list type="number">
/// <item><b>The Alt+Tab note</b> (<see cref="AltTabPolicy.TaskbarWarning"/>). It is the one
///   printed sentence in the product that names the tray icon as the way back to a hidden
///   EQBuddy — a DOOR, and the switch it sits under is the switch that closes the other two.
///   Behind an ⓘ, a player who ticks the box without hovering has lost the only place they
///   were told (traps 29/34/59). It is also 21 words, one over the ceiling, and the string
///   this block actually prints is <c>TaskbarWarning + UnavailableNote</c>, whose length is
///   decided at RUNTIME by the platform — so the policy cannot answer for it either way.</item>
/// <item><b>The "hide while the game isn't running" note.</b> Same reason, different door:
///   "Launch EQBuddy again from the Start menu" appears nowhere else in the product, and it
///   is the answer to the question that switch creates. Its twin above it DID move, because
///   alt-tabbing back to the game is automatic rather than a door anybody has to find.</item>
/// <item><b>EQBuddy Mobile's two lines.</b> The panel's own is the standing record of
///   CLAUDE.md's carve-out — the title-bar 📱 button is a second door, and the comment above
///   <see cref="BuildSecondScreen"/> has always said the line names it on purpose. The sounds
///   switch's helper is <see cref="MobileAlertSounds.HelperText"/>, which exists to say the
///   DEFAULT out loud so nobody has to flip a switch to discover it; hiding it behind a hover
///   is that reason with the answer removed. It reaches the ceiling only because the block
///   prints it joined to <see cref="MobileAlertSounds.ScopeNote"/> — twelve words and eleven.</item>
/// </list>
/// </summary>
internal sealed class SettingsBehaviorView
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private readonly Func<bool> _hostReady;
    private readonly Func<object, object> _resource;

    /// <summary>
    /// **How a host re-opens the Evolved shell's first-run Setup screen (OE-6) — and the
    /// one row of this block that a host may legitimately not have.**
    ///
    /// Setup is a layer of <see cref="ShellWindow"/>: it is drawn over the active room, and
    /// <c>OptionsWindow</c> — which is not the shell, has no room, and is explicitly out of
    /// the owner's lock ("Evolved Settings, not OptionsWindow") — has nowhere to put it. A
    /// button that opened nothing there would be "silent no-ops are broken" with the switch
    /// on the other side, and the honest alternative to that is not to draw it.
    ///
    /// So it is a CAPABILITY the host supplies rather than a flag the block reads: null
    /// means "this host cannot show Setup", which is a fact about the host, and the default
    /// keeps <c>OptionsWindow</c>'s construction untouched. <c>behaviorSetup</c> in the
    /// <c>EQBUDDY_EXPAND</c> dump is what says which host got the row, from a launched app —
    /// an absent control photographs as an unremarkable list (trap 29).
    /// </summary>
    private readonly Action? _openSetup;

    private bool Ready => _hostReady();

    public SettingsBehaviorView(MainWindow main, OptionsViewModel vm, Func<bool> ready,
        Func<object, object> resource, Action? openSetup = null)
    {
        _main = main;
        _vm = vm;
        _hostReady = ready;
        _resource = resource;
        _openSetup = openSetup;
    }

    private UIElement? _block;

    /// <summary>This instance's body, built on first ask and kept — the host re-shows it rather
    /// than re-building, so a half-typed regen number survives a tab switch.</summary>
    public UIElement Block => _block ??= Build();

    /// <summary>
    /// This instance's facts for the <c>EQBUDDY_EXPAND</c> dump, in the block's OWN
    /// vocabulary — see <see cref="SettingsLookView.DebugFacts"/> for why the host, not the
    /// block, adds the prefix (trap 58) and why these are counted off BUILT controls.
    ///
    /// <c>behaviorHotkeys</c> is the row worth having here: the hotkey rows are the one
    /// piece of this block a host has to help with (the key ROUTE below), so a host that
    /// composed the block and forgot the route would have rows on screen that silently never
    /// record — and an absent behaviour photographs as a perfectly ordinary list (trap 29).
    /// </summary>
    public string DebugFacts() => _block is null
        ? ""
        : $"behaviorHotkeys={_hotkeysPanel.Children.Count} " +
          $"behaviorRecording={(_recordingAction is null ? 0 : 1)} " +
          // Counted off the BUILT control, not off the callback: "the host supplied a way
          // to open Setup" and "the row is on screen" are different claims, and only the
          // second one is the feature (trap 42). It is the one row of this block a host may
          // legitimately not have, so it is also the one worth reporting.
          $"behaviorSetup={(_setupBtn is null ? 0 : 1)} " +
          // Since the prose pass eight of this block's explanations exist ONLY behind an ⓘ,
          // so an ⓘ that failed to build is a paragraph that has left the product with
          // nothing in a diff, a build or a screenshot to say so. Counted off BUILT buttons
          // rather than off a list of the eight (traps 34/39) — and it MOVES with
          // `behaviorSetup`, because Setup's own note is one of the eight and a host without
          // the row does not build its ⓘ either.
          $"behaviorHints={_hints}";

    // -------------------------------------------------- the paragraphs on an ⓘ ----
    //
    // Pass 2 of the prose-to-hover conversion (Bevel's faces, Helm-signed 2026-09-08;
    // SettingsProsePolicy is the arithmetic). These are the sentences that used to be
    // PRINTED under their control and are now the content of the ⓘ beside it — consts
    // rather than literals at the call site so `SettingsProsePass2Tests` can measure the
    // SENTENCE rather than an identifier. Not one word of any of them was rewritten.
    //
    // `SetupReadout.BehaviorNote` is the fifth and lives in UI.Shared, so it has no const
    // here; it hangs on `_setupBtn`.

    /// <summary>What alt-tabbing away does with the switch on. Hangs on
    /// <c>_hideUnfocused</c> — and it is the pair to the note under the NEXT box, which
    /// stayed in the body because it names a door (see the class comment).</summary>
    private const string HideUnfocusedBlurb =
        "Alt-tab to a browser and EQBuddy — with its chips and every window it has open — "
        + "gets out of the way; alt-tab back to the game and everything returns. This one "
        + "always shows EQBuddy when the game isn't running — the next box covers that.";

    /// <summary>Why the re-lift exists and when to turn it off. Hangs on
    /// <c>_keepAbove</c>.</summary>
    private const string KeepAboveBlurb =
        "Overlay apps created after EQBuddy land above it in Windows' always-on-top pile, "
        + "hiding it; this quietly re-lifts every EQBuddy window every few seconds. Untick "
        + "if your screen-capture setup shows EQBuddy twice (a real copy plus the captured "
        + "one).";

    /// <summary>What binding a global key costs, and how to bind one. Hangs on the "Global
    /// hotkeys" HEADING — the rows underneath are built and rebuilt by
    /// <see cref="BuildHotkeyRows"/>, so the heading is the one anchor in this section that
    /// survives a rebuild.</summary>
    private const string HotkeysBlurb =
        "Nothing is bound until you bind it. A bound key is claimed system-wide while "
        + "EQBuddy runs — it will stop reaching the game and every other app — so pick "
        + "combos nothing else uses (Ctrl+Alt+… is usually safe). Click a box, press your "
        + "keys; ✕ unbinds.";

    /// <summary>Why the regen number is a guess and how to correct it. Hangs on the ROW,
    /// which is the whole "Regen heals about [ ] hp per tick" sentence.</summary>
    private const string RegenOverrideBlurb =
        "Hymn of Restoration and similar regen ticks never log an amount, so their healing "
        + "is estimated. The wiki knows the unamplified base (Hymn: 9), but instruments and "
        + "ranks raise the real number — read yours off the heal text over your head and "
        + "type it here. Your number wins.";

    /// <summary>Who should turn auto-empty off. Hangs on <c>_truncate</c>.</summary>
    private const string AutoEmptyBlurb =
        "Turn off if you use GINA/GamParse or upload your log files elsewhere — they will "
        + "grow forever, so clean them up yourself occasionally. (Cleanup already stands "
        + "down whenever the game, GINA, or GamParse is running.)";

    /// <summary>What an archive is and what EQBuddy will never do to it. Hangs on
    /// <c>_archive</c>.
    ///
    /// ONE paragraph, not two. The window declared this explanation twice — the second copy
    /// was a strict subset of the first and rendered directly under it, which is a
    /// duplication a diff shows and a screenshot shows better. Carrying it into a block that
    /// serves two hosts would have shipped it twice in two places.</summary>
    private const string ArchiveBlurb =
        "On by default: each finished session is saved as "
        + "eqlog_name_server_YYYYMMDDHHMMSS.txt — the stamp is when the session ended — and "
        + "Reset session splits the log here rather than letting it run on. Archives are "
        + "yours to keep or clean up; EQBuddy never deletes them. Untick if you would rather "
        + "have the disk space back.";

    /// <summary>What the title-bar readout is and why it exists. Hangs on
    /// <c>_perfStats</c>.</summary>
    private const string PerfReadoutBlurb =
        "A small dim readout (\"0.3% · 84 MB\") refreshed every few seconds — CPU is the "
        + "share of ALL cores. Diagnostic honesty: if EQBuddy ever hogs your machine, this "
        + "is how you catch it and tell us.";

    private CheckBox _mobileSounds = null!, _hideUnfocused = null!, _hideNotRunning = null!;
    private CheckBox _hideAltTab = null!, _keepAbove = null!, _clickThrough = null!;
    private CheckBox _truncate = null!, _archive = null!, _tutorial = null!, _perfStats = null!;
    private StackPanel _hotkeysPanel = null!;
    private TextBox _regenPerTickBox = null!;
    private Button _reviewLogBtn = null!;

    /// <summary>Guards the checkbox's own <c>Checked</c>/<c>Unchecked</c> handler while THIS
    /// class is the one pushing the value (from <see cref="MainWindow.ClickThroughChanged"/>)
    /// rather than the player clicking it — click-through has two OTHER doors
    /// (the "toggleClickThrough" hotkey, the unlock chip), so without the guard a push from
    /// either of those would immediately call back into <see cref="MainWindow.SetClickThrough"/>
    /// a second time.</summary>
    private bool _syncingClickThrough;

    private UIElement Build()
    {
        var panel = new StackPanel();

        // TOP OF THE LIST (gear-menu-slim faces §A2): the control this block gained from the
        // cut expanded gear menu, placed above the hide-policies so it stays the fastest
        // thing to find here even though it used to be one right-click away.
        panel.Children.Add(BuildClickThrough());
        panel.Children.Add(BuildSecondScreen());
        panel.Children.Add(BuildHideRules());
        panel.Children.Add(BuildHotkeys());
        panel.Children.Add(BuildRegenOverride());
        panel.Children.Add(BuildLogHousekeeping());
        panel.Children.Add(BuildDataGroup());

        _tutorial = Check("Show quick tutorial at launch", _vm.ShowTutorial,
            new Thickness(0, 10, 0, 0),
            () => { if (Ready) _vm.ShowTutorial = _tutorial.IsChecked == true; });
        panel.Children.Add(_tutorial);

        // Directly under the launch tour, because it is the same job for a different gap:
        // this block's own doc comment already claims onboarding as its territory, which is
        // why Bevel's ruling puts the re-open here rather than in a fifth tab (four is
        // signed — I-11/#331, pinned by SettingsRoomTests).
        if (_openSetup is not null) panel.Children.Add(BuildSetupReopen());

        _perfStats = Check("Show EQBuddy's own CPU & memory in the title bar",
            _main.Settings.ShowPerfStats, new Thickness(0),
            () =>
            {
                if (!Ready) return;
                _main.Settings.ShowPerfStats = _perfStats.IsChecked == true;
                _main.Settings.Save();
            });
        panel.Children.Add(HintRow(_perfStats, PerfReadoutBlurb, new Thickness(0, 10, 0, 0)));

        return panel;
    }

    // ================================================================ Setup (OE-6) ====

    private Button? _setupBtn;

    /// <summary>The way back into the first-run screen. A BUTTON rather than a tick box:
    /// there is nothing to configure here — the auto-launch answer is a fact about the
    /// dumps plus the player's one "stop offering", and a checkbox would invite somebody to
    /// re-arm the nag. The words are <see cref="SetupReadout"/>'s, so this row and the
    /// screen it opens cannot come to different ideas about what it is for.</summary>
    private UIElement BuildSetupReopen()
    {
        _setupBtn = new Button
        {
            Content = SetupReadout.BehaviorLabel, Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _setupBtn.Click += (_, _) => _openSetup?.Invoke();
        // The prose pass: `BehaviorNote` is what the ⓘ beside the button says, rather than a
        // paragraph under it. The row IS the panel now — a StackPanel wrapping one row would
        // be a box that decides nothing (trap 15).
        return HintRow(_setupBtn, SetupReadout.BehaviorNote, new Thickness(0, 10, 0, 0));
    }

    // ============================================================ EQBuddy Mobile ====

    /// <summary>FIRST in the block, not buried at the bottom — the primary way in is the
    /// title-bar 📱 button; this is the explanation that sits beside it.</summary>
    private UIElement BuildSecondScreen()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(Heading("EQBuddy Mobile (Beta)"));
        panel.Children.Add(Dim(
            "Show EQBuddy on a phone or tablet on your Wi-Fi: scan the code once, then pick "
            + "which windows that device shows. LAN-only and off by default — nothing leaves "
            + "your network. The 📱 button in the title bar opens it any time.",
            new Thickness(0, 2, 0, 4)));

        var open = new Button
        {
            Content = "EQBuddy Mobile…", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) => _main.OpenCompanionWindow();
        panel.Children.Add(open);

        // #208: adjacent to pairing on purpose — it is a property of the phone, and the player
        // who wants it is the player who just paired one. Label and both notes come from
        // UI.Shared/MobileAlertSounds so no two surfaces can drift (#122, #152).
        //
        // No sample plays on the flip — Bevel's lock is explicit about that, and a demo noise
        // from a PC while the phone is the surface under discussion would be answering a
        // different question.
        _mobileSounds = Check(MobileAlertSounds.Label, _vm.MobileSounds, new Thickness(0, 10, 0, 0),
            () => { if (Ready) _vm.MobileSounds = _mobileSounds.IsChecked == true; });
        panel.Children.Add(_mobileSounds);
        panel.Children.Add(Dim(
            MobileAlertSounds.HelperText + " " + MobileAlertSounds.ScopeNote,
            new Thickness(20, 2, 0, 0)));

        return panel;
    }

    // ============================================================ click-through ====
    // Moved here from the expanded gear menu (gear-menu-slim faces §A2, DRA-25): the
    // control still lives on MainWindow (it flips a window style bit) — this is a REMOTE
    // control for it, not a second copy of the state.

    private UIElement BuildClickThrough()
    {
        _clickThrough = Check("Click-through (game clicks pass through)",
            _main.ClickThroughEnabled, new Thickness(0, 0, 0, 14),
            () =>
            {
                if (!Ready || _syncingClickThrough) return;
                _main.SetClickThrough(_clickThrough.IsChecked == true);
            });
        return _clickThrough;
    }

    /// <summary>Called by <c>OptionsWindow.SyncClickThrough</c> — see
    /// <see cref="MainWindow.SetClickThrough"/> — when the hotkey or the unlock chip flips
    /// it while this block is on screen. Guarded so the resulting <c>Checked</c>/
    /// <c>Unchecked</c> event does not call back into <c>SetClickThrough</c> a second time.
    /// </summary>
    internal void SyncClickThrough(bool on)
    {
        if (_block is null || _clickThrough.IsChecked == on) return;
        _syncingClickThrough = true;
        _clickThrough.IsChecked = on;
        _syncingClickThrough = false;
    }

    // ============================================================== when to hide ====

    private UIElement BuildHideRules()
    {
        var panel = new StackPanel();

        _hideUnfocused = Check("Hide EQBuddy while the game is running but not focused",
            _vm.HideWhenGameUnfocused, new Thickness(0),
            () => { if (Ready) _vm.HideWhenGameUnfocused = _hideUnfocused.IsChecked == true; });
        panel.Children.Add(HintRow(_hideUnfocused, HideUnfocusedBlurb, new Thickness(0)));

        _hideNotRunning = Check("Hide EQBuddy while the game isn't running at all",
            _vm.HideWhenGameNotRunning, new Thickness(0, 8, 0, 0),
            () => { if (Ready) _vm.HideWhenGameNotRunning = _hideNotRunning.IsChecked == true; });
        panel.Children.Add(_hideNotRunning);
        panel.Children.Add(Dim(
            "EQBuddy is on screen only while you play: quit the game and everything disappears, "
            + "launch it and everything returns (within a few seconds). Need it back without the "
            + "game — say, to browse session history? Launch EQBuddy again from the Start menu: "
            + "the running copy surfaces, and stays visible while any of its windows has focus.",
            new Thickness(20, 2, 0, 0)));

        // Applied to every open window immediately, not on the next launch — a tick-box whose
        // effect waits for a relaunch is indistinguishable from a broken one, and this one has
        // a visible answer the moment it lands.
        _hideAltTab = Check("Keep EQBuddy out of the Alt+Tab switcher",
            _vm.HideFromAltTab, new Thickness(0, 8, 0, 0),
            () =>
            {
                if (!Ready) return;
                _vm.HideFromAltTab = _hideAltTab.IsChecked == true;
                _main.ApplyAltTabStyle();
            });
        panel.Children.Add(_hideAltTab);
        // The cost is stated where the choice is made: one flag, both effects.
        panel.Children.Add(Dim(
            string.Join(" ", new[] { AltTabPolicy.TaskbarWarning, AltTabPolicy.UnavailableNote }
                .Where(s => s.Length > 0)),
            new Thickness(20, 2, 0, 0)));

        _keepAbove = Check("Keep EQBuddy above fullscreen overlays (Lossless Scaling and kin)",
            _vm.KeepAboveOverlays, new Thickness(0),
            () => { if (Ready) _vm.KeepAboveOverlays = _keepAbove.IsChecked == true; });
        panel.Children.Add(HintRow(_keepAbove, KeepAboveBlurb, new Thickness(0, 10, 0, 0)));

        return panel;
    }

    // ========================================================= global hotkeys ====
    // Opt-in only (#100 — see HotkeyManager). Nothing is bound until the player binds it.

    private string? _recordingAction;

    /// <summary>Transient message shown in the recording button after a rejected press.</summary>
    private string? _recordingHint;

    private UIElement BuildHotkeys()
    {
        var panel = new StackPanel();
        panel.Children.Add(HintRow(Heading("Global hotkeys", "TextBrush"), HotkeysBlurb,
            new Thickness(0, 14, 0, 4)));

        _hotkeysPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
        panel.Children.Add(_hotkeysPanel);
        BuildHotkeyRows();
        return panel;
    }

    private void BuildHotkeyRows()
    {
        _hotkeysPanel.Children.Clear();
        foreach (var (key, label) in HotkeyManager.Actions)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            row.Children.Add(name);

            var bound = _main.Settings.Hotkeys.GetValueOrDefault(key, "");
            var recorder = new Button
            {
                Style = (Style)_resource("ActionButton"), FontSize = 11,
                // The recording prompt names the rule instead of hiding it: a bare key is
                // rejected on purpose (a global "G" would eat chat typing), and the 1.66 field
                // test proved silent rejection reads as a dead recorder.
                Content = _recordingAction == key
                    ? _recordingHint ?? "press Ctrl/Alt/Shift + a key…"
                    : bound.Length > 0 ? bound : "not bound — click to set",
                Tag = key,
            };
            recorder.Click += (_, _) =>
            {
                _recordingAction = _recordingAction == key ? null : key;
                _recordingHint = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(recorder, 1);
            row.Children.Add(recorder);

            var clear = new Button
            {
                Style = (Style)_resource("IconButton"), Content = "✕", FontSize = 11,
                Margin = new Thickness(4, 0, 0, 0), ToolTip = "Unbind",
                Visibility = bound.Length > 0 ? Visibility.Visible : Visibility.Hidden,
            };
            clear.Click += (_, _) =>
            {
                _main.Settings.Hotkeys.Remove(key);
                _main.Settings.Save();
                _main.ApplyHotkeys();
                _recordingAction = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(clear, 2);
            row.Children.Add(clear);
            _hotkeysPanel.Children.Add(row);
        }
    }

    /// <summary>
    /// A key press while a recorder is armed. The HOST forwards its <c>PreviewKeyDown</c> here
    /// and honours the answer — true means "recorded, rejected or cancelled; do not let this
    /// key do anything else."
    ///
    /// It is a method rather than a handler on this block's own panel because
    /// <see cref="BuildHotkeyRows"/> replaces the button that was clicked, so nothing inside
    /// the panel has focus when the press arrives and the tunnelling route never reaches us.
    /// The block still owns every decision — which keys are gestures, what a rejection says —
    /// and the host owns only the routing.
    /// </summary>
    public bool HandleRecordingKey(KeyEventArgs e)
    {
        if (_recordingAction is not { } action) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { _recordingAction = null; BuildHotkeyRows(); return true; }
        // A bare modifier press isn't a gesture yet — wait for the real key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return true;

        var mods = Keyboard.Modifiers;
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        var gesture = string.Join("+", parts);
        // Modifier required — a bare global letter would eat the game's chat typing. Say so on
        // the button itself: a silent return looks like a dead recorder.
        if (HotkeyManager.Parse(gesture) is null)
        {
            _recordingHint = $"{key} alone won't do — add Ctrl, Alt or Shift";
            BuildHotkeyRows();
            return true;
        }
        _main.Settings.Hotkeys[action] = gesture;
        _main.Settings.Save();
        _main.ApplyHotkeys();
        _recordingAction = null;
        _recordingHint = null;
        BuildHotkeyRows();
        return true;
    }

    // ============================================================ regen override ====

    private UIElement BuildRegenOverride()
    {
        var panel = new StackPanel();

        var row = new StackPanel
        { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        row.Children.Add(Body("Regen heals about"));
        _regenPerTickBox = new TextBox
        {
            Width = 48, Margin = new Thickness(6, 0, 6, 0), FontSize = 12,
            TextAlignment = TextAlignment.Right,
            Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "",
        };
        _regenPerTickBox.SetResourceReference(Control.BackgroundProperty, "PanelBrush");
        _regenPerTickBox.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _regenPerTickBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        _regenPerTickBox.LostFocus += (_, _) =>
        {
            if (!Ready) return;
            // Blank or unparseable = back to the wiki base; the box shows any clamp.
            _vm.RegenPerTickOverride = int.TryParse(_regenPerTickBox.Text.Trim(), out var v) ? v : 0;
            _regenPerTickBox.Text = _vm.RegenPerTickOverride > 0
                ? _vm.RegenPerTickOverride.ToString() : "";
        };
        row.Children.Add(_regenPerTickBox);
        row.Children.Add(Body("hp per tick (blank = wiki base)"));
        // The explained thing is the whole SENTENCE-with-a-box, so the ⓘ hangs on the row
        // rather than on the TextBox: an ⓘ between "about" and "hp per tick" would read as
        // part of the sentence. The horizontal stack goes INSIDE HintRow's WrapPanel so the
        // ⓘ wraps to the next line at a narrow width instead of being clipped (trap 25).
        row.Margin = new Thickness(0);
        panel.Children.Add(HintRow(row, RegenOverrideBlurb, new Thickness(0, 10, 0, 0)));
        return panel;
    }

    // ========================================================= log housekeeping ====

    private UIElement BuildLogHousekeeping()
    {
        var panel = new StackPanel();

        _truncate = Check("Auto-empty finished-session logs", _vm.TruncateLogs,
            new Thickness(0),
            () => { if (Ready) _vm.TruncateLogs = _truncate.IsChecked == true; });
        panel.Children.Add(HintRow(_truncate, AutoEmptyBlurb, new Thickness(0, 12, 0, 0)));

        _archive = Check("Keep a timestamped copy before emptying (Logs\\archive)",
            _vm.ArchiveLogs, new Thickness(0),
            () => { if (Ready) _vm.ArchiveLogs = _archive.IsChecked == true; });
        panel.Children.Add(HintRow(_archive, ArchiveBlurb, new Thickness(20, 6, 0, 0)));

        return panel;
    }

    // ==================================================================== data ====
    // Four of the six rows the expanded gear menu's "Data & imports" submenu used to carry
    // (gear-menu-slim faces §A3, DRA-25), grouped as one labeled cluster rather than four
    // loose rows — the same shape AlertSurface.AlertTab already gives the Alerts tab's four
    // families. The other two, Import achievements… and Copy /outputfile achievements, feed
    // the Guide checklist directly and moved to QuestsView instead — an import belongs on
    // the surface its output lives on (trap 43), and that surface is not this one.

    private UIElement BuildDataGroup()
    {
        var panel = new StackPanel();
        panel.Children.Add(Heading("Data", margin: new Thickness(0, 14, 0, 4)));

        var wikiPack = new Button
        {
            Content = "Wiki contribution pack…", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0),
            ToolTip = "Paste-ready eqlwiki edits built from your own loot log — creatures "
                + "with no page, pages that list no loot, and drops missing from a page. "
                + "Nothing publishes automatically; you open each edit link, review and save.",
        };
        wikiPack.Click += (_, _) => _main.ShowWikiPackWindow();
        panel.Children.Add(wikiPack);

        _reviewLogBtn = new Button
        {
            Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0),
            ToolTip = "Replay a saved log read-only — Drops by Creature and the wiki "
                + "contribution pack work against that session",
        };
        _reviewLogBtn.Click += (_, _) => _main.OnReviewLog(_reviewLogBtn, new RoutedEventArgs());
        PaintReviewLogButton();
        panel.Children.Add(_reviewLogBtn);

        var chooseFolder = new Button
        {
            Content = "Choose log folder…", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0),
            // Computed on hover rather than kept in sync from outside (OnResetToolTipOpening's
            // pattern) — simpler than pushing a value nobody but a tooltip ever reads.
            ToolTip = _main.Settings.LogFolder ?? "(no folder found)",
        };
        chooseFolder.ToolTipOpening += (_, _) =>
            chooseFolder.ToolTip = _main.Settings.LogFolder ?? "(no folder found)";
        chooseFolder.Click += (_, _) => _main.OnChooseLogFolder(chooseFolder, new RoutedEventArgs());
        panel.Children.Add(chooseFolder);

        var autoDetect = new Button
        {
            Content = "Auto-detect log folder", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0),
        };
        autoDetect.Click += (_, _) => _main.OnAutoDetectLogFolder(autoDetect, new RoutedEventArgs());
        panel.Children.Add(autoDetect);

        return panel;
    }

    /// <summary>The review-log button's live label — the old <c>ReviewLogItem</c> had an
    /// <c>Icon</c> slot for the "reviewing now" tick; a Button's <c>Content</c> carries the
    /// same fact as text instead. Called at build time and by <see cref="SyncReviewState"/>.
    /// </summary>
    private void PaintReviewLogButton() => _reviewLogBtn.Content = _main.IsReviewingArchive
        ? "Reviewing an archive — return to live log"
        : "Review an archived log…";

    /// <summary>Called by <c>OptionsWindow.SyncReviewState</c> when the OTHER door out of a
    /// review — clicking the widget's CharLabel — fires while this block is on screen.
    /// </summary>
    internal void SyncReviewState()
    {
        if (_block is not null) PaintReviewLogButton();
    }

    // ================================================================== plumbing ====

    /// <summary>A control (or a heading) and the explanation that used to be printed under
    /// it, now on an ⓘ beside it. The row is <see cref="DesignSystem.HintRow"/> so the four
    /// Settings blocks cannot come to different ideas about how it wraps (trap 25).</summary>
    private UIElement HintRow(FrameworkElement control, string prose, Thickness margin) =>
        DesignSystem.HintRow(control, Hint(prose), margin);

    /// <summary>The ⓘ itself, and the only place this block counts one — see
    /// <see cref="DebugFacts"/>, which reports what was BUILT rather than how many the
    /// source names.</summary>
    private Button Hint(string prose)
    {
        var hint = DesignSystem.InfoHint(prose);
        hint.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        _hints++;
        return hint;
    }

    /// <summary>How many ⓘ affordances this instance has built.</summary>
    private int _hints;

    private static TextBlock Heading(string text, string brush = "AccentBrush", Thickness margin = default)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = margin,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    private static TextBlock Body(string text)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }

    private TextBlock Dim(string text, Thickness margin) => new()
    {
        Text = text, Style = (Style)_resource("Dim"),
        TextWrapping = TextWrapping.Wrap, Margin = margin,
    };

    private CheckBox Check(string text, bool initial, Thickness margin, Action changed)
    {
        var label = new TextBlock { Text = text, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var box = new CheckBox { Content = label, Margin = margin, IsChecked = initial };
        box.Checked += (_, _) => changed();
        box.Unchecked += (_, _) => changed();
        return box;
    }
}
