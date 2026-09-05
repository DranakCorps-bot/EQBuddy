using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The LIVE room — the seventh room of the Evolved shell and the last of the Helm-signed
/// Quests → Home → Live order. Bevel's Live pre-design, Helm-signed 2026-09-05 ~6:35 AM CT.
///
/// **It is the first room that is a MERGE, and that is why its sources are named out
/// loud.** Every room before it had one thing to point at: World and Gear were
/// already-folded windows (a MOVE), Quests was one window with no view (a LIFT), Home had
/// no v1 surface at all (a BUILD). Live has five separate places that all answer *"what is
/// happening in this sitting"*, and they are, exactly:
///
///  1. <c>MainWindow.CombatSection</c> — the Combat card, inline on the widget.
///  2. <c>MainWindow.HealingSection</c> — the Healing card, inline on the widget.
///  3. <see cref="BreakoutWindow"/>'s Damage, Healing and Pet kinds.
///  4. <see cref="FightTimelineWindow"/> — its own pop-out.
///  5. <see cref="CreatureWindow"/>'s **Kills** tab, and <see cref="RaidsCardView"/>.
///
/// **Two things that look like they belong here and do not.** <c>CreatureWindow</c>'s
/// **Drops** tab is camp research — *"is this camp worth it"* — which the disposition
/// table's own Why column sends to World, even though it ships from the same v1 window as
/// the kills counter that IS Live's; splitting one window's two tabs across two rooms in
/// one PR is the shape Bevel §1 flagged as *"the biggest redesign in E-3"*. And
/// <c>HistoryWindow</c>'s this-session half is a real Live-shaped fact whose window is not
/// gated on this room existing. Both are their own asks. A PR that quietly also built them
/// would be a PR that grew a second room's redesign inside its own.
///
/// **NOTHING IS SUBTRACTED FROM THE WIDGET HERE.** All five sources go on working exactly
/// as they ship, in the same PR that builds this room. A v1 surface may be retired only
/// when its shell room has landed, its HUD chip (if any) has shipped and a screenshot
/// proves the replacement does the job — and that is a SECOND PR, per Bevel's per-item HUD
/// gate. So this file touches no <c>OverlaySections</c> entry, no <c>MiniStats</c> key and
/// no card. The one thing that DOES move is Raids, and it moves between two shell rooms
/// that both already exist rather than off the widget — see
/// <see cref="ProgressSurface.MovedToLive"/> for why that is a different question with a
/// different answer.
///
/// **The Home/Live boundary, read from this side.** Home carries no combat numbers by
/// construction: <c>RecentSession</c> has no <c>Dps</c>, <c>Kills</c> or <c>Deaths</c>
/// field to reach for, and a reflection test fails the build if one appears. Live needs
/// all three — and takes them from <see cref="LiveSession"/>, a SIBLING record built from
/// the same <see cref="SessionSummary.Pick"/>. The fields differ; the decision about which
/// sitting is being described does not, and re-deriving that decision here is what would
/// eventually have the two rooms disagree at exactly the boundary a race exposes.
///
/// **Scrolling belongs to the host** (trap 36) — the room's own bounded <c>*</c> cell,
/// a real overflow rather than the infinite-height measure that makes a child scroller
/// swallow the wheel and scroll nothing. The report and the tab strip stay pinned above it
/// rather than being concatenated into the scrolling body, which is what trap 37 cost the
/// Drops tab's footer.
/// </summary>
internal sealed class LiveRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;

    /// <summary>The room proper — report, strip, body — shown for every state but one.</summary>
    private readonly Grid _page = new();

    /// <summary>The whole-room empty, built once on first need. Both children live in this
    /// Grid's single cell and one of them is collapsed, rather than the room swapping its
    /// content: a room that rebuilt its visual tree to say "nothing yet" would throw away
    /// the scroll position every time a fight ended.</summary>
    private FrameworkElement? _emptyRoom;

    private readonly TextBlock _reportHead;
    private readonly TextBlock _reportDetail;
    private readonly EqSegmentedStrip _tabs;
    private readonly ScrollViewer _scroll;
    private readonly ContentControl _body = new();

    private LiveTab _tab = LiveSurface.DefaultTab;

    private readonly MeterPane _damage;
    private readonly MeterPane _healing;
    private readonly MeterPane _pet;
    private readonly TimelinePane _timeline;
    private readonly KillsCardView _kills;
    private readonly RaidsCardView _raids;

    private LiveSession _session =
        new(RecentSessionState.NeverPlayed, "", "", "", null, null, TimeSpan.Zero, 0, 0, 0);
    private bool _empty;

    public UIElement Body => this;

    public LiveRoom(MainWindow main)
    {
        _main = main;

        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_page);

        // ---- the session report, pinned above everything -------------------------
        var report = new StackPanel
        {
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceL, Tok.SpaceL, 0),
        };
        _reportHead = DesignSystem.Text(Role.TitleSection, "");
        _reportHead.TextWrapping = TextWrapping.Wrap;
        _reportHead.Ink("AccentBrush");
        report.Children.Add(_reportHead);
        _reportDetail = DesignSystem.Text(Role.BodySecondary, "");
        _reportDetail.TextWrapping = TextWrapping.Wrap;
        _reportDetail.Ink("DimBrush");
        report.Children.Add(_reportDetail);
        SetRow(report, 0);
        _page.Children.Add(report);

        // WRAPS, and it has to: a horizontal StackPanel measures its children with INFINITE
        // width, so a chip never reaches a boundary to wrap at and the last one is simply
        // clipped at the panel's edge — silently, with no ellipsis (trap 25, itself trap 14
        // with chips). Six chips carrying badges like "118.4 dps" and "2 / 21" is exactly
        // the content that overflows a fixed strip.
        var strip = new WrapPanel { Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0) };
        SetRow(strip, 1);
        _page.Children.Add(strip);
        _tabs = new EqSegmentedStrip(strip);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, Tok.SpaceM),
            Content = _body,
        };
        SetRow(_scroll, 2);
        _page.Children.Add(_scroll);

        // ---- the six surfaces, every one of them built HERE ----------------------
        // A UIElement has exactly one parent, so a surface borrowed from the widget or from
        // a breakout would be torn out of whichever host painted it last — on WPF silently,
        // with no exception to point at, which is harder to notice than the Avalonia crash
        // that found this (trap 45). SurfaceOwnershipTests scans for the accessor shape.
        // The blocked-by lookup is HANDED IN rather than reached for: it is the widget's,
        // it is only meaningful on session-scope damage rows, and a pane that walked back
        // up to its host to find it would be the shape LanesPanel's hard cast to
        // FightTimelineWindow already was (see LanesPanel.Panned).
        _damage = new MeterPane(this, BreakoutPresentation.Damage, SortStrip.ForDamage,
            Repaint, main.BlockedByLookup);
        _healing = new MeterPane(this, BreakoutPresentation.Healing, SortStrip.ForHealing,
            Repaint, main.BlockedByLookup);
        _pet = new MeterPane(this, BreakoutPresentation.Pet, SortStrip.ForDamage,
            Repaint, main.BlockedByLookup);
        _timeline = new TimelinePane(main);
        (_kills, _raids) = main.NewLiveSurfaces();

        // The blocks the Combat card carries under its breakdown, in its own order. Built
        // here rather than at their declarations because the panes they hang in do not
        // exist until the three lines above have run.
        _petAbilities = new Block(_damage.Extras, "Pet abilities");
        _damageTaken = new Block(_damage.Extras, "Damage you took");
        _recentFights = new Block(_damage.Extras, "Recent fights");
        _areaSpells = new Block(_damage.Extras, "Area spells");
        _procs = new Block(_damage.Extras, "Procs");
        _stances = new Block(_damage.Extras, "Stances");
        _invocations = new Block(_damage.Extras, "Invocations");
        _healers = new Block(_healing.Extras, "Who healed you");
    }

    private void Repaint() => Render(_main.CurrentSnapshot());

    /// <summary>Land on a tab by its wire key. An unknown key is left alone rather than
    /// snapped to a default: showing the wrong room silently is worse than showing the one
    /// already open, which is the refusal every room here makes.
    ///
    /// **<c>live:raids</c> is the address the Raids surface answers to now.** It was
    /// <c>progress:raids</c> until this PR, and <c>ProgressRoom.SetTab</c> refuses that key
    /// rather than landing on a tab it no longer draws — see there.</summary>
    public void SetTab(string key)
    {
        if (LiveSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Repaint();
    }

    /// <summary>
    /// **Nothing to give back, and the interesting part is why not.**
    ///
    /// Live is the room Bevel named as most likely to reintroduce the leak
    /// <see cref="IShellRoom.Release"/> exists to prevent: it is the one room whose content
    /// genuinely changes every second, so a redraw cadence of its own is the obvious thing
    /// to reach for — and <see cref="FightTimelineWindow"/>, one of the five sources, owns
    /// exactly such a <c>DispatcherTimer</c> and stops it in its own <c>Closed</c> handler.
    ///
    /// This room starts no timer. The shell already ticks the VISIBLE room once a second
    /// from the widget's own snapshot (<c>ShellWindow.Refresh</c>), which is the cadence the
    /// timeline window's timer was approximating, and taking it means one paint per tick
    /// instead of two racing ones — trap 56's rule, that a surface and the dump beside it
    /// should describe one moment. So there is no timer, no token, no file handle and no
    /// watcher here, and <c>shellLiveTimers=0</c> in the dump is the assertion that says so
    /// from outside rather than a claim this comment makes.
    ///
    /// Empty with a reason, per the interface's own contract.
    /// </summary>
    public void Release() { }

    /// <summary>
    /// Nothing to arrange, and it was CHECKED rather than assumed — Bevel §4 asked for
    /// exactly that.
    ///
    /// <see cref="ShellLayout.RoomSinglePane"/> decides one thing: whether a room is too
    /// narrow to draw a list BESIDE a detail pane, which is the Quests room's shape. Live
    /// has no such pair. Five of its six tabs are a single column of rows, and the sixth —
    /// the timeline — is one canvas whose lane names sit in a 176-unit gutter it draws
    /// itself, inside the same element as the plot rather than in a pane beside it. At the
    /// 520-unit floor that leaves 344 units of plot, which is a narrow fight and not a
    /// broken layout, and no threshold would improve it: collapsing the gutter would take
    /// the lane names away, which is the one thing the canvas cannot be read without.
    ///
    /// Empty with a reason rather than absent, per the interface's own contract.
    /// </summary>
    public void ApplyLayout(ShellLayout layout) { }

    // ---- paint -----------------------------------------------------------------

    /// <summary>Who is being followed, in the order <see cref="SessionSummary"/> takes it.
    ///
    /// **THIS IS A DESTRUCTURE AND NOT AN ASSIGNMENT, AND IT HAS TO BE** — the same trap
    /// <c>HomeRoom.Who</c> already paid for. The two identity pairs in this codebase are
    /// spelled in OPPOSITE orders (<c>MainWindow.Identity</c> is <c>(Character, Server)</c>,
    /// <c>SessionArchiver.Identity</c> is <c>(Server, Character)</c>) and a C# tuple
    /// conversion is POSITIONAL: the element names are checked by nobody, so assigning one
    /// to the other compiles, runs, and hands every reader the two strings the wrong way
    /// round. The rows themselves are scoped by the ARCHIVER's identity inside
    /// <c>MainWindow.StoredSessions</c>; these two strings only name the character on
    /// screen.</summary>
    private (string Server, string Character) Who() => ShellRoomIdentity.Of(_main);

    public void Render(StatsSnapshot s)
    {
        _session = SessionSummary.LiveOf(Who(), _main.StoredSessions(), s);
        var defeated = _raids.DefeatedCount;

        // **The whole-room empty, and the only state that gets one.** With no fight, no
        // kill, no heal and no clear there is nothing for any of the six tabs to be about,
        // and six separate "nothing yet" panels would be six ways of saying the one thing
        // that matters. It is deliberately NOT the same test as "this tab is empty": a
        // player who switched to Healing asked about healing specifically and gets that
        // tab's own explanation, which for a bard mid-song is not "nothing happened".
        _empty = LivePresentation.RoomIsEmpty(_session, s, defeated);
        if (_empty)
        {
            _emptyRoom ??= AddEmptyRoom();
            _emptyRoom.Visibility = Visibility.Visible;
            _page.Visibility = Visibility.Collapsed;
            return;
        }
        if (_emptyRoom is not null) _emptyRoom.Visibility = Visibility.Collapsed;
        _page.Visibility = Visibility.Visible;

        _reportHead.Text = LivePresentation.Headline(_session);
        _reportDetail.Text = LivePresentation.Detail(_session);
        BuildTabs(s, defeated);

        // Only the ACTIVE tab paints, and its body is swapped in rather than all six being
        // stacked and hidden — a hidden StackPanel still measures on every layout pass, and
        // the Raids list alone is 29 rows of it (trap 46's cost half).
        _body.Content = _tab switch
        {
            LiveTab.Healing => _healing.Body,
            LiveTab.Pet => _pet.Body,
            LiveTab.Timeline => _timeline.Body,
            LiveTab.Kills => _kills.Body,
            LiveTab.Raids => _raids.Body,
            _ => _damage.Body,
        };
        // **The timeline is a canvas and everything else is a list.** A ScrollViewer whose
        // vertical scrolling is DISABLED constrains its child to the viewport instead of
        // measuring it with infinite height, which is what a fill-the-cell canvas needs and
        // exactly what a list must not have. One line rather than a second scroller per
        // tab, and it is the host deciding — scrolling belongs to the host (trap 36).
        _scroll.VerticalScrollBarVisibility = _tab == LiveTab.Timeline
            ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

        switch (_tab)
        {
            case LiveTab.Healing: _healing.Render(s); RenderHealingExtras(s); break;
            case LiveTab.Pet: _pet.Render(s); break;
            case LiveTab.Timeline: _timeline.Render(); break;
            case LiveTab.Kills: _kills.Render(s); break;
            case LiveTab.Raids: _raids.Render(s); break;
            default: _damage.Render(s); RenderDamageExtras(s); break;
        }
    }

    /// <summary>The room-level empty, positioned by the shared wrapper Home built —
    /// centred in whatever cell the shell gives the room, with its own words. Home's copy
    /// is about not knowing who you are; this is about a session that is running and has
    /// nothing in it yet, which is a different fact and gets a different sentence.</summary>
    private FrameworkElement AddEmptyRoom()
    {
        var built = RoomEmptyState.Build(
            LivePresentation.EmptyHeading, LivePresentation.EmptyExplanation);
        Children.Add(built);
        return built;
    }

    /// <summary>Build the strip from Core's <see cref="LiveSurface"/> with UI.Shared's
    /// badges — the same split every other room makes, so the arithmetic stays where a unit
    /// test can reach it and this method only draws.</summary>
    private void BuildTabs(StatsSnapshot s, int defeated)
    {
        _tabs.Clear();
        foreach (var header in LivePresentation.Tabs(
                     s, s.YourKills.Count, defeated, RaidTargetCatalog.Default.BossCount))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                Repaint();
            });
        }
        // Chips first, THEN the selection — colouring before rebuilding leaves every fresh
        // chip unstyled, including the selected one, which is the whole signal.
        _tabs.Select(_tab);
    }

    // ---- the Damage tab's extra blocks -----------------------------------------

    private readonly Block _petAbilities;
    private readonly Block _damageTaken;
    private readonly Block _recentFights;
    private readonly Block _areaSpells;
    private readonly Block _procs;
    private readonly Block _stances;
    private readonly Block _invocations;
    private readonly Block _healers;

    /// <summary>
    /// A heading and a list that appear and disappear TOGETHER.
    ///
    /// Both, always, which is the point: setting the state on one of a pair is trap 17's
    /// shape — a heading with no visual for its empty state renders exactly like a live one
    /// and photographs as a section that lost its contents. The pair is one object here so
    /// there is no second switch to forget.
    ///
    /// And these hide rather than showing an empty line, unlike a CARD, which always shows.
    /// David's rule is about cards — a hidden card reads as a missing feature — and a block
    /// INSIDE one is the opposite case: "Procs" over nothing reads as a proc list that
    /// broke, when the honest fact is that this character has no procs.
    /// </summary>
    private sealed class Block
    {
        private readonly TextBlock _label;
        public ItemsControl List { get; } = new();

        public Block(Panel host, string heading)
        {
            _label = CardParts.BlockLabel(heading, hidden: true);
            host.Children.Add(_label);
            host.Children.Add(List);
        }

        public void Fill(bool any, Action fill)
        {
            _label.Visibility = List.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            if (any) fill();
            else List.Items.Clear();
        }
    }

    private void RenderDamageExtras(StatsSnapshot s)
    {
        // The pet's per-ability split, which the Combat card folds by default because a pet
        // class drowning in rows is what asked for the fold (#28). Here it is a block among
        // blocks and the Pet TAB is the un-folded version, so nothing is hidden twice.
        _petAbilities.Fill(s.PetAbilities.Count > 0,
            () => BreakdownRows.FillAbilityRowsSorted(this, _petAbilities.List, s.PetAbilities,
                _damage.Sort, Math.Max(1, s.CombatSeconds), "dps", max: CardRowCap));
        _damageTaken.Fill(s.DamageByAttacker.Count > 0,
            () => BreakdownRows.FillStatRows(this, _damageTaken.List, s.DamageByAttacker,
                _damage.Sort, "hit"));
        _recentFights.Fill(s.RecentEncounters.Count > 0, () =>
        {
            // Bars compare per-fight DPS against the hottest recent fight.
            var top = Math.Max(0.1, s.RecentEncounters.Max(f => f.Dps));
            var brush = BreakdownRows.BarBrush(this);
            _recentFights.List.Items.Clear();
            foreach (var f in s.RecentEncounters)
                _recentFights.List.Items.Add(BreakdownRows.Row(this, f.Name,
                    $"{f.DurationSeconds:0}s · {f.Dps:0.#} dps{(f.Outcome == "Timeout" ? " · ?" : "")}",
                    f.Dps / top, brush,
                    $"{f.DamageOut:N0} damage over {f.DurationSeconds:0}s"));
        });
        // Per cast, not per target — an AoE's whole value is what one cast produces.
        _areaSpells.Fill(s.AreaSpells.Count > 0,
            () => BreakdownRows.FillPairRows(this, _areaSpells.List, s.AreaSpells.Select(x =>
                (x.Name, $"{x.DamagePerCast:N0}/cast · ×{x.Casts} · {x.AvgTargets:0.#} targets" +
                         (x.MaxTargets > x.AvgTargets + 0.05 ? $" (best {x.MaxTargets})" : "")))));
        // Procs per combat-MINUTE (#85, Kerdude): the same denominator as DPS, so downtime
        // does not flatter the weapon.
        var combatMinutes = Math.Max(1.0 / 60, s.CombatSeconds / 60.0);
        _procs.Fill(s.Procs.Count > 0,
            () => BreakdownRows.FillPairRows(this, _procs.List, s.Procs.Select(x =>
                (x.Name, $"×{x.Count} · {x.Damage:N0} dmg · {x.Count / combatMinutes:0.#}/min"))));
        _stances.Fill(s.Stances.Count > 0,
            () => BreakdownRows.FillPairRows(this, _stances.List, s.Stances.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg · {(int)x.CombatSeconds}s · {x.Dps:0.#} dps"))));
        _invocations.Fill(s.Invocations.Count > 0,
            () => BreakdownRows.FillPairRows(this, _invocations.List, s.Invocations.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg · {(int)x.CombatSeconds}s · {x.Dps:0.#} dps"))));
    }

    private void RenderHealingExtras(StatsSnapshot s) =>
        _healers.Fill(s.HealsByHealer.Count > 0,
            () => EqCardRows.Fill(_healers.List, CombatPresentation.HealerRows(s)));

    /// <summary>Card lists cap at 30 rows, the widget's own <c>CardRowCap</c> — a long
    /// session's Combat card once built EVERY ability row ever seen and paid seconds of
    /// layout for rows below the fold. Sorting still surfaces anything.</summary>
    private const int CardRowCap = 30;

    // ---- the dump ---------------------------------------------------------------

    /// <summary>
    /// The room's facts, under <c>shellLive*</c>.
    ///
    /// **Two of these exist to be COMPARED with a v1 window that is still open**, which is
    /// the whole reason a room's dump is prefixed rather than re-keyed by hand
    /// (<see cref="ShellDumpFacts"/>): <c>shellLiveKillRows</c> against
    /// <c>CreatureWindow</c>'s <c>kills</c>, and <c>shellLiveRaidsRows</c> against
    /// <c>ProgressWindow</c>'s <c>progressRaidsRows</c>. Live is a second host for five
    /// surfaces the widget still draws, for as long as HUD subtraction stays gated — so a
    /// silent divergence between the two is exactly the thing that would otherwise reach a
    /// player, and the WPF layer has no unit tests to catch it any other way.
    ///
    /// <c>shellLiveTimers</c> is the one with no v1 counterpart, and it is here because a
    /// leaked <c>DispatcherTimer</c> shows in nothing else — not a diff, not a build, not a
    /// screenshot. It is a literal 0 rather than a count of a list, deliberately: there is
    /// no timer field to count, so the day somebody adds one this line is what has to be
    /// edited to stay true, and editing it is the moment <see cref="Release"/> gets read.
    /// </summary>
    public string DebugFacts() =>
        $"shellLiveTab={LiveSurface.KeyFor(_tab)} " +
        $"shellLiveTabs={_tabs.Count} " +
        $"shellLiveEmpty={(_empty ? 1 : 0)} " +
        $"shellLiveSession={_session.State.ToString().ToLowerInvariant()} " +
        $"shellLiveKills={_session.Kills} " +
        $"shellLiveDeaths={_session.Deaths} " +
        $"shellLiveScope={(CurrentPane()?.FightScope == true ? "fight" : "session")} " +
        $"shellLiveRows={CurrentPane()?.RowCount ?? 0} " +
        $"shellLiveKillRows={_kills.KillRowCount} " +
        $"shellLivePartyRows={_kills.PartyRowCount} " +
        $"shellLiveRaidsRows={_raids.RowCount} " +
        $"shellLiveRaidsDefeated={_raids.DefeatedCount} " +
        $"shellLiveTimelineLanes={_timeline.LaneCount} " +
        // Must be 0, always. See Release().
        "shellLiveTimers=0";

    private MeterPane? CurrentPane() => _tab switch
    {
        LiveTab.Damage => _damage,
        LiveTab.Healing => _healing,
        LiveTab.Pet => _pet,
        _ => null,
    };

    // ============================================================================
    // The meter panes
    // ============================================================================

    /// <summary>
    /// One ability meter — Damage, Healing or Pet — with the Fight/Session axis and the
    /// sort strip the breakout window has always carried.
    ///
    /// **What it shows is <see cref="LivePresentation.Meter"/>'s answer and not its own.**
    /// The same three meters ship as <see cref="BreakoutWindow"/> kinds today and will for
    /// as long as HUD subtraction stays gated, so "which rows does Fight scope mean" has
    /// two hosts asking it — trap 33's shape, where the failure is not a stale answer but
    /// two answers, each current. This pane draws; it does not decide.
    ///
    /// **Its scope and sort are room state and are NOT persisted**, which is a deliberate
    /// refusal rather than an omission. The breakout's equivalents live in
    /// <c>AppSettings.BreakoutDamageScope</c> and friends; writing those from here would
    /// make two writers of one settings key, which is trap 13's loaded gun — a save writes
    /// the WHOLE file from the snapshot loaded at startup, so the second writer's changes
    /// go back silently with nothing on screen. The Evolved shell is behind
    /// <c>EQBUDDY_SHELL</c> with no player door, so a preference it forgets on close costs
    /// nobody anything; a preference it fights the widget over would cost a bug report.
    /// </summary>
    private sealed class MeterPane
    {
        private readonly FrameworkElement _resources;
        private readonly string _kind;
        private readonly Action _repaint;
        private readonly Func<StatsSnapshot, IReadOnlyDictionary<string, string>?> _blockedBy;
        private readonly StackPanel _panel = new();
        private readonly TextBlock _title;
        private readonly TextBlock _subtext;
        private readonly TextBlock _summary;
        private readonly TextBlock _emptyText;
        private readonly ItemsControl _rows = new();
        private readonly EqSegmentedStrip _scope;
        private readonly EqSegmentedStrip _sort;
        private string _signature = "";

        public UIElement Body => _panel;

        /// <summary>Where the room hangs this meter's extra blocks. Owned by the pane so
        /// they scroll with the rows rather than being pinned somewhere the room would have
        /// to keep in sync.</summary>
        public StackPanel Extras { get; } = new();

        public bool FightScope { get; private set; }

        public StatSort Sort { get; private set; } = StatSort.Total;

        public int RowCount => _rows.Items.Count;

        public MeterPane(FrameworkElement resources, string kind,
            IReadOnlyList<SortStrip.Option> sortOptions, Action repaint,
            Func<StatsSnapshot, IReadOnlyDictionary<string, string>?> blockedBy)
        {
            _resources = resources;
            _kind = kind;
            _repaint = repaint;
            _blockedBy = blockedBy;

            _title = DesignSystem.Text(Role.TitleSection, "");
            _title.Ink("TextBrush");
            _panel.Children.Add(_title);
            _subtext = DesignSystem.Text(Role.Caption, "");
            _subtext.TextWrapping = TextWrapping.Wrap;
            _subtext.Ink("DimBrush");
            _panel.Children.Add(_subtext);

            // Both strips wrap, for trap 25's reason. The scope strip is compact — the same
            // segmented chrome the breakout's Fight/Session toggle wears, so the control
            // that means "which numbers" looks the same in both hosts.
            var scopeHost = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
            _panel.Children.Add(scopeHost);
            _scope = new EqSegmentedStrip(scopeHost, compact: true);
            _scope.Add("Fight", true, tip: "The current (or last) pull's numbers",
                onClick: () => { FightScope = true; Invalidate(); });
            _scope.Add("Session", false,
                tip: "The whole session's numbers — in-combat time, so medding does not dilute it",
                onClick: () => { FightScope = false; Invalidate(); });
            // SESSION is the default here and FIGHT is the breakout's, and the difference is
            // the surface's job rather than an inconsistency: a floating bar over the game
            // is about the pull you are in, and a room called "This sitting" is about the
            // sitting. Nothing is persisted either way, so neither default overwrites the
            // other (see the type's summary).
            _scope.Select(false);

            var sortHost = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
            _panel.Children.Add(sortHost);
            _sort = new EqSegmentedStrip(sortHost);
            foreach (var option in sortOptions)
            {
                var metric = option.Metric;
                _sort.Add(option.Label, metric, tip: option.Tip, onClick: () =>
                {
                    Sort = metric switch
                    {
                        SortStrip.Metric.Hits => StatSort.Hits,
                        SortStrip.Metric.Avg => StatSort.Avg,
                        SortStrip.Metric.Rate => StatSort.Rate,
                        _ => StatSort.Total,
                    };
                    Invalidate();
                });
            }
            _sort.Select(SortStrip.Metric.Total);

            _summary = DesignSystem.Text(Role.BodySecondary, "");
            _summary.TextWrapping = TextWrapping.Wrap;
            _summary.Margin = new Thickness(0, Tok.SpaceS, 0, Tok.SpaceXs);
            _summary.Ink("DimBrush");
            _panel.Children.Add(_summary);

            _emptyText = DesignSystem.Text(Role.Caption, "");
            _emptyText.TextWrapping = TextWrapping.Wrap;
            _emptyText.Ink("DimBrush");
            _emptyText.Visibility = Visibility.Collapsed;
            _panel.Children.Add(_emptyText);

            _panel.Children.Add(_rows);
            _panel.Children.Add(Extras);
        }

        /// <summary>A click on scope or sort repaints NOW, not on the next tick — a second
        /// is long enough to read as a click that did nothing, which is the same fix the
        /// breakout's own toggle needed. Clearing the signature is what lets the repaint
        /// through the gate below.</summary>
        private void Invalidate()
        {
            _scope.Select(FightScope);
            _signature = "";
            _repaint();
        }

        public void Render(StatsSnapshot s)
        {
            var meter = LivePresentation.Meter(_kind, s, FightScope, DateTime.Now);
            _title.Text = meter.Title;
            _subtext.Text = meter.Subtext;

            // The card's own summary block, from the same UI.Shared formatter the widget's
            // Combat and Healing cards use. Session-scope only: those lines are session
            // arithmetic (accuracy, crit rate, both DPS models), and stamping them on a
            // single pull would misstate it exactly the way the resist tallies would.
            var summary = !FightScope ? SummaryFor(s) : null;
            _summary.Text = summary ?? "";
            _summary.Visibility = summary is null ? Visibility.Collapsed : Visibility.Visible;

            var empty = meter.Empty is not null;
            _emptyText.Text = meter.Empty ?? "";
            _emptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            if (empty)
            {
                _rows.Items.Clear();
                _signature = "";
                return;
            }

            // Rebuilding ten bar rows a second is cheap and pointless between fights, and it
            // throws away whatever the pointer was over. No countdown and no age in the key
            // (trap 8).
            var sortKey = Sort.ToString();
            var signature = LivePresentation.MeterSignature(_kind, FightScope, sortKey, meter);
            if (signature == _signature) return;
            _signature = signature;

            // Resist % rides only the SESSION-scope damage rows — the tallies are
            // session-wide, and stamping them on a single fight would misstate it.
            var resists = _kind == BreakoutPresentation.Damage && !FightScope
                ? MainWindow.SpellResistLookup(s) : null;
            BreakdownRows.FillAbilityRowsSorted(_resources, _rows, meter.Rows, Sort,
                Math.Max(1, meter.Seconds), meter.RateLabel, max: CardRowCap,
                resists: resists, blockedBy: resists is null ? null : _blockedBy(s));
        }

        private string? SummaryFor(StatsSnapshot s) => _kind switch
        {
            BreakoutPresentation.Damage =>
                string.Join(Environment.NewLine, CombatPresentation.SummaryLines(s)),
            BreakoutPresentation.Healing =>
                string.Join(Environment.NewLine, CombatPresentation.HealingLines(s, null)),
            _ => null,
        };
    }

    // ============================================================================
    // The fight timeline, in a room
    // ============================================================================

    /// <summary>
    /// The fight timeline as a tab: the same <see cref="DpsGraphPanel"/> and
    /// <see cref="LanesPanel"/> <see cref="FightTimelineWindow"/> draws, over the same
    /// <see cref="TimelineViewport"/>, from the same journal slice.
    ///
    /// **What the WINDOW was doing for these panels that this pane has to do instead**
    /// (trap 46's rule, and the reason it is listed rather than left to be noticed):
    ///
    ///  * **A one-second <c>DispatcherTimer</c>.** Not carried: the shell already ticks the
    ///    visible room once a second, so a timer here would be a second cadence for one
    ///    surface. That is also what keeps <see cref="LiveRoom.Release"/> empty.
    ///  * **The PAN.** <c>LanesPanel</c> used to cast <c>Window.GetWindow(this)</c> to
    ///    <c>FightTimelineWindow</c> and call its <c>Pan</c> — which would have thrown
    ///    <c>InvalidCastException</c> here on the first left-drag. It raises
    ///    <c>Panned</c> now and each host wires its own; see that event.
    ///  * **The window's fit-to-fight resize.** Deliberately not carried: a room does not
    ///    get to resize the shell, and the lanes stretch to their cell either way.
    ///  * **The hover popup.** Replaced by a line under the graph rather than a
    ///    <c>Popup</c> — a popup is a top-level window that has to be closed, and this room
    ///    is built to hold nothing that has to be given back.
    /// </summary>
    private sealed class TimelinePane
    {
        private readonly MainWindow _main;
        private readonly Grid _grid = new();
        private readonly TimelineViewport _view = new();
        private readonly DpsGraphPanel _graph = new();
        private readonly LanesPanel _lanes = new();
        private readonly TextBlock _title;
        private readonly TextBlock _subtitle;
        private readonly TextBlock _hover;
        private string _signature = "";
        private long _sourceVersion = -1;

        public UIElement Body => _grid;

        public int LaneCount => _view.Timeline?.Lanes.Count ?? 0;

        public TimelinePane(MainWindow main)
        {
            _main = main;

            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var head = new StackPanel();
            _title = DesignSystem.Text(Role.TitleSection, "Fight timeline");
            _title.Ink("TextBrush");
            head.Children.Add(_title);
            _subtitle = DesignSystem.Text(Role.Caption, "");
            _subtitle.TextWrapping = TextWrapping.Wrap;
            _subtitle.Ink("DimBrush");
            head.Children.Add(_subtitle);
            _hover = DesignSystem.Text(Role.Metadata, "");
            _hover.Ink("AccentBrush");
            head.Children.Add(_hover);
            SetRow(head, 0);
            _grid.Children.Add(head);

            _graph.View = _view;
            SetRow(_graph, 1);
            _grid.Children.Add(_graph);

            _lanes.View = _view;
            _lanes.HoverChanged += OnHover;
            _lanes.Panned += Pan;
            // **A ScrollViewer with the panel TOP-ALIGNED inside it, because that is what
            // the panel was written against** — and this is trap 46's third instance in this
            // one pane. `LanesPanel.Refit` asks `Parent is ScrollViewer sv` for the viewport
            // height it should stretch its lanes to fill, and falls back to its OWN
            // ActualHeight when the parent is anything else: a height it set itself on the
            // previous pass, which is a feedback loop that settles at the 24-unit minimum.
            // Dropped straight into the Grid's `*` row it also CENTRED — an element with an
            // explicit Height no longer stretches — so the first shot of this tab came back
            // with four minimum-height lanes floating in the middle of a half-empty cell,
            // under a graph they had visibly come adrift from. Both are the same miss: the
            // window gave this panel a scroller and a Top alignment, and the room has to
            // give it the same two things or it is hosting a different control.
            var lanes = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _lanes,
            };
            _lanes.VerticalAlignment = VerticalAlignment.Top;
            SetRow(lanes, 2);
            _grid.Children.Add(lanes);

            _grid.MouseWheel += OnZoom;
            _grid.SizeChanged += (_, _) => { if (_view.Fit) ApplyFit(); Redraw(); };
        }

        /// <summary>Re-pull the fight; rebuild only when it moved. The version poll is what
        /// makes a quiet second free — a tick where nobody swung used to cost a full
        /// snapshot plus a journal copy just to learn nothing had changed.</summary>
        public void Render()
        {
            var version = _main.StatsVersion;
            if (version == _sourceVersion && _view.Timeline is not null) return;
            _sourceVersion = version;
            var (fight, events, pet) = _main.FightTimelineSource();
            if (fight is null || fight.Start == DateTime.MinValue)
            {
                _title.Text = "Fight timeline";
                _subtitle.Text = "no fight yet — pull something";
                _view.Timeline = null;
                _signature = "";
                Redraw();
                return;
            }

            var signature = $"{fight.Name}|{fight.Start.Ticks}|{events.Count}|{(int)fight.DurationSeconds}";
            if (signature == _signature) return;
            _signature = signature;

            _view.Timeline = TimelineBuilder.Build(events, fight.Start, fight.DurationSeconds, pet);
            _title.Text = fight.Name;
            _subtitle.Text =
                $"{FightTimelineWindow.Clock(fight.DurationSeconds)} · {_view.Timeline.EventCount:N0} events"
                + (fight.InProgress ? " · live" : "")
                + $" · peak {_view.Timeline.PeakDps:N0} dps @ {FightTimelineWindow.Clock(_view.Timeline.PeakSec)}";
            if (_view.Fit) ApplyFit();
            Redraw();
        }

        private void ApplyFit()
        {
            _view.Fit = true;
            _view.OffsetSec = 0;
            _view.PixelsPerSec = _view.Timeline is { } t
                ? Math.Max(0.5, (_lanes.ActualWidth - LanesPanel.LabelWidth) / t.DurationSeconds)
                : 1;
        }

        private void Redraw() { _graph.InvalidateVisual(); _lanes.Refit(); }

        private void Pan(double deltaPixels)
        {
            if (_view.Timeline is not { } t || _view.Fit) return;
            _view.OffsetSec = Math.Clamp(_view.OffsetSec - deltaPixels / _view.PixelsPerSec,
                0, t.DurationSeconds);
            Redraw();
        }

        private void OnZoom(object sender, MouseWheelEventArgs e)
        {
            if (_view.Timeline is not { } t) return;
            var pos = e.GetPosition(_lanes).X - LanesPanel.LabelWidth;
            if (pos < 0) return;
            var anchor = _view.OffsetSec + pos / _view.PixelsPerSec;
            var factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
            var fitPps = Math.Max(0.5, (_lanes.ActualWidth - LanesPanel.LabelWidth) / t.DurationSeconds);
            _view.PixelsPerSec = Math.Clamp(_view.PixelsPerSec * factor, fitPps, 120);
            _view.Fit = Math.Abs(_view.PixelsPerSec - fitPps) < 0.001;
            _view.OffsetSec = _view.Fit ? 0
                : Math.Clamp(anchor - pos / _view.PixelsPerSec, 0, t.DurationSeconds);
            Redraw();
            // Handled, so the room's ScrollViewer does not also scroll. It cannot here —
            // the Timeline tab disables that scroller — but a wheel that both zoomed and
            // scrolled would be the bug this line prevents if that ever changes.
            e.Handled = true;
        }

        private void OnHover(TimelineMark? mark, TimelineLane? lane, Point at)
        {
            if (mark is null || lane is null) { _hover.Text = ""; return; }
            var what = mark.Hollow ? mark.Label
                : $"{mark.Amount:N0}{(mark.Crit ? " · Critical" : "")}"
                  + (mark.Label.Length > 0 && !mark.Crit ? $" · {mark.Label}" : "");
            _hover.Text = $"{lane.Name} · {what} · {FightTimelineWindow.Clock(mark.Sec)}";
        }
    }
}
