using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Level-ups list is a SQLite read that probes up to a thousand stored snapshots, and
/// the Experience surface paints every tick the room is open. So "how often does it read"
/// is a behaviour, not an implementation detail — and a memo that quietly stops memoizing
/// looks exactly like one that works.
/// </summary>
public class LevelHistoryMemoTests
{
    private static readonly DateTime Aug22 = new(2026, 8, 22, 21, 30, 0);
    private static readonly DateTime Aug23 = new(2026, 8, 23, 19, 5, 0);

    private sealed class Store
    {
        public int Reads;
        public List<SessionRepository.ProgressPoint> Points = [];
        public IReadOnlyList<SessionRepository.ProgressPoint> Read()
        {
            Reads++;
            return Points;
        }
    }

    private static StatsSnapshot Session(DateTime start, params (DateTime Time, int Level)[] dings) =>
        new()
        {
            SessionStart = start,
            Levels = [.. dings.Select(d => new TimedDetail(d.Time, $"Level {d.Level}"))],
        };

    /// <summary>A quiet tick — no ding, same session, same character — reads nothing. This
    /// is the ordinary case: once a second, for as long as the room is open.</summary>
    [Fact]
    public void AQuietTickDoesNotTouchTheStore()
    {
        var store = new Store();
        var memo = new LevelHistoryMemo(store.Read, () => "dranak_legends");
        var s = Session(Aug23.AddHours(-1), (Aug23, 24));

        for (var i = 0; i < 20; i++) memo.Rows(s);

        Assert.Equal(1, store.Reads);
    }

    /// <summary>A ding is the one thing that can add a row while EQBuddy is running, so it
    /// is one of the three things that recompute.</summary>
    [Fact]
    public void ADingRecomputes()
    {
        var store = new Store();
        var memo = new LevelHistoryMemo(store.Read, () => "dranak_legends");
        var start = Aug23.AddHours(-2);

        Assert.Single(memo.Rows(Session(start, (Aug23, 24))));
        var after = memo.Rows(Session(start, (Aug23, 24), (Aug23.AddHours(1), 25)));

        Assert.Equal(2, store.Reads);
        Assert.Equal([25, 24], after.Select(r => r.Level));
    }

    /// <summary>The session roll is the OTHER half of the same event: the live list clears
    /// and the dings it held move into the store. Keying on the ding count alone would see
    /// 1 → 0 and recompute anyway, but keying on the session start is what makes a roll
    /// with no dings — the common case — recompute too, so the newly archived session's
    /// rows are picked up.</summary>
    [Fact]
    public void ASessionRollRecomputesEvenWithNoDingsInEitherSession()
    {
        var store = new Store();
        var memo = new LevelHistoryMemo(store.Read, () => "dranak_legends");

        Assert.Empty(memo.Rows(Session(Aug22)));
        store.Points = [new SessionRepository.ProgressPoint(Aug22, 0, [(Aug22, 23)])];
        var after = memo.Rows(Session(Aug23));

        Assert.Equal(2, store.Reads);
        Assert.Equal([23], after.Select(r => r.Level));
    }

    /// <summary>Following a different character re-reads. Two characters can be one ding
    /// into the same session length, and answering the second with the first one's list is
    /// the failure the character key exists to prevent.</summary>
    [Fact]
    public void ACharacterSwitchRecomputes()
    {
        var store = new Store();
        var who = "dranak_legends";
        var memo = new LevelHistoryMemo(store.Read, () => who);
        var s = Session(Aug23.AddHours(-1), (Aug23, 24));

        memo.Rows(s);
        who = "aludra_legends";
        memo.Rows(s);

        Assert.Equal(2, store.Reads);
    }
}
