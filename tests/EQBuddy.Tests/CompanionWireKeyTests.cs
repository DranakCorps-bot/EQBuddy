using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>The Level-ups block's three keys (#240), pinned the day they were written
    /// for the reason the class above exists: the page reads <c>levelUps</c>,
    /// <c>levelUpsLabel</c> and a row's <c>tip</c>, and nothing between the record and the
    /// phone would object to any other spelling. The negatives are the C# names that would
    /// emit something plausible-but-wrong — <c>SincePrevious</c> as a row field, or a
    /// <c>Tooltip</c> that reaches the page as <c>tooltip</c>.</summary>
    [Fact]
    public void TheLevelUpsBlockCarriesTheKeysThePageDraws()
    {
        var json = JsonSerializer.Serialize(
            new CompanionProgressSection(
                XpPercent: 16, XpPerHour: 12.3, XpPerActiveHour: 20, HoursToLevel: 2.5,
                AaGained: 1, AaTotal: 4, AaPerHour: 0.5, Level: 24,
                UnlocksLabel: null, Unlocks: [],
                Tabs: [], Wealth: new CompanionWealthBlock("0c", "0c", "0c", "0c", 0, 0, [], "", []),
                Faction: [], Raids: new CompanionRaidsBlock(0, 21, []),
                LevelUpsLabel: "Level-ups (3) · last Aug 23",
                LevelUps:
                [
                    new CompanionLevelUpRow("Level 24", "Aug 23, 7:05 PM",
                        "1d 22h since the previous level-up"),
                    new CompanionLevelUpRow("Level 22", "Aug 21, 8:14 PM", null),
                ]),
            CompanionSnapshot.JsonOpts);

        // The label up to its separator: `JsonOpts` escapes non-ASCII, so the "·" the
        // player sees rides as · and asserting the rendered character here would be
        // asserting the ENCODER rather than the key.
        Assert.Contains("\"levelUpsLabel\":\"Level-ups (3) ", json);
        // The list's own key, spelled out rather than left to the label's substring.
        Assert.Contains("\"levelUps\":[", json);
        Assert.Contains("\"name\":\"Level 24\"", json);
        Assert.Contains("\"value\":\"Aug 23, 7:05 PM\"", json);
        Assert.Contains("\"tip\":\"1d 22h since the previous level-up\"", json);
        Assert.DoesNotContain("tooltip", json);
        Assert.DoesNotContain("sincePrevious", json);
        // The oldest row has no gap. `JsonOpts` is WhenWritingNull, so its `tip` is not on
        // the wire at all — and the page tests `if (r.tip)`, which reads an absent key and
        // a null the same way. What must NOT happen is an empty string, which is truthy
        // nowhere but sets a blank hover box on the row that has nothing to say.
        Assert.Equal(1, Regex.Matches(json, "\"tip\":").Count);
        Assert.DoesNotContain("\"tip\":\"\"", json);
    }

    /// <summary>
    /// The quest section's class fields, added 2026-08-23 with the multi-class fix. Pinned
    /// here the same day they were written rather than after a reporter finds them: the
    /// LAST field added to this wire (`CompanionUnlockGroup.ClassName`) reached the page as
    /// `className` while the page read `class`, and the manual check that was supposed to
    /// catch it used a hand-typed payload in the shape the page wanted.
    /// </summary>
    [Fact]
    public void TheQuestSectionsClassFieldsUseTheKeysThePageReads()
    {
        var json = JsonSerializer.Serialize(
            new CompanionQuestsSection(
                Tabs: [], CatalogStamp: "s", Catalog: null, Mine: [], MineMore: 0,
                Owned: new Dictionary<string, int>(), Tracked: [], Hidden: [],
                Completed: new Dictionary<string, int>(), Classes: [],
                InferredClass: "Druid",
                CharacterClasses: ["Warrior", "Druid", "Monk"],
                ClassSourceLabel: "from your achievements",
                Epics: new CompanionChecklistSection(0, 0, []),
                Sky: new CompanionChecklistSection(0, 0, [])),
            CompanionSnapshot.JsonOpts);

        Assert.Contains("\"characterClasses\":[\"Warrior\",\"Druid\",\"Monk\"]", json);
        Assert.Contains("\"classSourceLabel\":\"from your achievements\"", json);
        // The old single-class field rides along for one release, because an open phone
        // runs the page it downloaded weeks ago (trap 32) and that page reads it.
        Assert.Contains("\"inferredClass\":\"Druid\"", json);
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
