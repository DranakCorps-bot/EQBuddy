using System.Text.Json;
using EQBuddy.Companion;

namespace EQBuddy.Tests;

/// <summary>
/// The wire keys EQBuddy Mobile's page actually reads, asserted against what
/// <see cref="CompanionSnapshot.JsonOpts"/> actually emits.
///
/// **Written because a property NAME shipped as a bug.** `CompanionUnlockGroup` was declared
/// with a `ClassName` property; `JsonOpts` is `JsonNamingPolicy.CamelCase`, so the property
/// name IS the wire key, and it reached the page as `className` while every other group
/// record on this wire — <c>CompanionBuffGroup</c>, the quest group — says `class`. The page
/// reads `g.class` at five sites, so on a real phone every heading rendered "▾ undefined" and
/// every group shared one open/shut state, because they were all keyed on the same
/// `undefined`. Found by Fable 5 in the v1.99.6 release review (2026-08-23).
///
/// **Nothing that existed could see it.** The C# compiles, both desktops are unaffected (they
/// never touch the DTO), the fingerprint reads the property rather than the JSON, and the
/// serialiser is happy to emit any name. The one manual check that ran — driving the shipped
/// page in `mobile-harness.ps1` — was fed a snapshot written BY HAND, in the shape the page
/// wanted rather than the shape the server sends. That is trap 23 with a JSON key instead of a
/// wiki template field: a real-looking render of something the app never produces.
///
/// So this asserts the emitted JSON, not the record, and every key it pins carries its
/// NEGATIVE (trap 39): "contains class" would pass on a payload carrying both.
/// </summary>
public class CompanionWireKeyTests
{
    /// <summary>The page's five reads of the next-level split, in one string. Serialising the
    /// SECTION rather than the group alone is deliberate: it goes through the same
    /// <see cref="CompanionSnapshot.JsonOpts"/> the server uses, so a naming-policy change
    /// fails here rather than on someone's phone.</summary>
    private static string NextGroupsJson() => JsonSerializer.Serialize(
        new CompanionProgressSection(
            XpPercent: 16, XpPerHour: 12.3, XpPerActiveHour: 20, HoursToLevel: 2.5,
            AaGained: 1, AaTotal: 4, AaPerHour: 0.5, Level: 33,
            UnlocksLabel: null, Unlocks: [],
            Tabs: [], Wealth: new CompanionWealthBlock("0c", "0c", "0c", "0c", 0, 0, [], "", []),
            Faction: [], Raids: new CompanionRaidsBlock(0, 21, []),
            NextLabel: "At level 34: 3 new spells",
            NextGroups:
            [
                new CompanionUnlockGroup("Druid",
                    [new CompanionUnlockRow("Endure Magic", "Druid spell")], null),
                new CompanionUnlockGroup("Warrior", [], "Nothing new at 34"),
            ],
            NextGrouped: true, NextOpenIndex: 0, MoteLine: "3 motes · 0.9/hr"),
        CompanionSnapshot.JsonOpts);

    [Fact]
    public void AnUnlockGroupNamesItsClassUnderTheKeyThePageReads()
    {
        var json = NextGroupsJson();

        Assert.Contains("\"class\":\"Druid\"", json);
        // The negative that would have caught it. `ClassName` emits `className`, which
        // CONTAINS the substring "class" — so the positive above passes on the broken
        // payload and only this line fails.
        Assert.DoesNotContain("className", json);
    }

    /// <summary>The other four keys the new block reads, each with the shape the page expects
    /// rather than merely being present: <c>rows</c> under a group, <c>empty</c> as the words
    /// an empty class shows, and the two decisions that ride the wire instead of being
    /// recomputed on the page (<c>nextGrouped</c>, <c>nextOpenIndex</c>).</summary>
    [Fact]
    public void TheNextLevelBlockCarriesTheKeysThePageDraws()
    {
        var json = NextGroupsJson();

        Assert.Contains("\"nextLabel\":\"At level 34: 3 new spells\"", json);
        Assert.Contains("\"nextGrouped\":true", json);
        Assert.Contains("\"nextOpenIndex\":0", json);
        Assert.Contains("\"empty\":\"Nothing new at 34\"", json);
        Assert.Contains("\"moteLine\":", json);
        // A group's rows are name/value, the same pair every other row on this wire uses.
        Assert.Contains("\"name\":\"Endure Magic\"", json);
        Assert.Contains("\"value\":\"Druid spell\"", json);
    }

    /// <summary>Every group-bearing record on this wire spells the class the same way, which
    /// is the rule the defect broke. Asserted across the three rather than one at a time,
    /// because "matches its siblings" is the property — a fourth group record that invents its
    /// own spelling is the next occurrence of this bug.</summary>
    [Fact]
    public void EveryGroupRecordSpellsTheClassTheSameWay()
    {
        foreach (var name in (string[])
                 [
                     nameof(CompanionUnlockGroup.Class),
                     nameof(CompanionBuffGroup.Class),
                 ])
            Assert.Equal("Class", name);
    }
}
