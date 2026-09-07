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
/// ~5:20 AM CT.
///
/// **Four blocks, locked by Bevel's door 1: Identity · Readiness · Recent session · Deep
/// links.** They answer, in order, who EQBuddy is following, what it is missing, where you
/// left off, and where to go next — which is <see cref="ShellPages.Describe"/>'s one-line
/// pitch for this room, written before the room existed and unchanged by it.
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
/// Advanced under Progress). Home names deep links to rooms; it does not show their
/// contents.
///
/// **The deep links go through the shell's own <see cref="ShellWindow.Navigate"/>**, handed
/// in rather than re-implemented, and they are built from <see cref="ShellPages.Landed"/> —
/// the same list the rail draws from. A hand-written link list would put a "Live" row in a
/// room's body that opens nothing, which is the rail's own forbidden shape (*"an affordance
/// that opens nothing is a trap"*) reappearing one level in, where the rail's guard cannot
/// see it. <c>shellHomeDeadLinks</c> is the assertion that says so from outside.
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

        // The fingerprint is what the four blocks are BUILT from, so a tick that changed
        // nothing costs one string compare instead of a torn-down visual tree. It carries no
        // countdown and no age — trap 8's rule, and the reason nothing on this surface says
        // "x ago": a value that ticks makes every tick a rebuild, which is the same defect
        // as no gate at all.
        var key = string.Join('|',
            identity.Character, identity.Server, s.CurrentZone,
            _session.State, _session.EndedLocal?.Ticks ?? 0, _session.Zone,
            _session.Elapsed.Ticks, _session.XpPercent, _session.Copper, _session.LootCount,
            string.Join(',', _readiness.Select(r => $"{r.Kind}{r.State}{r.ScannedAt?.Ticks ?? 0}")),
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

        // **The whole-room empty, and the only state that gets one.** With no character
        // there is nothing for any of the four blocks to be about, and four separate "we do
        // not know yet" panels would be four ways of saying the one thing that matters. This
        // is also the first screen a brand-new player's Evolved shell ever draws, so it is
        // the one place the room hands over the whole answer at once: what is missing, what
        // to do, where, and what happens next.
        _empty = HomeReadout.Identity(identity) == IdentityState.NoCharacter;
        if (_empty)
        {
            // **INSIDE the scroller here, where the other five rooms make it a sibling of
            // their page, and the difference is content rather than centring.** Home has
            // no tab strip to collapse — the four blocks ARE the room — so the scroller is
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
        BuildLinks();
    }

    // ---- the four blocks --------------------------------------------------------

    private void BuildIdentity((string Server, string Character) identity, StatsSnapshot s)
    {
        var block = Block("Character");
        var name = DesignSystem.Text(Role.Metric, HomeReadout.IdentityHeadline(identity));
        name.Ink("AccentBrush");
        block.Children.Add(name);
        block.Children.Add(Line(HomeReadout.IdentityDetail(identity, s.CurrentZone), Role.Body));
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
            var (view, copies) = ReadinessRows.Row(row, _navigate);
            _copyCommands += copies;
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

    private void BuildLinks()
    {
        var links = HomeReadout.Links();
        var block = Block("Go to");
        if (links.Count == 0)
        {
            block.Children.Add(Line(HomeReadout.EmptyLinks, Role.BodySecondary));
            return;
        }
        foreach (var link in links)
        {
            _links++;
            if (!ShellPages.Landed.Contains(link.Page)) _deadLinks++;

            var row = new StackPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
            var label = DesignSystem.Text(Role.Body, link.Label);
            label.Ink("AccentBrush");
            row.Children.Add(label);
            var detail = DesignSystem.Text(Role.Metadata, link.Detail);
            detail.TextWrapping = TextWrapping.Wrap;
            detail.Ink("DimBrush");
            row.Children.Add(detail);
            DesignSystem.WireClick(row, () => _navigate(link.Address));
            block.Children.Add(row);
        }
    }

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
    /// <c>shellHomeDeadLinks</c> (a link into a room that has not landed — the forbidden
    /// affordance, asserted from outside) and <c>shellHomeCopyCmd</c> (a control that is
    /// ABSENT photographs as an unremarkable panel, trap 29, so only a launched app can say
    /// the ⧉ copies are there).
    /// </summary>
    public string DebugFacts() =>
        $"shellHomeEmpty={(_empty ? 1 : 0)} " +
        $"shellHomeBlocks={(_empty ? 0 : 4)} " +
        $"shellHomeIdentity={(HomeReadout.Identity(Who()) == IdentityState.Following ? 1 : 0)} " +
        $"shellHomeSession={_session.State.ToString().ToLowerInvariant()} " +
        $"shellHomeReadiness={_readiness.Count} " +
        $"shellHomeReadinessWaiting={_readiness.Count(r => r.State == ReadinessState.NeverScanned)} " +
        $"shellHomeCopyCmd={_copyCommands} " +
        $"shellHomeLinks={_links} " +
        // Must be 0, always. See BuildLinks.
        $"shellHomeDeadLinks={_deadLinks}";
}
