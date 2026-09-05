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
    private (string Server, string Character) Who()
    {
        var (character, server) = _main.Identity;
        return (Server: server, Character: character);
    }

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
        var logFolder = _main.Settings.LogFolder;
        _readiness = HomeReadout.Readiness(identity,
            kind => OutputfileAutoImport.WrittenAt(logFolder, identity.Character, kind));
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
        foreach (var row in _readiness) block.Children.Add(ReadinessRowView(row));
    }

    /// <summary>
    /// One readiness row: what the dump feeds, when it last landed, and — only when it
    /// never has — the ⧉ copy of the command that produces it.
    ///
    /// **The copy button is the row's whole point in its empty state.** A surface that asks
    /// the player for an output file and hands them no way to run it is the defect David
    /// reported on 2026-08-20, and it is worse in the empty state, which is the only state a
    /// new player sees. <c>GameCommandsTests.SurfacesNeedingACommand</c> carries three rows
    /// for this file because a missing control is invisible to a diff, a build and a
    /// screenshot alike (trap 34) — and <c>shellHomeCopyCmd</c> counts them from a launched
    /// app, which is the only thing that can see the control exists.
    /// </summary>
    private FrameworkElement ReadinessRowView(ReadinessRow row)
    {
        var stack = new StackPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };

        // A GRID and never a horizontal StackPanel: a stack measures its children with
        // infinite width, so the answer would be pushed off the edge with no ellipsis and
        // the row would simply be cut (trap 14, and trap 25 with chips).
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = DesignSystem.Text(Role.Body, row.Name);
        name.TextWrapping = TextWrapping.Wrap;
        head.Children.Add(name);

        var answer = DesignSystem.Text(Role.Caption, HomeReadout.ReadinessAnswer(row));
        answer.Ink(row.State == ReadinessState.NeverScanned ? "AccentBrush" : "DimBrush");
        answer.Margin = new Thickness(Tok.SpaceM, 0, 0, 0);
        Grid.SetColumn(answer, 1);
        head.Children.Add(answer);
        stack.Children.Add(head);

        stack.Children.Add(Line(row.Feeds, Role.Metadata));

        if (row.State == ReadinessState.NeverScanned)
        {
            var copy = Theming.WireCopyCommand(Theming.Button(""), CommandFor(row.Kind));
            copy.FontSize = Tok.Spec(Role.Caption).Size;
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            copy.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
            copy.ToolTip = "Copies the command — paste it into the game's chat. The game "
                + "writes the file beside its own folders and EQBuddy reads it by itself.";
            stack.Children.Add(copy);
            _copyCommands++;
        }
        else if (row.Address.Length > 0)
        {
            // A row whose dump HAS landed is a way into the surface that uses it — through
            // the same Navigate the rail calls, never a second dispatch (trap 33 lifted into
            // navigation). Filtered by Landed in HomeReadout, so this cannot offer a room
            // that does not exist.
            stack.Children.Add(LinkLine("Open", row.Address));
        }

        return stack;
    }

    /// <summary>
    /// The command a dump needs, named as the constant rather than as a literal — the whole
    /// point of centralising them, and what
    /// <c>GameCommandsTests.NoCopySurfaceCarriesItsOwnCommandLiteral</c> forbids the other
    /// way round.
    ///
    /// The switch is HERE rather than on <c>GameCommands</c> on purpose: the must-list scan
    /// asserts that a surface which needs a command NAMES it, and a helper in UI.Shared
    /// would satisfy the compiler while making this file's three rows unverifiable.
    /// </summary>
    private static string CommandFor(OutputfileKind kind) => kind switch
    {
        OutputfileKind.Achievements => GameCommands.OutputfileAchievements,
        OutputfileKind.Factions => GameCommands.OutputfileFaction,
        _ => GameCommands.OutputfileInventory,
    };

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

    private FrameworkElement LinkLine(string label, string address)
    {
        var text = DesignSystem.Text(Role.Caption, label);
        text.Ink("AccentBrush");
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
        DesignSystem.WireClick(text, () => _navigate(address));
        return text;
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
