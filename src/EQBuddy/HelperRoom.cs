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
/// <para><b>THE GOALS ARE THE FOUNDER'S NINE, VERBATIM</b>, and five of them say they are not
/// answered yet. That is deliberate rather than unfinished: the list is what he wrote down,
/// a goal that vanished until its engine landed would make the feature look smaller than the
/// plan it is executing, and each deferred one hands over the door to the room that answers
/// its question TODAY. A goal that produced one apologetic sentence and pointed nowhere would
/// be the rail's own forbidden shape — <i>"an affordance that opens nothing is a trap"</i> —
/// reappearing one level in, where the rail's guard cannot see it.</para>
///
/// <para><b>THEY ARE ONE DROPDOWN SINCE DRA-71 D2, NOT NINE CHIPS.</b> D1 drew them as a
/// <c>WrapPanel</c> of <see cref="EqChip"/>s, and the Founder smoked it and called the result
/// flat checkbox soup — nine pills the player has to read before they can do anything, with
/// every sub-picker a later slice adds widening the same wall. So the nine are rows inside one
/// <see cref="EqMultiPicker"/> and the faction sub-picker is a second face that exists only
/// once its goal is picked. Nothing about what the room DECIDES moved: the same nine, the same
/// per-character store, the same empty-means-all.</para>
///
/// <para><b>NO WORD ON THIS SURFACE IS WRITTEN HERE.</b> Every sentence comes from
/// <see cref="HelperPresentation"/>, which is where HOME-006's vocabulary ban can be swept:
/// nothing the Helper says may claim a camp is safe, easy or survivable, and a rule about
/// words can only be guarded where the words are. This file decides layout and counts
/// controls, which is the half a launched app can prove and a source scan cannot (trap 29).</para>
///
/// <para><b>THE UNLOCK SUB-PICKER IS THE THIRD FACE, AND ITS STORE IS SHARED</b> (DRA-71 D5).
/// The Quests window's Unlocks tab reads and writes the same <c>AppSettings.UnlockPicks</c>
/// through the same <see cref="UnlockPickStore"/> — one producer of the pick, because a
/// selection made in one room and ignored in the other is the second room reading as broken.
/// The narrowing itself happens in the ENGINE rather than here, so the phone gets it the day
/// it calls <c>Recommendations.Rank</c>.</para>
///
/// <para><b>Trap 72 is the failure this room is most likely to have shipped.</b> The Quests
/// tab spent a session drawing the moment before because its repaint signature carried every
/// store except the two the feature wrote. The Helper reads eight stores and writes three, so
/// <see cref="Render"/>'s fingerprint folds every one of them — including the goal selection,
/// the faction picks and the unlock picks, which are the three a click changes — and a click
/// repaints immediately rather than waiting for the next tick.</para>
/// </summary>
internal sealed class HelperRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly Action<string> _navigate;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _blocks = new();

    public UIElement Body => this;

    /// <summary>
    /// **Every store this room ranks over, and the phone's projection ranks over the same
    /// bundle** — <see cref="HelperSources"/>, which owns the seven reads, the three folds
    /// and the five-second throttle in front of them.
    ///
    /// <para>The reads used to live in this file. DRA-71 D9 put a second host on the same
    /// engine, and the plan's word for that slice is *parity by shared module* — so the
    /// assembly moved to UI.Shared and this room became one of its two callers rather than
    /// the place the phone would have had to be kept level with by hand (#210).</para>
    ///
    /// <para>The INSTANCE stays this room's own, which is trap 45's rule about a memo: a
    /// cache with a clock in it that two owners could invalidate is state, not a
    /// producer.</para>
    /// </summary>
    private readonly HelperSources _sources;

    /// <summary>**Where this character's profession skills stand** (DRA-71 D8) — the ledger's
    /// own rows, read every tick like the level beside it because it is a dictionary copy and
    /// a skill-up announced mid-sitting must not wait five seconds to show. The LEDGER's and
    /// never the live session's: a value that died at midnight is the thing that slice
    /// fixed.</summary>
    private IReadOnlyList<(string Skill, int Value, DateTime At)> _skills = [];
    private RecommendationSet _answers = RecommendationSet.Empty;

    /// <summary>Whether DRA-84 D2's band gate could run at all — a resolved level AND a band
    /// table. Dumped so a zero refusal count can be told from a gate that stood down.</summary>
    private bool _bandGate;
    /// <summary>The level the engine was handed this Build — captured so the disclosure line
    /// and the ranking it describes come from one moment (trap 56). It is
    /// <c>MainWindow.ResolvedLevel</c>'s answer and never a second reading of the two
    /// stores.</summary>
    private ResolvedLevel _level = ResolvedLevel.Unknown;

    /// <summary>What the blocks were last built FROM. A rebuild swaps every element in the
    /// body, which throws away scroll position and whatever the pointer was over.</summary>
    private string _painted = "";

    /// <summary>
    /// The screenshot hook for the one state this room's own controls cannot photograph.
    ///
    /// <para>A dropdown that is SHUT looks like a button, so a shot of the Helper says nothing
    /// about the nine rows behind the face — which is precisely trap 22: a surface with no
    /// fixture state cannot be reviewed, and a surface nobody can review reads as reviewed
    /// anyway. <c>EQBUDDY_HELPER_PICKER=goals</c> opens the goals picker; <c>=factions</c> and
    /// <c>=unlocks</c> open the two sub-pickers. Same family as <c>EQBUDDY_SHELL</c> and the
    /// sixteen hooks in
    /// <see cref="DebugHooks"/>, and like all of them it is unset in every shipping run — see
    /// <see cref="OpenForReview"/> for why it re-arms on each rebuild rather than firing
    /// once.</para>
    /// </summary>
    private readonly string _openPicker =
        Environment.GetEnvironmentVariable("EQBUDDY_HELPER_PICKER") ?? "";
    /// <summary>Whether the hook above actually FOUND the picker it names. Reported in the
    /// dump beside <c>helperPickerOpen</c> so a staged shot that comes back shut can say which
    /// half failed: a hook nobody read, or a popup that would not open. "The environment says
    /// goals" and "the room found a goals picker" are different claims.</summary>
    private bool _reviewHookArmed;

    // ---- what the dump reports, all captured during one Build ------------------------
    private EqMultiPicker? _goalPicker;
    private EqMultiPicker? _factionPicker;
    private EqMultiPicker? _unlockPicker;
    private EqMultiPicker? _wornPicker;
    /// <summary>DRA-71 D8's picker, and the counts that say what it and the rows under it
    /// actually drew. <see cref="_professionsKnown"/> is the one key that can tell "the log has
    /// never announced a profession skill-up" from "the ledger never stored one" — two
    /// different bugs behind one grey sentence.</summary>
    private EqMultiPicker? _professionPicker;
    private string _professionFace = "";
    private int _professionChips;
    private int _professionRows;
    private int _professionsKnown;
    private int _professionsWatched;
    private int _watchPresets;
    /// <summary>DRA-149 D4: how many transcribed merchant lines the professions block actually
    /// drew. It is counted at DRAW rather than read back off <see cref="ZoneMerchants"/>,
    /// because "the catalog has thirty for Jewelcrafting" and "the screen shows three" are
    /// different claims and the E2E assertion is about the second (trap 56).</summary>
    private int _merchantRows;
    private string _goalFace = "";
    private string _factionFace = "";
    private string _unlockFace = "";
    private string _wornFace = "";
    private int _goalChips;
    private int _factionChips;
    private int _unlockChips;
    private int _wornChips;
    /// <summary>How many segments the intent strip drew, and which one is on. Counted from
    /// the BUILT strip rather than from the enum for trap 29's reason: an absent segment
    /// photographs as an unremarkable row of two.</summary>
    private int _intentChips;
    private GearIntent _intent = GearUpgrades.DefaultIntent;
    private bool _includeQuests;
    /// <summary>Whether the include-quests pill was drawn at all. It exists only for the two
    /// answered intents, and "the room decided not to offer it" and "the room forgot" look
    /// identical in a screenshot.</summary>
    private bool _questToggle;
    private int _whyLines;
    private int _personalWhy;
    private int _catalogWhy;
    private int _doors;
    private int _deadDoors;
    private int _copyCommands;
    private bool _empty;
    /// <summary>Whether the unknown-level line drew its Character door. Counted rather than
    /// inferred from <see cref="_level"/>, because trap 29's whole point is that an absent
    /// control photographs as an unremarkable panel — "the room knows the level is unknown"
    /// and "the player has a way to fix it" are different claims.</summary>
    private bool _levelDoor;
    private bool _moneyNote;
    private bool _gearBaseNote;
    /// <summary>What this character is going after (DRA-216 D4), captured from the SAME bundle
    /// the answers were ranked from so the block and the row buttons beside it cannot disagree
    /// about what is tracked (trap 56).</summary>
    private IReadOnlyList<TrackedUpgrade> _tracked = [];
    /// <summary>How many tracked rows the block DREW, and how many Track buttons the answers
    /// carried. Counted from the built tree rather than from the store, because an absent
    /// control photographs as an unremarkable panel (trap 29) — "the store holds three goals"
    /// and "the player can see three goals" are different claims.</summary>
    private int _trackedRows;
    private int _trackButtons;

    public HelperRoom(MainWindow main, Action<string> navigate)
    {
        _main = main;
        _navigate = navigate;
        _sources = new HelperSources(new HelperSources.Reads(
            StoredMobRows: () => _main.StoredMobRows(),
            StoredSessions: () => _main.StoredSessions(),
            StoredThroughput: () => _main.StoredThroughput(),
            StoredSales: () => _main.StoredSales(),
            LatestInventory: () => _main.LatestInventory(),
            StatsFor: _main.WikiItems.StatsFor,
            ActiveSessionRowId: () => _main.ActiveSessionRowId));

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _blocks,
        };
        Children.Add(_scroll);
    }

    /// <summary>The Helper has no rooms inside it — goals are a filter, not tabs, which is
    /// HOME-001's own wording (<i>"goals/filters rather than a permanent wall of
    /// sections"</i>). A tab strip would make nine goals nine destinations, and the whole
    /// point is that they are weighed together. An address's room half is left alone rather
    /// than snapped to something, the refusal every other room makes.</summary>
    public void SetTab(string key) { }

    /// <summary>Nothing to give back: no timer, no token, no file handle, no watcher. The
    /// pool is a plain in-memory fold and the reads are on-demand behind
    /// <see cref="HelperSources.CacheFor"/>, which is a cost decision rather than a
    /// resource one.
    /// Empty with a reason, per the interface's own contract.</summary>
    public void Release() { }

    /// <summary>Nothing to arrange. Stacked blocks are one column that reflows; there is no
    /// list beside a detail pane to collapse, which is the only thing
    /// <see cref="ShellLayout.RoomSinglePane"/> decides. Empty with a reason rather than
    /// absent.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>
    /// A dump the player just produced landed. Re-read now rather than up to
    /// <see cref="HelperSources.CacheFor"/> later.
    ///
    /// <para>This room asks for two commands in its empty states, so the gap between running
    /// one and seeing it answered is the whole of what those states promise. A Helper still
    /// saying "run the faction command" seconds after the game wrote the file is the
    /// "EQBuddy did nothing" reading the auto-import exists to prevent — the same reason
    /// <c>HomeRoom.Refreshed</c> and <c>GearRoom.InventoryChanged</c> exist.</para>
    /// </summary>
    public void Refreshed()
    {
        _sources.Invalidate();
        Repaint();
    }

    private (string Server, string Character) Who() => ShellRoomIdentity.Of(_main);

    public void Render(StatsSnapshot s)
    {
        var identity = Who();
        ReadSources(identity, s);

        var unlocks = _main.Unlocks;
        // The SAME resolution the Character room draws and the unlock preview keys off
        // (MainWindow.ResolvedLevel), so the number this room ranks with is the number that
        // room shows.
        _level = _main.ResolvedLevel;

        // **ONE PASS OVER EVERY STORE THIS ROOM READS, and the phone's projection ranks over
        // the object it produces** (DRA-71 D9). Every selection below is read here rather than
        // by each consumer for trap 33's reason: two callers reading one store at slightly
        // different moments produce two current answers and whichever ran last wins. The picks
        // this room DRAWS and the inputs it RANKS with came out of the same `Gather`.
        var bundle = _sources.Gather(
            _main.Settings, _main.QuestCharacterKey, unlocks, _level, _main.QuestCatalog,
            ItemCatalog.Default,
            _main.QuestLedger?.ClassesFor(_main.QuestCharacterKey) ?? [],
            _main.CurrentSnapshot().InferredClass ?? "",
            _main.QuestLedger?.SkillsFor(_main.QuestCharacterKey) ?? []);

        var goals = bundle.Goals;
        var factions = bundle.Factions;
        var picks = bundle.UnlockPicks;
        var wornPicks = bundle.WornPicks;
        var professions = bundle.Professions;
        _intent = bundle.Inputs.GearIntent;
        // DRA-84 D2: captured from the SAME bundle the ranking used, so the dump cannot report
        // a gate state from a different moment than the answers it sits beside (trap 56).
        _bandGate = bundle.Inputs.Level.Known && bundle.Inputs.Bands is not null;
        _includeQuests = bundle.Inputs.IncludeQuests;
        _skills = bundle.Skills;
        // DRA-216 D4: off the same bundle, for the reason the gate state above is.
        _tracked = bundle.Inputs.Tracked;

        // **THE FINGERPRINT, AND EVERY STORE THIS ROOM READS IS IN IT** (trap 72: the Quests
        // tab drew the moment before for a whole session because its signature carried
        // everything except the two lists the feature wrote). Everything the player can change
        // with a click is folded by CONTENT and not by count, because a swap leaves a count
        // unmoved. Nothing here ticks on the clock (trap 8): no countdown, no age, no "x ago",
        // so an idle room costs one string compare per second and not a torn-down visual tree.
        var key = string.Join('|',
            identity.Character, identity.Server,
            string.Join(',', goals), string.Join(',', factions),
            // The THIRD store a click in this room changes, folded by CONTENT for the reason
            // the two above it are: a swap leaves a count unmoved, and the whole of D5's
            // player-visible change is what this selection does to the answers (trap 72).
            string.Join(',', picks),
            // DRA-71 D6's three clicks. The dump they are ABOUT rides the bundle's own
            // signature below.
            _intent, string.Join(',', wornPicks), _includeQuests,
            // **DRA-216 D4's store, folded by CONTENT** (trap 72: the Quests tab drew the
            // moment before for a whole session because its key carried everything except the
            // lists the feature wrote). A count could not see an untrack-and-track in one pass,
            // and the STAMP rides because it is in the drawn sentence — a goal re-tracked after
            // being dropped is the same item on a different day.
            string.Join(',', _tracked.Select(t => $"{t.Item}:{t.TrackedAt.Ticks}")),
            // **Every store the shared bundle holds, folded by CONTENT by the bundle itself**
            // (trap 72). It is one call rather than six lines here because the phone's
            // projection is fed by the identical object: two hosts keying on two different
            // subsets of one bundle is how one of them ends up drawing the moment before, and
            // the field most likely to be missed is whichever one the next slice adds.
            _sources.Signature(),
            unlocks.HasAchievements, unlocks.Races.Count, unlocks.Classes.Count,
            unlocks.Races.Count(u => u.Complete), unlocks.Classes.Count(u => u.Complete),
            unlocks.Factions?.WrittenAt.Ticks ?? 0,
            // The level is an INPUT to the ranking, so it belongs in what makes the room
            // redraw. The SOURCE rides with the number: a clear that lands back on the same
            // level still changes the sentence this room prints about where it came from.
            _level.Level, _level.Source,
            // **DRA-71 D8's two stores, folded by CONTENT** (trap 72). The picks are a click
            // in this room; the standings are written by the LOG thread while the room is on
            // screen, and a skill-up that moved a number without moving anything else in this
            // key would leave the block drawing the moment before it for the rest of the
            // session — which is the exact bug the Quests tab shipped. The watch rules ride
            // too, because the preset's own LABEL is read from them.
            string.Join(',', professions),
            string.Join(',', _skills.OrderBy(k => k.Skill, StringComparer.OrdinalIgnoreCase)
                .Select(k => $"{k.Skill}:{k.Value}:{k.At.Ticks}")),
            _main.Settings.TrackedRules.Count(r => r.Kind == WatchKind.SkillUp),
            ShellPages.Landed.Count);
        if (key == _painted) return;
        _painted = key;

        // The sweep and both D7 engines live in Core behind `Rank`, not here, so the phone
        // gets them by calling the same method with the same bundle — porting a feature TO a
        // surface is the signal its logic never went through the shared layer.
        _answers = Recommendations.Rank(bundle.Inputs, goals);

        Build(goals, factions, picks, wornPicks, professions, unlocks);
    }

    /// <summary>The reads that are not free, behind one throttle and one clock so a caller
    /// cannot accidentally take one and skip the other — <see cref="HelperSources"/>'s own
    /// job since DRA-71 D9, and the phone's projection calls the same thing.</summary>
    private void ReadSources((string Server, string Character) identity, StatsSnapshot s) =>
        _sources.Read(s, identity.Character, identity.Server);

    // ---- the body -------------------------------------------------------------------

    private void Build(
        IReadOnlyList<HelperGoal> goals, IReadOnlyList<string> factions,
        IReadOnlyList<string> picks, IReadOnlyList<string> wornPicks,
        IReadOnlyList<Tradeskill> professions, UnlockSource unlocks)
    {
        _blocks.Children.Clear();
        _goalPicker = null;
        _factionPicker = null;
        _unlockPicker = null;
        _wornPicker = null;
        _professionPicker = null;
        _professionFace = "";
        _professionChips = 0;
        _professionRows = 0;
        _professionsKnown = 0;
        _professionsWatched = 0;
        _watchPresets = 0;
        _merchantRows = 0;
        _goalFace = "";
        _factionFace = "";
        _unlockFace = "";
        _wornFace = "";
        _goalChips = 0;
        _factionChips = 0;
        _unlockChips = 0;
        _wornChips = 0;
        _intentChips = 0;
        _questToggle = false;
        _whyLines = 0;
        _personalWhy = 0;
        _catalogWhy = 0;
        _doors = 0;
        _deadDoors = 0;
        _copyCommands = 0;
        _levelDoor = false;
        _moneyNote = false;
        _gearBaseNote = false;
        _trackedRows = 0;
        _trackButtons = 0;

        _scroll.Content = _blocks;
        _blocks.Margin = new Thickness(Tok.SpaceL);
        // The same measured column Home uses, and left rather than stretched: WPF centres a
        // MaxWidth child in the slack it did not use, and a column that drifts toward the
        // middle as the window widens is a defect with better manners.
        _blocks.MaxWidth = ShellLayoutPolicy.MinRoomWidth;
        _blocks.HorizontalAlignment = HorizontalAlignment.Left;

        BuildGoals(goals);
        // DRA-71 D6, drawn on the same condition every sub-block in this room is: only while
        // its goal is picked, or while nothing is — which weighs everything.
        if (goals.Count == 0 || goals.Contains(HelperGoal.FarmGear))
        {
            BuildGearIntent(wornPicks);
            // DRA-216 D4, on the gear goal's own condition and directly under the question it
            // is the durable half of. It is the one block here with no picker in it: a tracked
            // goal is written by a ROW below, which is why it draws nothing at all when nothing
            // is tracked — a heading over an empty list would be a control that is not there.
            BuildTracked();
        }
        if (goals.Count == 0 || goals.Contains(HelperGoal.WorkOnFaction))
            BuildFactionPicker(factions, unlocks);
        if (goals.Count == 0 || goals.Contains(HelperGoal.UnlockRaces)
            || goals.Contains(HelperGoal.UnlockClasses))
            BuildUnlockPicker(goals, picks, unlocks);
        // DRA-71 D8, on the same condition every sub-block here is drawn on. It is the one
        // block whose goal has no ENGINE — the ranking PARKED on its own evidence survey — and
        // it is still drawn on the goal's own terms, because a picker that appeared only once
        // an engine existed would make a deferred goal look like a missing feature.
        if (goals.Count == 0 || goals.Contains(HelperGoal.FarmMaterials))
            BuildProfessions(professions);
        BuildAnswers();
        OpenForReview();
    }

    /// <summary>
    /// See <see cref="_openPicker"/>. A popup needs its face in a rendered tree before it can
    /// place itself, so this waits for the layout pass the Build it follows will cause.
    ///
    /// <para><b>It re-opens after EVERY rebuild, and the first version of it did not — which is
    /// why this comment exists.</b> <see cref="Build"/> throws away every control and makes new
    /// ones, so a hook that fired once opened a picker that a rebuild moments later had already
    /// replaced: the dump said shut, the shot would have photographed shut, and the hook would
    /// have looked spelled-correctly-and-wired-to-nothing. Nothing here reaches a player —
    /// <c>EQBUDDY_HELPER_PICKER</c> is unset in every shipping run — so "staged open" is a state
    /// the room holds for as long as the hook asks for it, rather than an event it fires
    /// once.</para>
    /// </summary>
    private void OpenForReview()
    {
        if (_openPicker.Length == 0) return;
        var picker = _openPicker switch
        {
            "goals" => _goalPicker,
            "factions" => _factionPicker,
            "unlocks" => _unlockPicker,
            "worn" => _wornPicker,
            "professions" => _professionPicker,
            _ => null,
        };
        if (picker is null) return;
        _reviewHookArmed = true;
        Dispatcher.BeginInvoke(picker.Open,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// The goals, as ONE dropdown face (DRA-71 D2, plan P2; Founder smoke item 1).
    ///
    /// <para>D1 drew nine <see cref="EqChip"/>s in a <c>WrapPanel</c> — the "flat checkbox
    /// soup" the Founder named when he smoked it. Nine pills is a wall the player has to read
    /// before they can do anything, and every sub-picker a later slice adds (worn items, unlock
    /// subjects, professions) would have widened that wall. So the nine become rows inside one
    /// <see cref="EqMultiPicker"/>, the face says what is picked, and the room's first line is
    /// a decision rather than an inventory.</para>
    ///
    /// <para><b>The face gets a roomier budget than the class lens, and that is a measured
    /// difference rather than an opt-out.</b> <see cref="PickerFace.MaxChars"/> is 16 because
    /// the quest window's face SHARES its row with the era, state and mode strips — #184 was
    /// that row running out. This face owns its own row in a column of
    /// <see cref="ShellLayoutPolicy.MinRoomWidth"/>, so it can hold two of the longest goal
    /// names ("Work on Faction · Farm Materials" is 32) and still counts past that.</para>
    ///
    /// <para>Empty means all of them, unchanged and still said out loud above the face —
    /// "Any goal" is the same sentence in the control's own words.</para>
    /// </summary>
    private void BuildGoals(IReadOnlyList<HelperGoal> goals)
    {
        var block = Block(HelperPresentation.GoalsHeading);
        block.Children.Add(Line(HelperPresentation.GoalStripNote, Role.BodySecondary));

        var picker = new EqMultiPicker(key => ToggleGoal((HelperGoal)key),
            tip: HelperPresentation.GoalPickerTip);
        picker.SetRows([.. Recommendations.All.Select(goal => new PickerRow(
            goal, HelperPresentation.GoalLabel(goal), goals.Contains(goal),
            HelperPresentation.GoalTip(goal)))]);
        picker.SetFace(HelperPresentation.GoalFace(goals));
        picker.Host.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(picker.Host);

        _goalChips = picker.RowCount;
        _goalFace = (string)picker.Face.Content;
        _goalPicker = picker;
    }

    private void ToggleGoal(HelperGoal goal)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        HelperGoalStore.Toggle(_main.Settings, _main.QuestCharacterKey, goal);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// **FARM GEAR ASKS THE INTENT FIRST** (DRA-71 D6, plan P8; Founder smoke items 4a/4b).
    ///
    /// <para>The Founder did not ask for a gear engine — he asked for three, and named them:
    /// upgrade what I wear, replace gear with better, farm valuable gear to sell. So the block
    /// opens with the question rather than with an answer, and the control is an
    /// <see cref="EqSegmentedStrip"/> because exactly one of them is being asked: unlike the
    /// goals above it, "upgrade what I wear" and "replace with better" ticked together would
    /// produce one merged list whose rows nobody could attribute.</para>
    ///
    /// <para><b>THE WORN PICKER BELONGS TO ONE INTENT AND THAT IS THE POINT.</b> The second
    /// face appears for "upgrade what I wear" and not for "replace with better", which is the
    /// whole observable difference between them — the first anchors on items you name, the
    /// second on every slot you have something in. It is the same one-face-per-decision rule
    /// the faction and unlock blocks keep, applied to a control that only half the strip
    /// needs.</para>
    ///
    /// <para><b>The catalog note is drawn here rather than on a tooltip.</b> This is the one
    /// surface in the app that compares the shipped catalog against a player's gear, and the
    /// Gear Locker's own "never best in slot" honesty is owed in the same place the answers
    /// are — see <see cref="HelperPresentation.GearCatalogNote"/>.</para>
    /// </summary>
    private void BuildGearIntent(IReadOnlyList<string> wornPicks)
    {
        var block = Block(HelperPresentation.GoalLabel(HelperGoal.FarmGear));
        block.Children.Add(Line(HelperPresentation.GearIntentNote, Role.BodySecondary));

        var host = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
        block.Children.Add(host);
        // A WrapPanel and not a horizontal StackPanel: three segments whose longest label is
        // "Upgrade what I wear" do not fit the floor width on one line, and a StackPanel would
        // clip the third rather than wrap it (trap 25).
        var strip = new EqSegmentedStrip(host);
        foreach (var intent in GearUpgrades.All)
            strip.Add(HelperPresentation.GearIntentLabel(intent), intent,
                tip: HelperPresentation.GearIntentTip(intent),
                onClick: () => ChooseIntent(intent));
        strip.Select(_intent);
        _intentChips = strip.Count;

        // The picker the OTHER intent does not have. See the summary.
        if (_intent == GearIntent.UpgradeWorn) BuildWornPicker(block, wornPicks);

        // The toggle belongs to the two intents that SWEEP THE CATALOG, and to no other — an
        // item a quest hands out is the same offer whichever way the sweep was anchored, but
        // "farm to sell" never reaches the catalog's items at all (DRA-71 D7), so offering it
        // there would be a control with no effect.
        if (_intent != GearIntent.FarmToSell
            && GearUpgrades.ShapeFor(_intent) == GearIntentShape.Answered) BuildQuestToggle(block);

        // **The catalog note belongs to the SWEEP and is drawn only for the intents that use
        // it.** It is about base stats and a "+N" the wiki does not state, which is nothing to
        // do with "farm to sell" — that intent's own caveat is about vendor prices and is drawn
        // under the ANSWERS, beside the lines it qualifies (see BuildAnswers). One caveat per
        // claim, where the claim is: two under every intent is how a player learns to skip
        // them.
        if (_intent != GearIntent.FarmToSell)
            block.Children.Add(Line(HelperPresentation.GearCatalogNote, Role.Caption));
    }

    /// <summary>
    /// **WHAT THIS CHARACTER IS GOING AFTER** (DRA-216 D4, S12).
    ///
    /// <para>The durable half of the Farm Gear question, and the only block in this room built
    /// from a store the ANSWERS below write. Its rows survive everything that can remove the
    /// offer they came from — a new dump, a worn pick, a catalog refresh, the band gate — which
    /// is the whole reason a tracked goal is its own object rather than a flag on a swept row
    /// (plan §3 Q3).</para>
    ///
    /// <para><b>Nothing tracked draws NOTHING</b>, not a heading with an empty state under it.
    /// Every other block here is a control the player can use on arrival; this one is a record
    /// of decisions they have already made, so before the first one there is nothing to say —
    /// and the Track buttons on the gear lines below are where the feature is discovered.</para>
    ///
    /// <para>Each row's untrack control is the same toggle the answers carry, with the same
    /// words from the same producer: two spellings of one action is how a surface ends up
    /// disagreeing with itself about what a click does (trap 4).</para>
    /// </summary>
    private void BuildTracked()
    {
        if (_tracked.Count == 0) return;

        var block = Block(HelperPresentation.TrackedHeading);
        block.Children.Add(Line(HelperPresentation.TrackedNote, Role.BodySecondary));

        foreach (var goal in _tracked)
        {
            var row = new StackPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
            row.Children.Add(Line(HelperPresentation.TrackedRow(goal), Role.Body));
            var controls = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            // Always the tracked face here, and it is read from the STORE rather than assumed
            // from the fact that this row was drawn: the two cannot disagree that way round.
            controls.Children.Add(TrackControl(goal.Item, () => Untrack(goal.Item)));
            row.Children.Add(controls);
            block.Children.Add(row);
            _trackedRows++;
        }

        // ONE door for the block rather than one per row — the room's own rule, written down
        // where `BuildAnswers` draws its captions: a door per line here would be the same Gear
        // door however many goals the player is holding, and the row's own control is the thing
        // each line exists to offer.
        block.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
    }

    /// <summary>
    /// The Track / Tracked ✓ control — <b>one control, drawn in two places, labelled from the
    /// store both times</b>.
    ///
    /// <para><b>The label is read from <c>TrackedUpgradeStore</c> rather than passed in</b>, so
    /// a caller cannot draw "Track" over something already tracked; the ACTION is passed in,
    /// because the two callers reach the store by different doors on purpose. An answer row
    /// holds a real <c>GearUpgradeFact</c> and toggles with it; a tracked row holds a goal and
    /// can only ever untrack. Neither host fabricates a fact — see
    /// <c>TrackedUpgradeStore</c>'s summary for why the way IN is a fact the engine produced
    /// and nothing else.</para>
    ///
    /// <para>It is a caption-styled link like the doors beside it, deliberately: it does not
    /// navigate, but it is the same weight of action, and a button in this stack would read as
    /// the primary thing on a row whose point is the sentence above it.</para>
    /// </summary>
    private UIElement TrackControl(string item, Action click)
    {
        var tracked = TrackedUpgradeStore.IsTracked(
            _main.Settings, _main.QuestCharacterKey, item);
        var link = DesignSystem.Text(Role.Caption, HelperPresentation.TrackLabel(tracked));
        link.Ink("AccentBrush");
        link.Margin = new Thickness(0, 0, Tok.SpaceM, 0);
        link.ToolTip = HelperPresentation.TrackTip(tracked, item);
        DesignSystem.WireClick(link, click);
        _trackButtons++;
        return link;
    }

    /// <summary>Start or stop going after an offer the engine actually made, then repaint so
    /// the block above the answers moves in the same beat as the label that was clicked. The
    /// write is the store's and the save is <see cref="AppSettings"/>'s own — the
    /// <see cref="ChooseIntent"/> idiom, which is what makes a goal survive the restart S18.3
    /// asks for.</summary>
    private void ToggleTracked(GearUpgradeFact offer)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        TrackedUpgradeStore.Toggle(
            _main.Settings, _main.QuestCharacterKey, offer, DateTime.Now);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>Stop going after one, from the block that lists them. It is the store's
    /// <c>Untrack</c> and not its <c>Toggle</c>: this control only ever exists over a goal that
    /// IS tracked, and a toggle here would be a path that could re-add one with a stamp of
    /// now.</summary>
    private void Untrack(string item)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        TrackedUpgradeStore.Untrack(_main.Settings, _main.QuestCharacterKey, item);
        _main.Settings.Save();
        Repaint();
    }

    private void ChooseIntent(GearIntent intent)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        GearIntentStore.Choose(_main.Settings, _main.QuestCharacterKey, intent);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// The worn-item picker — the third <see cref="EqMultiPicker"/> in this room, and the
    /// first one whose rows come out of a dump of the player's bags.
    ///
    /// <para>Every worn item is offered and the list is NOT capped, which is why its face is
    /// told how many there are and can say "Any worn item": a character wears about twenty
    /// things. That is the difference from the faction face beside it, whose offer IS capped
    /// and which therefore never claims "all".</para>
    /// </summary>
    private void BuildWornPicker(StackPanel block, IReadOnlyList<string> picked)
    {
        if (_sources.Worn.Count == 0)
        {
            block.Children.Add(Line(HelperPresentation.WornPickerNoDump, Role.BodySecondary));
            block.Children.Add(CopyCommand(GameCommands.OutputfileInventory,
                HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.Gear, ""))));
            return;
        }

        block.Children.Add(Line(HelperPresentation.WornPickerNote, Role.BodySecondary));

        // Slot order, which is the order a player walks their own character sheet — the same
        // list the Gear Locker groups by, so the two rooms do not disagree about where a ring
        // sits relative to a helm. A slot the order does not know sorts last rather than
        // vanishing.
        var rows = _sources.Worn
            .OrderBy(w => Array.IndexOf(GearLocker.SlotOrder, w.Slot) is var i && i >= 0
                ? i : int.MaxValue)
            .ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var picker = new EqMultiPicker(key => ToggleWorn((string)key),
            tip: HelperPresentation.WornPickerTip);
        picker.SetRows([.. rows.Select(w => new PickerRow(
            w.Name, HelperPresentation.WornRow(w),
            picked.Contains(w.Name, StringComparer.OrdinalIgnoreCase)))]);
        picker.SetFace(HelperPresentation.WornFace(
            [.. rows.Where(w => picked.Contains(w.Name, StringComparer.OrdinalIgnoreCase))
                .Select(w => w.Name).Distinct(StringComparer.OrdinalIgnoreCase)],
            rows.DistinctBy(w => w.Name, StringComparer.OrdinalIgnoreCase).Count()));
        picker.Host.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(picker.Host);

        _wornChips = picker.RowCount;
        _wornFace = (string)picker.Face.Content;
        _wornPicker = picker;
    }

    private void ToggleWorn(string item)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        GearIntentStore.ToggleWorn(_main.Settings, _main.QuestCharacterKey, item);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// The include-quests toggle — the Founder's own "± quests".
    ///
    /// <para>An <see cref="EqChip"/> and not a <c>CheckBox</c>: the selectable pill is
    /// <c>EqChip</c> and this room never hand-builds another one. A single pill whose selected
    /// state IS the setting is the same control the chip rule describes, used for one thing
    /// rather than for a strip.</para>
    /// </summary>
    private void BuildQuestToggle(StackPanel block)
    {
        var host = new WrapPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
        var chip = new EqChip(
            HelperPresentation.IncludeQuestsLabel, "quests",
            tip: HelperPresentation.IncludeQuestsTip, onClick: ToggleQuests);
        chip.SetSelected(_includeQuests);
        host.Children.Add(chip);
        block.Children.Add(host);
        _questToggle = true;
    }

    private void ToggleQuests()
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        GearIntentStore.ToggleQuests(_main.Settings, _main.QuestCharacterKey);
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
    ///
    /// <para><b>A SECONDARY <see cref="EqMultiPicker"/> since DRA-71 D2</b>, and it is drawn on
    /// exactly the condition it always was — only while its goal is picked (or while nothing is,
    /// which weighs everything). That is the plan's answer to checkbox soup in full: one face
    /// per decision, and the second face only exists once the first one has been made. The chips
    /// it used to draw were the same list without the fold.</para>
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

        var picker = new EqMultiPicker(key => ToggleFaction((string)key),
            tip: HelperPresentation.FactionPickerTip);
        picker.SetRows([.. shown.Select(standing => new PickerRow(
            standing.Name, HelperPresentation.FactionChip(standing),
            picked.Contains(standing.Name, StringComparer.OrdinalIgnoreCase)))]);
        // The offer is CAPPED, so the face is told nothing about "all of them": a player who
        // ticked every row it shows has not picked every faction they have, and "All factions"
        // would be the picker saying something the cap note directly below contradicts.
        picker.SetFace(HelperPresentation.FactionFace(
            [.. shown.Where(f => picked.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                .Select(f => f.Name)]));
        picker.Host.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(picker.Host);

        _factionChips = picker.RowCount;
        _factionFace = (string)picker.Face.Content;
        _factionPicker = picker;

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

    /// <summary>
    /// **THE UNLOCK SUB-PICKER** (DRA-71 D5, plan P11; Founder smoke item 7).
    ///
    /// <para>Same shape as the faction picker above it — a second face that exists only once
    /// its goal is picked — and the same reason: one face per decision is the answer to
    /// checkbox soup, and thirty check rows drawn unconditionally would be the wall the goals
    /// dropdown was built to remove, one block lower.</para>
    ///
    /// <para><b>The OFFER follows the goals; the STORE does not.</b> Picking only "Unlock
    /// Races" offers races, so the popup is about the decision the player just made. What the
    /// face COUNTS is narrowed to that same offer, because a face that counted a class pick
    /// while showing only races would be reporting a selection the player cannot see or undo
    /// — the rule the faction face already follows for its cap.</para>
    ///
    /// <para><b>Every subject, complete ones included.</b> The engine skips finished unlocks
    /// on its own (<c>UnlockProgress.Complete</c>), so leaving them out here would buy nothing
    /// and would silently drop a row the Quests tab — the OTHER reader of this one store —
    /// draws and lets you pick. The row says "unlocked" rather than a count.</para>
    /// </summary>
    private void BuildUnlockPicker(
        IReadOnlyList<HelperGoal> goals, IReadOnlyList<string> picks, UnlockSource unlocks)
    {
        var block = Block(HelperPresentation.UnlockPickerHeading);

        if (!unlocks.HasAchievements)
        {
            block.Children.Add(Line(UnlockPickReadout.NoDump, Role.BodySecondary));
            block.Children.Add(CopyCommand(GameCommands.OutputfileAchievements,
                HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.Unlocks, ""))));
            return;
        }

        // Which sections this player's goals put in play. Nothing picked weighs everything, so
        // it offers both — the same reading the room makes one block up.
        var wantsRaces = goals.Count == 0 || goals.Contains(HelperGoal.UnlockRaces);
        var wantsClasses = goals.Count == 0 || goals.Contains(HelperGoal.UnlockClasses);
        var offered = new List<UnlockProgress>();
        if (wantsRaces) offered.AddRange(unlocks.Races);
        if (wantsClasses) offered.AddRange(unlocks.Classes);
        if (offered.Count == 0)
        {
            block.Children.Add(Line(UnlockPickReadout.NoDump, Role.BodySecondary));
            return;
        }

        block.Children.Add(Line(UnlockPickReadout.Note, Role.BodySecondary));

        // Closest to done first, then alphabetically — the SAME ordering the engine ranks its
        // candidates by, so the row a player is most likely to want is the row they see first
        // and the picker does not disagree with the answers under it. A finished unlock sorts
        // last because it is a finished job.
        var rows = offered
            .OrderBy(u => u.Complete)
            .ThenByDescending(u => u.Score is { } s && s.Total > 0 ? s.Done / (double)s.Total : -1)
            .ThenBy(u => u.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var picker = new EqMultiPicker(key => ToggleUnlock((string)key),
            tip: UnlockPickReadout.Tip);
        picker.SetRows([.. rows.Select(u => new PickerRow(
            u.Subject, UnlockPickReadout.Row(u), UnlockPickStore.IsPicked(picks, u.Subject)))]);
        picker.SetFace(UnlockPickReadout.Face(
            [.. rows.Where(u => UnlockPickStore.IsPicked(picks, u.Subject)).Select(u => u.Subject)],
            rows.Count, HelperPresentation.FaceChars));
        picker.Host.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(picker.Host);

        _unlockChips = picker.RowCount;
        _unlockFace = (string)picker.Face.Content;
        _unlockPicker = picker;
    }

    private void ToggleUnlock(string subject)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        UnlockPickStore.Toggle(_main.Settings, _main.QuestCharacterKey, subject);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// **THE PROFESSIONS BLOCK — profession-first, and honest about the half that is missing**
    /// (DRA-71 D8, plan P13; Founder smoke item 6).
    ///
    /// <para>Three things per profession, and the plan named all three: where your skill
    /// stands, a one-click watch on its next skill-up, and the wiki page that says what it
    /// makes. Not one of them is a ranking, and the block says so out loud rather than letting
    /// the absence read as a bug. <b>Since DRA-149 D3 the ranking IS below</b>, so that sentence is
    /// no longer a park — it now says where the rows come from and what still has no page
    /// (<see cref="HelperPresentation.ProfessionsFarmNote"/>).</para>
    ///
    /// <para><b>The standings are the reason this block exists at all.</b> A skill value used
    /// to die with the session — <c>StatsSnapshot.SkillUps</c> has always known what you raised
    /// tonight and nothing has ever remembered it — so the writer and the reader land in one
    /// slice (trap 20) and the number here is the ledger's, not the live session's.</para>
    ///
    /// <para><b>The pick is a FILTER and the face may say "all"</b>, which is the difference
    /// from the faction picker directly above: this offer is the whole curated eight and
    /// nothing is capped away, so a player who ticked every row really has picked all of
    /// them.</para>
    /// </summary>
    private void BuildProfessions(IReadOnlyList<Tradeskill> picked)
    {
        var block = Block(HelperPresentation.GoalLabel(HelperGoal.FarmMaterials));
        block.Children.Add(Line(HelperPresentation.ProfessionPickerNote, Role.BodySecondary));
        // Said ONCE, over the list, rather than on every row that has nothing yet — see
        // HelperPresentation.ProfessionLearnNote for the shot that moved it here.
        block.Children.Add(Line(HelperPresentation.ProfessionLearnNote, Role.Caption));

        var standings = Tradeskills.Standings(_skills);
        var bySkill = standings.ToDictionary(st => st.Skill);

        var picker = new EqMultiPicker(key => ToggleProfession((Tradeskill)key),
            tip: HelperPresentation.ProfessionPickerTip);
        picker.SetRows([.. standings.Select(st => new PickerRow(
            st.Skill, HelperPresentation.ProfessionRow(st), picked.Contains(st.Skill)))]);
        picker.SetFace(HelperPresentation.ProfessionFace(
            [.. picked.Select(p => Tradeskills.For(p).Name)], standings.Count));
        picker.Host.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(picker.Host);

        _professionChips = picker.RowCount;
        _professionFace = (string)picker.Face.Content;
        _professionPicker = picker;

        // The listed set — every profession while nothing is picked, which is what makes the
        // control's empty state a list rather than a blank panel. In the CURATED order, which
        // is the enum's: eight fixed rows want a stable order more than a clever one, and a
        // list that re-sorted itself when a number moved would shift under the player's
        // pointer for no gain.
        foreach (var skill in TradeskillPickStore.Listed(_main.Settings, _main.QuestCharacterKey))
        {
            var standing = bySkill[skill];
            var row = new StackPanel { Margin = new Thickness(0, Tok.SpaceM, 0, 0) };
            row.Children.Add(Line(HelperPresentation.ProfessionStanding(standing), Role.Caption));
            if (standing.Known) _professionsKnown++;

            var doors = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            doors.Children.Add(WatchPreset(skill));
            doors.Children.Add(Door(new HelperDoor(
                HelperDoorKind.WikiSkill, Tradeskills.For(skill).WikiPage)));
            row.Children.Add(doors);
            BuildMerchants(row, skill);
            block.Children.Add(row);
            _professionRows++;
        }

        // A margin of its own: it is the BLOCK's caveat and not the last row's, and without one
        // it butts against that row's doors and reads as belonging to it — which is how the
        // first staged shot came back.
        var note = Line(HelperPresentation.ProfessionsFarmNote, Role.Caption);
        note.Margin = new Thickness(0, Tok.SpaceM, 0, 0);
        block.Children.Add(note);

        // The source caption for the merchant lines, under the whole block for
        // ProfessionLearnNote's reason. It is LAST because it explains rows the reader has
        // already passed, and because putting it first would head the block with a sentence
        // about a sub-list rather than about professions.
        var merchants = Line(HelperPresentation.MerchantsNote, Role.Caption);
        merchants.Margin = new Thickness(0, Tok.SpaceS, 0, 0);
        block.Children.Add(merchants);
    }

    /// <summary>
    /// **THE VENDOR HALF — where the wiki says you can BUY this profession's supplies**
    /// (DRA-149 D4, plan P5; the Founder's FAIL item 3, second half).
    ///
    /// <para>D3's rows above this block answer "what drops and where". This answers the other
    /// plan for the same evening: the shop. It is drawn on the PROFESSION's row rather than as a
    /// block of its own because that is the question it answers — a player reading the
    /// Jewelcrafting row wants Jewelcrafting's shops, and a fourteenth block between them and
    /// the farming rows would be a second place to look for one trade's answer.</para>
    ///
    /// <para><b>Every line is the page's own sentence</b>, from <see cref="ZoneMerchants"/>, and
    /// the door beside it opens the page it came from — because the map key it was lifted out of
    /// sits under a map image this room does not ship, so "where in the zone" is an answer only
    /// the page can give.</para>
    ///
    /// <para><b>Capped at <see cref="HelperPresentation.MerchantLineCap"/>, and the cap says
    /// so</b> (trap 50). A profession nothing names draws
    /// <see cref="HelperPresentation.NoMerchantsFor"/> rather than nothing, so an absent list is
    /// distinguishable from a list that has not loaded.</para>
    /// </summary>
    private void BuildMerchants(Panel row, Tradeskill skill)
    {
        var shown = HelperPresentation.MerchantsShown(ZoneMerchants.Default, skill);
        if (shown.Count == 0)
        {
            var empty = Line(HelperPresentation.NoMerchantsFor(skill), Role.Caption);
            empty.Margin = new Thickness(Tok.SpaceM, Tok.SpaceXs, 0, 0);
            row.Children.Add(empty);
            return;
        }

        foreach (var merchant in shown)
        {
            var text = Line(HelperPresentation.MerchantRow(merchant), Role.Caption);
            text.Margin = new Thickness(Tok.SpaceM, Tok.SpaceXs, 0, 0);
            row.Children.Add(text);

            var doors = new WrapPanel { Margin = new Thickness(Tok.SpaceM, 0, 0, 0) };
            doors.Children.Add(Door(new HelperDoor(HelperDoorKind.WikiZone, merchant.Zone)));
            doors.Children.Add(Door(new HelperDoor(HelperDoorKind.World, merchant.Zone)));
            row.Children.Add(doors);
            _merchantRows++;
        }

        var capped = HelperPresentation.MerchantsCapped(shown.Count, ZoneMerchants.Default.ZonesFor(skill));
        if (capped.Length == 0) return;
        var more = Line(capped, Role.Caption);
        more.Margin = new Thickness(Tok.SpaceM, Tok.SpaceXs, 0, 0);
        row.Children.Add(more);
    }

    private void ToggleProfession(Tradeskill skill)
    {
        if (_main.QuestCharacterKey.Length == 0) return;
        TradeskillPickStore.Toggle(_main.Settings, _main.QuestCharacterKey, skill);
        _main.Settings.Save();
        Repaint();
    }

    /// <summary>
    /// **THE WATCH PRESET — the one control in this room that WRITES something the player
    /// could have written themselves** (DRA-71 D8, plan P13).
    ///
    /// <para>It is a door with a side effect, and the rule that makes that the right shape is
    /// the in-game-command one: a surface that names an action ships the action (David,
    /// 2026-08-14). "Get told when this skill goes up" with no way to do it is the silent
    /// no-op wearing a sentence, and landing the player in an empty rules list to type the
    /// skill name themselves is the same defect with a walk attached.</para>
    ///
    /// <para><b>Idempotent, and the label says which state you are in.</b> A second click adds
    /// nothing and just opens the list. The state is read from the player's OWN rules every
    /// paint rather than from a flag this room set, so a rule they delete in Options takes the
    /// label back with it — one fact, one producer (trap 4).</para>
    /// </summary>
    private UIElement WatchPreset(Tradeskill skill)
    {
        var watching = ExistingWatch(skill) is not null;
        if (watching) _professionsWatched++;

        var link = DesignSystem.Text(Role.Caption, HelperPresentation.WatchPresetLabel(watching));
        link.Ink("AccentBrush");
        link.Margin = new Thickness(0, 0, Tok.SpaceM, 0);
        link.ToolTip = HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.WatchRules, ""));

        var address = HelperPresentation.AddressFor(HelperDoorKind.WatchRules);
        if (address is null) return link;
        DesignSystem.WireClick(link, () =>
        {
            AddWatch(skill);
            _navigate(address);
        });
        _doors++;
        _watchPresets++;
        if (ShellPages.ParseAddress(address) is not { } parsed
            || !ShellPages.Landed.Contains(parsed.Page))
            _deadDoors++;
        return link;
    }

    /// <summary>
    /// The player's own skill-up rule for this profession, or null.
    ///
    /// <para>Asked through <see cref="TrackedRule.Matches"/> — the rule's OWN matcher — rather
    /// than by comparing strings here, so a rule they wrote themselves and named "smithing!!"
    /// counts and is not duplicated. A second copy of a rule the player already has is the
    /// worst thing this control could do: watch rules fire alerts, and two of them fire
    /// twice.</para>
    ///
    /// <para><b>A DISABLED rule still counts.</b> It is a rule they wrote and turned off on
    /// purpose; adding a second enabled copy beside it would overrule a decision they made,
    /// and the door opens the list where they can see its switch.</para>
    /// </summary>
    private TrackedRule? ExistingWatch(Tradeskill skill) =>
        _main.Settings.TrackedRules.FirstOrDefault(r =>
            r.Kind == WatchKind.SkillUp && r.Matches(Tradeskills.For(skill).Name));

    private void AddWatch(Tradeskill skill)
    {
        if (ExistingWatch(skill) is not null) return;
        _main.Settings.TrackedRules.Add(new TrackedRule
        {
            Name = HelperPresentation.WatchRuleName(skill),
            Kind = WatchKind.SkillUp,
            // The profession's canonical name, which is the skill name the log prints. Set
            // explicitly rather than leaning on `EffectivePattern`'s name fallback: the
            // fallback is a convenience for hand-typed rules, and a rule this room wrote
            // should say what it matches in the box the player will look at.
            Pattern = Tradeskills.For(skill).Name,
            AlertBanner = true,
        });
        _main.Settings.Save();
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
        BuildLevelNote(block);

        foreach (var rec in _answers.Top) block.Children.Add(Answer(rec));

        // **The vendor-price caveat, beside the lines it is about** (DRA-71 D7, plan P9). It is
        // drawn from what was actually BUILT rather than from which goal is ticked, so it can
        // never appear over a list with no price in it and can never be missing from one that
        // has: the two money facts are the only things in the room that quote a price, and a
        // vendor's price moves with the seller's Charisma and faction.
        _moneyNote = _answers.Top.Any(r =>
            r.Why.Any(w => w is SellableDropFact or CatalogValueFact));
        if (_moneyNote) block.Children.Add(Line(HelperPresentation.MoneyPriceNote, Role.Caption));

        // **The base-vs-base caveat, once for the whole block** (DRA-149 D1, plan P1). Same
        // idiom as the money note directly above and for the same reason: it is driven by what
        // was actually BUILT — a gear row exists in this list — rather than by which goal is
        // ticked, so it cannot appear over a list with no gear row in it and cannot be missing
        // from one that has. Once here rather than on each of up to eight rows: the caveat is
        // identical every time, and a row's own words are what the player came for (trap 73).
        _gearBaseNote = _answers.Top.Any(r => r.Why.Any(w => w is GearUpgradeFact));
        if (_gearBaseNote)
            block.Children.Add(Line(HelperPresentation.GearBaseClaimNote, Role.Caption));

        // The cap, out loud when it held something back.
        if (HelperPresentation.Cap(_answers.Withheld) is { Length: > 0 } cap)
            block.Children.Add(Line(cap, Role.Caption));

        // And the gear sweep's OWN cap (DRA-71 D6), which is spent before any row exists and
        // so cannot ride one. The Gear room has the whole wishlist, which is why the door
        // under it goes there (trap 50: a surviving cap says what it withheld, and points at
        // where the rest is).
        if (HelperPresentation.GearWithheld(_answers.GearWithheld) is { Length: > 0 } gearCap)
        {
            block.Children.Add(Line(gearCap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
        }

        // **THE ERA GATE'S REFUSALS COME FIRST** (DRA-180 D2, plan P3), because the gate ran
        // first and because its sentence is the complete explanation: a place refused for its
        // era would otherwise be read against the band numbers directly below, which are not
        // the reason it is missing. Its door is the Gear room's, like the band caption's — the
        // wishlist behind both is the same one.
        if (HelperPresentation.EraRefused(
                _answers.GearEraRefusals, HelperPresentation.BandRefusedUpgrades)
            is { Length: > 0 } eraCap)
        {
            block.Children.Add(Line(eraCap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
        }

        // **AND THE BAND GATE'S REFUSALS** (DRA-84 D2, plan P2). The same shape one rule out:
        // a count spent before any row exists, said out loud with the numbers it was spent on,
        // pointing at the room that has the whole wishlist. It is drawn from what the ENGINE
        // refused rather than from which goal is ticked, so it cannot appear over a list the
        // gate never ran on.
        if (HelperPresentation.BandRefused(
                _answers.GearBandRefusals, HelperPresentation.BandRefusedUpgrades)
            is { Length: > 0 } bandCap)
        {
            block.Children.Add(Line(bandCap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
        }

        // **AND THE SAME GATE'S REFUSALS ON THE MATERIALS LIST** (DRA-149 D3, plan P4). Its own
        // sentence beside the gear one rather than summed into it: both are the same rule over
        // the same catalog, which is exactly why one merged count could explain neither — a
        // player reading about "zones EQBuddy has upgrades for" that silently also counted
        // where their gems drop cannot act on either half. Its door is the wiki's own skill
        // pages rather than the Gear room, because the list this refused is a recipe list.
        if (HelperPresentation.BandRefused(
                _answers.MaterialBandRefusals, HelperPresentation.BandRefusedMaterials)
            is { Length: > 0 } matBandCap)
            block.Children.Add(Line(matBandCap, Role.Caption));

        // The era gate's refusals on the MATERIALS list — its own sentence beside the gear one
        // for the reason the materials BAND caption is its own sentence (DRA-180 D2).
        if (HelperPresentation.EraRefused(
                _answers.MaterialEraRefusals, HelperPresentation.BandRefusedMaterials)
            is { Length: > 0 } matEraCap)
            block.Children.Add(Line(matEraCap, Role.Caption));

        // **AND THE WHO RULE'S** (DRA-84 D4, plan P3). The third count spent before a row
        // exists, and the third to get its own sentence rather than be summed into the others:
        // a cap, a band and a missing creature are three causes with three remedies, and the
        // player can act on all three only if they can tell which one happened.
        if (HelperPresentation.DropOffersWithheld(_answers.GearWhoWithheld)
            is { Length: > 0 } whoCap)
        {
            block.Children.Add(Line(whoCap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
        }

        // **AND THE QUEST-SOURCE RULE'S** (DRA-219, S10/S11). The who rule's sibling on the
        // other acquisition path, drawn directly after it because they are the same principle
        // — an offer that cannot say how to pursue it is not an offer — and its own sentence
        // rather than a merged count, because the remedies differ: one is an item page naming
        // no creature, this is the shipped quest list not holding a quest at all. Its door is
        // the quest list, which is both the thing that is missing the quest and the place a
        // player would look for it.
        if (HelperPresentation.QuestOffersWithheld(_answers.GearQuestWithheld)
            is { Length: > 0 } questCap)
        {
            block.Children.Add(Line(questCap, Role.Caption));
            block.Children.Add(Door(new HelperDoor(HelperDoorKind.QuestCatalog, "")));
        }

        // **AND THE TWO THINGS THE SWEEP NEVER PASSED ON** (DRA-219, S10.1/S19.2). Every caption
        // above is about an offer that existed and was removed; these are about items that never
        // reached a bucket. Apart from each other because only one of them has a remedy — the
        // include-quests toggle is on this screen, and "no page says where it comes from" has
        // nothing behind it at all.
        if (HelperPresentation.SourcelessUpgrades(_answers.GearNoSource)
            is { Length: > 0 } noSourceCap)
            block.Children.Add(Line(noSourceCap, Role.Caption));

        if (HelperPresentation.QuestOnlyUpgrades(_answers.GearQuestOnly)
            is { Length: > 0 } questOnlyCap)
            block.Children.Add(Line(questOnlyCap, Role.Caption));

        // The who rule's count on the MATERIALS list (DRA-149 D3). Same sentence from the same
        // producer — the rule, the cause and the remedy are identical and a re-worded copy is
        // the one that goes stale (trap 4) — and its own line, for the reason the band caption
        // above has one.
        if (HelperPresentation.DropOffersWithheld(_answers.MaterialWhoWithheld)
            is { Length: > 0 } matWhoCap)
            block.Children.Add(Line(matWhoCap, Role.Caption));

        // **AND THE ROWS THERE WAS NEVER AN ANCHOR FOR** (DRA-149 D2, plan P2). The fourth
        // sentence in this stack and the only one that is not a decision EQBuddy made: the
        // three above chose to hold something back, this one admits it never had the row. It is
        // drawn LAST of the four and directly above the gaps because that is where the
        // `NothingWornIsReadable` gap lands — the caption names the items and the gap says what
        // it cost, in that order.
        //
        // Its doors are the wiki, one per NAMED item rather than one for the block: the check a
        // player can actually make is per item (is this how eqlwiki spells it?), and a single
        // door would have to pick one of them to be about.
        if (HelperPresentation.UnreadWorn(_answers.UnreadWorn) is { Length: > 0 } unreadCap)
        {
            block.Children.Add(Line(unreadCap, Role.Caption));
            var doors = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            foreach (var item in _answers.UnreadWorn.Take(HelperPresentation.UnreadWornNamed))
                doors.Children.Add(Door(new HelperDoor(HelperDoorKind.WikiItem, item)));
            doors.Children.Add(Door(new HelperDoor(HelperDoorKind.Gear, "")));
            block.Children.Add(doors);
        }

        // **AND THEN, PER WORN ITEM, THE ANSWER THE FOUNDER ASKED FOR** (DRA-180 D3, plan P3).
        //
        // Every caption above counts PLACES and every one of them can be true while the player's
        // actual question goes unanswered — he asked about a bow and about the Baron's Blade,
        // and a sentence about Sleeper's Tomb does not name either. These lines are the same
        // evidence turned to face the character.
        //
        // They are drawn AFTER the block captions and BEFORE the gaps because they are more
        // specific than the first and less final than the second: the captions explain the list,
        // these explain one row of the player's own gear, and a gap closes the goal. A door per
        // line would be the same Gear door three times over, so the block's one door (already
        // drawn with the captions above) is left to serve them.
        var anchors = _answers.GearAnchorsRemoved;
        foreach (var anchor in anchors.Take(HelperPresentation.GearAnchorsNamed))
            block.Children.Add(Line(HelperPresentation.AnchorAllRemoved(anchor), Role.Caption));
        if (HelperPresentation.AnchorsNotNamed(
                anchors.Count - Math.Min(anchors.Count, HelperPresentation.GearAnchorsNamed))
            is { Length: > 0 } anchorCap)
            block.Children.Add(Line(anchorCap, Role.Caption));

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

    /// <summary>
    /// **THE HELPER NAMES THE LEVEL IT USED** (DRA-71 D3, plan P4).
    ///
    /// <para>A ranking that quietly weighed a number the player disagrees with — and never
    /// said which — is the shape that makes somebody distrust a whole room. So the input is
    /// disclosed on the same surface as the answers, in the same voice as the source note
    /// above it, and it names where the number came from as well as what it was.</para>
    ///
    /// <para><b>An unknown level draws a sentence and a door, never a guess</b> (the plan's
    /// own words). The sentence says what the room did ANYWAY — the answers above are ranked
    /// from the player's own stored play and are unaffected — and the door goes to the one
    /// room that can fix it. Inventing a level from an xp rate, a zone or a spell would be
    /// trap 73's shape with arithmetic instead of prose, and it would then outrank the
    /// player's own next ding.</para>
    /// </summary>
    private void BuildLevelNote(StackPanel block)
    {
        var line = Line(LevelReadout.UsedByHelper(_level), Role.Metadata);
        line.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
        block.Children.Add(line);
        if (_level.Known) return;
        block.Children.Add(Door(new HelperDoor(HelperDoorKind.Character, "")));
        _levelDoor = true;
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

            // **DRA-216 D4: the one why-line a player can act on with a click.** It rides the
            // GEAR line rather than the row, because a zone row can carry several upgrades and
            // a control under the row would not say which item it was about — and because this
            // is the only fact here that names something the player can decide to go and get.
            // Every other line is evidence about a place.
            if (fact is GearUpgradeFact offer)
                stack.Children.Add(TrackControl(offer.Item, () => ToggleTracked(offer)));
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
            case GoalGapReason.NoInventoryDump:
                // DRA-71 D6. The one gear gap with a command behind it — a surface that needs
                // an in-game command SHIPS the command, off GameCommands and never a literal.
                stack.Children.Add(CopyCommand(GameCommands.OutputfileInventory,
                    HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.Gear, ""))));
                break;
            case GoalGapReason.GearIntentNotAnsweredYet:
                // A deferred INTENT points at the room that answers its question today, the
                // same pairing a deferred GOAL keeps — an affordance that produced one
                // apologetic sentence and pointed nowhere is the rail's own forbidden shape.
                stack.Children.Add(Door(new HelperDoor(HelperDoorKind.Wealth, "")));
                break;
            case GoalGapReason.NoCatalogUpgrade:
                // Nothing to copy and nowhere new to go: the Gear room's door is already on
                // the block above, beside the control that produced this state.
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

        // **THE THREE WIKI ARMS, AND THE SECOND ONE WAS A SILENT NO-OP** (found in DRA-149 D2).
        // `AddressFor` answers null for every wiki kind — correctly, a page is not a room — and
        // the fall-through below returns the label UNWIRED, so `WikiSkill` has drawn an
        // "eqlwiki" control with a tooltip and no click since DRA-71 D8 shipped it under all
        // eight professions. `helperDeadDoors` could not see it either: that counter is only
        // reached past the `address is null` return. The must-list proves the WORDS exist
        // (`EveryDoorEitherLandsOnARoomOrOpensTheWiki`) and nothing proved the CONTROL opens —
        // trap 34 one layer down. Adding a third wiki door beside a dead one was not an option,
        // so both are wired here and the switch is exhaustive by kind rather than by fall-
        // through, which is what makes a fourth wiki kind a compile-time question.
        var url = door.Kind switch
        {
            HelperDoorKind.WikiFaction => WikiLinks.Faction(door.Target),
            // `Page`, not `Search`: a profession's page title is not an item, and the item
            // rule would fold a "+N" and consult the item alias table on the way past.
            HelperDoorKind.WikiSkill => WikiLinks.Page(door.Target),
            // The name the GAME printed, searched rather than resolved — see WikiItem's own
            // summary: this door only exists for names that matched no page.
            HelperDoorKind.WikiItem => WikiLinks.Search(door.Target),
            // DRA-149 D4, and it is the FOURTH arm the comment above predicted. `Page`, for
            // WikiSkill's reason exactly: a zone title is not an item, and putting one through
            // the item rule would fold a "+N" off it and consult the alias table on the way.
            HelperDoorKind.WikiZone => WikiLinks.Page(door.Target),
            _ => null,
        };
        if (url is { Length: > 0 })
        {
            DesignSystem.WireClick(link, () => MainWindow.OpenWikiUrl(url));
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
        // The goal ROWS the picker holds — still nine, still a trap-29 assertion: the rows
        // moved inside a popup in D2 and a room that built the face and no list would
        // photograph as a perfectly ordinary button. The key keeps its D1 name because it
        // keeps its D1 MEANING ("how many goals this room offers"); renaming it would cost the
        // E2E row that has asserted the Founder's nine since the room landed.
        $"helperChips={_goalChips} " +
        $"helperFactionChips={_factionChips} " +
        // **DRA-71 D5.** What the SHARED store holds for this character, and what the room
        // actually OFFERED and DREW from it. Three keys because they are three claims: the
        // store's, the popup's, and the face's. A pick that reached settings.json and no
        // control is exactly the state trap 20 is about, and it photographs as an ordinary
        // room (trap 29).
        $"helperUnlockPicks={string.Join(',', UnlockPickStore.Picked(_main.Settings, _main.QuestCharacterKey).Select(p => p.Replace(" ", "")))} " +
        $"helperUnlockChips={_unlockChips} " +
        $"helperUnlockFace={_unlockFace.Replace(" ", "")} " +
        // What the FACE says — the whole of D2's player-visible change in one string, and the
        // only fact that can tell a capped face from a wrong one. Spaces are dropped because
        // the dump is one flat namespace (trap 58), so "2 goals" reads as "2goals".
        $"helperGoalFace={_goalFace.Replace(" ", "")} " +
        $"helperFactionFace={_factionFace.Replace(" ", "")} " +
        // **DRA-71 D6.** The intent the STORE holds and the segments the strip actually DREW
        // — two claims, because an intent that reached settings.json and no control is trap
        // 20's shape and photographs as an ordinary row (trap 29). `helperWorn` is how many
        // anchors the sweep was handed, which is the one key that can tell "no inventory dump"
        // from "nothing in the catalog beats it": both draw one grey sentence.
        $"helperIntent={_intent.ToString().ToLowerInvariant()} " +
        $"helperIntentChips={_intentChips} " +
        $"helperWorn={_sources.Worn.Count} " +
        // DRA-149 D5. **The number that tells the two empty gear screens apart.** A sweep that
        // found nothing is what the tier rule guaranteed for every plussed character until D1;
        // a sweep that found plenty and had every zone refused is a different event with the
        // same grey sentence. `helperGearWhy` counts DRAWN rows and cannot separate them,
        // because both are 0.
        $"helperCandidates={_answers.GearCandidates} " +
        $"helperWornChips={_wornChips} " +
        $"helperWornFace={_wornFace.Replace(" ", "")} " +
        $"helperWornPicks={string.Join(',', GearIntentStore.WornPicks(_main.Settings, _main.QuestCharacterKey).Select(p => p.Replace(" ", "")))} " +
        // The toggle's state AND whether it was drawn at all — "the room decided not to offer
        // it" and "the room forgot" are different claims and an absent control photographs as
        // an unremarkable panel.
        $"helperQuestsOn={(_includeQuests ? 1 : 0)} " +
        $"helperQuestToggle={(_questToggle ? 1 : 0)} " +
        // **DRA-216 D4: what the STORE holds, what the block DREW, and how many controls the
        // room offered** — three keys because they are three claims, the `helperUnlockPicks`
        // trio's own shape one store along. A goal that reached settings.json and no row is
        // trap 20's state and photographs as an ordinary room (trap 29); a room with rows and
        // no Track button is a feature nobody can start. The items are folded by NAME, because
        // a count cannot see one goal swapped for another (trap 72).
        $"helperTracked={string.Join(',', _tracked.Select(t => t.Item.Replace(" ", "")))} " +
        $"helperTrackedRows={_trackedRows} " +
        $"helperTrackButtons={_trackButtons} " +
        // What the ENGINE found: how many drawn answers carry a catalog upgrade line, how many
        // carry an observed drop (the personal half), and what the sweep's own cap withheld.
        $"helperGearWhy={_answers.Top.Count(r => r.Why.OfType<GearUpgradeFact>().Any())} " +
        $"helperGearSeen={_answers.Top.Count(r => r.Why.OfType<GearDropSeenFact>().Any())} " +
        $"helperGearWithheld={_answers.GearWithheld} " +
        // **DRA-84 D2: what the band gate REFUSED, and whether the sentence for it was drawn**
        // — two numbers from one moment (trap 56), because "the engine refused two zones" and
        // "the room told the player so" are different claims and a refusal nobody was told
        // about is a row that vanished. `helperBandLine` is the line, `helperBandGate` is
        // whether the gate could run at all (a level AND a band table), so a green run with a
        // zero count can be told from a run where the gate stood down.
        // **DRA-180 D2: the LIVENESS fact comes FIRST and is not a count.** `helperEraGate` says
        // the gate was wired and asked at all; a refusal count of 0 is the same number on a
        // build where the gate does not exist, which is the assertion DRA-149 D5 item 2 was
        // caught by. It reads the INPUTS the gate stands down on, so it is 1 only when the
        // world's era is known and rankable and an era table is present.
        $"helperEraGate={(_answers.EraGateLive ? 1 : 0)} " +
        $"helperEraRefused={_answers.GearEraRefusals.Count} " +
        $"helperEraLine={(HelperPresentation.EraRefused(_answers.GearEraRefusals, HelperPresentation.BandRefusedUpgrades).Length > 0 ? 1 : 0)} " +
        $"helperMaterialEraRefused={_answers.MaterialEraRefusals.Count} " +
        $"helperBandRefused={_answers.GearBandRefusals.Count} " +
        $"helperBandLine={(HelperPresentation.BandRefused(_answers.GearBandRefusals, HelperPresentation.BandRefusedUpgrades).Length > 0 ? 1 : 0)} " +
        $"helperBandGate={(_bandGate ? 1 : 0)} " +
        // **DRA-84 D5: the gate's INPUTS beside its verdict** (plan P6). `helperBandRefused`
        // above is a COUNT, and a count is equally true of a gate that refused the right two
        // zones for the wrong reason — the band it read, the arm that fired, or the level it
        // compared against could each be wrong without moving it. This key carries what the
        // gate actually COMPARED: the zone, eqlwiki's own row verbatim, and which of the two
        // arms decided. Read beside `helperLevel`, an E2E can assert the RELATIONSHIP — this
        // band against that level, therefore refused on this arm — instead of leaving the
        // arithmetic in a doc comment nobody runs. Spaces go and `:` separates, because the
        // dump is one flat space-separated namespace (trap 58).
        $"helperBandRefusals={string.Join(',', _answers.GearBandRefusals.Select(r => $"{r.Zone.Replace(" ", "")}:{r.Verbatim.Replace(" ", "")}:{r.Arm}"))} " +
        // **DRA-84 D4: the who rule, in the same two-numbers-one-moment shape.** `helperWho` is
        // how many DRAWN item lines can name a creature from the page, `helperWhoWithheld` is
        // how many offers the rule removed, and `helperWhoLine` is whether the room said so. A
        // room drawing three items with three silent who clauses and a room drawing three with
        // named creatures are the same screen to every other key here.
        // **DRA-180 D3: the per-anchor answer, and its INPUTS beside its count** — the shape
        // `helperBandRefusals` above established. `helperAnchorsEmptied` is how many worn items
        // the ladder left with nothing and `helperAnchorLines` is how many the room actually
        // DREW, so a cap that silently swallowed them all can be told from an engine that found
        // none (trap 34 in dump form). `helperAnchorsRemoved` carries the arithmetic the
        // sentence rests on — the anchor, what was found, and the three causes in the gates'
        // own order — so an E2E asserts era+band+who == found rather than trusting the prose.
        // Spaces go and `:` separates: one flat namespace (trap 58).
        $"helperAnchorsEmptied={_answers.GearAnchorsRemoved.Count} " +
        $"helperAnchorLines={Math.Min(_answers.GearAnchorsRemoved.Count, HelperPresentation.GearAnchorsNamed)} " +
        // DRA-219 adds the FOURTH cause to the tuple, in the rules' own order, so the E2E's
        // `era+band+who == found` assertion becomes `era+band+who+quest == found` rather than
        // quietly starting to fail on a character with quests switched on.
        $"helperAnchorsRemoved={string.Join(',', _answers.GearAnchorsRemoved.Select(a => $"{a.Anchor.Replace(" ", "")}:{a.Found}:{a.LaterContent}:{a.OutsideBand}:{a.NoCreature}:{a.NoQuestPath}"))} " +
        $"helperWho={_answers.Top.Sum(r => r.Why.OfType<GearUpgradeFact>().Count(f => f.Who.Count > 0))} " +
        $"helperWhoWithheld={_answers.GearWhoWithheld} " +
        $"helperWhoLine={(HelperPresentation.DropOffersWithheld(_answers.GearWhoWithheld).Length > 0 ? 1 : 0)} " +
        // **DRA-219: the QUEST acquisition path, in the shape the who keys above it use** (S10,
        // S11). `helperQuestRows` is how many drawn rows are quests and `helperQuestSource` how
        // many of those can actually say who starts it and where — the pair that separates "a
        // quest row exists" from "a quest row is a direction", which is the whole of S11.2.
        // `helperQuestWithheld`/`helperQuestLine` mirror the who rule's count-and-did-it-say-so
        // pair (trap 56: the engine refusing and the screen saying so are different claims).
        // `helperNoSource` and `helperQuestOnly` are the two the sweep never passed on at all,
        // apart because their remedies are apart.
        $"helperQuestRows={_answers.Top.Count(r => r.Kind == RecommendationKind.Quest)} " +
        $"helperQuestSource={_answers.Top.Sum(r => r.Why.OfType<QuestSourceFact>().Count(f => f.Giver.Length > 0 || f.StartZone.Length > 0))} " +
        $"helperQuestWithheld={_answers.GearQuestWithheld} " +
        $"helperQuestLine={(HelperPresentation.QuestOffersWithheld(_answers.GearQuestWithheld).Length > 0 ? 1 : 0)} " +
        $"helperNoSource={_answers.GearNoSource} " +
        $"helperNoSourceLine={(HelperPresentation.SourcelessUpgrades(_answers.GearNoSource).Length > 0 ? 1 : 0)} " +
        $"helperQuestOnly={_answers.GearQuestOnly} " +
        $"helperQuestOnlyLine={(HelperPresentation.QuestOnlyUpgrades(_answers.GearQuestOnly).Length > 0 ? 1 : 0)} " +
        // **DRA-149 D2: the worn rows that never became an anchor** — the same two-numbers-one-
        // moment shape, and the one it matters most for. `helperWorn` above is the anchor count
        // and it was the ONLY thing this dump said about the dump: twenty anchors from a
        // twenty-one-row sheet and twenty from a twenty-row sheet are the same number, which is
        // precisely how the Founder's bow left without a trace. `helperUnreadWorn` is the count,
        // `helperUnreadNames` is what they were (spaces out, `,` between — trap 58's flat
        // namespace), and `helperUnreadLine` is whether the room actually SAID it, because the
        // engine reporting it and the screen drawing it are different claims (trap 56).
        $"helperUnreadWorn={_answers.UnreadWorn.Count} " +
        $"helperUnreadNames={string.Join(',', _answers.UnreadWorn.Select(n => n.Replace(" ", "")))} " +
        $"helperUnreadLine={(HelperPresentation.UnreadWorn(_answers.UnreadWorn).Length > 0 ? 1 : 0)} " +
        // **DRA-149 D3: the materials engine's INPUTS, in the shape the gear keys above use**
        // (plan P4). `helperMaterialWhy` is how many DRAWN rows carry an ingredient line and
        // `helperMaterialNamed` how many of those lines can name a creature — the two that
        // separate "the engine answered" from "the engine answered with something to DO", which
        // is the whole of the Founder's FAIL item 3a. `helperProfessions` is what it ranked FOR
        // (empty = all eight, the store's own filter semantics), so a run that answered nothing
        // because the pick was narrow can be told from one that answered nothing because the
        // pages were silent. The refusal keys mirror the band/who pair above rather than
        // reusing them: two engines refusing zones into one number is a count of a list nobody
        // asked for (trap 56 — both halves from one moment, and about the right list).
        $"helperProfessions={string.Join(',', TradeskillPickStore.Picked(_main.Settings, _main.QuestCharacterKey))} " +
        $"helperMaterialWhy={_answers.Top.Count(r => r.Why.OfType<TradeskillMaterialFact>().Any())} " +
        $"helperMaterialNamed={_answers.Top.Sum(r => r.Why.OfType<TradeskillMaterialFact>().Count(f => f.Who.Count > 0))} " +
        $"helperMaterialSeen={_answers.Top.Count(r => r.Why.OfType<TradeskillMaterialFact>().Any() && r.Why.OfType<GearDropSeenFact>().Any())} " +
        $"helperMaterialBandRefused={_answers.MaterialBandRefusals.Count} " +
        $"helperMaterialBandLine={(HelperPresentation.BandRefused(_answers.MaterialBandRefusals, HelperPresentation.BandRefusedMaterials).Length > 0 ? 1 : 0)} " +
        $"helperMaterialWhoWithheld={_answers.MaterialWhoWithheld} " +
        $"helperMaterialWhoLine={(HelperPresentation.DropOffersWithheld(_answers.MaterialWhoWithheld).Length > 0 ? 1 : 0)} " +
        // Whether a popup is OPEN. The staged state the shot photographs, and the assertion
        // that the review hook armed the control rather than merely being spelled correctly.
        $"helperPickerOpen={((_goalPicker?.IsOpen ?? false) || (_factionPicker?.IsOpen ?? false) || (_unlockPicker?.IsOpen ?? false) || (_wornPicker?.IsOpen ?? false) || (_professionPicker?.IsOpen ?? false) ? 1 : 0)} " +
        $"helperPickerHook={(_reviewHookArmed ? 1 : 0)} " +
        // What the ENGINE answered.
        $"helperRecs={_answers.Top.Count} " +
        $"helperWithheld={_answers.Withheld} " +
        // What the PER-ROW why cap held back, summed — a different cap from `helperWithheld`
        // above it, which is the LIST's. Added in DRA-71 D7 because that is the slice where a
        // zone first served three engines at once and the cap started trimming whole sentences
        // off a row: a cap that trims is fine, a cap that trims in silence is trap 50, and only
        // a launched app can say the row admitted it.
        $"helperWhyWithheld={_answers.Top.Sum(r => r.WithheldWhy)} " +
        $"helperGaps={_answers.Gaps.Count} " +
        $"helperNotYet={_answers.NotAnsweredYet.Count} " +
        // The zones it named, in rank order. The dump is one flat space-separated namespace,
        // so a zone's internal spaces are dropped — an E2E reads this to know WHICH place is
        // being recommended, not to typeset it.
        $"helperZones={string.Join(',', _answers.Top.Select(r => r.Zone.Replace(" ", "")))} " +
        // WHAT each answer is ABOUT, in rank order. `helperZones` cannot say: an unlock the
        // player has never farmed has no place to travel to, so its zone is empty and every
        // such answer reads as the same blank. This is the only key that can tell "the pick
        // narrowed the engine" from "the dump was short" (DRA-71 D5).
        $"helperSubjects={string.Join(',', _answers.Top.Select(r => r.Subject.Replace(" ", "")))} " +
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
        $"helperCopyCmd={_copyCommands} " +
        // The level the ENGINE was handed, with its source (DRA-71 D3). Both halves, because
        // a number alone cannot tell a ding from a statement — which is exactly what the two
        // fixtures prove — and because `helperOutgrown` below is only meaningful against a
        // level somebody can read.
        $"helperLevel={_level.Level} " +
        $"helperLevelSource={_level.Source.ToString().ToLowerInvariant()} " +
        $"helperLevelDoor={(_levelDoor ? 1 : 0)} " +
        // How many of the drawn answers carried the P6 discount's sentence. The SCREEN's
        // claim about the discount, beside the engine's own weights — a zone that was
        // down-weighted and said nothing about it would satisfy the ranking assertion and
        // still be the bug.
        $"helperOutgrown={_answers.Top.Count(r => r.Why.OfType<ZoneOutgrownFact>().Any())} " +
        // **DRA-71 D4.** How many of the DRAWN answers carried each of the new outcome
        // sentences — the screen's claim, beside the engine's, from the same Build (trap 56).
        // `helperThroughput` is the one that would have shipped broken on its own: the probe
        // is a second query over `history.db` and a room that folded it and never drew it
        // would satisfy every store-side assertion in the repo.
        $"helperThroughput={_answers.Top.Count(r => r.Why.OfType<ZoneThroughputFact>().Any())} " +
        $"helperDowntime={_answers.Top.Count(r => r.Why.OfType<ZoneDowntimeFact>().Any())} " +
        $"helperTier={_answers.Top.Count(r => r.Why.OfType<ZoneTierFact>().Any())} " +
        // The top answer's measured dps ×10, as an integer: the dump is one flat
        // space-separated namespace of key=value (trap 58), so a decimal point is fine but a
        // culture that writes it as a comma is not. 0 is "nothing measured" — the same
        // silence `ZoneRoll.Dps` answers with, carried out rather than rounded into a claim.
        $"helperTopDps10={(int)Math.Round((_answers.Top.Count > 0
            ? _answers.Top[0].Why.OfType<ZoneThroughputFact>().FirstOrDefault()?.Dps ?? 0
            : 0) * 10)} " +
        // **DRA-71 D7.** The two new folds' own sizes, so "the engine ranked nothing" can be
        // told from "the fold found nothing" — two different bugs that draw one grey sentence.
        // `helperMoteZones` is how many zones the fold produced at all and `helperMoteRated`
        // how many cleared BOTH floors (MinHours and MinKills), which is the one pair that can
        // explain a character with motes in their bags and no mote answer on screen.
        $"helperMoteZones={_sources.Motes.Count} " +
        $"helperMoteRated={_sources.Motes.Count(m => m.HasRate)} " +
        $"helperSales={_sources.Sales.Count} " +
        // What the SCREEN drew from them (trap 56: the store's claim and the screen's claim are
        // different claims, captured in one Build). `helperCatalogValue` must stay 0 until the
        // weekly refresh puts vendor values in the shipped catalog — it is the one key that
        // says out loud whether the promoter's field has data behind it yet.
        $"helperMoteWhy={_answers.Top.Count(r => r.Why.OfType<ZoneMoteRateFact>().Any())} " +
        $"helperMoteSource={_answers.Top.Count(r => r.Why.OfType<MoteSourceFact>().Any())} " +
        $"helperTierPref={_answers.Top.Count(r => r.Why.OfType<ZoneTierPreferenceFact>().Any())} " +
        $"helperCoinWhy={_answers.Top.Count(r => r.Why.OfType<ZoneCoinRateFact>().Any())} " +
        $"helperSellable={_answers.Top.Count(r => r.Why.OfType<SellableDropFact>().Any())} " +
        $"helperCatalogValue={_answers.Top.Count(r => r.Why.OfType<CatalogValueFact>().Any())} " +
        $"helperMoneyNote={(_moneyNote ? 1 : 0)} " +
        $"helperGearBaseNote={(_gearBaseNote ? 1 : 0)} " +
        // **DRA-71 D8.** The store's claim and the screen's claim, from one Build (trap 56):
        // `helperSkills` is how many profession standings the LEDGER holds and
        // `helperProfKnown` how many of the DRAWN rows carried a number. A skill-up that
        // reached quest-ledger.json and no row is trap 20's shape, and it photographs as an
        // ordinary block (trap 29). `helperProfChips` is the picker's rows — eight, always —
        // and `helperProfRows` what the pick actually left on screen, which is the only pair
        // that can tell a filter from an empty fold.
        $"helperSkills={_skills.Count} " +
        $"helperProfChips={_professionChips} " +
        $"helperProfRows={_professionRows} " +
        $"helperProfKnown={_professionsKnown} " +
        $"helperProfFace={_professionFace.Replace(" ", "")} " +
        // The preset's two claims: how many rows OFFERED it, and how many already had a rule.
        // The second is read from the player's own TrackedRules every paint, so a rule deleted
        // in Options moves it back — "the room added one" and "the player has one" are the same
        // fact with one producer, and this is what says so from outside.
        $"helperWatchPresets={_watchPresets} " +
        $"helperWatched={_professionsWatched} " +
        // DRA-149 D4. The vendor half's two numbers, from ONE moment: what the SCREEN drew and
        // what the shipped catalog HAS for the same professions. The relationship is the
        // assertion — drawn is capped at three per profession, so it can never exceed the
        // catalog's, and a zero beside a non-zero catalog is the cap or the pick misbehaving
        // rather than an empty file (trap 56).
        $"helperMerchantRows={_merchantRows} " +
        $"helperMerchantZones={ZoneMerchants.Default.ZoneCount} " +
        $"helperMerchantLines={ZoneMerchants.Default.LineCount}";
}
