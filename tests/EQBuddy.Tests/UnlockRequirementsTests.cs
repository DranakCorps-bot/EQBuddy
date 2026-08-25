using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Race and class unlocks read out of `/outputfile achievements`, cross-referenced with
/// `/outputfile faction`.
///
/// The fixtures are David's own pair, written by the same game for the same character
/// three minutes apart on 2026-08-25. That matters more than usual here: the two files
/// disagree about how to spell four factions, and only a real pair could have shown that.
/// </summary>
public class UnlockRequirementsTests
{
    private static string[] Achievements() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "achievements", "hateborne.txt"));

    private static FactionsFile.Snapshot Factions()
    {
        var path = Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "factions", "hateborne.txt");
        return new FactionsFile.Snapshot(path, DateTime.Now,
            FactionsFile.Parse(File.ReadAllLines(path)));
    }

    // ---- the faction dump itself ------------------------------------------------

    [Fact]
    public void TheFactionDumpParsesAndMaxedIsPointsToMaxZero()
    {
        var f = Factions();
        Assert.True(f.Standings.Count > 150, $"only {f.Standings.Count} standings parsed");

        // The header row must not become a standing — it fails to parse rather than being
        // special-cased by name.
        Assert.DoesNotContain(f.Standings, s => s.Name == "Name");

        var guk = f["Frogloks of Guk"];
        Assert.NotNull(guk);
        Assert.Equal(2000, guk!.Value);
        Assert.True(guk.Maxed);
        Assert.Equal(1.0, guk.Fraction);

        var heretics = f["Heretics"];
        Assert.NotNull(heretics);
        Assert.Equal(-420, heretics!.Value);
        Assert.False(heretics.Maxed);
        // Negative standing draws as an empty bar, not a negative one.
        Assert.Equal(0, heretics.Fraction);

        var qeynos = f["Guards of Qeynos"];
        Assert.NotNull(qeynos);
        Assert.Equal(1535, qeynos!.Value);
        Assert.Equal(465, qeynos.PointsToMax);
        Assert.False(qeynos.Maxed);
    }

    /// <summary>The ceiling the progress bar divides by, asserted rather than assumed —
    /// every row in a real dump agrees, and if one ever does not, this says so instead of
    /// a bar quietly reading wrong.</summary>
    [Fact]
    public void EveryRowAgreesOnTheCap()
    {
        Assert.All(Factions().Standings,
            s => Assert.Equal(FactionsFile.Cap, s.Value + s.PointsToMax));
    }

    /// <summary>The class code sits in the MIDDLE of the filename, so the rule is a
    /// suffix and never a segment count — trap 48's distinction, on a different file.</summary>
    [Fact]
    public void TheClassCodeInTheFilenameDoesNotStopItBeingAFactionDump()
    {
        Assert.True(FactionsFile.IsFactionDump("Hateborne_neriak-ENC-Factions.txt"));
        Assert.True(FactionsFile.IsFactionDump("Dranak_freeport-Factions.txt"));
        Assert.False(FactionsFile.IsFactionDump("Hateborne_neriak-Inventory.txt"));
        Assert.False(FactionsFile.IsFactionDump("eqlog_Hateborne_neriak.txt"));

        // And the auto-import agrees, so the game's own announcement lands on a reader.
        Assert.Equal(OutputfileKind.Factions,
            OutputfileAutoImport.KindOf("Hateborne_neriak-ENC-Factions.txt"));
    }

    // ---- classifying the criteria -----------------------------------------------

    /// <summary>
    /// The two "will autocomplete" shapes mean opposite things and start the same way.
    /// One is a way the unlock can be handed to you (and the tell that its children are
    /// meaningless); the other is a real consequence of real work, and Half Elf has only
    /// that one.
    /// </summary>
    [Fact]
    public void BypassAndDerivedAreToldApart()
    {
        Assert.Equal(UnlockNeed.Bypass, UnlockRequirements.Classify(
            "This achievement will autocomplete if your character was created as a Barbarian.", false).Need);
        Assert.Equal(UnlockNeed.Bypass, UnlockRequirements.Classify(
            "This achievement can be bypassed using a Race Unlock Token.", false).Need);
        Assert.Equal(UnlockNeed.Bypass, UnlockRequirements.Classify(
            "This achievement will autocomplete if you chose to confirm your Primary Class as a Bard.", false).Need);

        Assert.Equal(UnlockNeed.Derived, UnlockRequirements.Classify(
            "This achievement will autocomplete when you unlock Human or Wood Elf as a race.", false).Need);

        var faction = UnlockRequirements.Classify("Get maximum faction with Dark Bargainers.", true);
        Assert.Equal(UnlockNeed.MaxFaction, faction.Need);
        Assert.Equal("Dark Bargainers", faction.Subject);

        // No trailing period on Beastlord's and Berserker's rows, and no rule may depend
        // on that difference.
        var obtain = UnlockRequirements.Classify("Obtain Skycleaver", false);
        Assert.Equal(UnlockNeed.Obtain, obtain.Need);
        Assert.Equal("Skycleaver", obtain.Subject);
        Assert.Equal("Sash of Ferocity",
            UnlockRequirements.Classify("Obtain Sash of Ferocity.", false).Subject);

        var task = UnlockRequirements.Classify(
            "Complete the 'Aid the Kerrans of Kerra Isle' Task.", false);
        Assert.Equal(UnlockNeed.Task, task.Need);
    }

    // ---- the races ---------------------------------------------------------------

    [Fact]
    public void EveryRaceUnlockIsReadWithItsOwnRequirements()
    {
        var races = UnlockRequirements.Races(AchievementsImport.Parse(Achievements()));
        Assert.Equal(16, races.Count);   // fourteen races, and Human twice

        var barb = Assert.Single(races, r => r.Subject == "Barbarian");
        Assert.False(barb.Complete);
        Assert.Equal(3, barb.Actionable.Count);
        Assert.All(barb.Actionable, c => Assert.Equal(UnlockNeed.MaxFaction, c.Need));
        Assert.Equal((0, 3), barb.Score);

        // Human has two separate paths and they are two separate unlocks, so the race
        // name is left exactly as the dump spells it.
        Assert.Contains(races, r => r.Subject == "Human (Freeport)");
        Assert.Contains(races, r => r.Subject == "Human (Qeynos)");

        // Kerran is a task, not a faction grind — shown, never guessed at.
        var kerran = Assert.Single(races, r => r.Subject == "Kerran");
        Assert.Equal(UnlockNeed.Task, Assert.Single(kerran.Actionable).Need);

        // Iksar wants exactly one faction; a surface that assumed three would be wrong
        // about a sixteenth of its rows.
        Assert.Single(Assert.Single(races, r => r.Subject == "Iksar").Actionable);
    }

    /// <summary>
    /// Half Elf's only rows are Derived and Bypass — there is no work to count. "0 of 0"
    /// reads as a stalled checklist, so the score is null and the surface says the
    /// sentence instead.
    /// </summary>
    [Fact]
    public void AnUnlockWithNoWorkOfItsOwnScoresNothingAndSaysWhy()
    {
        var races = UnlockRequirements.Races(AchievementsImport.Parse(Achievements()));
        var halfElf = Assert.Single(races, r => r.Subject == "Half Elf");

        Assert.Empty(halfElf.Actionable);
        Assert.Null(halfElf.Score);
        Assert.NotNull(halfElf.DerivedNote);
        Assert.Contains("Human or Wood Elf", halfElf.DerivedNote!);
    }

    /// <summary>
    /// **The measurement this whole feature turns on.** "Race Unlock - Dark Elf" is
    /// COMPLETE in David's dump with all three faction criteria flagged complete — and
    /// the faction dump, written three minutes later for the same character, says those
    /// three factions stand at 0/2000, 5/1995 and 0/2000.
    ///
    /// He was created a Dark Elf. The game marked the children when the parent completed.
    /// So a completed unlock's per-criterion flags prove nothing, and a surface that
    /// believed them would tell a player they had maxed three factions they have barely
    /// touched. This is #101 and #193 generalised from classes to races rather than a
    /// second copy of the same guard.
    /// </summary>
    [Fact]
    public void AGrantedUnlocksTicksAreNotEvidenceAndTheFactionDumpProvesIt()
    {
        var races = UnlockRequirements.Races(AchievementsImport.Parse(Achievements()));
        var factions = Factions();

        var darkElf = Assert.Single(races, r => r.Subject == "Dark Elf");
        Assert.True(darkElf.Complete);
        Assert.True(darkElf.Inherited);
        // The dump really does flag all three...
        Assert.All(darkElf.Actionable, c => Assert.True(c.Done));
        // ...and the other dump really does say they are nowhere near maxed.
        foreach (var c in darkElf.Actionable)
        {
            var standing = FactionNames.Resolve(factions, c.Subject);
            Assert.NotNull(standing);
            Assert.False(standing!.Maxed,
                $"{c.Subject} reads maxed in the faction dump; the premise of this test is gone");
        }

        // An INCOMPLETE unlock keeps its per-criterion trust — the control that stops
        // this from simply distrusting everything.
        var wood = Assert.Single(races, r => r.Subject == "Wood Elf");
        Assert.False(wood.Complete);
        Assert.False(wood.Inherited);
    }

    // ---- the names -------------------------------------------------------------

    /// <summary>
    /// Four of the forty-two race requirement lines name a faction the other dump spells
    /// differently. Every one of the forty-two must resolve, or a player sees "not found"
    /// where the answer exists.
    /// </summary>
    [Fact]
    public void EveryFactionARaceUnlockNamesIsFoundInTheFactionDump()
    {
        var races = UnlockRequirements.Races(AchievementsImport.Parse(Achievements()));
        var factions = Factions();

        var missing = races
            .SelectMany(r => r.Actionable.Where(c => c.Need == UnlockNeed.MaxFaction))
            .Select(c => c.Subject)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => FactionNames.Resolve(factions, n) is null)
            .ToList();

        Assert.True(missing.Count == 0,
            "unresolved faction names: " + string.Join(", ", missing));
    }

    /// <summary>The four measured pairs, named individually so a change to the resolver
    /// cannot silently drop one — and one negative, because a resolver that answers
    /// everything would satisfy every assertion above and be useless (trap 39).</summary>
    [Fact]
    public void TheFourMeasuredSpellingMismatchesResolve()
    {
        var f = Factions();

        Assert.Equal("Coalition of Tradefolk",
            FactionNames.Resolve(f, "Coalition of Tradesfolk")?.Name);
        Assert.Equal("The Freeport Militia",
            FactionNames.Resolve(f, "Freeport Militia")?.Name);
        Assert.Equal("Corrupt Qeynos Guards",
            FactionNames.Resolve(f, "Corrupt Qeynos Guard")?.Name);
        Assert.Equal("DaBashers", FactionNames.Resolve(f, "Da Bashers")?.Name);

        // Exact names still work, and a name that is not a faction gets null rather than
        // the nearest thing.
        Assert.Equal("Dark Bargainers", FactionNames.Resolve(f, "Dark Bargainers")?.Name);
        Assert.Null(FactionNames.Resolve(f, "Coalition of Nobody"));
        Assert.Null(FactionNames.Resolve(f, "Guards"));
        Assert.Null(FactionNames.Resolve(f, ""));
        Assert.Null(FactionNames.Resolve(null, "Dark Bargainers"));
    }

    // ---- the classes -------------------------------------------------------------

    [Fact]
    public void ClassUnlocksReadTheirObtainLinesAndUseTheCatalogSpelling()
    {
        var classes = UnlockRequirements.Classes(AchievementsImport.Parse(Achievements()));
        Assert.Equal(16, classes.Count);

        // "Shadowknight" in the dump, "Shadow Knight" everywhere else.
        var shd = Assert.Single(classes, c => c.Subject == "Shadow Knight");
        Assert.DoesNotContain(classes, c => c.Subject == "Shadowknight");
        Assert.False(shd.Complete);
        Assert.Equal(7, shd.Actionable.Count);
        Assert.All(shd.Actionable, c => Assert.Equal(UnlockNeed.Obtain, c.Need));
        Assert.Equal((2, 7), shd.Score);   // both confirmed present in his inventory dump

        // Shaman was bought with a token: complete, every child flagged, nothing earned.
        var shaman = Assert.Single(classes, c => c.Subject == "Shaman");
        Assert.True(shaman.Complete);
        Assert.True(shaman.Inherited);

        // Enchanter is his primary and autocompleted — the other inheritance shape.
        var enc = Assert.Single(classes, c => c.Subject == "Enchanter");
        Assert.True(enc.Inherited);

        // Untouched classes stay untouched: nothing done, nothing inherited.
        var warrior = Assert.Single(classes, c => c.Subject == "Warrior");
        Assert.False(warrior.Inherited);
        Assert.Equal((0, 6), warrior.Score);
    }
}
