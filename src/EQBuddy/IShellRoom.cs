using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// One ROOM of the Evolved shell — the seam every room hangs on, added in E-3 Phase 2
/// PR 2 when the second and third rooms arrived and the host's four dispatch points
/// (content, address, paint, dump) each grew a switch.
///
/// **Four hand-written switches over one list of rooms is trap 30's shape**: a staging
/// list is code that cannot be type-checked, and the failure is never an error — it is a
/// room that paints on arrival and then never again because whoever added it updated
/// three of the four. One interface and one dictionary makes adding a room a compile
/// question instead of a memory question.
///
/// **A room is a CONTROL, not a window.** Everything <c>ProgressWindow</c>,
/// <c>WorldWindow</c> and <c>GearLootWindow</c> do that is about being a window — the
/// hand-drawn title bar and its drag handler, <c>WindowZoom</c>, <c>ScreenGuard</c>
/// placement, the <c>Left</c>/<c>Top</c> persistence on close, the monitor-derived
/// <c>MaxHeight</c> — is chrome the shell already supplies once, natively, for every room.
/// It is deleted rather than ported, which is the same call the World fold made for four
/// cog menu entries and the same one PR 1 made for the shell's own header.
///
/// **What is NOT chrome is the trap-46 half of the move, and it is the half that hurts.**
/// When a surface moves to a new host, check what the OLD host was doing for it every
/// tick — and, here, what it was doing for it at CLOSE. Two of the three rooms hold
/// something that has to be given back: <c>SpawnsView</c> owns a one-second
/// <c>DispatcherTimer</c> that <c>WorldWindow.Closed</c> stops, and <c>InventoryView</c>
/// holds a <c>CancellationTokenSource</c> that <c>GearLootWindow.Closed</c> disposes.
/// A shell that closed without doing both would leak a ticking timer per open, silently,
/// for as long as the process lives — which nothing in a diff, a test or a screenshot can
/// see. <see cref="Release"/> is that obligation, named on the interface so a new room
/// has to answer it rather than inherit a default of "nothing".
/// </summary>
internal interface IShellRoom
{
    /// <summary>What the host puts in its content cell. The room IS the control, so this
    /// hands out nothing the room did not build and does not own — it is not the
    /// ownership transfer trap 45 is about (<c>SurfaceOwnershipTests</c> scans for a host
    /// handing out a body PER TAB, which is the shape that had two windows sharing one
    /// instance). Every room builds its own surfaces in its own constructor.</summary>
    UIElement Body { get; }

    /// <summary>Land on a room by its wire key — the second half of a <c>page:room</c>
    /// address, in the SURFACE's vocabulary. An unrecognised key is left alone rather than
    /// snapped to a default, the refusal <c>ProgressWindow.SetTab</c> already makes:
    /// showing the wrong room silently is worse than showing the one already open.</summary>
    void SetTab(string key);

    /// <summary>Paint from the widget's snapshot. The shell calls this on the tick for the
    /// VISIBLE room only, the way every theme window paints only its active tab.</summary>
    void Render(StatsSnapshot s);

    /// <summary>The room's facts for the <c>EQBUDDY_EXPAND</c> dump, under a
    /// <c>shell*</c> prefix so they sit beside — and can be compared with — the keys the
    /// v1 window reports from the same surfaces. Two hosts of one room is exactly where a
    /// silent divergence would live, and the WPF layer has no unit tests to catch it any
    /// other way.</summary>
    string DebugFacts();

    /// <summary>Give back whatever the room is holding: timers, tokens, file watchers.
    /// Called once when the shell closes. A room with nothing to give back implements it
    /// empty and says so — an empty method with a reason is a decision, and a missing one
    /// is a question nobody asked.</summary>
    void Release();

    /// <summary>
    /// The shell's width answer, pushed down: whether this room is now too narrow to draw
    /// a list beside a detail pane (<see cref="ShellLayout.RoomSinglePane"/>). Called
    /// whenever the window is resized, and once on a room's first arrival so a room built
    /// after the last resize is not a beat behind.
    ///
    /// **Pushed rather than pulled, and that is the whole reason it is on the interface**
    /// (E-3 PR 3, whose Quests room is the axis's first consumer since PR 1 decided it).
    /// The threshold is about the ROOM's share of the window AFTER the rail takes its
    /// own — arithmetic only the host has both halves of — so a room measuring itself
    /// would be a second producer of one answer, and the two would disagree at exactly the
    /// boundary where a resize bug lives (trap 33).
    ///
    /// **A single-column room implements it empty and says so**, the same contract
    /// <see cref="Release"/> already sets: an empty method with a reason is a decision, and
    /// a missing one is a question nobody asked. Three of the four rooms are in that case
    /// today, and each one says why on its own implementation rather than here.
    /// </summary>
    void ApplyLayout(ShellLayout layout);
}
