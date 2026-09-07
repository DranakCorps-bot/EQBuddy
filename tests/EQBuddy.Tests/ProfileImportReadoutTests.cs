using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The words the import says. They are tested because they are PROMISES about a player's
/// data — "copied, never moved" is <c>LEGACY-V1.md</c>'s public commitment said to the one
/// player it applies to, at the moment it applies — and the WPF layer has no unit tests, so
/// a sentence spelled inline in the dialog would be a promise nothing can check.
/// </summary>
public class ProfileImportReadoutTests
{
    /// <summary>The two facts the consent screen may not omit: that nothing is moved, and
    /// that declining is a real answer. A default-checked box with neither of those beside
    /// it is a decision nobody made.</summary>
    [Fact]
    public void TheConsentScreenSaysBothThatNothingMovesAndThatDecliningIsAnOption()
    {
        Assert.Contains("never moved", ProfileImportReadout.CopyNeverMove);
        Assert.Contains("keeps working", ProfileImportReadout.CopyNeverMove);
        Assert.Contains("unticked", ProfileImportReadout.StartFresh);
        Assert.Contains("start fresh", ProfileImportReadout.StartFresh);
    }

    /// <summary>Trap 24 inside one process: a second window titled plainly "EQBuddy" is a
    /// window a capture harness cannot tell from the widget. It carries what it is about,
    /// the way the shell's title carries its room.</summary>
    [Fact]
    public void TheWindowTitleIsNotABareEqbuddy()
    {
        Assert.NotEqual("EQBuddy", ProfileImportReadout.WindowTitle);
        Assert.Contains("EQBuddy", ProfileImportReadout.WindowTitle);
        Assert.Contains("1.x", ProfileImportReadout.WindowTitle);
    }

    /// <summary>"Close EQBuddy 1.x first" — the plan's own wording for the one refusal a
    /// player can clear, and it says how to clear it rather than only that it happened.
    /// </summary>
    [Fact]
    public void TheRunningRefusalNamesTheProductAndSaysHowToClearIt()
    {
        Assert.Contains("Close EQBuddy 1.x", ProfileImportReadout.LegacyRunningHeadline);
        Assert.Contains("start EQBuddy Evolved again", ProfileImportReadout.LegacyRunning);
        Assert.Contains("Nothing has been copied", ProfileImportReadout.LegacyRunning);
    }

    [Theory]
    [InlineData(512, "512 bytes")]
    [InlineData(2048, "2 KB")]
    [InlineData(4_404_019, "4.2 MB")]
    public void SizesAreRenderedAtAScaleAPlayerCanJudge(long bytes, string expected) =>
        Assert.Equal(expected, ProfileImportReadout.Size(bytes));

    [Fact]
    public void OneFileIsNotOneFiles()
    {
        Assert.StartsWith("1 file ·", ProfileImportReadout.Volume(1, 10));
        Assert.StartsWith("2 files ·", ProfileImportReadout.Volume(2, 10));
    }

    /// <summary>A directory rolls its contents up; naming a wiki cache's thousands of files
    /// is not information (trap 50's other half — a list that cannot be complete should not
    /// pretend to be a list).</summary>
    [Fact]
    public void ADirectoryRowRollsUpAndAFileRowDoesNot()
    {
        Assert.Equal("wiki — 3 files, 2 KB",
            ProfileImportReadout.ManifestRow(new ProfileImportEntry("wiki", true, 3, 2048)));
        Assert.Equal("settings.json — 512 bytes",
            ProfileImportReadout.ManifestRow(new ProfileImportEntry("settings.json", false, 1, 512)));
    }

    /// <summary>The report distinguishes the two outcomes — an import that landed and a
    /// player who started fresh are different facts, and a report that read the same for
    /// both would be trap 43's silence wearing a sentence.</summary>
    [Fact]
    public void TheReportSaysWhichOfTheTwoAnswersThisProfileGave()
    {
        var imported = new ProfileImportMarker
        {
            Decision = "imported", Files = 12, Bytes = 4_404_019,
            SourceVersion = "1.99.18", WhenUtc = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
        };
        var declined = new ProfileImportMarker { Decision = "declined" };

        Assert.Contains("came over", ProfileImportReadout.ReportHeadline(imported));
        Assert.Contains("12 files", ProfileImportReadout.Report(imported));
        Assert.Contains("4.2 MB", ProfileImportReadout.Report(imported));
        Assert.Contains("v1.99.18", ProfileImportReadout.Report(imported));
        Assert.Contains("untouched", ProfileImportReadout.Report(imported));

        Assert.Contains("started fresh", ProfileImportReadout.ReportHeadline(declined));
        Assert.DoesNotContain("copied from", ProfileImportReadout.Report(declined));
    }

    /// <summary>An unversioned v1 profile does not get a "(v)" with nothing in it.</summary>
    [Fact]
    public void AMarkerWithNoVersionPrintsNoVersion() =>
        Assert.DoesNotContain("(v",
            ProfileImportReadout.Report(new ProfileImportMarker { Decision = "imported" }));

    /// <summary>The undo is STRUCTURAL — clear the Evolved profile — so the report says
    /// what it is rather than offering a button that would have to delete a profile to
    /// work. It also says the thing that makes that safe: v1 still has all of it.</summary>
    [Fact]
    public void TheReportNamesTheStartFreshAlternativeAndWhyItIsSafe()
    {
        Assert.Contains("EQBuddy Evolved folder", ProfileImportReadout.ReportUndo);
        Assert.Contains("EQBuddy 1.x still has all of it", ProfileImportReadout.ReportUndo);
    }
}
