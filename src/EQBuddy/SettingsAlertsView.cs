using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The four alert blocks, host-neutral — the first spend of <see cref="AlertSurface"/>.**
///
/// Everything Options knows about "alert me, at this volume, with this sound" lives here:
/// the shared sound/voice header, and one block per <see cref="AlertTab"/> — Watch (the
/// rules editor), Buffs (expiring-only, the warn window, the buff-set builder), Spawns
/// (respawn timers) and Crowd (mez chips and the durations they count down).
///
/// **Blocks, not tabs, are the unit that moves** (Fable's SR series). A block builds its own
/// controls, carries its own visibility and spacing (trap 15), and knows nothing about the
/// window it hangs in — so `OptionsWindow` can keep its five tabs in the arrangement players
/// already have while the Evolved shell's Settings room composes the SAME blocks under the
/// signed four-tab IA. The alternative — building the room fresh beside a live
/// `OptionsWindow` — is two copies of forty control wirings drifting until retirement day,
/// which is #210's mechanism with a bigger surface.
///
/// **Each host constructs its own instance** (trap 45). A WPF <c>UIElement</c> has exactly
/// one parent, so a block shared between two hosts is torn out of whichever painted it last,
/// silently, with nothing in a diff or a screenshot to say so. <see cref="Block"/> hands out
/// a body per tab, and that is only safe because the body belongs to THIS instance and this
/// instance belongs to one host: it is a factory's output, not a loan from another window.
///
/// **Both hosts wrap one <see cref="AppSettings"/>** (trap 13). The view takes the
/// <see cref="OptionsViewModel"/> the host already built over <c>MainWindow.Settings</c>; it
/// never loads settings for itself, because a second snapshot would clobber the first one
/// wholesale on its next save.
///
/// **<c>PinWatchChips</c> was the one control this lift deliberately left behind, and it is
/// now RETIRED rather than pending** (Surface A / SA-R, Helm #341). "Show watch chips in the
/// mini dashboard" was presence, not what-fires — and so is the per-rule 📌 sitting a few
/// pixels away on every rule row in the Watch block below. Two switches, one question, which
/// is what the reconciliation asked to reduce to one; the pin is the survivor, because it is
/// the finer-grained of the two and the only one BOTH hosts can carry. So this file gained no
/// presence switch: the master left the v1 window instead, and
/// <c>UI.Shared.WatchPinMigration</c> translates an unticked master into per-rule unpins once
/// so nobody's HUD changes under them.
/// </summary>
internal sealed class SettingsAlertsView
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private readonly Func<bool> _hostReady;
    private readonly Func<object, object> _resource;
    private readonly Func<Window?> _owner;

    /// <summary>The host's gate (false while it is still building) AND our own, which the
    /// file-picker paths flip while they snap a combo box back after a cancel. Both have to
    /// be open before an event is a player's doing.</summary>
    private bool _selfReady = true;

    private bool Ready => _selfReady && _hostReady();

    public SettingsAlertsView(MainWindow main, OptionsViewModel vm, Func<bool> ready,
        Func<object, object> resource, Func<Window?> owner)
    {
        _main = main;
        _vm = vm;
        _hostReady = ready;
        _resource = resource;
        _owner = owner;
    }

    // ---------------------------------------------------------------- the strip ----

    /// <summary>
    /// The four headers a host draws its strip (or its section headings) from, with the
    /// counts this profile actually has. Ordering and labels are <see cref="AlertSurface"/>'s
    /// so the desktop window, the shell room and EQBuddy Mobile cannot come to different
    /// ideas about what the tabs ARE.
    ///
    /// <para><b>Crowd counts nothing, on purpose.</b> The record's own rule is that null means
    /// "not applicable" while 0 means "none yet, and that is actionable" — three of these
    /// tabs configure a LIST (rules written, buckets assembled, timers running) and Crowd
    /// configures a switch plus a table of durations nobody is expected to fill in. A "0"
    /// badge beside Crowd control would read as failure rather than as a default.</para>
    ///
    /// <para><b>Whoever renders these as a strip owes trap 25 a `WrapPanel`.</b> A badge makes
    /// a chip's width depend on its content, and a horizontal `StackPanel` measures with
    /// infinite width in the stacking direction — so the fourth chip is CLIPPED at the panel
    /// edge with no ellipsis to say so. That is exactly how the Progress window shipped three
    /// visible tabs out of four.</para>
    /// </summary>
    public IReadOnlyList<AlertTabHeader> Tabs() => AlertSurface.Tabs(
        watch: _vm.Rules.Count,
        buffs: BuffSetBucketCount(),
        spawns: _main.SpawnTimers.Snapshot(DateTime.Now).Count,
        crowd: null);

    private int BuffSetBucketCount()
    {
        var key = _main.BuffSetKey;
        if (key.Length == 0) return 0;
        var stored = _main.Settings.BuffSetsByClass.GetValueOrDefault(key);
        return stored is null ? 0 : stored.Count(b => b.Value.Count > 0);
    }

    /// <summary>A block's heading, for a host that stacks blocks instead of paging them.
    /// The shell's Settings room does not use this — there the label IS the tab.</summary>
    public UIElement Heading(AlertTabHeader header)
    {
        var text = new TextBlock
        {
            Text = header.Badge is { } badge ? $"{header.Label}  {badge}" : header.Label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 2),
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return text;
    }

    // ---------------------------------------------------------------- the blocks ----

    private UIElement? _header, _watch, _buffs, _spawns, _crowd;

    /// <summary>
    /// The shared sound/voice/volume/rate block, rendered ONCE above the four — a
    /// cross-cutting default every rule can override, not one family's content. Every
    /// per-rule "Default" in the Watch block resolves to what is picked here.
    /// </summary>
    public UIElement Header => _header ??= BuildHeader();

    /// <summary>This instance's body for a tab, built on first ask and kept — the host
    /// re-shows it rather than re-building, so a half-typed box survives a tab switch.</summary>
    public UIElement Block(AlertTab tab) => tab switch
    {
        AlertTab.Watch => _watch ??= BuildWatchBlock(),
        AlertTab.Buffs => _buffs ??= BuildBuffsBlock(),
        AlertTab.Spawns => _spawns ??= BuildSpawnsBlock(),
        AlertTab.Crowd => _crowd ??= BuildCrowdBlock(),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "Not an alert tab."),
    };

    // ================================================================ shared header ====

    private ComboBox _soundCombo = null!;
    private Slider _alertVolumeSlider = null!;
    private TextBlock _alertVolumeLabel = null!, _soundFileNote = null!;
    private ComboBox _voiceCombo = null!;
    private Slider _speechRateSlider = null!, _speechVolumeSlider = null!;
    private TextBlock _speechRateLabel = null!, _speechVolumeLabel = null!;
    private CheckBox _slowAlert = null!, _slowSpoken = null!, _slowRaidOnly = null!;
    private IReadOnlyList<string> _installedVoices = [];

    private UIElement BuildHeader()
    {
        var panel = new StackPanel();

        _soundCombo = new ComboBox { Width = 120, FontSize = 12 };
        foreach (var choice in OptionsViewModel.SoundChoices) _soundCombo.Items.Add(choice);
        _soundCombo.SelectedIndex = _vm.SoundIndex;
        _soundCombo.SelectionChanged += OnSoundChanged;
        panel.Children.Add(RowWithControls("Alert sound",
            _soundCombo,
            IconButton("▶", "Play the alert sound", () => _main.PlayAlertSound())));

        _alertVolumeLabel = AccentValue("100%");
        panel.Children.Add(LabelledValue("Alert volume", _alertVolumeLabel, new Thickness(0, 6, 0, 0)));
        _alertVolumeSlider = new Slider
        {
            Minimum = 0.1, Maximum = 1.0, TickFrequency = 0.05, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 4),
            Value = Math.Clamp(_vm.Settings.AlertVolume, 0.1, 1.0),
        };
        _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";
        _alertVolumeSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.Settings.AlertVolume = _alertVolumeSlider.Value;
            _main.PersistSettings();
            _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";
        };
        panel.Children.Add(_alertVolumeSlider);

        _soundFileNote = Dim("", new Thickness(0));
        _soundFileNote.Visibility = Visibility.Collapsed;
        panel.Children.Add(_soundFileNote);
        UpdateSoundFileNote();

        panel.Children.Add(Dim(
            "While Options is open, the ★ alert banner tile is visible — drag it to where "
            + "alerts should appear. During play it's click-through and never steals focus.",
            new Thickness(0, 4, 0, 0)));

        // Speech gets its own volume: the slider above drives only the MediaPlayer that
        // plays sound files — SAPI never saw it, so one slider claiming both would be a lie
        // in whichever direction it didn't reach.
        //
        // Enumerated once per build — voices install with language packs, not mid-session,
        // and the SAPI walk isn't free.
        _installedVoices = SpokenAlerts.InstalledVoiceNames();
        _voiceCombo = new ComboBox { Width = 164, FontSize = 12 };
        foreach (var choice in OptionsViewModel.VoiceChoices(_installedVoices)) _voiceCombo.Items.Add(choice);
        _voiceCombo.SelectedIndex = _vm.VoiceIndex(_installedVoices);
        _voiceCombo.SelectionChanged += (_, _) =>
        {
            if (!Ready || _voiceCombo.SelectedIndex < 0) return;
            _vm.SelectVoice(_installedVoices, _voiceCombo.SelectedIndex);
            SpeakSample();   // a voice choice you can hear, like the sound picker's instant play
        };
        var voiceRow = RowWithControls("Alert voice", _voiceCombo,
            IconButton("▶", "Hear a sample with the current voice, rate and volume", SpeakSample));
        voiceRow.Margin = new Thickness(0, 12, 0, 0);
        panel.Children.Add(voiceRow);
        panel.Children.Add(Dim(
            "Used wherever EQBuddy speaks — watch rules with the S toggle, and the slow alert.",
            new Thickness(0, 2, 0, 0)));

        _speechRateLabel = AccentValue(_vm.SpeechRateLabel);
        panel.Children.Add(LabelledValue("Speech rate", _speechRateLabel, new Thickness(0, 6, 0, 0)));
        _speechRateSlider = new Slider
        {
            Minimum = -5, Maximum = 5, TickFrequency = 1, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 4), Value = _vm.SpeechRate,
        };
        _speechRateSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.SpeechRate = (int)Math.Round(_speechRateSlider.Value);
            _speechRateLabel.Text = _vm.SpeechRateLabel;
        };
        panel.Children.Add(_speechRateSlider);

        _speechVolumeLabel = AccentValue(_vm.SpeechVolumeLabel);
        panel.Children.Add(LabelledValue("Speech volume", _speechVolumeLabel, new Thickness(0)));
        _speechVolumeSlider = new Slider
        {
            Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 4), Value = _vm.SpeechVolume,
        };
        _speechVolumeSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.SpeechVolume = (int)Math.Round(_speechVolumeSlider.Value);
            _speechVolumeLabel.Text = _vm.SpeechVolumeLabel;
        };
        panel.Children.Add(_speechVolumeSlider);

        // SLOW ALERT LIVES WITH THE HEADER, not with Watch (the executor call the plan asked
        // for, logged in DECISIONS.md). It is not a rule the player wrote — there is nothing
        // to add, reorder, share or delete — and it is the one built-in that the shared voice
        // above already names in its own helper line. Filing it under Watch would put a
        // fixed, undeletable row in the middle of an editor whose whole grammar is "these are
        // yours"; filing it here says what it is: a built-in alert, configured beside the
        // voice that speaks it.
        _slowAlert = Check("Slow alert (an attack-speed debuff lands on you)",
            _main.Settings.SlowAlertEnabled, new Thickness(0, 12, 0, 0), OnSlowAlertToggled);
        panel.Children.Add(_slowAlert);
        panel.Children.Add(Dim(
            "A 🐌 chip shows the slow's % and its counters; hover it for the cure line. "
            + "A silent 40% slow quietly doubles a fight.",
            new Thickness(20, 2, 0, 0)));
        _slowSpoken = Check("Speak it when it lands (\"Slowed 40 percent\")",
            _main.Settings.SlowAlertSpoken, new Thickness(20, 6, 0, 0), OnSlowAlertToggled);
        panel.Children.Add(_slowSpoken);
        _slowRaidOnly = Check("Only during raids",
            _main.Settings.SlowAlertRaidOnly, new Thickness(20, 6, 0, 0), OnSlowAlertToggled);
        panel.Children.Add(_slowRaidOnly);
        panel.Children.Add(Dim(
            "Raids are detected from raid-channel chat — the log's only raid signal. "
            + "A raid nobody has typed in for 10 minutes counts as over.",
            new Thickness(40, 2, 0, 0)));

        return panel;
    }

    private void OnSlowAlertToggled()
    {
        if (!Ready) return;
        _main.Settings.SlowAlertEnabled = _slowAlert.IsChecked == true;
        _main.Settings.SlowAlertSpoken = _slowSpoken.IsChecked == true;
        _main.Settings.SlowAlertRaidOnly = _slowRaidOnly.IsChecked == true;
        _main.Settings.Save();
    }

    private void OnSoundChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!Ready) return;
        if (!_vm.IsCustomSoundIndex(_soundCombo.SelectedIndex))
        {
            _vm.SelectNamedSound(_soundCombo.SelectedIndex);
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an alert sound",
                Filter = AlertSoundFormats.WpfFilter,
            };
            if (ShowDialog(dlg))
                _vm.SetCustomSound(dlg.FileName);
            else if (!_vm.IsCustomSoundIndex(_vm.SoundIndex))
            {
                _selfReady = false;
                _soundCombo.SelectedIndex = _vm.SoundIndex;   // cancelled — revert
                _selfReady = true;
            }
        }
        UpdateSoundFileNote();
        _main.PlayAlertSound();   // instant feedback on the new choice
    }

    private void UpdateSoundFileNote()
    {
        _soundFileNote.Text = _vm.SoundFileNote;
        _soundFileNote.Visibility = _vm.SoundFileNote.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Real alert text, × and all, so the sample demonstrates exactly what an alert will
    // sound like (SpokenAlerts.Speakable rewrites the × for the voice).
    private static void SpeakSample() => SpokenAlerts.SpeakSample("Rusty Sword ×3");

    // ==================================================================== Buffs ====

    private CheckBox _buffExpiringOnly = null!;
    private TextBox _buffWarnBox = null!;
    private TextBlock _buffSetCharNote = null!;
    private StackPanel _buffSetPanel = null!;
    private ComboBox _buffSetClassBox = null!;
    private TextBox _buffSetAddBox = null!;
    private Popup _buffSetPopup = null!;
    private Border _buffSetChrome = null!;
    private ListBox _buffSetMatches = null!;

    private UIElement BuildBuffsBlock()
    {
        var panel = new StackPanel();

        _buffExpiringOnly = Check("Buff timers: only show buffs about to fade",
            // No top margin: a block owns nothing above its first control. Its host supplies the
            // separation — a heading here, a tab strip in the shell (trap 15).
            _main.Settings.BuffTimersExpiringOnly, new Thickness(0), OnBuffDisplayChanged);
        panel.Children.Add(_buffExpiringOnly);

        // Two columns rather than a horizontal StackPanel would be wrong here — this row is
        // three fixed pieces around one 44px box, which is exactly what a stack is for.
        var warnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 4, 0, 0) };
        warnRow.Children.Add(Body("warn at"));
        _buffWarnBox = new TextBox
        {
            Width = 44, Margin = new Thickness(6, 0, 6, 0), FontSize = 12,
            TextAlignment = TextAlignment.Right,
            Text = _main.Settings.BuffWarnSeconds.ToString("0"),
        };
        _buffWarnBox.SetResourceReference(Control.BackgroundProperty, "PanelBrush");
        _buffWarnBox.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _buffWarnBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        _buffWarnBox.LostFocus += (_, _) => OnBuffDisplayChanged();
        warnRow.Children.Add(_buffWarnBox);
        warnRow.Children.Add(Body("seconds left"));
        panel.Children.Add(warnRow);

        panel.Children.Add(Dim(
            "Unticked, your buff list counts down everything that's running. Ticked, it stays "
            + "quiet (with an honest count) until a buff is inside the warning window — tell me "
            + "when it matters. Your own casts already include your Spell Casting Reinforcement "
            + "rank; a buff's first natural fade teaches its exact duration either way.",
            new Thickness(20, 2, 0, 0)));

        // ---- buff set (#120, Frankthetankk — the missing line's editor) ----
        // Stage 2: the set lives PER CLASS in Settings.BuffSetsByClass (BuffSetStore owns the
        // shape) and assembles from the active class combination plus "(any class)". This
        // editor shows every bucket — active or parked — because it is the one place a stored
        // pick can always be removed. Every edit routes through MainWindow.OnBuffSetEdited so
        // every surface showing the set repaints at once; an edit whose effect waits for the
        // next tick reads as a silent no-op.
        var setHeading = new TextBlock
        {
            Text = "Buff set — the missing line", FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 2),
        };
        setHeading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        panel.Children.Add(setHeading);
        panel.Children.Add(Dim(
            "Pick the buffs this character never camps without — per class: each pick lands in a "
            + "class bucket, and the live set assembles from the classes you're running plus "
            + "(any class), so swapping one class keeps the other classes' picks. The ⏳ buff "
            + "list grows one line ONLY when something's off: missing (seen fading, or its timer "
            + "ran out), expiring (inside the warn window above), or not seen (no landing line "
            + "this session — it may be up from before EQBuddy was watching; the log can't tell, "
            + "so it's shown as its own honest state). Everything up = no line at all. You build "
            + "the list yourself; nothing is ever added for you.",
            new Thickness(0)));

        _buffSetCharNote = Dim("", new Thickness(0, 2, 0, 0));
        panel.Children.Add(_buffSetCharNote);
        _buffSetPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        panel.Children.Add(_buffSetPanel);

        // Stage 2 (#120): the add box targets a class bucket. The FULL class list is offered
        // here (unlike the breakout's active-only list) so a swap can be configured in advance.
        var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _buffSetClassBox = new ComboBox
        {
            MinWidth = 110, Margin = new Thickness(0, 0, 6, 0), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Which class bucket the next pick goes into — (any class) applies whatever "
                + "combination you run",
        };
        addRow.Children.Add(_buffSetClassBox);
        _buffSetAddBox = new TextBox
        {
            Style = (Style)_resource("InputBox"),
            ToolTip = "Type a few letters of a buff's name — buffs you've been seen casting list "
                + "first, then the whole buff catalog",
        };
        _buffSetAddBox.TextChanged += (_, _) => OnBuffSetSearchChanged();
        Grid.SetColumn(_buffSetAddBox, 1);
        addRow.Children.Add(_buffSetAddBox);
        panel.Children.Add(addRow);

        _buffSetMatches = new ListBox { MaxHeight = 240, MaxWidth = 480, FontSize = 11.5 };
        _buffSetMatches.SelectionChanged += (_, _) => OnBuffSetMatchPicked();
        _buffSetChrome = new Border
        {
            BorderThickness = new Thickness(1), Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6), Child = _buffSetMatches,
        };
        _buffSetPopup = new Popup
        {
            PlacementTarget = _buffSetAddBox, Placement = PlacementMode.Bottom,
            StaysOpen = false, AllowsTransparency = true, Child = _buffSetChrome,
        };
        panel.Children.Add(_buffSetPopup);

        BuildBuffSetPanel();
        return panel;
    }

    private void OnBuffDisplayChanged()
    {
        if (!Ready) return;
        _main.Settings.BuffTimersExpiringOnly = _buffExpiringOnly.IsChecked == true;
        if (double.TryParse(_buffWarnBox.Text, out var seconds))
            _main.Settings.BuffWarnSeconds = Math.Clamp(seconds, 10, 3600);
        _buffWarnBox.Text = _main.Settings.BuffWarnSeconds.ToString("0");   // shows any clamp
        _main.Settings.Save();
    }

    /// <summary>The breakout editor writes the same storage; MainWindow calls this through
    /// its host so its edits appear here immediately too.</summary>
    internal void RefreshBuffSetEditor()
    {
        if (_buffs is not null) BuildBuffSetPanel();
    }

    private void BuildBuffSetPanel()
    {
        var key = _main.BuffSetKey;
        var (classes, picked) = _main.BuffSetClassSource(_main.CurrentSnapshot());
        _buffSetCharNote.Text = key.Length > 0
            ? $"Saved for {_main.BuffSetCharacterName}, per class — the live set is "
              + "(any class) plus "
              + (classes.Count > 0
                  ? $"{string.Join(", ", classes.Select(QuestClassFilter.Abbrev))} "
                    + (picked
                        ? "(picked in the Quest Tracker — picks WIDEN what EQBuddy already "
                          + "knows about your character rather than replacing it)."
                        // "(inferred)" was one of three things this can be, and said nothing
                        // at all when the GAME had told us through an achievements dump.
                        : $"({CharacterClasses.SourceLabel(ClassSource.Inferred)} — pick classes "
                          + "in the Quest Tracker to widen).")
                  : "your classes — none known yet: pick them in the Quest Tracker, or use (any class).")
            : "No character detected yet — once today's log names one, reopen Options and the editor unlocks.";
        _buffSetAddBox.IsEnabled = key.Length > 0;
        _buffSetClassBox.IsEnabled = key.Length > 0;
        RefreshBuffSetClassChoices();
        _buffSetPanel.Children.Clear();
        if (key.Length == 0) return;

        var stored = _main.Settings.BuffSetsByClass.GetValueOrDefault(key);
        // Active buckets first, in assembly order; then parked ones (stored picks whose class
        // isn't in the current combination) — visible and editable, so a swap never strands a
        // pick out of reach. That parked picks SURVIVE the swap is the requester's whole
        // design. Shared with the breakout (BuffSetStore.EditableSections) so the two editors
        // cannot disagree about which buckets are visible — they did, and a pick added to a
        // parked class vanished from the breakout entirely (#120, Frankthetankk). Options
        // additionally hides EMPTY active sections; the breakout keeps them, because there
        // they are the place you add.
        var sections = BuffSetStore.EditableSections(stored, classes)
            .Where(r => r.Section.Spells.Count > 0)
            .Select(r => (r.Section.Class, Spells: r.Section.Spells, r.Parked))
            .ToList();
        if (sections.Count == 0)
        {
            _buffSetPanel.Children.Add(Dim(
                "Nothing picked yet — pick a class bucket and search below to build the set.",
                new Thickness(0, 2, 0, 0)));
            return;
        }
        foreach (var (cls, spells, parked) in sections)
        {
            var header = new TextBlock
            {
                Text = cls + (parked ? "  · not in your current classes — kept for the swap back" : ""),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0),
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, parked ? "DimBrush" : "AccentBrush");
            _buffSetPanel.Children.Add(header);
            foreach (var spell in spells)
            {
                var row = new Grid { Margin = new Thickness(6, 2, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = spell, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, parked ? "DimBrush" : "TextBrush");
                row.Children.Add(label);
                var remove = new Button
                {
                    Style = (Style)_resource("IconButton"), Content = "✕", FontSize = 11,
                    Margin = new Thickness(4, 0, 0, 0), ToolTip = $"Remove {spell} from {cls}",
                };
                var (doomedClass, doomed) = (cls, spell);
                remove.Click += (_, _) =>
                {
                    BuffSetStore.Remove(_main.Settings.BuffSetsByClass, key, doomedClass, doomed);
                    _main.Settings.Save();
                    _main.OnBuffSetEdited();   // repaints every surface showing the set
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                _buffSetPanel.Children.Add(row);
            }
        }
    }

    /// <summary>Add-target buckets: "(any class)" plus the FULL class list — unlike the
    /// breakout's active-only list, so a coming swap can be configured here in advance.
    /// Selection survives rebuilds.</summary>
    private void RefreshBuffSetClassChoices()
    {
        var keep = _buffSetClassBox.SelectedItem as string;
        if (_buffSetClassBox.Items.Count == 0)
        {
            _buffSetClassBox.Items.Add(BuffSetStore.AnyClass);
            foreach (var cls in QuestClassFilter.Classes) _buffSetClassBox.Items.Add(cls);
        }
        _buffSetClassBox.SelectedItem = keep ?? BuffSetStore.AnyClass;
    }

    private string SelectedBuffSetClass =>
        _buffSetClassBox.SelectedItem as string ?? BuffSetStore.AnyClass;

    private void OnBuffSetSearchChanged()
    {
        if (!Ready) return;
        var query = _buffSetAddBox.Text.Trim();
        if (query.Length < 2) { _buffSetPopup.IsOpen = false; return; }
        _buffSetChrome.SetResourceReference(Border.BackgroundProperty, "PopupBrush");
        _buffSetChrome.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        _buffSetMatches.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
        _buffSetMatches.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _buffSetMatches.Items.Clear();

        // Seen first (the buffs this player demonstrably casts), then the whole buff catalog
        // — BuffSetSearch, shared with the breakout editor. Both draw from
        // BuffDurationCatalog's attributable spells, so nothing can be added that would sit at
        // "not seen" forever. Only the TARGET bucket's picks are excluded: the same buff under
        // another class is a legitimate pick.
        var inBucket = BuffSetStore.SpellsFor(
            _main.Settings.BuffSetsByClass.GetValueOrDefault(_main.BuffSetKey), SelectedBuffSetClass);
        foreach (var (s, seen) in BuffSetSearch.Rank(query, _main.SeenBuffCasts(),
                     inBucket, BuffDurationCatalog.Default.SpellNames))
            _buffSetMatches.Items.Add(new ListBoxItem
            {
                Content = seen ? s + "   · seen this session" : s,
                Tag = s,
            });
        if (_buffSetMatches.Items.Count == 0)
            _buffSetMatches.Items.Add(new ListBoxItem
            {
                Content = "No buff in the catalog matches — check the spelling?",
                IsEnabled = false,
            });
        _buffSetPopup.IsOpen = true;
    }

    private void OnBuffSetMatchPicked()
    {
        if (!Ready || _buffSetMatches.SelectedItem is not ListBoxItem { Tag: string spell }) return;
        _buffSetPopup.IsOpen = false;
        _buffSetMatches.SelectedItem = null;
        var key = _main.BuffSetKey;
        if (key.Length == 0) return;
        BuffSetStore.Add(_main.Settings.BuffSetsByClass, key, SelectedBuffSetClass, spell);
        _main.Settings.Save();
        _buffSetAddBox.Text = "";   // TextChanged with an empty box closes the popup
        _main.OnBuffSetEdited();    // repaints every surface showing the set
    }

    // ==================================================================== Spawns ====

    private CheckBox _trackSpawns = null!;

    private UIElement BuildSpawnsBlock()
    {
        var panel = new StackPanel();
        _trackSpawns = Check("🕒 Track spawns (named respawn timers)",
            _main.Settings.TrackSpawns, new Thickness(0),
            // Routed through MainWindow, not the view model: the setting, the right-click
            // menu check, and the window itself all have to move together.
            () => { if (Ready) _main.SetTrackSpawns(_trackSpawns.IsChecked == true); });
        panel.Children.Add(_trackSpawns);
        panel.Children.Add(Dim(
            "Kill a named — or its placeholder — and a small countdown chicklet appears "
            + "(⏳ Asaka L`Rei 3:12). Chicklets sit in one row under EQBuddy and move with it, "
            + "show every timer you have running in any zone, and flip to DUE for a minute "
            + "(click to dismiss sooner). Double-click one (or right-click → Spawn timers…) "
            + "for the full zone list, which follows you zone to zone. We captured the respawn "
            + "times we could from community sources — if you notice a discrepancy in game, "
            + "type over the duration: your number wins and survives updates.",
            new Thickness(20, 2, 0, 0)));

        // The two "grow upward" tick-boxes retired in Surface A / SA-2, with the two
        // separately-placed stacks they arbitrated between (#95: "park boss timers above mez
        // timers and each grows away from the other"). Spawn and mez chips are ONE row now,
        // under EQBuddy, so there is no second stack to grow away from and no saved position
        // for either.
        return panel;
    }

    /// <summary>Called back by MainWindow.SetTrackSpawns so closing the Spawns window (or
    /// toggling the menu) updates this checkbox while a host is showing it.</summary>
    internal void SyncTrackSpawns(bool on)
    {
        if (_spawns is null) return;
        var was = _selfReady;
        _selfReady = false;
        _trackSpawns.IsChecked = on;
        _selfReady = was;
    }

    // ===================================================================== Crowd ====

    private CheckBox _mezChips = null!;
    private MezDurationsView _mezDurations = null!;

    private UIElement BuildCrowdBlock()
    {
        var panel = new StackPanel();
        _mezChips = Check("Mez countdown chips (who's asleep, wake-up timers)",
            _main.Settings.MezChipsEnabled, new Thickness(0), () =>
            {
                if (!Ready) return;
                _main.Settings.MezChipsEnabled = _mezChips.IsChecked == true;
                _main.Settings.Save();
            });
        panel.Children.Add(_mezChips);
        panel.Children.Add(Dim(
            "Untick if your class never mezzes — mez chips stop appearing entirely.",
            new Thickness(20, 2, 0, 0)));

        // Mez durations, the same contract spawn durations have: your number outranks
        // anything EQBuddy works out. Asked for on Reddit (relayed by David, 2026-08-20) by a
        // player whose only cure for a wrong learned value was deleting a file by hand. This
        // sits under the mez chips box because that is where someone whose mez chip is wrong
        // already goes.
        var heading = new TextBlock
        {
            Text = "Mez durations", FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(20, 12, 0, 0),
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        panel.Children.Add(heading);

        var blurb = Dim("", new Thickness(20, 2, 0, 4));
        panel.Children.Add(blurb);
        var list = new ContentControl { Margin = new Thickness(20, 0, 0, 0) };
        panel.Children.Add(list);
        _mezDurations = new MezDurationsView(list, blurb, _main.MezTracker, _main.MezDurations);
        _mezDurations.Render();

        return panel;
    }

    // ===================================================================== Watch ====

    private Button _guideToggle = null!;
    private Border _guidePanel = null!;
    private StackPanel _guideContent = null!;
    private StackPanel _rulesPanel = null!;
    private Button _addFromLogBtn = null!;
    private Popup _recentLinesPopup = null!;
    private Border _recentLinesChrome = null!;
    private CheckBox _recentLinesHideChat = null!;
    private ListBox _recentLinesList = null!;
    private TextBox _importBox = null!;
    private TextBlock _importPreview = null!;
    private Button _importConfirmBtn = null!;

    private UIElement BuildWatchBlock()
    {
        var panel = new StackPanel();

        panel.Children.Add(Dim(
            "Watch loot, kills, skill-ups, deaths, milestones, your spells wearing off, or any "
            + "text in the log. Match is a case-insensitive substring, e.g. 'mote'. Spell fade "
            + "rules can pick a whole class (Any crowd control, Charm, Mez, Root, Lull, Stun, "
            + "HoT) instead of a named spell, needing no match text. Delay holds the alert back "
            + "that many seconds so it lands as a cue. 🔔 shows a banner; the sound box picks "
            + "this rule's own sound, so you can tell what happened by ear — 'Default' follows "
            + "the shared alert sound, 'Off' stays silent, 'Custom…' takes your own .wav/.mp3.",
            new Thickness(0)));

        // Collapsed by default: the examples answer the questions people actually ask (is
        // "Mote" enough? why isn't my rule matching?) but they're a wall of text for anyone
        // who already knows.
        _guideToggle = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0),
            Style = (Style)_resource("IconButton"), FontSize = 11,
            Content = "▸ Show examples", ToolTip = "Worked examples for every rule kind",
        };
        _guideToggle.Click += (_, _) =>
            ApplyGuideOpen(_guidePanel.Visibility != Visibility.Visible, persist: true);
        panel.Children.Add(_guideToggle);

        _guideContent = new StackPanel();
        _guidePanel = new Border
        {
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 2),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6, 8, 6),
            Child = _guideContent,
        };
        _guidePanel.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
        panel.Children.Add(_guidePanel);

        // Shared-size scope so the header row's Auto columns track the rule rows'.
        _rulesPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetIsSharedSizeScope(_rulesPanel, true);
        panel.Children.Add(_rulesPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var add = new Button
        {
            Content = "+ Add watch rule", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12,
        };
        add.Click += (_, _) => { _vm.AddRule(); BuildRulesEditor(); };
        buttons.Children.Add(add);
        _addFromLogBtn = new Button
        {
            Content = "+ From a recent log line…", Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Pick a line that just happened in your log — it becomes a Text rule, no "
                + "typing. Edit the match afterward to taste.",
        };
        _addFromLogBtn.Click += (_, _) => OnAddRuleFromLog();
        buttons.Children.Add(_addFromLogBtn);
        panel.Children.Add(buttons);

        _recentLinesHideChat = Check(
            "Hide chat (say/tell/channels) — combat and system lines only",
            _main.Settings.RecentLinesHideChat, new Thickness(4, 3, 4, 3), () =>
            {
                if (!Ready) return;
                _main.Settings.RecentLinesHideChat = _recentLinesHideChat.IsChecked == true;
                _main.Settings.Save();
                FillRecentLines();
            }, fontSize: 11, dimLabel: true);
        _recentLinesList = new ListBox { MaxHeight = 300, MaxWidth = 560, FontSize = 11.5 };
        _recentLinesList.SelectionChanged += (_, _) => OnRecentLinePicked();
        var recentBody = new StackPanel();
        recentBody.Children.Add(_recentLinesHideChat);
        recentBody.Children.Add(_recentLinesList);
        _recentLinesChrome = new Border
        {
            BorderThickness = new Thickness(1), Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6), Child = recentBody,
        };
        _recentLinesPopup = new Popup
        {
            PlacementTarget = _addFromLogBtn, Placement = PlacementMode.Top,
            StaysOpen = false, AllowsTransparency = true, Child = _recentLinesChrome,
        };
        panel.Children.Add(_recentLinesPopup);

        var importRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        importRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        importRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _importBox = new TextBox
        {
            Style = (Style)_resource("InputBox"),
            ToolTip = "Paste a shared watch rule (EQB1.…) from guild chat or Discord — the ⤴ on "
                + "any rule row makes one",
        };
        importRow.Children.Add(_importBox);
        var importBtn = new Button
        {
            Content = "Import…", Style = (Style)_resource("ActionButton"),
            Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3),
            ToolTip = "Preview what the pasted share string would add — nothing is added until "
                + "you confirm",
        };
        importBtn.Click += (_, _) => OnImportShare();
        Grid.SetColumn(importBtn, 1);
        importRow.Children.Add(importBtn);
        panel.Children.Add(importRow);

        _importPreview = Dim("", new Thickness(0, 4, 0, 0));
        _importPreview.Visibility = Visibility.Collapsed;
        panel.Children.Add(_importPreview);
        _importConfirmBtn = new Button
        {
            Style = (Style)_resource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed,
        };
        _importConfirmBtn.Click += (_, _) => OnImportConfirm();
        panel.Children.Add(_importConfirmBtn);

        BuildRulesEditor();
        // Restore the examples panel without persisting — this isn't the player changing it.
        ApplyGuideOpen(_main.Settings.ShowWatchGuide, persist: false);
        return panel;
    }

    /// <summary>Show or hide the worked examples, remembering the choice. Content is built on
    /// first expand rather than at construction — most people never open it.</summary>
    private void ApplyGuideOpen(bool open, bool persist)
    {
        _guideToggle.Content = open ? "▾ Hide examples" : "▸ Show examples";
        _guidePanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open && _guideContent.Children.Count == 0) BuildGuide();
        if (persist)
        {
            _main.Settings.ShowWatchGuide = open;
            _vm.Persist();
        }
    }

    private void BuildGuide()
    {
        TextBlock Line(string text, double size, Brush brush, double top, bool bold = false) => new()
        {
            Text = text, FontSize = size, Foreground = brush, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 0),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        };

        var accent = (Brush)_resource("AccentBrush");
        var text = (Brush)_resource("TextBrush");
        var dim = (Brush)_resource("DimBrush");

        _guideContent.Children.Add(Line("How matching works", 11, accent, 0, bold: true));
        foreach (var basic in WatchGuide.Basics)
            _guideContent.Children.Add(Line("• " + basic, 11, dim, 2));

        _guideContent.Children.Add(Line("Examples", 11, accent, 8, bold: true));
        foreach (var ex in WatchGuide.Examples)
        {
            // Kind · name · what to type, then what it gets you. Two lines per example reads
            // better than a table in a panel this narrow.
            var match = ex.Match.Length > 0 ? $"Match \"{ex.Match}\"" : "no match text";
            var delay = ex.Delay.Length > 0 ? $" · Delay {ex.Delay}" : "";
            _guideContent.Children.Add(Line(
                $"{OptionsViewModel.KindNames[(int)ex.Kind]} · \"{ex.Name}\" · {match}{delay}",
                11, text, 8));
            _guideContent.Children.Add(Line(ex.What, 11, dim, 1));
        }
    }

    /// <summary>"That thing that just happened — alert on it." The picker lists the last few
    /// log lines; clicking one mints a Text rule with the line as its match, ready to trim.
    /// Nobody should have to remember a log line to watch for it.</summary>
    private void OnAddRuleFromLog()
    {
        _recentLinesChrome.SetResourceReference(Border.BackgroundProperty, "PopupBrush");
        _recentLinesChrome.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        _recentLinesList.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
        _recentLinesList.SetResourceReference(Control.ForegroundProperty, "TextBrush");

        _recentLinesHideChat.IsChecked = _main.Settings.RecentLinesHideChat;
        FillRecentLines();
        _recentLinesPopup.IsOpen = true;
    }

    private void FillRecentLines()
    {
        var lines = _main.RecentLogLines();
        _recentLinesList.Items.Clear();
        var hideChat = _recentLinesHideChat.IsChecked == true;
        // Newest first: "just happened" is the whole point of the picker.
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var (time, message) = lines[i];
            // Chat lines quote their body (", '") — a busy General channel drowns the combat
            // lines the picker exists for (David's field note), but chat stays one untick
            // away: a "WTS" watch is a legitimate rule too.
            if (hideChat && message.Contains(", '", StringComparison.Ordinal)) continue;
            _recentLinesList.Items.Add(new ListBoxItem
            {
                Content = $"{time:HH:mm:ss}  {(message.Length <= 96 ? message : message[..95] + "…")}",
                Tag = message,
                ToolTip = message,
            });
        }
        if (_recentLinesList.Items.Count == 0)
            _recentLinesList.Items.Add(new ListBoxItem
            {
                Content = hideChat ? "Nothing but chat seen lately — untick the filter."
                    : "No log lines seen yet — play a little first.",
                IsEnabled = false,
            });
    }

    private void OnRecentLinePicked()
    {
        if (!Ready || _recentLinesList.SelectedItem is not ListBoxItem { Tag: string message }) return;
        _recentLinesPopup.IsOpen = false;
        _recentLinesList.SelectedItem = null;
        var rule = _vm.AddRule();
        rule.Kind = WatchKind.Text;
        rule.Pattern = message;
        rule.Name = message.Length <= 28 ? message : message[..27] + "…";
        _vm.Persist();
        BuildRulesEditor();
    }

    /// <summary>
    /// Column layout for both the header and every rule row. Auto columns are matched by
    /// SharedSizeGroup (the panel is a shared-size scope) so the header labels stay lined up
    /// with the controls no matter how wide the combo boxes render.
    /// </summary>
    private static Grid RuleGrid()
    {
        var grid = new Grid();
        void Auto(string group) => grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = group });
        void Star(double w) => grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

        // Kind and name were fixed at 58/60 px, which clipped their content even before the
        // spell-class picker existed. Name and match text share the free width, so widening
        // the host grows the fields that actually hold free text.
        Auto("RuleKind");
        Star(1);
        Star(1.4);
        Auto("RulePin");
        Auto("RuleBanner");
        Auto("RuleColor");
        Auto("RuleSpeech");
        Auto("RulePhrase");
        Auto("RuleSound");
        Auto("RuleDelay");
        Auto("RuleShare");
        Auto("RuleDelete");
        Auto("RuleArrange");
        return grid;
    }

    private void BuildRulesEditor()
    {
        _rulesPanel.Children.Clear();

        var header = RuleGrid();
        header.Margin = new Thickness(0, 2, 0, 2);
        var headings = new[] { ("Watch", 0), ("Name", 1), ("Match", 2), ("Delay", 9) };
        foreach (var (text, column) in headings)
        {
            var label = new TextBlock
            {
                Text = text, FontSize = 10, Opacity = 0.7,
                Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0),
            };
            Grid.SetColumn(label, column);
            header.Children.Add(label);
        }
        _rulesPanel.Children.Add(header);

        foreach (var rule in _vm.Rules)
        {
            var row = RuleGrid();
            row.Margin = new Thickness(0, 3, 0, 0);

            var kind = new ComboBox { FontSize = 11, ToolTip = "What this rule watches" };
            foreach (var k in OptionsViewModel.KindNames) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "name");
            name.Margin = new Thickness(4, 0, 0, 0);
            name.LostFocus += (_, _) => { rule.Name = name.Text.Trim(); _vm.Persist(); };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            // Column 2 holds the match text, preceded (for Spell fade rules) by a class
            // picker: one named spell, or a whole class that keeps working as the character
            // levels into new spells and ranks.
            var matchArea = new Grid();
            matchArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            matchArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(matchArea, 2);
            row.Children.Add(matchArea);

            var spellFilter = new ComboBox
            {
                FontSize = 11,
                MinWidth = 104,
                Margin = new Thickness(4, 0, 0, 0),
                // chaosrah (Reddit): the unlabeled dropdown read as a mystery — say plainly
                // that it's the spell CLASS and that it replaces match text.
                ToolTip = "Spell class: watch one named spell (\"By name\" + match text), " +
                    "or a whole class — Charm, Mez, HoT… — with no match text needed",
            };
            foreach (var f in OptionsViewModel.SpellFilterNames) spellFilter.Items.Add(f);
            spellFilter.SelectedIndex = (int)rule.SpellFilter;
            matchArea.Children.Add(spellFilter);

            const string patternTip = "match text (uses the name if left empty; optional for Death/Milestone)";
            var pattern = DarkBox(rule.Pattern, patternTip);
            pattern.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(pattern, 1);
            matchArea.Children.Add(pattern);

            // Regex mode (#83, KentCarmine): the same Match box, upgraded. Invalid patterns
            // match nothing and say why on the box's own tooltip.
            void ShowRegexState() => pattern.ToolTip =
                rule.RegexError is { } err ? $"Regex error: {err}" : patternTip;
            matchArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var regexToggle = RuleToggle(".*",
                "Treat Match as a regular expression (.NET syntax, case-insensitive). " +
                "An invalid pattern matches nothing — the Match box's tooltip shows the error.",
                2, rule.UseRegex, v => { rule.UseRegex = v; ShowRegexState(); });
            matchArea.Children.Add(regexToggle);
            pattern.LostFocus += (_, _) => { rule.Pattern = pattern.Text.Trim(); ShowRegexState(); _vm.Persist(); };
            ShowRegexState();

            var buffChoices = FadeMessageCatalog.Default.BuffSpellChoices.ToArray();
            var spellName = DarkBox(rule.Pattern,
                "Start typing a known buff/spell fade, then pick one. Free typing still works.");
            spellName.Margin = new Thickness(4, 0, 0, 0);
            var spellMatches = new ListBox { MaxHeight = 260, MinWidth = 220, FontSize = 12 };
            spellMatches.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
            spellMatches.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            var spellChrome = new Border
            {
                BorderThickness = new Thickness(1), Padding = new Thickness(2), Child = spellMatches,
            };
            spellChrome.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
            spellChrome.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
            var spellPopup = new Popup
            {
                PlacementTarget = spellName,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = spellChrome,
            };
            matchArea.Children.Add(spellPopup);

            var choosingSpell = false;
            void UpdateSpellChoices(string text, bool open)
            {
                var q = (text ?? "").Trim();
                spellMatches.ItemsSource = q.Length == 0
                    ? buffChoices
                    : buffChoices
                        .Where(s => s.Contains(q, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                spellPopup.IsOpen = open && spellMatches.Items.Count > 0;
            }
            void PickSpell(string picked)
            {
                choosingSpell = true;
                rule.Pattern = picked;
                spellName.Text = picked;
                pattern.Text = picked;
                spellName.CaretIndex = spellName.Text.Length;
                spellPopup.IsOpen = false;
                _vm.Persist();
                choosingSpell = false;
            }
            UpdateSpellChoices(rule.Pattern, open: false);
            spellName.TextChanged += (_, _) =>
            {
                if (choosingSpell) return;
                rule.Pattern = spellName.Text.Trim();
                pattern.Text = rule.Pattern;
                UpdateSpellChoices(spellName.Text, spellName.IsKeyboardFocusWithin);
            };
            spellName.GotKeyboardFocus += (_, _) => UpdateSpellChoices(spellName.Text, open: true);
            spellName.LostKeyboardFocus += (_, _) =>
            {
                rule.Pattern = (spellName.Text ?? "").Trim();
                pattern.Text = rule.Pattern;
                _vm.Persist();
            };
            spellName.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    spellPopup.IsOpen = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && spellPopup.IsOpen && spellMatches.Items.Count > 0)
                {
                    PickSpell((spellMatches.SelectedItem as string) ?? (string)spellMatches.Items[0]!);
                    e.Handled = true;
                }
            };
            spellMatches.SelectionChanged += (_, _) =>
            {
                if (!Ready || spellMatches.SelectedItem is not string picked) return;
                PickSpell(picked);
            };
            Grid.SetColumn(spellName, 1);
            matchArea.Children.Add(spellName);

            // A class filter needs no match text, so the box goes away rather than sitting
            // there inviting input that would be ignored.
            void SyncMatchArea()
            {
                var isFade = rule.Kind == WatchKind.SpellFade;
                var byName = rule.SpellFilter == SpellFilter.ByName;
                spellFilter.Visibility = isFade ? Visibility.Visible : Visibility.Collapsed;
                pattern.Visibility = !isFade ? Visibility.Visible : Visibility.Collapsed;
                // Regex pairs with the free-text Match box; the fade picker flow has its own
                // exact-name semantics.
                regexToggle.Visibility = !isFade ? Visibility.Visible : Visibility.Collapsed;
                spellName.Visibility = isFade && byName ? Visibility.Visible : Visibility.Collapsed;
                // With no match box beside it the combo takes the whole cell, so its text and
                // drop arrow stay inside the row instead of running under the toggles.
                Grid.SetColumnSpan(spellFilter, isFade && !byName ? 2 : 1);
                if (isFade && byName) spellName.Text = rule.Pattern;
                else pattern.Text = rule.Pattern;
            }
            SyncMatchArea();

            kind.SelectionChanged += (_, _) =>
            {
                if (!Ready || kind.SelectedIndex < 0) return;
                rule.Kind = (WatchKind)kind.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };
            spellFilter.SelectionChanged += (_, _) =>
            {
                if (!Ready || spellFilter.SelectedIndex < 0) return;
                rule.SpellFilter = (SpellFilter)spellFilter.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };

            row.Children.Add(RuleToggle("📌", "Show this rule as a chip on the HUD", 3,
                rule.Pinned, v => rule.Pinned = v));

            row.Children.Add(RuleToggle("🔔", "Banner alert on match", 4, rule.AlertBanner,
                v => rule.AlertBanner = v));

            // Banner color: one small dot cycling the palette on click (Chaosrah's
            // color-coded alerts) — a combo box would not fit the row.
            var colorDot = new Button
            {
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(2, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            void PaintDot()
            {
                var hex = AlertColors.Hex(rule.AlertColor);
                var choiceName = AlertColors.Choices[AlertColors.IndexOf(rule.AlertColor)].Name;
                colorDot.Content = new TextBlock
                {
                    Text = "●",
                    FontSize = 12,
                    Foreground = hex.Length > 0
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
                        : (Brush)_resource("AccentBrush"),
                };
                colorDot.ToolTip = $"Banner color: {choiceName} — click to change";
            }
            PaintDot();
            colorDot.Click += (_, _) =>
            {
                var next = (AlertColors.IndexOf(rule.AlertColor) + 1) % AlertColors.Choices.Length;
                var picked = AlertColors.Choices[next].Name;
                rule.AlertColor = picked == "Default" ? "" : picked;
                PaintDot();
                _vm.Persist();
            };
            Grid.SetColumn(colorDot, 5);
            row.Children.Add(colorDot);

            // Custom spoken phrase, beside the S toggle and only while it's on — a phrase box
            // on a rule that never speaks is dead weight in a tight row. Empty speaks the
            // alert's own label, exactly as before the box existed.
            var phrase = DarkBox(rule.SpokenPhrase,
                "What the voice says for this rule (empty = the alert text itself).\n" +
                "Say the instruction, not the event: \"Recast charm now\" instead of\n" +
                "\"Befriend Animal faded off a bear\".");
            phrase.Width = 76;
            phrase.FontSize = 11;
            phrase.Margin = new Thickness(4, 0, 0, 0);
            phrase.Visibility = rule.AlertSpeech ? Visibility.Visible : Visibility.Collapsed;
            phrase.LostFocus += (_, _) => { rule.SpokenPhrase = phrase.Text.Trim(); _vm.Persist(); };
            Grid.SetColumn(phrase, 7);

            row.Children.Add(RuleToggle("S", "Speak this alert with the Windows voice", 6,
                rule.AlertSpeech, v =>
                {
                    rule.AlertSpeech = v;
                    phrase.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
                }));
            row.Children.Add(phrase);

            // Per-rule sound, so you can tell what happened from the audio alone. Replaces the
            // old on/off toggle: "Off" mutes, "Default" follows the shared choice in the
            // header above, anything else is this rule's own sound.
            var sound = new ComboBox
            {
                FontSize = 11,
                MinWidth = 76,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Sound for this rule — pick a different one per rule to tell them apart by ear",
            };
            foreach (var s in AlertSoundCatalog.RuleChoices) sound.Items.Add(s);
            sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
            if (AlertSoundCatalog.IsCustom(rule.AlertSoundName) && rule.AlertSoundName.Length > 0)
                sound.ToolTip = $"Custom: {rule.AlertSoundName}";
            sound.SelectionChanged += (_, _) =>
            {
                if (!Ready || sound.SelectedIndex < 0) return;
                if (AlertSoundCatalog.ApplyRuleChoice(rule, sound.SelectedIndex))
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = $"Choose a sound for \"{(rule.Name.Length > 0 ? rule.Name : rule.Pattern)}\"",
                        Filter = AlertSoundFormats.WpfFilter,
                    };
                    if (ShowDialog(dlg))
                    {
                        rule.AlertSoundName = dlg.FileName;
                        sound.ToolTip = $"Custom: {dlg.FileName}";
                    }
                    else
                    {
                        // Cancelled — snap back to whatever the rule already had.
                        _selfReady = false;
                        sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
                        _selfReady = true;
                        return;
                    }
                }
                _vm.Persist();
                // Play it straight away so picking a sound is a decision you can hear.
                if (AlertSoundCatalog.Resolve(rule, _main.Settings.AlertSound) is { } preview)
                    _main.PlayAlertSound(preview);
            };
            Grid.SetColumn(sound, 8);
            row.Children.Add(sound);

            // Seconds to hold the alert back — 0 (or empty) is the immediate behaviour. Turns
            // a rule into a cue: sound 2.5 s after a heal-chain call to say "cast now", or
            // 25 s after a mez to say "recast before it breaks".
            var delay = DarkBox(DelayText.Format(rule.AlertDelaySeconds),
                "Wait this long before alerting (empty = at once, up to 30 minutes).\n" +
                "Seconds by default; add m for minutes — 2.5, 25, 8m, 1:30.\n" +
                "Use it as a cue: 2.5 after a heal-chain call, 25 into a 30s mez,\n" +
                "or 8m for a respawn. The count updates immediately either way.");
            delay.Width = 40;
            delay.Margin = new Thickness(4, 0, 0, 0);
            delay.TextAlignment = TextAlignment.Right;
            delay.LostFocus += (_, _) =>
            {
                // Unparseable means 0 rather than an error: the box is a few characters wide
                // and the failure is obvious the moment it snaps back.
                rule.AlertDelaySeconds = DelayText.Parse(delay.Text);
                delay.Text = DelayText.Format(rule.AlertDelaySeconds);   // shows any clamp
                _vm.Persist();
            };
            Grid.SetColumn(delay, 9);
            row.Children.Add(delay);

            // Share: the rule as a guild-chat string (WatchRuleShare). The ✓ flash is the only
            // feedback a clipboard write can honestly give.
            var share = new Button
            {
                Content = "⤴", Style = (Style)_resource("IconButton"), FontSize = 11,
                ToolTip = "Copy this rule as a share string — paste it in guild chat or Discord,\n" +
                          "and any EQBuddy imports it from the box below the rule list",
            };
            share.Click += (_, _) =>
            {
                try { Clipboard.SetText(WatchRuleShare.Encode([rule])); }
                catch (Exception ex) { CoreLog.Error(ex); return; }
                share.Content = "✓";
                var revert = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5),
                };
                revert.Tick += (_, _) => { share.Content = "⤴"; revert.Stop(); };
                revert.Start();
            };
            Grid.SetColumn(share, 10);
            row.Children.Add(share);

            var del = new Button
            {
                Content = "✕", Style = (Style)_resource("IconButton"), FontSize = 11,
            };
            del.Click += (_, _) => { _vm.RemoveRule(rule); BuildRulesEditor(); };
            Grid.SetColumn(del, 11);
            row.Children.Add(del);

            // Arrange (#105, wizen): this order IS the watch display's "manual" sort. Stacked
            // ▲▼ in one cell — precise where drag would fight the text boxes.
            var arrange = new StackPanel { Margin = new Thickness(2, 0, 0, 0) };
            foreach (var (glyph, delta) in new[] { ("▲", -1), ("▼", +1) })
            {
                var move = new Button
                {
                    Content = glyph, Style = (Style)_resource("IconButton"),
                    FontSize = 7, Padding = new Thickness(2, 0, 2, 0),
                    ToolTip = "Move this rule " + (delta < 0 ? "up" : "down") +
                              " — the watch display's \"manual\" sort follows this order",
                };
                var d = delta;
                move.Click += (_, _) => { _vm.MoveRule(rule, d); BuildRulesEditor(); };
                arrange.Children.Add(move);
            }
            Grid.SetColumn(arrange, 12);
            row.Children.Add(arrange);

            _rulesPanel.Children.Add(row);
        }
    }

    // ---- share-string import: paste → preview → confirm (nothing lands unseen) ----

    private List<TrackedRule>? _pendingImport;

    private void OnImportShare()
    {
        _pendingImport = WatchRuleShare.TryDecode(_importBox.Text, out var error);
        _importPreview.Visibility = Visibility.Visible;
        if (_pendingImport is null)
        {
            _importPreview.Text = error;
            _importConfirmBtn.Visibility = Visibility.Collapsed;
            return;
        }
        _importPreview.Text = "This will add:\n" +
            string.Join("\n", _pendingImport.Select(r => "  • " + WatchRuleShare.Describe(r)));
        _importConfirmBtn.Content = _pendingImport.Count == 1
            ? "✔ Add this rule" : $"✔ Add these {_pendingImport.Count} rules";
        _importConfirmBtn.Visibility = Visibility.Visible;
    }

    private void OnImportConfirm()
    {
        if (_pendingImport is null) return;
        _vm.ImportRules(_pendingImport);
        _pendingImport = null;
        _importBox.Text = "";
        _importPreview.Visibility = Visibility.Collapsed;
        _importConfirmBtn.Visibility = Visibility.Collapsed;
        BuildRulesEditor();
    }

    private ToggleButton RuleToggle(string glyph, string tip, int column, bool initial, Action<bool> apply)
    {
        var t = new ToggleButton
        {
            Content = glyph, ToolTip = tip, IsChecked = initial, FontSize = 11,
            Style = (Style)_resource("IconToggle"),
        };
        t.Checked += (_, _) => { apply(true); _vm.Persist(); };
        t.Unchecked += (_, _) => { apply(false); _vm.Persist(); };
        Grid.SetColumn(t, column);
        return t;
    }

    private static TextBox DarkBox(string text, string tip)
    {
        var box = new TextBox
        {
            Text = text, ToolTip = tip, FontSize = 12, Padding = new Thickness(4, 2, 4, 2),
        };
        // SetResourceReference (not FindResource) so an in-place theme switch repaints these
        // rows too, not just the chrome built from XAML.
        box.SetResourceReference(Control.BackgroundProperty, "ComboBoxBrush");
        box.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        return box;
    }

    // ================================================================== plumbing ====

    /// <summary>The host owns the window a modal picker parents to. A block with no host
    /// window still opens the dialog — ownerless is worse than nothing only when there IS an
    /// owner to name.</summary>
    private bool ShowDialog(Microsoft.Win32.OpenFileDialog dlg) =>
        (_owner() is { } window ? dlg.ShowDialog(window) : dlg.ShowDialog()) == true;

    private TextBlock Body(string text)
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

    private static TextBlock AccentValue(string text)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return block;
    }

    /// <summary>Label on the left, live value on the right — a one-cell Grid, never a
    /// horizontal StackPanel, because the value's width changes as it counts (trap 14).</summary>
    private static Grid LabelledValue(string label, TextBlock value, Thickness margin)
    {
        var grid = new Grid { Margin = margin };
        grid.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        grid.Children.Add(value);
        return grid;
    }

    private static Grid RowWithControls(string label, params UIElement[] right)
    {
        var grid = new Grid();
        grid.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
        });
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
        };
        foreach (var control in right) stack.Children.Add(control);
        grid.Children.Add(stack);
        return grid;
    }

    private Button IconButton(string glyph, string tip, Action click)
    {
        var button = new Button
        {
            Content = glyph, Style = (Style)_resource("IconButton"), FontSize = 11,
            ToolTip = tip, Margin = new Thickness(4, 0, 0, 0),
        };
        button.Click += (_, _) => click();
        return button;
    }

    private CheckBox Check(string text, bool initial, Thickness margin, Action changed,
        double fontSize = 12, bool dimLabel = false)
    {
        var label = new TextBlock { Text = text, FontSize = fontSize };
        label.SetResourceReference(TextBlock.ForegroundProperty, dimLabel ? "DimBrush" : "TextBrush");
        var box = new CheckBox { Content = label, Margin = margin, IsChecked = initial };
        box.Checked += (_, _) => changed();
        box.Unchecked += (_, _) => changed();
        return box;
    }
}
