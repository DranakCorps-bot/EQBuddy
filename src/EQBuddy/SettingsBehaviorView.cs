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
          $"behaviorSetup={(_setupBtn is null ? 0 : 1)}";

    private CheckBox _mobileSounds = null!, _hideUnfocused = null!, _hideNotRunning = null!;
    private CheckBox _hideAltTab = null!, _keepAbove = null!;
    private CheckBox _truncate = null!, _archive = null!, _tutorial = null!, _perfStats = null!;
    private StackPanel _hotkeysPanel = null!;
    private TextBox _regenPerTickBox = null!;

    private UIElement Build()
    {
        var panel = new StackPanel();

        panel.Children.Add(BuildSecondScreen());
        panel.Children.Add(BuildHideRules());
        panel.Children.Add(BuildHotkeys());
        panel.Children.Add(BuildRegenOverride());
        panel.Children.Add(BuildLogHousekeeping());

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
            _main.Settings.ShowPerfStats, new Thickness(0, 10, 0, 0),
            () =>
            {
                if (!Ready) return;
                _main.Settings.ShowPerfStats = _perfStats.IsChecked == true;
                _main.Settings.Save();
            });
        panel.Children.Add(_perfStats);
        panel.Children.Add(Dim(
            "A small dim readout (\"0.3% · 84 MB\") refreshed every few seconds — CPU is the "
            + "share of ALL cores. Diagnostic honesty: if EQBuddy ever hogs your machine, this "
            + "is how you catch it and tell us.",
            new Thickness(20, 2, 0, 0)));

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
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        _setupBtn = new Button
        {
            Content = SetupReadout.BehaviorLabel, Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _setupBtn.Click += (_, _) => _openSetup?.Invoke();
        panel.Children.Add(_setupBtn);
        panel.Children.Add(Dim(SetupReadout.BehaviorNote, new Thickness(0, 2, 0, 0)));
        return panel;
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

    // ============================================================== when to hide ====

    private UIElement BuildHideRules()
    {
        var panel = new StackPanel();

        _hideUnfocused = Check("Hide EQBuddy while the game is running but not focused",
            _vm.HideWhenGameUnfocused, new Thickness(0),
            () => { if (Ready) _vm.HideWhenGameUnfocused = _hideUnfocused.IsChecked == true; });
        panel.Children.Add(_hideUnfocused);
        panel.Children.Add(Dim(
            "Alt-tab to a browser and EQBuddy — with its chips and every window it has open — "
            + "gets out of the way; alt-tab back to the game and everything returns. This one "
            + "always shows EQBuddy when the game isn't running — the next box covers that.",
            new Thickness(20, 2, 0, 0)));

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
            _vm.KeepAboveOverlays, new Thickness(0, 10, 0, 0),
            () => { if (Ready) _vm.KeepAboveOverlays = _keepAbove.IsChecked == true; });
        panel.Children.Add(_keepAbove);
        panel.Children.Add(Dim(
            "Overlay apps created after EQBuddy land above it in Windows' always-on-top pile, "
            + "hiding it; this quietly re-lifts every EQBuddy window every few seconds. Untick "
            + "if your screen-capture setup shows EQBuddy twice (a real copy plus the captured "
            + "one).",
            new Thickness(20, 2, 0, 0)));

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
        panel.Children.Add(Heading("Global hotkeys", "TextBrush", new Thickness(0, 14, 0, 0)));
        panel.Children.Add(Dim(
            "Nothing is bound until you bind it. A bound key is claimed system-wide while "
            + "EQBuddy runs — it will stop reaching the game and every other app — so pick "
            + "combos nothing else uses (Ctrl+Alt+… is usually safe). Click a box, press your "
            + "keys; ✕ unbinds.",
            new Thickness(0, 2, 0, 4)));

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
        panel.Children.Add(row);

        panel.Children.Add(Dim(
            "Hymn of Restoration and similar regen ticks never log an amount, so their healing "
            + "is estimated. The wiki knows the unamplified base (Hymn: 9), but instruments and "
            + "ranks raise the real number — read yours off the heal text over your head and "
            + "type it here. Your number wins.",
            new Thickness(20, 2, 0, 0)));
        return panel;
    }

    // ========================================================= log housekeeping ====

    private UIElement BuildLogHousekeeping()
    {
        var panel = new StackPanel();

        _truncate = Check("Auto-empty finished-session logs", _vm.TruncateLogs,
            new Thickness(0, 12, 0, 0),
            () => { if (Ready) _vm.TruncateLogs = _truncate.IsChecked == true; });
        panel.Children.Add(_truncate);
        panel.Children.Add(Dim(
            "Turn off if you use GINA/GamParse or upload your log files elsewhere — they will "
            + "grow forever, so clean them up yourself occasionally. (Cleanup already stands "
            + "down whenever the game, GINA, or GamParse is running.)",
            new Thickness(20, 2, 0, 0)));

        _archive = Check("Keep a timestamped copy before emptying (Logs\\archive)",
            _vm.ArchiveLogs, new Thickness(20, 6, 0, 0),
            () => { if (Ready) _vm.ArchiveLogs = _archive.IsChecked == true; });
        panel.Children.Add(_archive);
        // ONE paragraph, not two. The window declared this explanation twice — the second copy
        // was a strict subset of the first and rendered directly under it, which is a
        // duplication a diff shows and a screenshot shows better. Carrying it into a block that
        // serves two hosts would have shipped it twice in two places.
        panel.Children.Add(Dim(
            "On by default: each finished session is saved as "
            + "eqlog_name_server_YYYYMMDDHHMMSS.txt — the stamp is when the session ended — and "
            + "Reset session splits the log here rather than letting it run on. Archives are "
            + "yours to keep or clean up; EQBuddy never deletes them. Untick if you would rather "
            + "have the disk space back.",
            new Thickness(40, 2, 0, 0)));

        return panel;
    }

    // ================================================================== plumbing ====

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
