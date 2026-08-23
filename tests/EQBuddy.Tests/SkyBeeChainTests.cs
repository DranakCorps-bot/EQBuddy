using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **The Island 6 bee chain, all four links** (#109, Frankthetankk — reported 2026-08-22
/// after testing v1.99.5, with a wiki page, a verbatim <c>/consider</c> and a verbatim slain
/// line for each).
///
/// The catalog carried the last two links and named the third as a trigger source it had no
/// entry for. So the first two bees fell through to the zone's default clock and got
/// countdowns for a spawn they do not have.
///
/// **His ask was to mark BOTH missing bees triggered, and eqlwiki says only one of them is.**
/// That is the tie-breaker rule doing its job (CLAUDE.md: match the wiki; departing needs
/// decisive evidence, not an expectation):
///
/// <list type="bullet">
/// <item><b>Bzzazzt</b> — <c>respawn_time = 12 hours</c>. It is the OPENER, and something has
/// to start a chain. A triggered opener would be a chain that can never begin.</item>
/// <item><b>Bazzzazzt</b> — <c>respawn_time = Triggered</c>, description "these spawn
/// immediately after killing Bzzazzt". Exactly as he reported.</item>
/// </list>
///
/// **His evidence and the wiki do not actually conflict**, which is the part worth writing
/// down: everything he has is from personal Sky instances, and he says plainly he has never
/// played the open world. Nothing respawns in a cleared instance, so a 12-hour open-world
/// clock is invisible from where he is standing. Both accounts are true about different
/// places, and the catalog describes the world.
///
/// Both are <c>multiSpawn</c>: three wasps share each name at island start. That is not a
/// detail — it is what makes a kill-to-kill gap here meaningless, and it is the same shape
/// as the #135 charm bug on this very island (two Bzzazzt, one charmed, one attacking).
/// </summary>
public class SkyBeeChainTests
{
    private static readonly SpawnCatalog Catalog = SpawnCatalog.LoadEmbedded();

    private static SpawnEntry Bee(string name) =>
        Catalog.Zones
            .Single(z => z.Zone.Equals("Plane of Sky", StringComparison.OrdinalIgnoreCase))
            .Named.Single(e => e.Name == name);

    /// <summary>The chain, end to end, in the order a player walks it. Asserted as a chain
    /// rather than four separate rows because the LINKS are the fact: a missing entry in the
    /// middle is what let the first two fall through to the zone default.</summary>
    [Fact]
    public void AllFourLinksOfTheBeeChainAreCatalogued()
    {
        Assert.False(Bee("Bzzazzt").IsTriggered);          // the opener, on a real clock
        Assert.Equal("Bzzazzt", Bee("Bazzzazzt").TriggeredBy);
        Assert.Equal("Bazzzazzt", Bee("Bzzzt").TriggeredBy);
        Assert.Equal("Bzzzt", Bee("Bazzt Zzzt").TriggeredBy);

        foreach (var triggered in new[] { "Bazzzazzt", "Bzzzt", "Bazzt Zzzt" })
        {
            Assert.True(Bee(triggered).IsTriggered);
            // A triggered link has no cycle, so it must carry no number to count down.
            Assert.Null(Bee(triggered).RespawnSeconds);
        }
    }

    /// <summary>**The opener keeps its clock, and this is the assertion that would have
    /// failed if the reporter's ask had been taken at face value.** eqlwiki gives Bzzazzt
    /// 12 hours; a chain whose first link is triggered is a chain that never starts.</summary>
    [Fact]
    public void TheOpenerIsOnTheWikisTwelveHourClockNotTriggered()
    {
        var opener = Bee("Bzzazzt");

        Assert.Equal(TimeSpan.FromHours(12).TotalSeconds, opener.RespawnSeconds);
        Assert.False(opener.IsTriggered);
        Assert.Equal("", opener.TriggeredBy);
        Assert.Equal("eqlwiki", opener.Source);
        // Not measured by us — it is the wiki's number, and Trusted would disable the
        // learning that could one day correct it from a real open-world camp.
        Assert.False(opener.Trusted);
    }

    /// <summary>Three wasps share each of the first two names, so no gap between two kills
    /// is ever this camp's respawn. `SpawnTimers` refuses to learn from a multiSpawn entry,
    /// which is the mechanism that stops a 12-hour clock being overwritten by a three-minute
    /// one — the exact failure that #109 opened with, one island over.</summary>
    [Fact]
    public void BothOpeningLinksAreMultiSpawn()
    {
        Assert.True(Bee("Bzzazzt").MultiSpawn);
        Assert.True(Bee("Bazzzazzt").MultiSpawn);
    }

    /// <summary>Every note says where its claim came from. A curated catalog whose rows
    /// cannot be traced is one nobody can correct — and this row in particular disagrees
    /// with the person who reported it, so the reason has to be readable.</summary>
    [Fact]
    public void EveryBeeNoteCitesItsSource()
    {
        foreach (var name in new[] { "Bzzazzt", "Bazzzazzt", "Bzzzt", "Bazzt Zzzt" })
        {
            var bee = Bee(name);
            Assert.Equal("eqlwiki", bee.Source);
            Assert.Contains("eqlwiki", bee.Note);
        }
        // And the one that contradicts the reporter says so in as many words.
        Assert.Contains("NOT triggered", Bee("Bzzazzt").Note);
    }
}
