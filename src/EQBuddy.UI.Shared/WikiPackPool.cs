using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The pack windows' pooled data source (#217 ask 2): stored sessions read ONCE per
/// window open, re-folded with the live snapshot only when the live session's mob set
/// actually changes. Both desktops hold one of these instead of each hand-rolling the
/// cache — the memo rule from trap 8 (nothing recomputed per tick without a signature)
/// applied to a fold that walks every stored session.
///
/// The signature deliberately keys on what the POOL consumes — kills, loot entries,
/// considers — and not on anything that moves on the clock, so an idle pack window
/// costs one string-join per tick and nothing else.
/// </summary>
public sealed class WikiPackPool(Func<IReadOnlyList<MobHistory.SessionMobs>> loadStoredRows)
{
    private IReadOnlyList<MobHistory.SessionMobs>? _stored;
    private string _liveSig = "\0";   // never matches, so the first Refresh always pools

    public IReadOnlyList<MobSummary> Mobs { get; private set; } = [];
    public PoolScope Scope { get; private set; } = new([], [], 0, null, null);

    /// <summary>Re-fold if the live picture moved (or on the first call). Returns true
    /// when the pool changed, so a caller can skip repaint work.</summary>
    public bool Refresh(StatsSnapshot live, string character, string server, long liveRowId)
    {
        var sig = string.Join("|", live.Mobs.Select(m =>
            $"{m.Name}:{m.Kills}:{m.Loot.Count}:{m.Considers}:{m.Factions.Count}"));
        if (sig == _liveSig && _stored is not null) return false;
        _liveSig = sig;
        _stored ??= loadStoredRows();
        (Mobs, Scope) = MobHistory.Pool(_stored, live, character, server, liveRowId);
        return true;
    }
}
