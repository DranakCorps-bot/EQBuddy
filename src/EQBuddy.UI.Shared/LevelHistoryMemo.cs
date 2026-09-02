using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// <see cref="LevelHistory.Rows"/>, computed only when its answer can have changed.
///
/// The stored half is a SQLite read that probes up to a thousand snapshots
/// (<see cref="SessionRepository.ProgressSeries"/>), and the Experience surface paints
/// every tick the room is open — so calling it straight from a render is the steady-state
/// cost perf audit #1 exists to remove, on a widget that also has to stay out of a game's
/// way (trap 12's neighbourhood).
///
/// **The key is what a new ding can move**, not a timer: the character being followed, the
/// session (its start), and how many dings the live session has seen. A ding changes the
/// live count; a session roll changes the start, and a roll is precisely the event that
/// moves the previous session's dings from the snapshot into the store. Nothing else can
/// add a row to this list while EQBuddy is running.
///
/// One instance per surface, like <see cref="LevelUnlockMemo"/> — a memo is state, and a
/// shared one is the borrowed-instance shape trap 45 is about.
/// </summary>
/// <param name="stored">Every stored session's mined dings for the character being
/// followed. Handed in rather than reached for: the repository lives on the widget, and
/// only the widget knows which (server, character) its rows are keyed by.</param>
/// <param name="characterKey">Who the rows are about. Recomputes on a character switch,
/// which the ding count alone would miss when two characters have the same number.</param>
public sealed class LevelHistoryMemo(
    Func<IReadOnlyList<SessionRepository.ProgressPoint>> stored, Func<string> characterKey)
{
    private string _key = "";
    private DateTime? _sessionStart;
    private int _liveDings = -1;
    private List<LevelHistory.Row> _rows = [];

    /// <summary>Every level-up this character has, newest first — the memoized answer.</summary>
    public IReadOnlyList<LevelHistory.Row> Rows(StatsSnapshot s)
    {
        var key = characterKey();
        if (_key != key || _sessionStart != s.SessionStart || _liveDings != s.Levels.Count)
        {
            _key = key;
            _sessionStart = s.SessionStart;
            _liveDings = s.Levels.Count;
            _rows = LevelHistory.Rows(stored(), s);
        }
        return _rows;
    }
}
