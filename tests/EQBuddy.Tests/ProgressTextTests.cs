using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The Progress card's header and summary prose, shared with the Progress breakout
/// window — one builder so the two surfaces can never drift apart. These pin the
/// words; LevelUnlocks/LevelUnlockText cover the unlock rows separately.
/// </summary>
public class ProgressTextTests
{
    private static readonly DateTime T0 = new(2026, 8, 17, 20, 0, 0);

    [Fact]
    public void HeaderIsJustXpUntilAnythingElseHappens()
    {
        var s = new StatsSnapshot { XpPercent = 12.34 };
        Assert.Equal("12.3% xp", ProgressText.Header(s, dingCount: 0));
    }

    [Fact]
    public void HeaderCarriesLevelsTheDingCueAndAa()
    {
        var s = new StatsSnapshot
        {
            XpPercent = 3.5,
            Levels = [new TimedDetail(T0, "You have reached level 30!")],
            AaGained = 2,
        };
        Assert.Equal("3.5% xp, +1 lvl (4 new), +2 aa", ProgressText.Header(s, dingCount: 4));
    }

    [Fact]
    public void HeaderOmitsTheDingCueWhenNothingUnlocked()
    {
        // "+1 lvl (0 new)" would read as a broken counter — a ding with no unlocks
        // just says the level.
        var s = new StatsSnapshot
        {
            XpPercent = 3.5,
            Levels = [new TimedDetail(T0, "You have reached level 30!")],
        };
        Assert.Equal("3.5% xp, +1 lvl", ProgressText.Header(s, dingCount: 0));
    }

    [Fact]
    public void SummaryBaseLineNamesTicksRatesAndSkillUps()
    {
        var s = new StatsSnapshot
        {
            XpTicks = 7, XpPerHour = 5.25, XpPerActiveHour = 8.5, SkillUpTotal = 3,
        };
        Assert.Equal("7 xp gains · 5.3%/hr · 8.5% active · 3 skill-ups",
            ProgressText.Summary(s));
    }

    [Fact]
    public void SummaryTakesTheAvaloniaBuildsAsciiSeparator()
    {
        // The Avalonia build passes " - " (its file-wide plain-ASCII convention under
        // fonts Wine/Linux may lack) — same builder, different glyph, so a wording fix
        // can never reach one UI and skip the other.
        var s = new StatsSnapshot
        {
            XpTicks = 7, XpPerHour = 5.25, XpPerActiveHour = 8.5, SkillUpTotal = 3,
        };
        Assert.Equal("7 xp gains - 5.3%/hr - 8.5% active - 3 skill-ups",
            ProgressText.Summary(s, " - "));
    }

    [Fact]
    public void SummaryAddsRecentAaAndEtaLines()
    {
        var s = new StatsSnapshot
        {
            XpTicks = 7, XpPerHour = 5.0, XpPerActiveHour = 8.0, SkillUpTotal = 0,
            Recent = new RecentRates(TimeSpan.FromMinutes(15), true, 1.2, 4.8, 0, 0, 0, 0),
            AaGained = 1, AaPerHour = 0.5, AaTotal = 12,
            HoursToLevel = 2.25,
        };
        var lines = ProgressText.Summary(s).Split('\n');
        Assert.Equal("Last 15m: 4.8%/hr", lines[1]);
        // Singular "point" — the plural is grammar, not a fixed string.
        Assert.Equal("1 AA point · 0.5 AA/hr (now 12 unspent)", lines[2]);
        Assert.Equal("Next level in ~2h 15m at this pace", lines[3]);
    }

    [Fact]
    public void SummaryPacesEachDingFromThePreviousOne()
    {
        // First ding is measured from session start, the second from the first —
        // "how long did that level take", not "how long since login".
        var s = new StatsSnapshot
        {
            XpTicks = 1, SessionStart = T0,
            Levels =
            [
                new TimedDetail(T0.AddMinutes(40), "You have reached level 30!"),
                new TimedDetail(T0.AddMinutes(95), "You have reached level 31!"),
            ],
        };
        var last = ProgressText.Summary(s).Split('\n')[^1];
        Assert.Equal(
            $"You have reached level 30! at {T0.AddMinutes(40):h:mm tt} (40m), " +
            $"You have reached level 31! at {T0.AddMinutes(95):h:mm tt} (55m)", last);
    }

    [Fact]
    public void EveryCounterTheHeaderCanNameCountsAsContent()
    {
        // The empty test must cover every fact the header can announce, or the window
        // contradicts itself — "+2 aa" above "nothing seen yet" was the review catch:
        // AA points accrue with no xp tick, no purchase, no ding.
        Assert.False(ProgressText.HasContent(new StatsSnapshot()));
        Assert.True(ProgressText.HasContent(new StatsSnapshot { XpTicks = 1 }));
        Assert.True(ProgressText.HasContent(new StatsSnapshot { AaGained = 2 }));
        Assert.True(ProgressText.HasContent(new StatsSnapshot
            { Levels = [new TimedDetail(T0, "You have reached level 30!")] }));
        Assert.True(ProgressText.HasContent(new StatsSnapshot
            { SkillUps = [new SkillDetail("Meditate", Ups: 1, Value: 100)] }));
        Assert.True(ProgressText.HasContent(new StatsSnapshot
            { AaAbilities = [new AaAbilityInfo("Adamant Will", 1, T0)] }));
    }

    [Fact]
    public void SessionNewAasSplitsTheLedgerAtSessionStart()
    {
        // The ledger holds the character's whole AA history; only announcements at or
        // after session start count as "learned this session". No session start (a
        // log with no timestamped activity yet) honestly claims nothing.
        var s = new StatsSnapshot
        {
            SessionStart = T0,
            AaAbilities =
            [
                new AaAbilityInfo("Adamant Will", 1, T0.AddDays(-3)),
                new AaAbilityInfo("Combat Fury", 2, T0),
                new AaAbilityInfo("Planar Power", 1, T0.AddMinutes(50)),
            ],
        };
        Assert.Equal(["Combat Fury", "Planar Power"],
            ProgressText.SessionNewAas(s).Select(a => a.Name));
        Assert.Empty(ProgressText.SessionNewAas(new StatsSnapshot
            { AaAbilities = [new AaAbilityInfo("Adamant Will", 1, T0)] }));
    }

}
