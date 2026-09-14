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

    /// <summary>What this character is WEARING, folded from the newest inventory dump behind
    /// the same throttle as the session query (DRA-71 D6). It is the Farm Gear sweep's anchor
    /// set; <see cref="GearUpgrades.WornFrom"/> is the one producer of it, so the phone reads
    /// the same rule when it gets here.</summary>
    private IReadOnlyList<WornItem> _worn = [];

    /// <summary>The dump this room folded <see cref="_worn"/> from, in the repaint
    /// fingerprint. A new dump changes what is worn, which changes every gear answer — trap
    /// 72's exact shape, one store along.</summary>
    private string _inventoryStamp = "";
    private RecommendationSet _answers = RecommendationSet.Empty;
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

    /// <summary>The Helper has no rooms inside it — goals are a filter, not tabs, which is
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
        // DRA-71 D5: the SAME store the Quests window's Unlocks tab reads. One producer of the
        // pick; neither room keeps a copy of it.
        var picks = UnlockPickStore.Picked(_main.Settings, _main.QuestCharacterKey);
        // DRA-71 D6: the three Farm Gear selections, from the one store that owns them. Read
        // every tick for the reason the level is — they are three dictionary lookups, and a
        // click in this room must not wait five seconds to change the answers.
        _intent = GearIntentStore.Intent(_main.Settings, _main.QuestCharacterKey);
        var wornPicks = GearIntentStore.WornPicks(_main.Settings, _main.QuestCharacterKey);
        _includeQuests = GearIntentStore.IncludeQuests(_main.Settings, _main.QuestCharacterKey);
        var unlocks = _main.Unlocks;
        // The SAME resolution the Character room draws and the unlock preview keys off
        // (MainWindow.ResolvedLevel), so the number this room ranks with is the number that
        // room shows. Read every tick: it is two dictionary lookups, and a level the player
        // just typed one room away must not wait five seconds to change the answers.
        _level = _main.ResolvedLevel;

        // **THE FINGERPRINT, AND EVERY STORE THIS ROOM READS IS IN IT** (trap 72: the Quests
        // tab drew the moment before for a whole session because its signature carried
        // everything except the two lists the feature wrote). The two the player can change
        // with a click — the goal picks and the faction picks — are folded by CONTENT and not
        // by count, because a swap leaves a count unmoved. Nothing here ticks on the clock
        // (trap 8): no countdown, no age, no "x ago", so an idle room costs one string
        // compare per second and not a torn-down visual tree.
        var key = string.Join('|',
            identity.Character, identity.Server,
            string.Join(',', goals), string.Join(',', factions),
            // The THIRD store a click in this room changes, folded by CONTENT for the reason
            // the two above it are: a swap leaves a count unmoved, and the whole of D5's
            // player-visible change is what this selection does to the answers (trap 72).
            string.Join(',', picks),
            // **The three stores DRA-71 D6's clicks change, plus the dump they are ABOUT.**
            // The picks are folded by CONTENT for the reason the two above them are — a swap
            // leaves a count unmoved — and the inventory stamp is here because a new dump
            // changes what is worn, which changes every gear answer without moving anything
            // else in this key (trap 72).
            _intent, string.Join(',', wornPicks), _includeQuests, _inventoryStamp,
            _poolVersion,
            // The throughput fields are in the fold's own signature for trap 72's reason: a
            // re-fold that gained combat seconds, damage or healing and moved a weight
            // without moving the session count or the experience total would leave this room
            // drawing the moment before it, for the rest of the session.
            string.Join(',', _zones.Select(z =>
                $"{z.Zone}:{z.Sessions}:{z.Kills}:{z.XpPercent:0.##}"
                + $":{z.CombatSeconds:0.##}:{z.CombatDamage:0.##}:{z.HealingDone:0.##}"
                + $":{z.ActiveHours:0.####}:{z.Deaths}")),
            unlocks.HasAchievements, unlocks.Races.Count, unlocks.Classes.Count,
            unlocks.Races.Count(u => u.Complete), unlocks.Classes.Count(u => u.Complete),
            unlocks.Factions?.WrittenAt.Ticks ?? 0,
            // The level is an INPUT to the ranking, so it belongs in what makes the room
            // redraw (trap 72 — the Quests tab drew the moment before for a whole session
            // because its signature carried everything except the store the feature wrote).
            // The SOURCE rides with the number: a clear that lands back on the same level
            // still changes the sentence this room prints about where it came from.
            _level.Level, _level.Source,
            ShellPages.Landed.Count);
        if (key == _painted) return;
        _painted = key;

        _answers = Recommendations.Rank(new HelperInputs(
            _zones, _pool.Mobs, unlocks.Factions, factions,
            unlocks.Races, unlocks.Classes, picks, unlocks.HasAchievements,
            _main.Settings.SkyQuestChecklist, _main.Settings.SkyQuestCompleted,
            _main.QuestCatalog, _level)
        {
            // DRA-71 D6. The sweep lives in Core behind `Rank`, not here, so the phone gets
            // it the day it calls the same method — porting a feature TO a surface is the
            // signal its logic never went through the shared layer.
            Worn = _worn,
            Items = ItemCatalog.Default,
            MyClasses = MyClassCodes(),
            GearIntent = _intent,
            WornPicks = wornPicks,
            IncludeQuests = _includeQuests,
        }, goals);

        Build(goals, factions, picks, wornPicks, unlocks);
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
        //
        // The third input is DRA-71 D4's throughput probe: one JsonDocument read per stored
        // snapshot, behind THIS throttle rather than the per-tick one, because it is the same
        // cost as the session query it sits beside and neither belongs on a one-second clock.
        _zones = ZoneHistory.Fold(
            _main.StoredSessions(), _pool.Mobs, _main.StoredThroughput());

        // **DRA-71 D6: what the character is WEARING.** Behind this throttle rather than the
        // per-tick one — it is a file read plus a fold over ~200 rows, the same cost as the
        // session query it sits beside — and `Refreshed()` clears the throttle, so a dump the
        // player just wrote answers immediately rather than up to five seconds later. That
        // matters more here than anywhere: the Farm Gear empty state is the thing that asked
        // them to run the command.
        var dump = _main.LatestInventory();
        _inventoryStamp = dump is null ? "" : $"{dump.Path}|{dump.WrittenAt:O}|{dump.Entries.Count}";
        _worn = dump is null ? [] : GearUpgrades.WornFrom(dump.Entries, _main.WikiItems.StatsFor);
    }

    /// <summary>
    /// This character's classes as the item blocks spell them (PAL, RNG) — the sweep's
    /// class-lock filter.
    ///
    /// <para>The same resolution the Gear room's Inventory tab makes, and deliberately the
    /// same fallback: the ledger's picked classes first, the log's inference behind them.
    /// Empty means unknown, and unknown filters NOTHING — hiding a real upgrade is worse than
    /// showing one the player will recognise as not theirs.</para>
    /// </summary>
    private IReadOnlyList<string> MyClassCodes()
    {
        var picked = _main.QuestLedger?.ClassesFor(_main.QuestCharacterKey) ?? [];
        if (picked.Count == 0 && _main.CurrentSnapshot().InferredClass is { Length: > 0 } inferred)
            picked = [inferred];
        return [.. picked.Select(GearLocker.Code)];
    }

    // ---- the body -------------------------------------------------------------------

    private void Build(
        IReadOnlyList<HelperGoal> goals, IReadOnlyList<string> factions,
        IReadOnlyList<string> picks, IReadOnlyList<string> wornPicks, UnlockSource unlocks)
    {
        _blocks.Children.Clear();
        _goalPicker = null;
        _factionPicker = null;
        _unlockPicker = null;
        _wornPicker = null;
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
            BuildGearIntent(wornPicks);
        if (goals.Count == 0 || goals.Contains(HelperGoal.WorkOnFaction))
            BuildFactionPicker(factions, unlocks);
        if (goals.Count == 0 || goals.Contains(HelperGoal.UnlockRaces)
            || goals.Contains(HelperGoal.UnlockClasses))
            BuildUnlockPicker(goals, picks, unlocks);
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

        // The toggle belongs to both ANSWERED intents and to neither deferred one — an item a
        // quest hands out is the same offer whichever way the sweep was anchored, and offering
        // it beside an intent that ranks nothing would be a control with no effect.
        if (GearUpgrades.ShapeFor(_intent) == GearIntentShape.Answered) BuildQuestToggle(block);

        block.Children.Add(Line(HelperPresentation.GearCatalogNote, Role.Caption));
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
        if (_worn.Count == 0)
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
        var rows = _worn
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
        $"helperWorn={_worn.Count} " +
        $"helperWornChips={_wornChips} " +
        $"helperWornFace={_wornFace.Replace(" ", "")} " +
        $"helperWornPicks={string.Join(',', GearIntentStore.WornPicks(_main.Settings, _main.QuestCharacterKey).Select(p => p.Replace(" ", "")))} " +
        // The toggle's state AND whether it was drawn at all — "the room decided not to offer
        // it" and "the room forgot" are different claims and an absent control photographs as
        // an unremarkable panel.
        $"helperQuestsOn={(_includeQuests ? 1 : 0)} " +
        $"helperQuestToggle={(_questToggle ? 1 : 0)} " +
        // What the ENGINE found: how many drawn answers carry a catalog upgrade line, how many
        // carry an observed drop (the personal half), and what the sweep's own cap withheld.
        $"helperGearWhy={_answers.Top.Count(r => r.Why.OfType<GearUpgradeFact>().Any())} " +
        $"helperGearSeen={_answers.Top.Count(r => r.Why.OfType<GearDropSeenFact>().Any())} " +
        $"helperGearWithheld={_answers.GearWithheld} " +
        // Whether a popup is OPEN. The staged state the shot photographs, and the assertion
        // that the review hook armed the control rather than merely being spelled correctly.
        $"helperPickerOpen={((_goalPicker?.IsOpen ?? false) || (_factionPicker?.IsOpen ?? false) || (_unlockPicker?.IsOpen ?? false) || (_wornPicker?.IsOpen ?? false) ? 1 : 0)} " +
        $"helperPickerHook={(_reviewHookArmed ? 1 : 0)} " +
        // What the ENGINE answered.
        $"helperRecs={_answers.Top.Count} " +
        $"helperWithheld={_answers.Withheld} " +
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
            : 0) * 10)}";
}
