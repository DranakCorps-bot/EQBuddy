using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A corrected reward name must not cost the player a turn-in.
///
/// <see cref="AppSettings.SkyQuestCompleted"/> keys on class + reward NAME, so renaming a
/// reward in the catalog silently un-completes it — the item ticks survive because they
/// key on stable ids, the hand-in does not, and nothing on screen says why.
///
/// #206 (bjstrange) is the first: his achievements export named "Shimmering Bracer of
/// Protection" and the catalog carried "Scintillating". eqlwiki serves the Shimmering page
/// and redirects Scintillating to it, and the game agrees with the wiki, so the catalog
/// was uniquely wrong rather than merely different.
/// </summary>
public class SkyRewardRenameTests
{
    private const string OldKey = "Rogue|Scintillating Bracer of Protection";
    private const string NewKey = "Rogue|Shimmering Bracer of Protection";

    [Fact]
    public void ATurnInRecordedUnderTheOldNameSurvivesTheRename()
    {
        var s = new AppSettings();
        s.SkyQuestCompleted.Add(OldKey);

        Assert.True(s.MigrateSkyRewardRenames());

        Assert.Equal([NewKey], s.SkyQuestCompleted);
    }

    /// <summary>#216 (Snagglefern): the second rename, and the first that differs only
    /// by CASE — "Staff of the Magister" against the wiki's "Staff of The Magister".
    /// eqlwiki does not redirect the lower-case form, it 404s, so three Magician rows
    /// linked nowhere.
    ///
    /// A case-only rename is the nastier shape: it looks like nothing, and whether it
    /// costs a turn-in depends entirely on which comparer a given reader used. It must
    /// migrate like any other.</summary>
    [Fact]
    public void ACaseOnlyCorrectionAlsoKeepsTheTurnIn()
    {
        var s = new AppSettings();
        s.SkyQuestCompleted.Add("Magician|Staff of the Magister");

        Assert.True(s.MigrateSkyRewardRenames());

        Assert.Equal(["Magician|Staff of The Magister"], s.SkyQuestCompleted);
        Assert.Contains("Staff of The Magister", s.SkyQuestCompleted[0], StringComparison.Ordinal);
    }

    /// <summary>Every rename in the table must name a reward the catalog actually ships
    /// under its NEW name — otherwise the migration quietly moves a record onto a key
    /// nothing will ever look up, which is worse than leaving it alone.</summary>
    [Fact]
    public void EveryRenameTargetExistsInTheShippedCatalog()
    {
        var rewards = SkyQuestDefaults.Items
            .Select(r => r.Reward)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var target in new[]
                 { "Shimmering Bracer of Protection", "Staff of The Magister" })
            Assert.Contains(target, rewards);

        // And the old spellings are gone from the catalog, or the correction did not land.
        foreach (var stale in new[]
                 { "Scintillating Bracer of Protection", "Staff of the Magister" })
            Assert.DoesNotContain(stale, rewards);
    }

    [Fact]
    public void NothingToMigrateChangesNothingAndReportsSo()
    {
        // The return value drives whether settings are re-saved on load. Reporting a
        // change that did not happen writes the whole file for no reason on every launch.
        var s = new AppSettings();
        s.SkyQuestCompleted.Add("Bard|Mask of Song");

        Assert.False(s.MigrateSkyRewardRenames());
        Assert.Equal(["Bard|Mask of Song"], s.SkyQuestCompleted);
    }

    [Fact]
    public void MigratingTwiceIsSafe()
    {
        var s = new AppSettings();
        s.SkyQuestCompleted.Add(OldKey);

        s.MigrateSkyRewardRenames();
        Assert.False(s.MigrateSkyRewardRenames());
        Assert.Equal([NewKey], s.SkyQuestCompleted);
    }

    [Fact]
    public void AlreadyHoldingBothNamesDoesNotDuplicate()
    {
        var s = new AppSettings();
        s.SkyQuestCompleted.Add(OldKey);
        s.SkyQuestCompleted.Add(NewKey);

        s.MigrateSkyRewardRenames();

        Assert.Equal([NewKey], s.SkyQuestCompleted);
    }

    [Fact]
    public void TheCatalogNowCarriesTheNameTheGameAndTheWikiBothUse()
    {
        var rogue = SkyQuestDefaults.Items
            .Where(i => i.ClassName == "Rogue")
            .Select(i => i.Reward)
            .Distinct()
            .ToList();

        Assert.Contains("Shimmering Bracer of Protection", rogue);
        Assert.DoesNotContain("Scintillating Bracer of Protection", rogue);
    }
}
