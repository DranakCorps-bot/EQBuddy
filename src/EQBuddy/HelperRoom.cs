using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **THE HELPER ROOM — "what should I do next?"** (Founder ask DRA-70; PRD §12
/// HOME-001..006; Fable's plan 2026-09-12, Helm-signed, delivery 1).
///
/// <para>Two blocks: the goals you pick, and the answers weighed against them. Everything
/// under the second one is read from this character's own log, this character's own dumps,
/// and catalogs EQBuddy ships — there is no comparison with anybody else and no number that
/// came off somebody else's screen.</para>
///
/// <para><b>WHY IT IS ITS OWN ROOM AND NOT A BLOCK INSIDE CHARACTER.</b> The obvious place
/// for "what next" is the room the shell opens on, and the obvious place is refused by two
/// standing locks that room carries. <c>HomeRoom</c>'s own summary says
/// <i>"THE HOME/LIVE BOUNDARY IS THE THING TO BREAK LAST"</i> — no combat numbers, no
/// previews of Raids or Faction — and <c>HomeReadout</c> carries a tombstone against
/// rebuilding the "Go to" link block the Founder cut in DRA-63. A recommender draws exactly
/// that content: rate arithmetic, faction lines, and a door on every row. Growing Character
/// would mean amending both locks three days after the Founder's smoke settled that room's
/// shape, so the Helper takes the rail slot directly beneath it instead, and the 2026-09-06
/// lock — <i>"Home stays the guidance hub … not a second metrics dashboard"</i> — is honored
/// by the hub getting its own row one line down. The alternative was named for Helm's veto in
/// the plan and Helm KEPT the room.</para>
///
/// <para><b>THE CHIPS ARE THE FOUNDER'S NINE, VERBATIM</b>, and five of them say they are not
/// answered yet. That is deliberate rather than unfinished: the list is what he wrote down,
/// a chip that vanished until its engine landed would make the feature look smaller than the
/// plan it is executing, and each deferred chip hands over the door to the room that answers
/// its question TODAY. A chip that produced one apologetic sentence and pointed nowhere would
/// be the rail's own forbidden shape — <i>"an affordance that opens nothing is a trap"</i> —
/// reappearing one level in, where the rail's guard cannot see it.</para>
///
/// <para><b>NO WORD ON THIS SURFACE IS WRITTEN HERE.</b> Every sentence comes from
/// <see cref="HelperPresentation"/>, which is where HOME-006's vocabulary ban can be swept:
/// nothing the Helper says may claim a camp is safe, easy or survivable, and a rule about
/// words can only be guarded where the words are. This file decides layout and counts
/// controls, which is the half a launched app can prove and a source scan cannot (trap 29).</para>
///
/// <para><b>Trap 72 is the failure this room is most likely to have shipped.</b> The Quests
/// tab spent a session drawing the moment before because its repaint signature carried every
/// store except the two the feature wrote. The Helper reads seven stores and writes two, so
/// <see cref="Render"/>'s fingerprint folds every one of them — including the chip selection
/// and the faction picks, which are the two a click changes — and a click repaints
/// immediately rather than waiting for the next tick.</para>
/// </summary>
internal sealed class HelperRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly Action<string> _navigate;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _blocks = new();

    public UIElement Body => this;

    /// <summary>
    /// How long the disk-and-database reads behind this room are trusted for.
    ///
    /// <para>Same five seconds and the same argument as <c>HomeRoom</c>: the visible room
    /// paints on the widget's one-second tick (only chrome was ever throttled — trap 46), and
    /// this room's inputs are a SQLite query over every stored session plus two dump files.
    /// Neither can change faster than a player can finish a sitting or type a command, and
    /// both are re-read immediately when something the player just did lands
    /// (<see cref="Refreshed"/>) — so the cache never stands between them and their own
    /// action.</para>
    /// </summary>
    private static readonly TimeSpan SourceCacheFor = TimeSpan.FromSeconds(5);

    /// <summary>The pooled creature list, cached and re-folded only when the live session's
    /// creatures actually move. The SAME <see cref="WikiPackPool"/> the Unlocks tab holds —
    /// <see cref="MobHistory.Pool"/> stays the one pooler, and this is a cache in front of it
    /// rather than a second fold (its own comment asks for exactly that).</summary>
    private readonly WikiPackPool _pool;

    private DateTime _readAt = DateTime.MinValue;
    private IReadOnlyList<ZoneRoll> _zones = [];
    private int _poolVersion;
    private RecommendationSet _answers = RecommendationSet.Empty;

    /// <summary>What the blocks were last built FROM. A rebuild swaps every element in the
    /// body, which throws away scroll position and whatever the pointer was over.</summary>
    private string _painted = "";

    // ---- what the dump reports, all captured during one Build ------------------------
    private int _goalChips;
    private int _factionChips;
    private int _whyLines;
    private int _personalWhy;
    private int _catalogWhy;
    private int _doors;
    private int _deadDoors;
    private int _copyCommands;
    private bool _empty;

    public HelperRoom(MainWindow main, Action<string> navigate)
    {
        _main = main;
        _navigate = navigate;
        _pool = new WikiPackPool(_main.StoredMobRows);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _blocks,
        };
        Children.Add(_scroll);
    }

    /// <summary>The Helper has no rooms inside it — goals are chips, not tabs, which is
    /// HOME-001's own wording (<i>"goals/filters rather than a permanent wall of
    /// sections"</i>). A tab strip would make nine goals nine destinations, and the whole
    /// point is that they are weighed together. An address's room half is left alone rather
    /// than snapped to something, the refusal every other room makes.</summary>
    public void SetTab(string key) { }

    /// <summary>Nothing to give back: no timer, no token, no file handle, no watcher. The
    /// pool is a plain in-memory fold and the reads are on-demand behind
    /// <see cref="SourceCacheFor"/>, which is a cost decision rather than a resource one.
    /// Empty with a reason, per the interface's own contract.</summary>
    public void Release() { }

    /// <summary>Nothing to arrange. Stacked blocks are one column that reflows; there is no
    /// list beside a detail pane to collapse, which is the only thing
    /// <see cref="ShellLayout.RoomSinglePane"/> decides. Empty with a reason rather than
    /// absent.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>
    /// A dump the player just produced landed. Re-read now rather than up to
    /// <see cref="SourceCacheFor"/> later.
    ///
    /// <para>This room asks for two commands in its empty states, so the gap between running
    /// one and seeing it answered is the whole of what those states promise. A Helper still
    /// saying "run the faction command" seconds after the game wrote the file is the
    /// "EQBuddy did nothing" reading the auto-import exists to prevent — the same reason
    /// <c>HomeRoom.Refreshed</c> and <c>GearRoom.InventoryChanged</c> exist.</para>
    /// </summary>
    public void Refreshed()
    {
        _readAt = DateTime.MinValue;
        Repaint();
    }

    private (string Server, string Character) Who() => ShellRoomIdentity.Of(_main);

    public void Render(StatsSnapshot s)
    {
        var identity = Who();
        ReadSources(identity, s);

        var goals = HelperGoalStore.Goals(_main.Settings, _main.QuestCharacterKey);
        var factions = HelperGoalStore.Factions(_main.Settings, _main.QuestCharacterKey);
        var unlocks = _main.Unlocks;

        // **THE FINGERPRINT, AND EVERY STORE THIS ROOM READS IS IN IT** (trap 72: the Quests
        // tab drew the moment before for a whole session because its signature carried
        // everything except the two lists the feature wrote). The two the player can change
        // with a click — the goal chips and the faction picks — are folded by CONTENT and not
        // by count, because a swap leaves a count unmoved. Nothing here ticks on the clock
        // (trap 8): no countdown, no age, no "x ago", so an idle room costs one string
        // compare per second and not a torn-down visual tree.
        var key = string.Join('|',
            identity.Character, identity.Server,
            string.Join(',', goals), string.Join(',', factions),
            _poolVersion,
            string.Join(',', _zones.Select(z => $"{z.Zone}:{z.Sessions}:{z.Kills}:{z.XpPercent:0.##}")),
            unlocks.HasAchievements, unlocks.Races.Count, unlocks.Classes.Count,
            unlocks.Races.Count(u => u.Complete), unlocks.Classes.Count(u => u.Complete),
            unlocks.Factions?.WrittenAt.Ticks ?? 0,
            ShellPages.Landed.Count);
        if (key == _painted) return;
        _painted = key;

        _answers = Recommendations.Rank(new HelperInputs(
            _zones, _pool.Mobs, unlocks.Factions, factions,
            unlocks.Races, unlocks.Classes, unlocks.HasAchievements,
            _main.Settings.SkyQuestChecklist, _main.Settings.SkyQuestCompleted,
            _main.QuestCatalog), goals);

        Build(goals, factions, unlocks);
    }

    /// <summary>The reads that are not free, behind one throttle and one clock so a caller
    /// cannot accidentally take one and skip the other.</summary>
    private void ReadSources((string Server, string Character) identity, StatsSnapshot s)
    {
        // The pool's own signature decides whether it re-folds, so asking it every tick is
        // one string-join — cheap, and it is what makes a kill during a live session move the
        // Helper's cadence line. The stored-session query behind it runs once per window.
        if (_pool.Refresh(s, identity.Character, identity.Server, _main.ActiveSessionRowId))
            _poolVersion++;

        if (DateTime.Now - _readAt < SourceCacheFor) return;
        _readAt = DateTime.Now;
        // ONE producer for the zone rollup (the plan's D7): `ZoneHistory.Fold` over the
        // session rows this character already has, joined to the pool above. Nothing here
        // re-pools creatures and nothing here mines dings — `MobHistory.Pool` and
        // `ProgressSeries` stay the only ones that do.
        _zones = ZoneHistory.Fold(_main.StoredSessions(), _pool.Mobs);
    }

    // ---- the body -------------------------------------------------------------------

    private void Build(
        IReadOnlyList<HelperGoal> goals, IReadOnlyList<string> factions, UnlockSource unlocks)
    {
        _blocks.Children.Clear();
        _goalChips = 0;
        _factionChips = 0;
        _whyLines = 0;
        _personalWhy = 0;
        _catalogWhy = 0;
        _doors = 0;
        _deadDoors = 0;
        _copyCommands = 0;

        _scroll.Content = _blocks;
        _blocks.Margin = new Thickness(Tok.SpaceL);
        // The same measured column Home uses, and left rather than stretched: WPF centres a
        // MaxWidth child in the slack it did not use, and a column that drifts toward the
        // middle as the window widens is a defect with better manners.
        _blocks.MaxWidth = ShellLayoutPolicy.MinRoomWidth;
        _blocks.HorizontalAlignment = HorizontalAlignment.Left;

        BuildGoals(goals);
        if (goals.Count == 0 || goals.Contains(HelperGoal.WorkOnFaction))
            BuildFactionPicker(factions, unlocks);
        BuildAnswers();
    }

    private void BuildGoals(IReadOnlyList<HelperGoal> goals)
    {
        var block = Block(HelperPresentation.GoalsHeading);
        block.Children.Add(Line(HelperPresentation.GoalStripNote, Role.BodySecondary));

        // A WrapPanel, never a horizontal StackPanel: nine chips at the room's floor width is
        // the canonical trap-25 strip, and a stack measures with infinite width in its
        // stacking direction, so the tail would be cut with no ellipsis and no error.
        var wrap = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
        foreach (var goal in Recommendations.All)
        {
            var chip = new EqChip(
                HelperPresentation.GoalLabel(goal), goal,
                tip: HelperPresentation.GoalTip(goal),
                onClick: () => ToggleGoal(goal));
            chip.SetSelected(goals.Contains(goal));
            wrap.Children.Add(chip);
            _goalChips++;
        }
        block.Children.Add(wrap);
    }

    private void ToggleGoal(HelperGoal goal)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        HelperGoalStore.Toggle(_main.Settings, _main.QuestCharacterKey, goal);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// The faction sub-picker, drawn only while its chip is selected (or while nothing is —
    /// which means "weigh everything").
    ///
    /// <para>A picker rather than "all of them", because a faction dump carries hundreds of
    /// standings and weighing all of them is the thirty weak answers HOME-002 asks for the
    /// opposite of. The list is capped and the cap says so, with a door to the room that has
    /// every one of them (trap 50).</para>
    /// </summary>
    private void BuildFactionPicker(IReadOnlyList<string> picked, UnlockSource unlocks)
    {
        var block = Block(HelperPresentation.GoalLabel(HelperGoal.WorkOnFaction));

        if (unlocks.Factions is not { } dump || dump.Standings.Count == 0)
        {
            block.Children.Add(Line(HelperPresentation.FactionPickerNoDump, Role.BodySecondary));
            block.Children.Add(CopyCommand(GameCommands.OutputfileFaction,
                HelperPresentation.Gap(
                    new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NoFactionDump))));
            return;
        }

        block.Children.Add(Line(HelperPresentation.FactionPickerNote, Role.BodySecondary));

        // Closest to the top first, so the pick a player is most likely to want is the one
        // they see; a maxed standing sorts last because it is a finished job.
        var offered = dump.Standings
            .OrderBy(f => f.Maxed)
            .ThenBy(f => f.PointsToMax)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shown = offered.Take(HelperPresentation.FactionPickerCap).ToList();
        // A standing the player already picked is always offered, wherever the cap left it —
        // a picker that could show a tick and not the row it belongs to would be a selection
        // the player cannot undo.
        foreach (var name in picked)
            if (!shown.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                && offered.FirstOrDefault(f =>
                    f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } extra)
                shown.Add(extra);

        var wrap = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
        foreach (var standing in shown)
        {
            var name = standing.Name;
            var chip = new EqChip(
                HelperPresentation.FactionChip(standing), name,
                onClick: () => ToggleFaction(name));
            chip.SetSelected(picked.Contains(name, StringComparer.OrdinalIgnoreCase));
            wrap.Children.Add(chip);
            _factionChips++;
        }
        block.Children.Add(wrap);

        if (HelperPresentation.FactionPickerCapNote(offered.Count - shown.Count)
            is { Length: > 0 } cap)
        {
            block.Children.Add(Line(cap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.FactionStandings, "")));
        }
    }

    private void ToggleFaction(string faction)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        HelperGoalStore.ToggleFaction(_main.Settings, _main.QuestCharacterKey, faction);
        _main.Settings.Save();
        Repaint();
    }

    private void BuildAnswers()
    {
        var block = Block(HelperPresentation.AnswersHeading);

        _empty = _answers.Top.Count == 0
                 && _answers.Gaps.Count == 0
                 && _answers.NotAnsweredYet.Count == 0;
        if (_empty)
        {
            block.Children.Add(Line(HelperPresentation.Nothing.Heading, Role.Body));
            block.Children.Add(Line(HelperPresentation.Nothing.Explanation, Role.BodySecondary));
            return;
        }

        block.Children.Add(Line(HelperPresentation.SourceNote, Role.Metadata));

        foreach (var rec in _answers.Top) block.Children.Add(Answer(rec));

        // The cap, out loud when it held something back.
        if (HelperPresentation.Cap(_answers.Withheld) is { Length: > 0 } cap)
            block.Children.Add(Line(cap, Role.Caption));

        foreach (var gap in _answers.Gaps) block.Children.Add(Gap(gap));

        foreach (var goal in _answers.NotAnsweredYet)
        {
            var stack = new StackPanel { Margin = new Thickness(0, Tok.SpaceM, 0, 0) };
            stack.Children.Add(Line(HelperPresentation.NotAnsweredYet(goal), Role.BodySecondary));
            // The door to the room that answers this goal's question today — see the class
            // summary for why a deferred chip is not allowed to point nowhere.
            if (HelperPresentation.NotAnsweredDoor(goal) is { } kind)
                stack.Children.Add(Door(new HelperDoor(kind, "")));
            block.Children.Add(stack);
        }
    }

    /// <summary>One recommendation: its headline, which of your goals it serves, its
    /// why-lines, and its doors.</summary>
    private UIElement Answer(Recommendation rec)
    {
        var stack = new StackPanel { Margin = new Thickness(0, Tok.SpaceM, 0, 0) };

        var head = DesignSystem.Text(Role.Body, HelperPresentation.Headline(rec));
        head.FontWeight = FontWeights.SemiBold;
        head.TextWrapping = TextWrapping.Wrap;
        head.Ink("AccentBrush");
        stack.Children.Add(head);

        // Which goals it answers — the cross-domain chain (HOME-005) said out loud, so a row
        // that earned its place by serving two of them shows why without the player counting
        // the reasons underneath it.
        if (HelperPresentation.Serves(rec) is { Length: > 0 } serves)
            stack.Children.Add(Line(serves, Role.Metadata));

        foreach (var fact in rec.Why)
        {
            if (HelperPresentation.Why(fact) is not { Length: > 0 } sentence) continue;
            stack.Children.Add(Line(sentence, Role.Caption));
            _whyLines++;
            if (fact.Evidence == Evidence.Personal) _personalWhy++; else _catalogWhy++;
        }

        if (HelperPresentation.WithheldWhy(rec.WithheldWhy) is { Length: > 0 } more)
            stack.Children.Add(Line(more, Role.Metadata));

        // A WrapPanel again: a recommendation with a zone, a wiki page and a tab has three
        // doors, and three at the floor width is a strip.
        var doors = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
        foreach (var door in rec.Doors) doors.Children.Add(Door(door));
        stack.Children.Add(doors);

        return stack;
    }

    /// <summary>
    /// An answerable goal with nothing to say yet, and — where what is missing is a file the
    /// game writes — the command that writes it.
    ///
    /// <para>A surface that needs an in-game command SHIPS the command (David, 2026-08-14;
    /// restated 2026-08-20). The constant comes off <see cref="GameCommands"/> and never a
    /// literal of this file's own, which is what <c>GameCommandsTests</c> asserts in both
    /// directions.</para>
    /// </summary>
    private UIElement Gap(GoalGap gap)
    {
        var stack = new StackPanel { Margin = new Thickness(0, Tok.SpaceM, 0, 0) };
        stack.Children.Add(Line(HelperPresentation.Gap(gap), Role.BodySecondary));
        switch (gap.Reason)
        {
            case GoalGapReason.NoFactionDump:
                stack.Children.Add(CopyCommand(GameCommands.OutputfileFaction,
                    HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.FactionStandings, ""))));
                break;
            case GoalGapReason.NoAchievementsDump:
                stack.Children.Add(CopyCommand(GameCommands.OutputfileAchievements,
                    HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.Unlocks, ""))));
                break;
            case GoalGapReason.NoFactionPicked:
                // No control, and that is the decision: this gap only fires while the
                // "Work on Faction" chip is on, which is exactly when the picker is already
                // drawn in the block above. A door that scrolled the player back up to
                // something already on their screen is furniture, and a second copy of the
                // picker would be a second writer of one selection (trap 4).
                break;
        }
        return stack;
    }

    private Button CopyCommand(string command, string tip)
    {
        var copy = Theming.WireCopyCommand(Theming.Button(""), command);
        copy.FontSize = Tok.Spec(Role.Caption).Size;
        copy.HorizontalAlignment = HorizontalAlignment.Left;
        copy.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        copy.ToolTip = tip;
        _copyCommands++;
        return copy;
    }

    /// <summary>
    /// One door.
    ///
    /// <para><b>Every room door goes through the shell's own <see cref="ShellWindow.Navigate"/>,
    /// handed in rather than re-implemented, at an address filtered through
    /// <see cref="ShellPages.Landed"/></b> — the same list the rail draws from. A hand-written
    /// address would put a row in a room's body that opens nothing, which is the rail's own
    /// forbidden shape reappearing one level in where the rail's guard cannot see it;
    /// <c>helperDeadDoors</c> is what says so from outside, and it is asked of the BUILT
    /// control rather than of the recommendation's data (trap 29).</para>
    ///
    /// <para>The wiki arm opens a browser and is player-clicked: EQBuddy asks eqlwiki for
    /// nothing here, so the request policy toward the wiki is untouched.</para>
    /// </summary>
    private UIElement Door(HelperDoor door)
    {
        var link = DesignSystem.Text(Role.Caption, HelperPresentation.DoorLabel(door.Kind));
        link.Ink("AccentBrush");
        link.Margin = new Thickness(0, 0, Tok.SpaceM, 0);
        link.ToolTip = HelperPresentation.DoorTip(door);

        if (door.Kind == HelperDoorKind.WikiFaction)
        {
            var faction = door.Target;
            DesignSystem.WireClick(link, () => MainWindow.OpenWikiUrl(WikiLinks.Faction(faction)));
            _doors++;
            return link;
        }

        var address = HelperPresentation.AddressFor(door.Kind);
        if (address is null) return link;
        DesignSystem.WireClick(link, () => _navigate(address));
        _doors++;
        if (ShellPages.ParseAddress(address) is not { } parsed
            || !ShellPages.Landed.Contains(parsed.Page))
            _deadDoors++;
        return link;
    }

    // ---- furniture ------------------------------------------------------------------

    /// <summary>Repaint NOW: a click must not wait for the next tick to look like it
    /// happened. The fingerprint would catch every one of these changes anyway — this only
    /// moves the moment.</summary>
    private void Repaint()
    {
        _painted = "";
        Render(_main.CurrentSnapshot());
    }

    /// <summary>A block: its heading and a stack under it. The heading is always drawn,
    /// including over an empty block — David's rule: cards always show.</summary>
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
    /// The room's facts, under <c>helper*</c>.
    ///
    /// <para><b>Store claims and screen claims from ONE moment</b> (trap 56):
    /// <c>helperRecs</c> is what the ENGINE returned and <c>helperWhy</c> is how many
    /// sentences were actually BUILT into the visual tree, both captured by the same
    /// <see cref="Build"/>. "The engine says three" and "the screen shows three" are
    /// different claims, and a room that ranked correctly and drew nothing would satisfy only
    /// the first — which is precisely the shape trap 72 shipped on the Quests tab.</para>
    ///
    /// <para><c>helperDeadDoors</c> must be 0, always, and <c>helperCopyCmd</c> is here for
    /// trap 29: a control that is ABSENT photographs as an unremarkable panel, so only a
    /// launched app can say the ⧉ is there.</para>
    /// </summary>
    public string DebugFacts() =>
        $"helperEmpty={(_empty ? 1 : 0)} " +
        $"helperGoals={string.Join(',', HelperGoalStore.Goals(_main.Settings, _main.QuestCharacterKey))} " +
        $"helperChips={_goalChips} " +
        $"helperFactionChips={_factionChips} " +
        // What the ENGINE answered.
        $"helperRecs={_answers.Top.Count} " +
        $"helperWithheld={_answers.Withheld} " +
        $"helperGaps={_answers.Gaps.Count} " +
        $"helperNotYet={_answers.NotAnsweredYet.Count} " +
        // The zones it named, in rank order. The dump is one flat space-separated namespace,
        // so a zone's internal spaces are dropped — an E2E reads this to know WHICH place is
        // being recommended, not to typeset it.
        $"helperZones={string.Join(',', _answers.Top.Select(r => r.Zone.Replace(" ", "")))} " +
        // How many goals the top answer serves: 2 or more is the cross-domain join actually
        // having fired (HOME-005), which no other key can report.
        $"helperTopGoals={(_answers.Top.Count > 0 ? _answers.Top[0].Goals.Count : 0)} " +
        // What the SCREEN drew.
        $"helperWhy={_whyLines} " +
        $"helperPersonalWhy={_personalWhy} " +
        $"helperCatalogWhy={_catalogWhy} " +
        $"helperDoors={_doors} " +
        // Must be 0, always. See Door().
        $"helperDeadDoors={_deadDoors} " +
        $"helperCopyCmd={_copyCommands}";
}
