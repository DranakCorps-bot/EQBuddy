using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **#217 ask 3 (Frankthetankk), and the first thing built under David's rule that eqlwiki
/// is the SOURCE and EQBuddy is the tool that helps it update** (2026-08-22).
///
/// The ask is not about item drop rates. It is about recording, on the CREATURE's own wiki
/// page, that the NPC itself was confirmed rare by its in-game <c>/consider</c> text — a
/// fact about the world, true for everyone, which is exactly the class of thing the rule
/// says to hand the player as a paste-ready edit rather than store in a catalog of our own.
///
/// The reporter carried the destination question to the wiki admins himself:
/// <c>{{Namedmobpage}}</c> has no rare-spawn parameter, they were positive about adding one,
/// and until it lands the interim home is the <c>description</c> field, matching hand-edited
/// precedent (Packmaster Dledsh's page already reads "Rare NPC" there). His wording,
/// unchanged: <i>"Confirmed as a rare spawn via in-game /consider"</i>.
///
/// **Three constraints, from him and from David, and every test below is one of them:**
/// never a paste-over of an editor's existing prose · never inferred from kill counts ·
/// never carried across characters.
///
/// The fourth thing under test is the separation from <see cref="WikiContribution.SuggestRarity"/>,
/// which was the wrong answer to this ask and was rejected for a reason worth keeping: a
/// trash mob can drop an ultra-rare item and a rare spawn can drop its piece every time.
/// </summary>
public class RareSpawnContributionTests
{
    private static MobSummary Conned(int considers, int rare) =>
        new("Magus Rokyl", 3, 3, 20, 0, 0, [new MobLoot("Runed Bolster Belt", 1, 33.3)])
        { Zone = "Najena", Considers = considers, RareConsiders = rare };

    private static readonly MobLookupResult Missing = new(null, ItemLookupState.NotFound, null);

    private static MobLookupResult PageWith(params string[] drops) => new(
        new MobInfo
        {
            IsCreaturePage = true,
            Name = "Magus Rokyl", PageTitle = "Magus Rokyl",
            Drops = drops.Select(d => (d, "Common")).ToList(),
        },
        ItemLookupState.Cached, DateTime.UtcNow);

    private static string Export(MobSummary mob, MobLookupResult? lookup) =>
        WikiContribution.BuildExport([new WikiContribution.MobObservation(mob, lookup)],
            "Dranak", "test", "Najena", new DateTime(2026, 8, 22, 21, 0, 0));

    // ---- the note itself ----

    /// <summary>One con is proof, and that is the whole difference from a drop rate. The
    /// game printed the word; there is no sample to be thin. A ten-con bar here would be
    /// statistics applied to something that was never a measurement.</summary>
    [Fact]
    public void OneRareConIsEnough()
    {
        var note = WikiContribution.RareSpawnNote(Conned(considers: 1, rare: 1));

        Assert.NotNull(note);
        Assert.Contains("a rare creature", note);
        Assert.Contains("your one /consider", note);
    }

    /// <summary>**Never inferred from kill counts.** The creature was killed three times
    /// and looted; without a con that said "rare", the pack says nothing at all.</summary>
    [Fact]
    public void KillsAndLootNeverImplyRarity()
    {
        Assert.Null(WikiContribution.RareSpawnNote(Conned(considers: 0, rare: 0)));
        Assert.Null(WikiContribution.RareSpawnNote(Conned(considers: 4, rare: 0)));
    }

    /// <summary>"I conned it four times and it never said rare" and "I never conned it" are
    /// different facts, and NEITHER is evidence of ordinariness. The pack claims no absence
    /// in either case — it simply has nothing to say.</summary>
    [Fact]
    public void AnAbsenceIsNeverClaimed()
    {
        var export = Export(Conned(considers: 4, rare: 0), Missing);

        Assert.DoesNotContain("rare spawn", export);
        Assert.DoesNotContain("not rare", export);
        Assert.DoesNotContain(WikiContribution.RareSpawnDescription, export);
    }

    /// <summary>Both numbers, always. Same-named spawns are not all rare, so 2-of-7 is a
    /// materially different claim from 7-of-7 and the person pasting it onto someone else's
    /// wiki is the one who gets to weigh it.</summary>
    [Fact]
    public void PartialAgreementIsReportedAsPartial()
    {
        var note = WikiContribution.RareSpawnNote(Conned(considers: 7, rare: 2));

        Assert.Contains("2 of your 7 /considers", note);
        // "all 7", not "all" — "called" contains it, which is how this assertion first
        // failed for a reason that had nothing to do with the behaviour under test.
        Assert.DoesNotContain("all 7", note);
    }

    [Fact]
    public void FullAgreementSaysSo() =>
        Assert.Contains("all 4 of your /considers",
            WikiContribution.RareSpawnNote(Conned(considers: 4, rare: 4)));

    // ---- what reaches the paste block ----

    /// <summary>A page that does not exist yet has no prose to overwrite, so the line goes
    /// straight into the skeleton's own description field.</summary>
    [Fact]
    public void ANewPageCarriesItInTheDescriptionField()
    {
        var export = Export(Conned(considers: 2, rare: 2), Missing);

        Assert.Contains("| description = " + WikiContribution.RareSpawnDescription, export);
    }

    /// <summary>**Never a paste-over of an editor's existing prose.** EQBuddy cannot read
    /// the description field (`EqlWikiMobs.Parse` does not parse it), so it cannot know
    /// whether the page already says this — which makes ADD the only honest instruction. A
    /// "replace the field with" block would be a tool telling a player to delete a
    /// stranger's writing sight unseen.</summary>
    [Fact]
    public void AnExistingPageIsToldToADDNeverReplace()
    {
        var export = Export(Conned(considers: 2, rare: 2), PageWith("Some Other Thing"));

        Assert.Contains("ADD this to the description field", export);
        Assert.Contains("never replace what is already written there", export);
        Assert.Contains(WikiContribution.RareSpawnDescription, export);
        // The instruction the loot half uses for an EMPTY field must not leak onto prose.
        Assert.DoesNotContain("Replace the empty description", export);
    }

    /// <summary>It says it is a stopgap, and why. Once the real template parameter lands,
    /// a pack that only ever said "put it in the description" would have quietly created
    /// folklore.</summary>
    [Fact]
    public void ThePasteBlockSaysWhyTheDescriptionFieldAndNotAParameter()
    {
        var export = Export(Conned(considers: 2, rare: 2), PageWith("Some Other Thing"));

        Assert.Contains("has no rare-spawn field yet", export);
        Assert.Contains("Packmaster Dledsh", export);
    }

    /// <summary>**It is said ONCE, and not in the stat block** — which is the correction
    /// the first real paste-block read produced. The stat block is gated on kills and heads
    /// itself "thin sample, for your notes rather than the wiki yet" below ten; that caveat
    /// is true of money, faction and level bounds, which are sampled. Con-rarity is not
    /// sampled — the game printed the word. Repeating it there put a paste-it instruction
    /// and a don't-paste-it-yet caveat on one fact, in one section, three lines apart.
    ///
    /// Asserted on a THREE-kill observation, which is the case that produced the
    /// contradiction; at ten kills the caveat changes and the bug would have hidden.</summary>
    [Fact]
    public void ItIsSaidOnceAndNotUnderTheThinSampleCaveat()
    {
        var export = Export(Conned(considers: 3, rare: 3), PageWith("Some Other Thing"));

        Assert.Contains("thin sample, for your notes rather than the wiki yet", export);
        Assert.Contains("ADD this to the description field", export);
        // Once, in the contribution block — never again under the caveat.
        Assert.Equal(1, Occurrences(export, "the game called this"));
    }

    /// <summary>The edit summary describes the WHOLE edit. A summary naming only the drops,
    /// under a block that also offers a description line, leaves the player to notice the
    /// gap — and the half they would forget is the one an editor is likeliest to
    /// question.</summary>
    [Fact]
    public void TheEditSummaryCoversTheDescriptionToo()
    {
        Assert.Contains("observed drops (1 item, 3 kills); rare spawn confirmed via /consider.",
            Export(Conned(considers: 3, rare: 3), PageWith("Some Other Thing")));
        // And says nothing about it when there is nothing to say.
        Assert.Contains("observed drops (1 item, 3 kills).",
            Export(Conned(considers: 3, rare: 0), PageWith("Some Other Thing")));
    }

    private static int Occurrences(string haystack, string needle)
    {
        var n = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

    // ---- the separation that matters ----

    /// <summary>Con-rarity and the pack's ITEM rarity label are different axes, and wiring
    /// one into the other was the answer this ask was nearly given. A rare spawn's drop is
    /// still labelled by its own observed rate — here 1 in 3, which is under the ten-kill
    /// bar, so it gets no label at all while the creature is still called rare.</summary>
    [Fact]
    public void CreatureRarityNeverBecomesAnItemRarityLabel()
    {
        var export = Export(Conned(considers: 3, rare: 3), PageWith("Some Other Thing"));

        Assert.Contains("a rare creature", export);   // the CREATURE: said
        Assert.DoesNotContain("drare", export);       // the ITEM: no rarity span at all
        Assert.Null(WikiContribution.SuggestRarity(33.3, kills: 3));
    }

    /// <summary>**Never carried across characters** — and it is true by construction rather
    /// than by a rule someone has to remember, because the count lives on the session's own
    /// mob aggregate. A fresh <see cref="SessionStats"/> is a fresh character's evidence.
    /// Asserted through the real parser and the real event, not a hand-built summary.</summary>
    [Fact]
    public void TheCountComesFromTheLogAndDiesWithTheSession()
    {
        var stats = new SessionStats();
        foreach (var line in new[]
        {
            "[Thu Aug 06 21:42:47 2026] Magus Rokyl - a rare creature - scowls at you, ready to attack -- looks like it would wipe the floor with you! (Lvl: 51)",
            "[Thu Aug 06 21:44:10 2026] Magus Rokyl scowls at you, ready to attack -- looks like it would wipe the floor with you! (Lvl: 51)",
        })
            stats.Apply(LogParser.Parse(line)!);

        var mob = stats.Snapshot().Mobs.Single(m => m.Name == "Magus Rokyl");
        Assert.Equal(2, mob.Considers);
        Assert.Equal(1, mob.RareConsiders);
        Assert.Contains("1 of your 2 /considers", WikiContribution.RareSpawnNote(mob));

        // A different character is a different session, and it starts with nothing.
        Assert.Empty(new SessionStats().Snapshot().Mobs);
    }
}
