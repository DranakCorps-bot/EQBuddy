using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// "What opened up at this level" and "what opens up at the next one", memoized per
/// (level, classes).
///
/// Both answers are read every UI tick — the Progress surface's header cue is one of
/// them — and both change only on a ding or a class pick, so recomputing them per tick
/// is the kind of steady-state allocation perf audit #1 exists to remove.
///
/// **It lives here because there were two of it.** `ProgressCardView` (WPF) and
/// `EQBuddy.Avalonia/MainWindow` (since deleted) each carried the same three memo fields and the same
/// method, hand-copied — the shape CLAUDE.md warns about, and the one that carried #122
/// and #152 to Linux and macOS after Windows had already paid for both. The immediate
/// reason to extract it now is smaller and more concrete: the Progress THEME takes the
/// Progress CARD off the widget, so the WPF window can no longer reach this answer
/// through a card view it no longer owns — and "keep an unattached card around as a memo
/// holder" is not a design, it is a leftover.
/// </summary>
public sealed class LevelUnlockMemo(
    Func<StatsSnapshot, IReadOnlyList<string>> classes, Func<int?>? storedLevel = null)
{
    private int? _dingLevel;
    private string _dingClasses = "";
    private LevelUnlockSet _ding = LevelUnlockSet.Empty;

    private int? _nextLevel;
    private string _nextClasses = "";
    private (int Level, LevelUnlockSet Unlocks)? _next;

    /// <summary>AAs and spells newly available at the session's latest level-up; empty
    /// when the session hasn't leveled.</summary>
    public LevelUnlockSet Ding(StatsSnapshot s)
    {
        if (s.LastLevel is not { } level) return LevelUnlockSet.Empty;
        var picked = classes(s);
        var key = string.Join(",", picked);
        if (_dingLevel != level || _dingClasses != key)
        {
            _dingLevel = level;
            _dingClasses = key;
            _ding = LevelUnlocks.UnlocksAt(picked, level);
        }
        return _ding;
    }

    /// <summary>The next-milestone preview, anchored to the last level the log announced,
    /// else the ledger's persisted one; null while no level is KNOWN at all. One
    /// computation site for every surface that shows it — they must agree on where "At N:"
    /// starts (trap 4: one fact, two derivations, silent drift).
    ///
    /// Always null when no <c>storedLevel</c> was supplied and the log has not announced
    /// one, which is the Avalonia widget's case today: it draws the ding list and no
    /// preview, so it passes nothing and asks nothing.</summary>
    public (int Level, LevelUnlockSet Unlocks)? Next(StatsSnapshot s)
    {
        var known = s.LastLevel ?? storedLevel?.Invoke();
        var picked = classes(s);
        var key = string.Join(",", picked);
        if (_nextLevel != known || _nextClasses != key)
        {
            _nextLevel = known;
            _nextClasses = key;
            _next = known is { } kl ? LevelUnlocks.Next(picked, kl) : null;
        }
        return _next;
    }
}
