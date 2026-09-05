using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The QUESTS room as the Evolved shell hosts it — Quests · Epic 1.0 · Plane of Sky ·
/// Unlocks, the fourth surface moved in (E-3 Phase 2 PR 3).
///
/// **Why Quests came after World and Gear, and alone.** Bevel's signed pre-design sorts
/// the remaining rooms by which IA verdict is already PAID FOR, not by size:
/// <c>WorldWindow</c> and <c>GearLootWindow</c> were already one window each, composed of
/// exactly the tabs a room needs, so hosting them was a MOVE. Quests had the tabs too —
/// but no view: 2,481 lines of window-owned rendering with nothing an
/// <see cref="IShellRoom"/> could be handed. So this is a LIFT, which is the shape that
/// has cost this repo real bugs (trap 46: what was the old host doing for the surface
/// every tick; trap 20's mirror: a rule with a home and no reader), and it earned its own
/// diff for that reason rather than for its line count.
///
/// **The lift itself is <see cref="QuestsView"/>, and it moved no rule.** All five
/// Helm-signed presentation rules the pre-design asked to be inventoried are properties of
/// the SURFACE and stayed inside it — the #241 turn-in provenance sentence, the Sky
/// leftover bands, their session-only folds, the Ready-band unlocked caveat, and the Sky
/// tab's two ⧉ commands. Nothing about them is re-decided here, and this room could not
/// re-decide them if it wanted to: it owns one view and asks it questions.
///
/// **It builds its OWN view and hands none out** (trap 45). A WPF <c>UIElement</c> has
/// exactly one parent, so a view shared with <see cref="QuestsWindow"/> would be torn out
/// of whichever host painted it last — silently, with no exception to point at.
///
/// **What the WINDOW does that this room does NOT** (trap 26: name every control and say
/// where it went):
///
///  * **The chrome** — the hand-drawn title row, the close button, the drag handler,
///    <c>WindowZoom</c>, <c>ScreenGuard</c> placement, the <c>Left</c>/<c>Top</c>
///    persistence and the monitor-derived <c>MaxHeight</c>. The shell supplies all of it
///    natively, once, for every room; <see cref="QuestsView.HideOwnTitleBar"/> is how the
///    view stops drawing a second copy, exactly as <c>SpawnsView</c> does in
///    <see cref="WorldRoom"/>.
///  * **…except the CHARACTER, which is the one thing that title row said and no other
///    room's did.** "Quest Tracker — Dranak" names whose quests these are, and the shell's
///    native title bar reads "EQBuddy — Quests" and cannot. So it is not dropped: the view
///    composes the string (one producer) and this room draws it as a caption above the
///    tabs. That is the whole visible difference between the two hosts, and it exists
///    because losing it would be trap 26's sentence — the data survived the move and the
///    thing that showed it did not.
///  * **The height cap.** <see cref="QuestsWindow"/> is <c>SizeToContent="Height"</c>, so
///    it must cap the view's two scrollers or a long catalog walks the footnotes off the
///    screen. This room is a bounded <c>*</c> cell in the shell — a real overflow — so it
///    asks for no cap at all. Scrolling belongs to the host (trap 36) and here the host
///    genuinely has a height to give.
///
/// **This is the first room to express <see cref="ShellLayout.RoomSinglePane"/>**, whose
/// comment has said *"no room expresses this yet"* since PR 1. The Turn-ins pane is a
/// list+detail layout, so below <c>SplitRoomWidth</c> it collapses to one pane with a way
/// back. The decision is the shell's and arrives through <see cref="ApplyLayout"/>; the
/// arrangement is the view's. Nothing here computes a width.
///
/// **Subtraction blocker, written at the PR that adds the room** (the World deaths-star /
/// Gear loot-star pattern, and Helm's per-item gate of 2026-09-04): this PR does not
/// retire <see cref="QuestsWindow"/>. It may only go once this room is on the rail (it is),
/// a screenshot proves parity (`shell-quests`, `shell-quests-sky`, `shell-quests-narrow`),
/// and the widget's Quests LAUNCHER card has somewhere to point — that card is the only
/// door a player has to this surface today, and `MainWindow.ShowQuestsWindow` is what the
/// map badge in the Loot views calls. **Nothing here writes a `MiniStats` key**, so unlike
/// World and Gear this room takes no last-writer with it.
/// </summary>
internal sealed class QuestsRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly QuestsView _view;
    private readonly TextBlock _heading;

    /// <summary>The caption and the view under it, in their own Grid so the whole page is
    /// ONE thing to collapse when the room-level empty takes over.</summary>
    private readonly Grid _page = new();

    /// <summary>The whole-room empty, built on the first render that needs one and kept
    /// afterwards. A SIBLING of <see cref="_page"/> rather than something dropped into the
    /// body, because a whole-room empty has to take the TAB STRIP with it: an empty room
    /// under a live strip of tabs is four invitations to open something that is not there.
    /// <c>LiveRoom</c> set this shape and the other three follow it.</summary>
    private FrameworkElement? _emptyRoom;

    private bool _empty;

    public UIElement Body => this;

    public QuestsRoom(MainWindow main)
    {
        _main = main;

        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_page);

        // The character, and only the character — a caption, not a second title bar. It
        // carries no icon and no app name, because the shell's title bar and rail already
        // say where you are; what they cannot say is whose quests these are.
        _heading = DesignSystem.Text(Role.Caption, "");
        _heading.Ink("DimBrush");
        _heading.Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0);
        SetRow(_heading, 0);
        _page.Children.Add(_heading);

        _view = new QuestsView(main);
        // Its own title row would be a second one under the shell's native chrome — the
        // same call WorldRoom makes on SpawnsView, for the same reason.
        _view.HideOwnTitleBar();
        _view.HeadingChanged += text => _heading.Text = text;
        _heading.Text = _view.Heading;
        SetRow(_view, 1);
        _page.Children.Add(_view);
    }

    /// <summary>
    /// The room-level empty, positioned by the shared wrapper Home built and worded for THIS
    /// room.
    ///
    /// **No ⧉ copy on it, and that is an ORDER rather than an omission** (trap 34 is about a
    /// room-level empty SWALLOWING an affordance the surface under it was offering). The Sky
    /// tab's two ⧉ commands are the view's and stay there — but both are <c>/outputfile</c>
    /// dumps the game names after the character, so on a profile with no character neither is
    /// the next thing to do. <c>/log on</c> is, and the explanation says so.
    /// </summary>
    private FrameworkElement AddEmptyRoom()
    {
        var built = RoomEmptyState.Build(ShellRoomEmpty.Quests);
        Children.Add(built);
        return built;
    }

    /// <summary>Land on a tab by its wire key — the second half of a <c>page:room</c>
    /// address, in the SURFACE's vocabulary. <see cref="QuestsView.SetTab"/> already
    /// resolves through Core's own <c>QuestSurface.TabForKey</c> table and already refuses
    /// to invent a tab, so this forwards rather than re-deciding: the shell knows the rooms
    /// exist, not what they are called.</summary>
    public void SetTab(string key) => _view.SetTab(key);

    /// <summary>
    /// Paint from the widget's snapshot.
    ///
    /// **<c>PaintNow</c> rather than <c>MaybeRefresh</c>, and it is trap 56 rather than an
    /// oversight.** The view carries a two-second throttle for the v1 window, whose own
    /// tick is the only thing driving it. Nesting that inside the shell's one-second tick
    /// would let this room report LAST tick's row counts beside this tick's totals in a
    /// dump whose whole contract is to describe one moment — the exact failure that cost
    /// the E2E suite four rounds and forty runner-minutes. The signature check inside
    /// <c>Refresh</c> is what makes the un-throttled call free when nothing has moved.
    /// </summary>
    public void Render(StatsSnapshot s)
    {
        // **The whole-room empty, and the only state that gets one.** The tracker follows
        // the log for turn-ins and ticks Epic and Sky steps off as the pieces arrive, so
        // with no character there is nothing for any of the four tabs to be about.
        //
        // **The ticked-step count is why this is a call and not a `NoCharacter` test.**
        // `EpicQuestCompleted` and `SkyQuestCompleted` are SETTINGS a player can tick by
        // hand, and three separate bugs (#204/#209, #210, #212) are about those very lists
        // losing the thing that showed them; a room that hid them because the identity was
        // unknown would be a fourth in the same family.
        _empty = ShellRoomEmpty.QuestsIsEmpty(ShellRoomIdentity.Of(_main),
            _main.Settings.EpicQuestCompleted.Count + _main.Settings.SkyQuestCompleted.Count);
        if (_empty)
        {
            // The view is not painted while it is collapsed, so `shellQuests*` below report
            // its own zeros — which is the truth on the only profile that can reach this
            // state, and the dump still describes ONE moment (trap 56).
            _emptyRoom ??= AddEmptyRoom();
            _emptyRoom.Visibility = Visibility.Visible;
            _page.Visibility = Visibility.Collapsed;
            return;
        }
        if (_emptyRoom is not null) _emptyRoom.Visibility = Visibility.Collapsed;
        _page.Visibility = Visibility.Visible;

        _view.PaintNow();
    }

    /// <summary>
    /// The shell says this room is too narrow to split. Handed straight to the view, which
    /// owns the arrangement — the FIRST consumer of an axis `ShellLayoutPolicy` has carried
    /// since PR 1 with nothing to exercise it.
    ///
    /// A one-line forward on purpose: the room must not cache the answer or re-derive it,
    /// or there would be two producers of one boolean and they would disagree at exactly
    /// the boundary a resize bug lives on (trap 33).
    /// </summary>
    public void ApplyLayout(ShellLayout layout) => _view.SinglePane = layout.RoomSinglePane;

    /// <summary>**The timer this room borrowed, given back** (trap 46, and the obligation
    /// <see cref="IShellRoom.Release"/> names). The view starts a search-debounce
    /// <c>DispatcherTimer</c> on every keystroke; a shell that closed inside the settle
    /// window would leave it queued to paint a torn-down surface, and the shell can be
    /// reopened where a closed window is closed once.</summary>
    public void Release() => _view.Release();

    /// <summary>
    /// The room's facts, under <c>shellQuests*</c>.
    ///
    /// **The view is asked for the SAME string the window asks it for**, and the keys are
    /// re-prefixed mechanically (<see cref="ShellDumpFacts"/>) rather than re-implemented.
    /// The dump is one flat namespace, so with both hosts open the room's `questsRows`
    /// would land on the window's and every existing `quests*` assertion would quietly
    /// start reading the other host (trap 58, which is trap 4 with the two sources being
    /// two hosts). And a hand-written list here would stop covering
    /// <see cref="QuestsView"/> the day it gains a fifteenth fact — trap 30, again.
    ///
    /// **The prefix is <c>shell</c> and not <c>shellQuests</c>, which looks wrong and is
    /// not.** <see cref="ShellDumpFacts.Prefixed"/> renames mechanically, and this view's
    /// own keys already begin with <c>quests</c> — <see cref="WorldRoom"/> passes
    /// <c>shellWorld</c> because <c>MapView</c>'s are <c>mapZones</c>, which carry no room
    /// name of their own. Passing the room name here produced <c>shellQuestsQuestsTab</c>,
    /// caught by the E2E suite on its first run rather than by reading the helper: the
    /// convention is <c>shell</c> + the view's own key, and for four rooms out of four that
    /// is what these two spellings both mean.
    ///
    /// <c>shellQuestsHeading</c> is the LENGTH of the caption rather than its text: the
    /// name in it is the player's character, and a dump the E2E suite compares is not the
    /// place for it. Zero is the honest answer for "no character seen yet" and is exactly
    /// the state that would mean the caption had been lost in the move.
    /// </summary>
    public string DebugFacts() =>
        // Zero on any profile with a character. A predicate that fired while the room had
        // content would collapse all four tabs and the Sky tab's two ⧉ copies with them.
        $"shellQuestsEmpty={(_empty ? 1 : 0)} " +
        $"shellQuestsHeading={_heading.Text.Length} " +
        ShellDumpFacts.Prefixed("shell", _view.DebugFacts());
}
