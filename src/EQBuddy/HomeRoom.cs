using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The HOME room — the sixth room of the Evolved shell, and the first one that is a NEW
/// surface rather than a move or a lift. Bevel's Home pre-design, Helm-signed 2026-09-05
/// ~5:20 AM CT. **A player reads "Character Setup" since DRA-66** (Founder smoke: *"nothing
/// home about it; the page only helps capture game data for the rest of EQBuddy"*) — the
/// class name, the enum member and every <c>shellHome*</c> key deliberately did not move,
/// the same discipline as the Quests room reading "Guide" (<see cref="ShellPages.Label"/>).
///
/// **Three blocks: Identity · Readiness · Recent session.** They answer, in order, who
/// EQBuddy is following, what it is missing, and where you left off — which is
/// <see cref="ShellPages.Describe"/>'s one-line pitch for this room. DRA-66 put the class
/// reading and its correction inside the Identity block: WHO you are playing includes what
/// the character is, and the one answer every class-aware surface already shares
/// (<c>ClassSourceFor</c>) is finally shown somewhere a player can argue with it.
///
/// **The fourth was "Go to", and the Founder cut it on 2026-09-11 (DRA-63 smoke).** Bevel's
/// door 1 locked four blocks when the rail had one room on it; once every room had landed it
/// was a second copy of the rail sitting under the fold in the same window. The rail owns the
/// doors. See <see cref="HomeReadout"/>'s tombstone for what the deletion cost.
///
/// **This room is now what the shell OPENS on**, which is the other half of this PR:
/// <c>ShellWindow._page</c> was <c>Progress</c> as an explicit placeholder for a room
/// nobody had built, flagged in that file's own comment as the Home PR's to fix.
///
/// **THE HOME/LIVE BOUNDARY IS THE THING TO BREAK LAST.** Home is a DESK surface — the
/// product's own table says desktop is *"before and after play"* — and Live is the sitting
/// you are in. So Home carries **no combat numbers at all**: no DPS, no kills, no deaths,
/// no damage or healing, whether a session is running or finished. The temptation is not
/// hypothetical, it is one property access away — <c>_main.CurrentSnapshot()</c> is right
/// there in this file with all four on it, and a kill count on the "recent session" line
/// would look like a small harmless convenience rather than Live's job arriving three PRs
/// early. What stops it is not discipline: <see cref="RecentSession"/> has no such field to
/// reach for, by construction. **When Live lands it reads the same
/// <see cref="SessionSummary"/> fact** — that is why the fact is in <c>UI.Shared</c> and
/// not in this file — and adds the meters on its own surface.
///
/// **Nothing here previews Raids or Faction either.** Both are one property read away too,
/// and Bevel's door 3 already settled where they live (Raids goes to Live; Faction becomes
/// Advanced under Progress). Home does not show other rooms' contents, and since DRA-63 it
/// does not list their doors either.
///
/// **The one navigation left in this body is a readiness row's "Open"**, and it goes through
/// the shell's own <see cref="ShellWindow.Navigate"/>, handed in rather than re-implemented,
/// at an address filtered through <see cref="ShellPages.Landed"/> — the same list the rail
/// draws from. A hand-written address would put a row in a room's body that opens nothing,
/// which is the rail's own forbidden shape (*"an affordance that opens nothing is a trap"*)
/// reappearing one level in, where the rail's guard cannot see it.
/// <c>shellHomeDeadLinks</c> is the assertion that says so from outside, and it survived the
/// "Go to" block it was first written for.
/// </summary>
internal sealed class HomeRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly Action<string> _navigate;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _blocks = new();

    public UIElement Body => this;

    /// <summary>
    /// How long a cached disk-and-database read is trusted for.
    ///
    /// **Home is the first room whose facts are not arithmetic over the snapshot the widget
    /// already holds.** Readiness stats three files and the recent session runs a SQLite
    /// query, and the visible room paints on the widget's one-second tick (trap 46's rule:
    /// only the CHROME was ever throttled). Doing either of those every second would be a
    /// file system and a database walked once a second for a surface that cannot change
    /// that fast — a dump appears because the player typed a command, and a session ends
    /// when they stop playing. Both are re-read immediately on arrival and on
    /// <see cref="Refreshed"/> regardless, so the cache never stands between the player and
    /// something they just did.
    /// </summary>
    private static readonly TimeSpan SourceCacheFor = TimeSpan.FromSeconds(5);

    private DateTime _readAt = DateTime.MinValue;
    private IReadOnlyList<ReadinessRow> _readiness = [];
    private RecentSession _session =
        new(RecentSessionState.NeverPlayed, "", "", "", null, null, TimeSpan.Zero, 0, 0, 0);

    /// <summary>What the blocks were last built FROM. A rebuild swaps every element in the
    /// body, which throws away scroll position and whatever the pointer was over, so it
    /// happens when something changed and not on the tick that noticed nothing had.</summary>
    private string _painted = "";

    private int _copyCommands;
    private int _links;
    private int _deadLinks;
    private bool _empty;

    // ---- the class reading (DRA-66) ----
    // Captured once per Render so the fingerprint, the line and the dump all describe the
    // same moment (trap 56's rule about two numbers from one thing).
    private IReadOnlyList<string> _classes = [];
    private ClassSource _classSource = ClassSource.Unknown;
    private List<string> _stated = [];
    /// <summary>Whether the class chip strip is open. Survives the rebuild a tick causes —
    /// the strip's own state lives in the store, but "the player is mid-edit" would
    /// otherwise be thrown away by the first repaint under their pointer.</summary>
    private bool _editingClasses;
    private int _classChips;

    public HomeRoom(MainWindow main, Action<string> navigate)
    {
        _main = main;
        _navigate = navigate;

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _blocks,
        };
        Children.Add(_scroll);
    }

    /// <summary>Home has no rooms inside it — <c>ShellPages.Rooms(Home)</c> is empty, which
    /// is why the palette offers this room once and the rail row has no tabs under it. Four
    /// blocks on one page IS the room. An address's room half is left alone rather than
    /// snapped to something, the refusal every other room makes.</summary>
    public void SetTab(string key) { }

    /// <summary>Nothing to give back. Every fact here is read on demand and cached in a
    /// field: no timer, no token, no file handle, no watcher. (The reads themselves are
    /// throttled — see <see cref="SourceCacheFor"/> — which is a cost decision rather than
    /// a resource one.) Empty with a reason, per the interface's own contract.</summary>
    public void Release() { }

    /// <summary>Nothing to arrange. Four stacked blocks are one column that reflows; there
    /// is no list beside a detail pane to collapse, which is the only thing
    /// <see cref="ShellLayout.RoomSinglePane"/> decides. The room-level empty centres itself
    /// in whatever cell it is given at any width (<see cref="RoomEmptyState"/>). Empty with
    /// a reason rather than absent.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>Something the player just did landed — a dump was auto-imported. Re-read
    /// now rather than up to <see cref="SourceCacheFor"/> later: a readiness row that still
    /// says "Not run yet" seconds after the game wrote the file is the "EQBuddy did nothing"
    /// reading the auto-import exists to prevent, which is the same reason
    /// <c>GearRoom.InventoryChanged</c> exists.</summary>
    public void Refreshed()
    {
        _readAt = DateTime.MinValue;
        Render(_main.CurrentSnapshot());
    }

    /// <summary>
    /// Who is being followed, in the order the rest of the app agrees on.
    ///
    /// **THIS IS A DESTRUCTURE AND NOT AN ASSIGNMENT, AND IT HAS TO BE.** The two identity
    /// pairs in this codebase are spelled in OPPOSITE orders — <c>MainWindow.Identity</c> is
    /// <c>(Character, Server)</c> and <c>SessionArchiver.Identity</c> is
    /// <c>(Server, Character)</c> — and a C# tuple conversion is POSITIONAL: the element
    /// names are checked by nobody, so assigning one to the other compiles, runs, and hands
    /// every reader the two strings the wrong way round. It did exactly that here for one
    /// build. The symptom was not an exception: Home named the SERVER as the character, and
    /// the readiness block globbed <c>test_*-Inventory.txt</c> and reported three dumps as
    /// never run while one was sitting on disk — a room that renders perfectly and is
    /// entirely wrong, which no diff, build or screenshot can see. It was caught by the E2E
    /// that stages a real dump and asserts the count goes DOWN
    /// (<c>ReadinessAsksForTheDumpsThatAreMissingAndStopsAskingForTheOneThatLanded</c>);
    /// an assertion that only checked "three rows appear" would have passed forever.
    /// </summary>
    private (string Server, string Character) Who() => ShellRoomIdentity.Of(_main);

    public void Render(StatsSnapshot s)
    {
        var identity = Who();
        ReadSources(identity, s);

        // The class reading — the SAME resolution the quest window and the phone read
        // (ClassSourceFor), so this room can never name a different character than they do.
        // Read every tick rather than behind SourceCacheFor: it is dictionary copies, not
        // disk, and a cast that finally qualifies a class should not wait five seconds.
        (_classes, _classSource) = _main.ClassSourceFor(s);
        _stated = _main.QuestLedger?.StatedClassesFor(_main.QuestCharacterKey) ?? [];

        // The fingerprint is what the three blocks are BUILT from, so a tick that changed
        // nothing costs one string compare instead of a torn-down visual tree. It carries no
        // countdown and no age — trap 8's rule, and the reason nothing on this surface says
        // "x ago": a value that ticks makes every tick a rebuild, which is the same defect
        // as no gate at all. The class line's inputs are in it (trap 72: a reader of a
        // store belongs in what makes its surface redraw), and so is the editor's own
        // open/shut — a door whose click repainted nothing would read as stuck.
        var key = string.Join('|',
            identity.Character, identity.Server, s.CurrentZone,
            _session.State, _session.EndedLocal?.Ticks ?? 0, _session.Zone,
            _session.Elapsed.Ticks, _session.XpPercent, _session.Copper, _session.LootCount,
            string.Join(',', _readiness.Select(r => $"{r.Kind}{r.State}{r.ScannedAt?.Ticks ?? 0}")),
            string.Join(',', _classes), _classSource, string.Join(',', _stated),
            _editingClasses,
            ShellPages.Landed.Count);
        if (key == _painted) return;
        _painted = key;

        Build(identity, s);
    }

    /// <summary>The two reads that are not free, behind one throttle and one clock so a
    /// caller cannot accidentally take one and skip the other.</summary>
    private void ReadSources((string Server, string Character) identity, StatsSnapshot s)
    {
        if (DateTime.Now - _readAt < SourceCacheFor) return;
        _readAt = DateTime.Now;
        // ONE read for two hosts (OE-6). `ReadinessRows.Read` is what the first-run Setup
        // screen asks as well — a second host that assembled the identity and the dump
        // timestamps itself could disagree with this one about which character it was even
        // looking at, which is trap 33 with the two producers being two surfaces.
        _readiness = ReadinessRows.Read(_main);
        // Scoped by the ARCHIVER's identity inside MainWindow.StoredSessions — the strings
        // the rows were written under, which is not always the same pair the log filename
        // gives `Identity`. See LevelHistory.Stored for what a "close enough" identity costs.
        _session = SessionSummary.Of(identity, _main.StoredSessions(), s);
    }

    private void Build((string Server, string Character) identity, StatsSnapshot s)
    {
        _blocks.Children.Clear();
        _copyCommands = 0;
        _links = 0;
        _deadLinks = 0;
        _classChips = 0;

        // **The whole-room empty, and the only state that gets one.** With no character
        // there is nothing for any of the three blocks to be about, and three separate "we do
        // not know yet" panels would be three ways of saying the one thing that matters. This
        // is also the first screen a brand-new player's Evolved shell ever draws, so it is
        // the one place the room hands over the whole answer at once: what is missing, what
        // to do, where, and what happens next.
        _empty = HomeReadout.Identity(identity) == IdentityState.NoCharacter;
        if (_empty)
        {
            // **INSIDE the scroller here, where the other five rooms make it a sibling of
            // their page, and the difference is content rather than centring.** Home has
            // no tab strip to collapse — the three blocks ARE the room — so the scroller is
            // the whole page, and leaving the empty inside it keeps the explanation
            // reachable on a window too short to hold it. It still centres: a ScrollViewer
            // ARRANGES content smaller than its viewport at the viewport's size, so the
            // wrapper's VerticalAlignment.Center has real slack (measured, not assumed —
            // the same probe that found `ContentControl`'s alignment defaults are not what
            // takes a room's cell away).
            _scroll.Content = RoomEmptyState.Build(
                HomeReadout.IdentityHeadline(identity), HomeReadout.EmptyIdentity);
            return;
        }

        _scroll.Content = _blocks;
        _blocks.Margin = new Thickness(Tok.SpaceL);
        // **A MEASURE, and the first screenshot is what asked for it.** Home is one column
        // of prose and label/answer rows, and a room is as wide as the player's window: at
        // 946 units the readiness answer ("Not run yet") sat about 600 units to the right of
        // the row it belongs to, which stops reading as one row and starts reading as two
        // unrelated columns. The number is `MinRoomWidth` rather than a fresh constant on
        // purpose — it is the narrowest this content is ever drawn, already measured and
        // already signed, so the room reads the same at every width instead of getting worse
        // as the window grows. LEFT, not stretched: WPF centres a MaxWidth child in the slack
        // it did not use, and a column that drifts toward the middle as the window widens is
        // the same defect wearing better manners.
        _blocks.MaxWidth = ShellLayoutPolicy.MinRoomWidth;
        _blocks.HorizontalAlignment = HorizontalAlignment.Left;

        BuildIdentity(identity, s);
        BuildReadiness();
        BuildRecentSession();
    }

    // ---- the three blocks -------------------------------------------------------

    private void BuildIdentity((string Server, string Character) identity, StatsSnapshot s)
    {
        var block = Block("Character");
        var name = DesignSystem.Text(Role.Metric, HomeReadout.IdentityHeadline(identity));
        name.Ink("AccentBrush");
        block.Children.Add(name);
        block.Children.Add(Line(HomeReadout.IdentityDetail(identity, s.CurrentZone), Role.Body));
        BuildClassLine(block);
    }

    /// <summary>
    /// The class reading and its correction (DRA-66): what EQBuddy thinks this character
    /// is, where that came from, and the door to say otherwise. The words are all
    /// <see cref="HomeReadout"/>'s; the STORE is the ledger's per-character
    /// <c>StatedClasses</c>, which <c>CharacterClasses.Resolve</c> honours for every
    /// surface at once — this strip is a writer of that one store, never a second
    /// resolution (trap 33).
    /// </summary>
    private void BuildClassLine(StackPanel block)
    {
        _classChips = 0;
        var line = Line(HomeReadout.ClassAnswer(_classes, _classSource), Role.Body);
        line.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        block.Children.Add(line);

        // No character KEY yet (the log has named a character but the ledger has not keyed
        // one) — nothing to write a statement onto, so no door to a strip that could not
        // save. The line above still answers; the door returns with the key.
        if (_main.QuestLedger is null || _main.QuestCharacterKey.Length == 0) return;

        var door = DesignSystem.Text(Role.Caption,
            _editingClasses ? HomeReadout.EditClassesDone : HomeReadout.EditClasses);
        door.Ink("AccentBrush");
        door.HorizontalAlignment = HorizontalAlignment.Left;
        door.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
        DesignSystem.WireClick(door, () =>
        {
            _editingClasses = !_editingClasses;
            Repaint();
        });
        block.Children.Add(door);

        if (!_editingClasses) return;

        var note = Line(HomeReadout.ClassEditorNote, Role.BodySecondary);
        note.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        block.Children.Add(note);

        // A WrapPanel, never a horizontal StackPanel — sixteen chips at any width is the
        // canonical trap-25 strip.
        var wrap = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
        foreach (var cls in QuestClassFilter.Classes)
        {
            var chip = new EqChip(cls, cls, onClick: () => ToggleStated(cls));
            chip.SetSelected(_stated.Contains(cls, StringComparer.OrdinalIgnoreCase));
            wrap.Children.Add(chip);
            _classChips++;
        }
        block.Children.Add(wrap);

        if (_stated.Count > 0)
        {
            var clear = DesignSystem.Text(Role.Caption, HomeReadout.ClearStated);
            clear.Ink("AccentBrush");
            clear.HorizontalAlignment = HorizontalAlignment.Left;
            clear.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
            DesignSystem.WireClick(clear, () => WriteStated([]));
            block.Children.Add(clear);
        }
    }

    private void ToggleStated(string cls)
    {
        var stated = new List<string>(_stated);
        if (stated.RemoveAll(c => c.Equals(cls, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            // The cap is announced in the note above the chips; a fourth tick changes
            // nothing rather than silently evicting a class the player chose first.
            if (stated.Count >= CharacterClasses.Max) return;
            stated.Add(cls);
        }
        WriteStated(stated);
    }

    private void WriteStated(List<string> stated)
    {
        if (_main.QuestLedger is not { } ledger || _main.QuestCharacterKey.Length == 0) return;
        ledger.SetStatedClasses(_main.QuestCharacterKey, stated);
        Repaint();
    }

    /// <summary>Repaint NOW: a click must not wait for the next tick to look like it
    /// happened. The fingerprint would catch every one of these changes anyway — this only
    /// moves the moment.</summary>
    private void Repaint()
    {
        _painted = "";
        Render(_main.CurrentSnapshot());
    }

    private void BuildReadiness()
    {
        var block = Block(HomeReadout.ReadinessHeadline(_readiness));
        if (_readiness.Count == 0)
        {
            block.Children.Add(Line(HomeReadout.EmptyReadiness, Role.BodySecondary));
            return;
        }
        // **The row and its ⧉ are built by `ReadinessRows`, which is a SHARED builder as of
        // OE-6 — the first-run Setup screen hosts the same rows.** It moved out of this file
        // rather than being copied into that one: a hand-rolled second copy of the treatment
        // is trap 33's shape with the two producers being two hosts, and it stops agreeing
        // the day `HomeReadout.Readiness` gains a fourth row (OE-5 PR-1's spellbook row is
        // already on the board). The three `GameCommandsTests.SurfacesNeedingACommand` rows
        // moved with it, the same way they followed `MapView` and `QuestsView` before.
        // `shellHomeCopyCmd` still counts what THIS host built, because only a launched app
        // can say a control exists (trap 29) and two hosts need two counts.
        foreach (var row in _readiness)
        {
            var (view, copies, opens) = ReadinessRows.Row(row, _navigate);
            _copyCommands += copies;
            _links += opens;
            // The dead-affordance question, asked of the only navigation left in this room's
            // body (DRA-63 took the "Go to" block). An address is filtered through
            // `ShellPages.Landed` in `HomeReadout.Readiness`; this counts the ones that got
            // past that filter and STILL do not name a landed room, which is what a
            // hand-written address, a renamed page key or a room leaving `Landed` would each
            // look like. It is asked of the BUILT link (`opens`), not of the row's data —
            // trap 29: a control that is absent photographs as an unremarkable panel.
            if (opens > 0
                && (ShellPages.ParseAddress(row.Address) is not { } parsed
                    || !ShellPages.Landed.Contains(parsed.Page)))
                _deadLinks++;
            block.Children.Add(view);
        }
    }

    private void BuildRecentSession()
    {
        var block = Block("Recent session");
        var head = DesignSystem.Text(Role.Body, SessionSummary.Headline(_session));
        head.TextWrapping = TextWrapping.Wrap;
        block.Children.Add(head);
        block.Children.Add(Line(SessionSummary.Detail(_session), Role.BodySecondary));
    }

    // **THERE IS NO "Go to" BLOCK, and that is DRA-63's ask 2** (Founder smoke 2026-09-11:
    // *"drop Go to Live/Progress/Gear/Quests/World — sidebar already owns those doors"*). It
    // was written when the rail had one room on it; by the time all seven had landed it was
    // a second copy of the rail, under the fold, in the same window, needing to be taught
    // the same thing twice. `HomeReadout.Links` went with it — see the tombstone in that
    // file for what the deletion cost and why `shellHomeDeadLinks` did NOT go with it.

    // ---- furniture --------------------------------------------------------------

    /// <summary>A block: its heading and a stack under it, appended to the body. The
    /// heading is always drawn, including over an empty block — a heading over "nothing
    /// yet" is an answer, and a block that hid itself would read as a missing feature
    /// (David's rule: cards always show).</summary>
    private StackPanel Block(string heading)
    {
        _blocks.Children.Add(CardParts.BlockLabel(heading, hidden: false));
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, Tok.SpaceL) };
        _blocks.Children.Add(stack);
        return stack;
    }

    private static TextBlock Line(string text, Role role)
    {
        var block = DesignSystem.Text(role, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(role == Role.Body ? "TextBrush" : "DimBrush");
        return block;
    }

    /// <summary>
    /// The room's facts, under <c>shellHome*</c>.
    ///
    /// **Home is the first room with no v1 window to be compared against**, so there is no
    /// two-host equality assertion to write here — every other room's dump exists mostly so
    /// the shell and the window can be checked against each other. What these keys pin
    /// instead is the shape of the room itself, and two of them have real teeth:
    /// <c>shellHomeDeadLinks</c> (a row that opens a room which has not landed — the
    /// forbidden affordance, asserted from outside) and <c>shellHomeCopyCmd</c> (a control
    /// that is ABSENT photographs as an unremarkable panel, trap 29, so only a launched app
    /// can say the ⧉ copies are there).
    ///
    /// **<c>shellHomeCopyCmd</c> gained its teeth back in DRA-63.** While the ⧉ was an
    /// empty-state affordance it merely restated <c>shellHomeReadinessWaiting</c>; now the
    /// copies must equal the ROW count whatever any row's state is, so a scanned row that
    /// lost its button is a number that no longer matches and not a number that quietly
    /// agrees with the state it was derived from.
    /// </summary>
    public string DebugFacts() =>
        $"shellHomeEmpty={(_empty ? 1 : 0)} " +
        $"shellHomeBlocks={(_empty ? 0 : 3)} " +
        $"shellHomeIdentity={(HomeReadout.Identity(Who()) == IdentityState.Following ? 1 : 0)} " +
        $"shellHomeSession={_session.State.ToString().ToLowerInvariant()} " +
        $"shellHomeReadiness={_readiness.Count} " +
        $"shellHomeReadinessWaiting={_readiness.Count(r => r.State == ReadinessState.NeverScanned)} " +
        $"shellHomeCopyCmd={_copyCommands} " +
        // The readiness rows' "Open" links since DRA-63 — the only navigation this body has.
        $"shellHomeLinks={_links} " +
        // Must be 0, always. See BuildReadiness.
        $"shellHomeDeadLinks={_deadLinks} " +
        // The class reading (DRA-66): how many classes the line names, where they came
        // from, and — when the editor is open — that all sixteen chips were BUILT (trap 29:
        // an absent control photographs as an unremarkable panel).
        $"shellHomeClasses={_classes.Count} " +
        $"shellHomeClassSource={_classSource.ToString().ToLowerInvariant()} " +
        $"shellHomeStated={_stated.Count} " +
        $"shellHomeClassChips={_classChips}";
}
