using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The standalone Quest Tracker's WINDOW — and, since E-3 Phase 2 PR 3, only its window.
///
/// **The surface itself is <see cref="QuestsView"/>.** It was lifted out of here because
/// the Evolved shell needed the content without the window and there was nothing to hand
/// it: 2,481 lines of window-owned rendering, which is why Bevel's signed pre-design
/// called Quests *"an extraction, not a redesign"* and gave it its own diff instead of
/// letting it ride PR 2 with World and Gear. <c>SpawnsView</c>/<c>SpawnsWindow</c>
/// (World PR 1) is the precedent this follows exactly.
///
/// What is left is what a literal OS window owns, and each line of it is a thing a ROOM
/// must not have:
///
///  * **Position** — <see cref="ScreenGuard"/> placement on open, the #117 rule on close
///    (never let an unmoved fallback overwrite a real saved spot). A room has no position.
///  * **Zoom and resize** — <see cref="WindowZoom"/> keyed "quests" (#186).
///  * **The height cap** — from the monitor this window is ACTUALLY on, re-applied when it
///    moves (#186 / #31), pushed into the view through <see cref="QuestsView.CapScrollers"/>.
///    The shell's room is a bounded cell and asks for no cap at all, which is why the view
///    defaults to uncapped rather than guessing which host it is in.
///  * **The follow tick** — <see cref="IFollowingSurface"/>, so `PaintOneMoment` can bring
///    this window level before the dump reads a row off it (trap 56).
///
/// **Every method below is a forward**, and that is the shape worth keeping: the day one
/// of them starts deciding something, this window has grown a second copy of a rule the
/// shell cannot see.
/// </summary>
public partial class QuestsWindow : Window, IFollowingSurface
{
    private readonly AppSettings _settings;
    private readonly QuestsView _view;

    public QuestsWindow(MainWindow main)
    {
        InitializeComponent();
        _settings = main.Settings;

        // ITS OWN instance, never one the shell is already rendering (trap 45): a WPF
        // UIElement has exactly one parent, so a shared view would be torn out of whichever
        // host painted it last — silently, with no exception to point at.
        _view = new QuestsView(main);
        // Assigned as Content rather than into a wrapper: WindowZoom puts its
        // LayoutTransform on `window.Content`, so that has to BE the thing that scales,
        // and the tree stays Window → QuestsView exactly as it was Window → Border.
        //
        // The view keeps its own title row here, heading and close button and all — this
        // window is borderless, so that row IS its title bar. Only the shell's room calls
        // HideOwnTitleBar(), because only the shell supplies chrome of its own.
        Content = _view;

        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "quests", _settings, baseWidth: Width);
        WindowZoom.AllowResize(this, "quests", _settings);
        // A drag changes how much room the body has; without this the window grows
        // and its content does not follow.
        SizeChanged += (_, _) => UpdateHeightCaps();

        // No ChipScale here — quests read at widget size, not chip size. That used to be
        // said as ChipScale.Apply(this, 1.0), which is not a no-op: it CLEARS the content
        // LayoutTransform, so it silently threw away the zoom WindowZoom had just restored
        // and every saved Ctrl+wheel zoom was lost on open (#186).
        var restored = ScreenGuard.OnScreen(_settings.QuestsLeft, _settings.QuestsTop, Width, 200);
        if (restored) { Left = _settings.QuestsLeft; Top = _settings.QuestsTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        // The cap follows the monitor the window is ACTUALLY on. Sizing against
        // SystemParameters.WorkArea caps against the PRIMARY screen, so dragging the
        // tracker to a smaller second monitor left it taller than that monitor with no
        // way to shrink it — "1/4 of the window is cut off" (#186, Kemble-Kemble). Same
        // primary-only bug class as discussion #31; SpawnsView's host already does this.
        UpdateHeightCaps();
        SourceInitialized += (_, _) => UpdateHeightCaps();
        LocationChanged += (_, _) => UpdateHeightCaps();
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.QuestsLeft, _settings.QuestsTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.QuestsLeft, _settings.QuestsTop);
            _settings.Save();
            // Give back what the view borrowed — the search debounce. The window never did
            // this before the lift and got away with it (a closed window is closed once);
            // the view names the obligation now, so both hosts honour it.
            _view.Release();
        };
    }

    /// <summary>Height caps follow the monitor this window occupies, re-applied whenever
    /// it moves — a window dragged to a shorter screen must shrink to fit it (#186).
    ///
    /// The BODY opens at a design constant, not at a fraction of the monitor. Deriving it
    /// from the screen is what made this window fill a tall display; UI.Shared owns the
    /// number so all seven pop-outs cannot disagree about it. The list and the pane share
    /// the window's height, so the SCROLLERS are capped rather than the window: without
    /// this the window grows past its cap on a long catalog and the footnotes walk off the
    /// bottom of the screen.</summary>
    private void UpdateHeightCaps()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;   // before the handle exists
        MaxHeight = Math.Max(220, height * 0.85);
        _view.CapScrollers(
            WindowSizing.BodyCap(MaxHeight, 280, FramelessResize.ManualHeight(this)));
    }

    // ---- forwards ---------------------------------------------------------------

    /// <summary>Raised when the PLAYER switches tabs in the view. Not raised by
    /// <see cref="SetTab"/> — the theme host follows the player, not a hook.</summary>
    internal event Action<QuestTab>? TabChanged
    {
        add => _view.TabChanged += value;
        remove => _view.TabChanged -= value;
    }

    /// <summary>Jump the window to one item's quests (the map badge in the Loot views).
    /// Fronting the window is the HOST's half of that job — a view does not know whether
    /// it is in a window or a room.</summary>
    public void FilterToItem(string item)
    {
        _view.FilterToItem(item);
        Activate();
    }

    internal void SetMode(string mode) => _view.SetMode(mode);

    internal void SetTab(string tab) => _view.SetTab(tab);

    internal void FactionsChanged() => _view.FactionsChanged();

    public void MaybeRefresh() => _view.MaybeRefresh();

    void IFollowingSurface.MaybeFollow() => _view.MaybeRefresh();

    void IFollowingSurface.PaintNow() => _view.PaintNow();

    public long RenderedVersion => _view.RenderedVersion;

    /// <summary>The view's facts, verbatim and under their original keys — every E2E
    /// assertion written against `quests*` still reads this window. The SHELL asks the
    /// same method for the same string and re-keys it under `shellQuests*`, because the
    /// dump is one flat namespace and two hosts of one surface would otherwise write over
    /// each other (trap 58).</summary>
    internal string DebugFacts() => _view.DebugFacts();
}
