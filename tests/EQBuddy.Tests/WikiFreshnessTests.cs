using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The words and the one rule behind the Drops tab's wiki re-check (#226). The bug was
/// SILENT staleness — a corrected wiki page could not clear a ✦ for a week, and nothing on
/// screen said why — so the caption is the half of the fix that prevents the next one,
/// and the 30 s rule is the half that keeps a volunteer wiki from being hammered.
/// </summary>
public class WikiFreshnessTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    private static MobLookupResult Read(ItemLookupState state, TimeSpan ago, string title = "Lockjaw") =>
        new(new MobInfo { Name = title, PageTitle = title }, state, Now - ago);

    [Fact]
    public void ARecentReadRefusesARecheckAndAnOlderOneAllowsIt()
    {
        Assert.False(WikiFreshness.CanRecheck(Now - TimeSpan.FromSeconds(5), inFlight: false, Now));
        Assert.True(WikiFreshness.CanRecheck(Now - TimeSpan.FromSeconds(31), inFlight: false, Now));
        // In flight always refuses, however old the last read was.
        Assert.False(WikiFreshness.CanRecheck(Now - TimeSpan.FromDays(8), inFlight: true, Now));
        // Never read at all: asking is the cure for Pending, so it is always allowed.
        Assert.True(WikiFreshness.CanRecheck(null, inFlight: false, Now));
    }

    /// <summary>Bevel, 2026-08-22: no "read" anywhere in the caption — on a surface whose
    /// vocabulary is a red ✦, "wiki read just now" hears as "wiki RED just now".</summary>
    [Fact]
    public void TheCaptionNeverSaysRead()
    {
        foreach (var state in new[] { ItemLookupState.Live, ItemLookupState.Cached, ItemLookupState.StaleCache })
            Assert.DoesNotContain("read", WikiFreshness.Caption(Read(state, TimeSpan.FromDays(2)), false, Now));
    }

    [Fact]
    public void TheCaptionBucketsTimeAndNeverTicksInSeconds()
    {
        Assert.Equal("wiki just now", WikiFreshness.Caption(Read(ItemLookupState.Live, TimeSpan.FromSeconds(20)), false, Now));
        Assert.Equal("wiki 3m ago", WikiFreshness.Caption(Read(ItemLookupState.Cached, TimeSpan.FromMinutes(3)), false, Now));
        Assert.Equal("wiki 2h ago", WikiFreshness.Caption(Read(ItemLookupState.Cached, TimeSpan.FromHours(2.9)), false, Now));
        Assert.Equal("wiki 8d ago", WikiFreshness.Caption(Read(ItemLookupState.Cached, TimeSpan.FromDays(8)), false, Now));
        // Two reads twenty seconds apart produce the SAME caption — trap 8, the signature
        // must not see a value that moves on a clock.
        Assert.Equal(
            WikiFreshness.SignatureToken(Read(ItemLookupState.Cached, TimeSpan.FromMinutes(3)), false, Now),
            WikiFreshness.SignatureToken(Read(ItemLookupState.Cached, TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(20)), false, Now));
    }

    [Fact]
    public void AnOfflineRecheckSaysSoAndKeepsTheOldReadsAge()
    {
        // The #217 rule on the caption: a failed re-check is not "nothing new" and not
        // "not checked" — it is the OLD read, and the caption says so with its age.
        Assert.Equal("wiki unreachable \u2014 showing 8d ago",
            WikiFreshness.Caption(Read(ItemLookupState.StaleCache, TimeSpan.FromDays(8)), false, Now));
        Assert.Equal("wiki unreachable",
            WikiFreshness.Caption(new MobLookupResult(null, ItemLookupState.Offline, null), false, Now));
        Assert.Equal("checking\u2026", WikiFreshness.Caption(Read(ItemLookupState.Cached, TimeSpan.FromDays(8)), true, Now));
        Assert.Equal("wiki not read yet", WikiFreshness.Caption(null, false, Now));
        Assert.Equal("no wiki page", WikiFreshness.Caption(new MobLookupResult(null, ItemLookupState.NotFound, null), false, Now));
    }

    [Fact]
    public void TheTooltipNamesTheServedPage()
    {
        // A redirect can answer with a different page than the one asked for (trap 3),
        // and Innoruk's lookup resolving to a Lore page (#226) is exactly the bug the
        // served title makes visible.
        var tip = WikiFreshness.RecheckTip(Read(ItemLookupState.Cached, TimeSpan.FromDays(2), "Innoruuk (God)"), false, Now);
        Assert.Contains("Innoruuk (God)", tip);
        Assert.Equal("Checked just now.", WikiFreshness.RecheckTip(Read(ItemLookupState.Live, TimeSpan.FromSeconds(3)), false, Now));
        Assert.StartsWith("Reading", WikiFreshness.RecheckTip(null, true, Now));
    }
}
