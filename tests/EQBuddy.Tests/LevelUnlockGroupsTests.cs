using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The per-class split of a level's unlocks — Bevel's rules (Helm-signed 2026-08-23) as
/// assertions rather than as prose in a doc comment.
///
/// Three surfaces draw this list and all three call <see cref="LevelUnlockGroups"/>, so
/// these are the only tests that exist for the DECISIONS: what a group contains, what
/// order the groups come in, when a fold is worth drawing, and — the two that are easiest
/// to lose in a refactor and invisible in a screenshot — that a shared spell appears under
/// BOTH classes and that a class which gains nothing keeps its row.
///
/// **Why the empty-group rule needs a test at all.** Dropping a class that gains nothing
/// looks like tidying and is indistinguishable, on screen, from that class not being one
/// of yours. A Warrior has no spell table at any level; "Nothing new at 35" is the honest
/// and complete answer, and an absence is not.
/// </summary>
public class LevelUnlockGroupsTests
{
    private static AaCatalogEntry Aa(string name, string category, string? cls = null,
        int? maxRank = null) =>
        new() { Name = name, Category = category, Class = cls, MaxRank = maxRank, LevelRequirement = 35 };

    private static LevelUnlockSet Set(
        IEnumerable<AaCatalogEntry>? aas = null, IEnumerable<SpellUnlock>? spells = null) =>
        new([.. aas ?? []], [.. spells ?? []]);

    [Fact]
    public void EachClassGetsItsOwnAaAndSpellRows()
    {
        var set = Set(
            aas: [Aa("Innate Regeneration", "Class", "Druid"), Aa("Mend Companion", "Class", "Monk")],
            spells: [new SpellUnlock("Greater Healing", ["Druid"]),
                     new SpellUnlock("Eye of Zomm", ["Monk"])]);

        var groups = LevelUnlockGroups.ByClass(set, ["Druid", "Monk"]);

        Assert.Equal(["Druid", "Monk"], groups.Select(g => g.ClassName));
        Assert.Equal(["Innate Regeneration", "Greater Healing"],
            groups[0].Rows.Select(r => r.Name));
        Assert.Equal(["Mend Companion", "Eye of Zomm"], groups[1].Rows.Select(r => r.Name));
    }

    /// <summary>The caller's order, not an alphabetical one: the group that opens first is
    /// the class the player's own list names first.</summary>
    [Fact]
    public void GroupOrderIsTheCallersClassOrder()
    {
        var set = Set(spells:
        [
            new SpellUnlock("Greater Healing", ["Druid"]),
            new SpellUnlock("Eye of Zomm", ["Monk"]),
        ]);

        Assert.Equal(["Monk", "Druid"],
            LevelUnlockGroups.ByClass(set, ["Monk", "Druid"]).Select(g => g.ClassName));
    }

    /// <summary>Class-agnostic AA rows (General, Archetype, Special) belong to no class, so
    /// they get their own group rather than being repeated under every one — which would
    /// triple them on the three-class character Legends actually allows. Last, because a
    /// class-agnostic row is the least specific answer to "what do I get".</summary>
    [Fact]
    public void ClassAgnosticAasBecomeOneSharedGroupAtTheEnd()
    {
        var set = Set(aas:
        [
            Aa("Natural Durability", "General"),
            Aa("Mend Companion", "Archetype"),
            Aa("Innate Regeneration", "Class", "Druid"),
        ]);

        var groups = LevelUnlockGroups.ByClass(set, ["Druid"]);

        Assert.Equal(["Druid", LevelUnlockGroups.SharedGroup], groups.Select(g => g.ClassName));
        Assert.Equal(["Natural Durability", "Mend Companion"], groups[^1].Rows.Select(r => r.Name));
    }

    [Fact]
    public void NoClassAgnosticRowsMeansNoSharedGroup()
    {
        var set = Set(aas: [Aa("Innate Regeneration", "Class", "Druid")]);

        Assert.Equal(["Druid"],
            LevelUnlockGroups.ByClass(set, ["Druid"]).Select(g => g.ClassName));
    }

    /// <summary>One unlock, two answers to "what does this class get". Hiding it from the
    /// second class to avoid repeating it answers a different question.</summary>
    [Fact]
    public void ASpellTwoClassesShareAppearsUnderBoth()
    {
        var set = Set(spells: [new SpellUnlock("Snare", ["Druid", "Ranger"])]);

        var groups = LevelUnlockGroups.ByClass(set, ["Druid", "Ranger"]);

        Assert.Equal("Snare", Assert.Single(groups[0].Rows).Name);
        Assert.Equal("Snare", Assert.Single(groups[1].Rows).Name);
        // And the row says so on both sides — the value column is the same fact, not a
        // per-group rewrite.
        Assert.Equal("Druid/Ranger spell", groups[0].Rows[0].Value);
        Assert.Equal("Druid/Ranger spell", groups[1].Rows[0].Value);
    }

    /// <summary>Bevel, Helm-signed: *"Class with nothing at next (Warrior/Monk/Berserker
    /// have no spell tables): keep the class row, 'nothing new at N'. Do not drop the
    /// group."*</summary>
    [Fact]
    public void AClassThatGainsNothingKeepsAnEmptyGroup()
    {
        var set = Set(spells: [new SpellUnlock("Greater Healing", ["Druid"])]);

        var groups = LevelUnlockGroups.ByClass(set, ["Warrior", "Druid", "Monk"]);

        Assert.Equal(["Warrior", "Druid", "Monk"], groups.Select(g => g.ClassName));
        Assert.True(groups[0].IsEmpty);
        Assert.False(groups[1].IsEmpty);
        Assert.True(groups[2].IsEmpty);
    }

    [Fact]
    public void ClassMatchingIsCaseInsensitive()
    {
        var set = Set(
            aas: [Aa("Innate Regeneration", "Class", "druid")],
            spells: [new SpellUnlock("Greater Healing", ["DRUID"])]);

        var group = Assert.Single(LevelUnlockGroups.ByClass(set, ["Druid"]));

        Assert.Equal(2, group.Rows.Count);
    }

    /// <summary>The value column is <see cref="LevelUnlockText"/>'s, not a second opinion:
    /// an AA names its class (or its class-agnostic category) plus its rank count, and a
    /// spell names its classes plus the word that keeps the two row kinds apart.</summary>
    [Fact]
    public void RowValuesComeFromTheSharedWording()
    {
        var set = Set(
            aas: [Aa("Innate Regeneration", "Class", "Druid", maxRank: 3),
                  Aa("Mend Companion", "Archetype")],
            spells: [new SpellUnlock("Greater Healing", ["Druid"])]);

        var groups = LevelUnlockGroups.ByClass(set, ["Druid"]);

        Assert.Equal("Druid · 3 ranks", groups[0].Rows[0].Value);
        Assert.Equal("Druid spell", groups[0].Rows[1].Value);
        Assert.Equal("Archetype", groups[1].Rows[0].Value);
    }

    /// <summary>No classes and nothing class-agnostic is genuinely nothing — the surface's
    /// cue to draw no fold at all rather than an empty one.</summary>
    [Fact]
    public void NoClassesAndNoSharedRowsIsNoGroups()
    {
        Assert.Empty(LevelUnlockGroups.ByClass(
            Set(spells: [new SpellUnlock("Greater Healing", ["Druid"])]), []));
    }

    /// <summary>*"One inferred class = names under the heading, no lone expander."* The
    /// rule is asked rather than counted at each call site, so the two desktops and the
    /// phone cannot disagree about when a fold appears.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void GroupingIsWorthChromeOnlyAboveOnePlayerClass(int count, bool expected)
    {
        var groups = Enumerable.Range(0, count)
            .Select(i => new LevelUnlockGroup($"Class{i}", [])).ToList();

        Assert.Equal(expected, LevelUnlockGroups.WorthGrouping(groups));
    }

    /// <summary>
    /// **"Any class" does not vote** — Bevel, Helm-signed 2026-08-23 1:05 PM CT: *"'Any
    /// class' is a shared bucket, not a player class. It does not trip the one-class
    /// no-expander rule. One player class with content stays flat names."*
    ///
    /// The first build counted GROUPS, so a single-class character who reached a level
    /// carrying a General AA grew two expanders — and the rule was always about how many of
    /// the player's own classes there are to choose between, which is still one.
    /// </summary>
    [Fact]
    public void TheSharedBucketDoesNotTurnOneClassIntoAFold()
    {
        LevelUnlockGroup WithRows(string name) => new(name, [("Endure Magic", "Druid spell")]);

        // One class WITH content, plus the shared bucket: flat names, no expanders.
        Assert.False(LevelUnlockGroups.WorthGrouping(
            [WithRows("Druid"), WithRows(LevelUnlockGroups.SharedGroup)]));
        // Two player classes: a fold, shared bucket or not.
        Assert.True(LevelUnlockGroups.WorthGrouping(
            [WithRows("Druid"), WithRows("Cleric"), WithRows(LevelUnlockGroups.SharedGroup)]));
    }

    /// <summary>
    /// The exception Bevel names in the same breath: when the one class gains NOTHING and
    /// the shared bucket holds the level's only rows, the fold IS drawn — so the rows sit
    /// under a heading saying whose they are — and the bucket is what opens.
    ///
    /// This is not hypothetical: it is what a WARRIOR sees at almost every milestone, and
    /// it is the state in `docs/screenshots/theme-inline-progress.png`.
    /// </summary>
    [Fact]
    public void AnEmptyLoneClassStillFoldsSoTheSharedRowsAreAttributed()
    {
        List<LevelUnlockGroup> groups =
        [
            new("Warrior", []),
            new(LevelUnlockGroups.SharedGroup, [("Double Riposte", "Archetype · 3 ranks")]),
        ];

        Assert.True(LevelUnlockGroups.WorthGrouping(groups));
        // ...and the bucket opens, not the empty class.
        Assert.Equal(1, LevelUnlockGroups.DefaultOpenIndex(groups));
    }

    /// <summary>
    /// Which group opens. *"First inferred class open"* read as *the first class with
    /// something to show* — the same group in every ordinary case, and a different one in
    /// the case that made this a method.
    ///
    /// **Found from a written prediction before a screenshot, not from a bug report.** A
    /// Warrior whose next milestone is an Archetype AA produces exactly the second case
    /// below: an empty Warrior group, then the shared bucket holding the only row. Opening
    /// by index would have shown "Warrior — nothing new at 15" above a collapsed heading,
    /// with the one row the preview exists for two clicks away.
    /// </summary>
    [Fact]
    public void TheFirstGroupWithSomethingToShowIsTheOneThatOpens()
    {
        LevelUnlockGroup Full(string name) =>
            new(name, [("Endure Magic", "Druid spell")]);
        LevelUnlockGroup Bare(string name) => new(name, []);

        Assert.Equal(0, LevelUnlockGroups.DefaultOpenIndex([Full("Druid"), Full("Cleric")]));
        // The case that earned the method: empty class first, the only row behind it.
        Assert.Equal(1, LevelUnlockGroups.DefaultOpenIndex(
            [Bare("Warrior"), Full(LevelUnlockGroups.SharedGroup)]));
        Assert.Equal(2, LevelUnlockGroups.DefaultOpenIndex(
            [Bare("Warrior"), Bare("Monk"), Full("Druid")]));
        // Nothing anywhere: -1 rather than 0, so a caller cannot open a group that has
        // nothing in it by accident.
        Assert.Equal(-1, LevelUnlockGroups.DefaultOpenIndex([Bare("Warrior"), Bare("Monk")]));
        Assert.Equal(-1, LevelUnlockGroups.DefaultOpenIndex([]));
    }

    [Fact]
    public void TheEmptyGroupReadsAsAnsweredRatherThanBroken()
    {
        Assert.Equal("Nothing new at 35", LevelUnlockGroups.NothingNew(35));
    }
}
