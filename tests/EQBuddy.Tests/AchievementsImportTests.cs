using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The /outputfile achievements import (#88): C/I-flagged sections →
/// achievements → criteria, Sky rewards resolved from completed "Obtain X" lines with
/// drift-tolerant matching, and an apply step that only ever adds progress. Fixture is
/// typical-usual-chaos's real file from the discussion.</summary>
public class AchievementsImportTests
{
    private static string[] Fixture() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "achievements", "averaj.txt"));

    [Fact]
    public void ParsesSectionsAchievementsAndFlaggedCriteria()
    {
        var all = AchievementsImport.Parse(Fixture());
        Assert.True(all.Count > 100, $"only {all.Count} achievements parsed");

        var barb = Assert.Single(all, a => a.Name == "Race Unlock - Barbarian");
        Assert.Equal("Untapped Potential: Races", barb.Section);
        Assert.True(barb.Complete);
        Assert.Contains(barb.Criteria, c =>
            c.Text == "Get maximum faction with Wolves of the North." && c.Complete);

        // Incomplete achievements keep their per-criterion detail.
        var bard = Assert.Single(all, a => a.Name == "Primary Class Unlock - Bard");
        Assert.False(bard.Complete);
        Assert.Contains(bard.Criteria, c => c.Text == "Obtain Amulet of the Fae." && c.Complete);
        Assert.Contains(bard.Criteria, c => c.Text == "Obtain Mask of Song." && !c.Complete);
    }

    [Fact]
    public void CompletedObtainsResolveToSkyRewardsIncompleteOnesDoNot()
    {
        var checklist = new List<SkyQuestChecklistItem>
        {
            new() { Id = "1", ClassName = "Bard", Reward = "Amulet of the Fae", QuestItem = "x" },
            new() { Id = "2", ClassName = "Bard", Reward = "Mask of Song", QuestItem = "y" },
            new() { Id = "3", ClassName = "Bard", Reward = "Harmonic Spear", QuestItem = "z" },
            new() { Id = "4", ClassName = "Beastlord", Reward = "Windhowl/Spirit Render", QuestItem = "w" },
        };
        var (matches, _, _) = AchievementsImport.SkyRewards(AchievementsImport.Parse(Fixture()), checklist);

        // Bard: Amulet of the Fae is C in the fixture, Mask of Song is I.
        Assert.Contains(matches, m => m is { ClassName: "Bard", Reward: "Amulet of the Fae" });
        Assert.DoesNotContain(matches, m => m.Reward == "Mask of Song");
        // Drift: the fixture's incomplete "Obtain Spear of Harmony." must NOT match,
        // but a completed one would — prove the matcher itself handles the drift.
        Assert.True(AchievementsImport.NamesMatch("Harmonic Spear", "Spear of Harmony"));
        Assert.True(AchievementsImport.NamesMatch("Windhowl/Spirit Render", "Windhowl and Spirit Render"));
        Assert.False(AchievementsImport.NamesMatch("Wind Rune Azia", "Wind Rune Fana"));
    }

    /// <summary>#101 (Frankthetankk): the player's PRIMARY class unlock is granted at
    /// creation, and the dump marks its reward criteria complete without the items
    /// existing. A completed unlock whose "will autocomplete" criterion is itself
    /// complete was granted, not earned — its obtains are skipped and reported.
    /// An INCOMPLETE unlock's individually-earned criteria stay trustworthy.</summary>
    [Fact]
    public void AutoGrantedPrimaryClassUnlockNeverImports()
    {
        var checklist = new List<SkyQuestChecklistItem>
        {
            new() { Id = "1", ClassName = "Paladin", Reward = "Girdle of Faith", QuestItem = "x" },
            new() { Id = "2", ClassName = "Bard", Reward = "Amulet of the Fae", QuestItem = "y" },
        };
        var lines = new[]
        {
            "Untapped Potential: Classes",
            // Auto-granted: achievement complete AND the autocomplete criterion complete.
            "C\tPrimary Class Unlock - Paladin",
            "C\t\tObtain Girdle of Faith.",
            "C\t\tThis achievement will autocomplete if you chose to confirm your Primary Class as a Paladin.",
            // Earned progress on an incomplete unlock: individually trustworthy.
            "I\tPrimary Class Unlock - Bard",
            "C\t\tObtain Amulet of the Fae.",
            "I\t\tThis achievement will autocomplete if you chose to confirm your Primary Class as a Bard.",
        };
        var (matches, _, autoGranted) =
            AchievementsImport.SkyRewards(AchievementsImport.Parse(lines), checklist);


        Assert.DoesNotContain(matches, m => m.Reward == "Girdle of Faith");
        Assert.Contains(autoGranted, g => g.Contains("Girdle of Faith"));
        Assert.Contains(matches, m => m is { ClassName: "Bard", Reward: "Amulet of the Fae" });
    }

    /// <summary>
    /// #193 (wizen) — the OTHER way to get a class unlock without doing the quests.
    ///
    /// 1.57.3 guarded the primary-class auto-grant and #101 read as fixed, but wizen kept
    /// reporting ticked Bard rewards he had never obtained. He bought **Primary Class
    /// Unlock Tokens**, and a token unlock announces itself with a different completed
    /// criterion: the "will autocomplete" line stays INCOMPLETE and the "can be bypassed
    /// using a Primary Class Unlock Token" line is the one flagged complete. The guard
    /// looked only at the first, so the token case walked straight through it.
    ///
    /// The three cases below are wizen's own achievements dump, pasted on 2026-08-20,
    /// verbatim and unedited — and it is a genuine control set rather than one example,
    /// which is what makes it safe to key on (CLAUDE.md: never match on one person's file):
    ///
    ///   Druid      confirmed as primary  autocomplete C, bypass I  → skip
    ///   Bard       bought with a token   autocomplete I, bypass C  → skip
    ///   Berserker  never unlocked        everything I              → untouched
    ///
    /// The Berserker row is the one that keeps this honest: it proves the fix does not
    /// simply skip every class unlock it sees.
    /// </summary>
    [Fact]
    public void TokenUnlockedClassNeverImports()
    {
        var checklist = new List<SkyQuestChecklistItem>
        {
            new() { Id = "1", ClassName = "Druid", Reward = "Shillelagh", QuestItem = "a" },
            new() { Id = "2", ClassName = "Bard", Reward = "Mask of Song", QuestItem = "b" },
            new() { Id = "3", ClassName = "Bard", Reward = "Amulet of the Fae", QuestItem = "c" },
            new() { Id = "4", ClassName = "Berserker", Reward = "Skycleaver", QuestItem = "d" },
        };
        var lines = new[]
        {
            "Untapped Potential: Classes",
            // 1. Confirmed as primary — the case 1.57.3 already handled.
            "C	Primary Class Unlock - Druid",
            "C		Obtain Shillelagh.",
            "C		This achievement will autocomplete if you chose to confirm your Primary Class as a Druid.",
            "I		This achievement can be bypassed using a Primary Class Unlock Token.",
            // 2. Bought with a token — the case that was still importing.
            "C	Primary Class Unlock - Bard",
            "C		Obtain Mask of Song.",
            "C		Obtain Amulet of the Fae.",
            "I		This achievement will autocomplete if you chose to confirm your Primary Class as a Bard.",
            "C		This achievement can be bypassed using a Primary Class Unlock Token.",
            // 3. Never unlocked — nothing to skip and nothing to import.
            "I	Primary Class Unlock - Berserker",
            "I		Obtain Skycleaver",
            "I		This achievement will autocomplete if you chose to confirm your Primary Class as a Berserker.",
            "I		This achievement can be bypassed using a Primary Class Unlock Token.",
        };

        var (matches, _, autoGranted) =
            AchievementsImport.SkyRewards(AchievementsImport.Parse(lines), checklist);

        // THE SAME DUMP names which classes wizen HOLDS, and that is a wider question
        // than which rewards may be imported — deliberately so. Druid was confirmed as
        // primary and Bard was token-bought: neither EARNED its Sky rewards, and he is
        // both all the same. Berserker is incomplete, so it is a class he is working
        // towards rather than one he has.
        //
        // These rows have been in this file since #101 and were read only to REFUSE
        // things. Reading them for what they plainly say is what lets the app know a
        // character is more than one class without being told (`CharacterClasses`).
        Assert.Equal(["Druid", "Bard"],
            AchievementsImport.UnlockedClasses(AchievementsImport.Parse(lines)));

        // Nothing from either granted class is imported…
        Assert.DoesNotContain(matches, m => m.ClassName is "Druid" or "Bard");
        // …and both are REPORTED rather than silently dropped, which is what lets a
        // player see why their rewards did not tick.
        Assert.Contains(autoGranted, g => g.Contains("Shillelagh"));
        Assert.Contains(autoGranted, g => g.Contains("Mask of Song"));
        Assert.Contains(autoGranted, g => g.Contains("Amulet of the Fae"));
        // The unearned class is untouched by all of it.
        Assert.DoesNotContain(autoGranted, g => g.Contains("Skycleaver"));
        Assert.DoesNotContain(matches, m => m.ClassName == "Berserker");
    }

    [Fact]
    public void ApplyAddsWithoutEverRegressing()
    {
        var settings = new AppSettings
        {
            SkyQuestChecklist =
            [
                new() { Id = "1", ClassName = "Berserker", Reward = "Skycleaver", QuestItem = "x" },
                new() { Id = "2", ClassName = "Berserker", Reward = "Cudgel of the Fool", QuestItem = "y", Acquired = true },
            ],
            SkyQuestCompleted = ["Berserker|Cudgel of the Fool"],
        };
        var matches = new List<SkyRewardMatch>
        {
            new("Berserker", "Skycleaver", "Skycleaver"),
            new("Berserker", "Cudgel of the Fool", "Cudgel of the Fool"),
        };

        Assert.Equal(1, AchievementsImport.Apply(matches, settings));   // only the new one counts
        Assert.Contains("Berserker|Skycleaver", settings.SkyQuestCompleted);
        Assert.All(settings.SkyQuestChecklist, i => Assert.True(i.Acquired));

        // Re-applying is a no-op — the import can teach, never untick.
        Assert.Equal(0, AchievementsImport.Apply(matches, settings));
    }
}
