using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The guide surface's WORDS, and the share-back door's URL (Fable plan §2 D3, lock 4).
///
/// <para>The door's whole promise is that it sends nothing: the body is composed from the
/// CATALOG, it carries no log line, no character name and no inventory, and the player reads
/// it in their own browser before anything is posted. That promise is asserted here as a
/// negative with real personal data present, because "we did not add it" is a claim only a
/// test that plants the data can make (trap 34's shape: forbid the wrong thing AND require
/// the right one).</para>
/// </summary>
public sealed class GuidePresentationTests
{
    private static GuideObjective Authored() => new()
    {
        Id = "stone-amulet", Order = 1, ObjectiveType = "Loot",
        Title = "Loot the Stone Amulet from the Keeper of Souls",
        ShortInstruction = "Kill the Keeper of Souls and loot the Stone Amulet.",
        Who = "Keeper of Souls",
        Where = "Plane of Sky - Isle 4.",
        What = "Loot Stone Amulet (1).",
        When = "Whenever it is up.",
        Why = "Stone Amulet is one of the turn-in pieces for the Runed Wind Amulet.",
        How = "Kill the named and loot it.",
        ItemNames = ["Stone Amulet"],
        Authoring = GuideAuthoring.Authored,
        Sources =
        [
            new GuideSource
            {
                Url = "https://eqlwiki.com/Warrior_Plane_of_Sky_Tests",
                Title = "Warrior Plane of Sky Tests",
                RetrievedAt = "2026-08-31",
            },
        ],
    };

    private static GuideObjective Stub() => new()
    {
        Id = "wind-rune-azia", Order = 1, ObjectiveType = "Farm",
        Title = "Collect Wind Rune Azia",
        ShortInstruction = "Loot a Wind Rune Azia from Plane of Sky trash.",
        Authoring = GuideAuthoring.Stub,
        StubNote = "eqlwiki's Warrior page does not say which isle or which mobs drop it.",
    };

    private static Guide Guide(params GuideObjective[] objectives) => new()
    {
        Id = "pos-warrior-runed-wind-amulet",
        Name = "Runed Wind Amulet - Warrior, Plane of Sky",
        ApplicableClasses = ["Warrior"],
        ZoneNames = ["Plane of Sky"],
        Stages = [new GuideStage { Id = "s", Name = "Isle 4", Order = 1, Objectives = [.. objectives] }],
    };

    private static string Body(string url) =>
        Uri.UnescapeDataString(Regex.Match(url, @"&body=(.*)$").Groups[1].Value);

    // ---- the caption -----------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 3, 1, "Guide · 0 of 3 · 1 stub")]
    [InlineData(2, 0, 3, 0, "Guide · 2 of 3")]
    [InlineData(1, 1, 4, 2, "Guide · 1 of 4 · 1 skipped · 2 stubs")]
    [InlineData(3, 0, 3, 0, "Guide · 3 of 3")]
    public void TheCaptionSaysProgressAndHowHonestTheDataIs(
        int done, int skipped, int total, int stubs, string expected) =>
        Assert.Equal(expected, GuidePresentation.GuidedCaption(done, skipped, total, stubs));

    // ---- row prose -------------------------------------------------------------------

    /// <summary>Each of the six is drawn in exactly ONE place (David, 2026-09-09: "we don't
    /// want to be redundant, but all must be addressed"). The row line carries WHO and WHERE
    /// — the two an authored step always answers; WHAT is the row's own title, and WHEN, WHY
    /// and HOW are on the hover, where an empty one simply does not appear.</summary>
    [Fact]
    public void TheRowLineCarriesWhoAndWhereAndNothingElse()
    {
        var objective = Authored();

        var detail = GuidePresentation.RowDetail(objective);

        Assert.Equal("Keeper of Souls · Plane of Sky - Isle 4.", detail);
        Assert.DoesNotContain(objective.When, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(objective.Why, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(objective.How, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(objective.What, detail, StringComparison.Ordinal);
    }

    /// <summary>A step the sources could not answer WHEN or HOW for still renders — the row
    /// line is unchanged and the hover simply carries fewer questions. This is the shape the
    /// shipped catalog is mostly in now, so it is the case worth pinning.</summary>
    [Fact]
    public void AStepWithNoWhenOrHowStillRendersAndTheHoverJustSaysLess()
    {
        var objective = Authored();
        objective.When = "";
        objective.How = "";

        Assert.Equal("Keeper of Souls · Plane of Sky - Isle 4.",
            GuidePresentation.RowDetail(objective));

        var tip = GuidePresentation.RowTooltip(objective);
        Assert.Contains("What:", tip, StringComparison.Ordinal);
        Assert.Contains("Why:", tip, StringComparison.Ordinal);
        Assert.DoesNotContain("When:", tip, StringComparison.Ordinal);
        Assert.DoesNotContain("How:", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void AHalfAnsweredRowLeavesNoDanglingSeparator()
    {
        var whoOnly = Authored();
        whoOnly.Where = "";
        Assert.Equal("Keeper of Souls", GuidePresentation.RowDetail(whoOnly));

        var whereOnly = Authored();
        whereOnly.Who = "";
        Assert.Equal("Plane of Sky - Isle 4.", GuidePresentation.RowDetail(whereOnly));

        Assert.Equal("", GuidePresentation.RowDetail(Stub()));
    }

    /// <summary>The hover answers ALL SIX, labelled — so nothing is unanswerable even though
    /// nothing is said twice on the row itself.</summary>
    [Fact]
    public void TheHoverAnswersAllSixQuestions()
    {
        var tip = GuidePresentation.RowTooltip(Authored());

        foreach (var label in new[] { "What:", "Who:", "Where:", "When:", "Why:", "How:" })
            Assert.Contains(label, tip, StringComparison.Ordinal);
        // One line each, in the order a player asks them.
        Assert.Equal(6, tip.Split('\n').Length);
        Assert.StartsWith("What:", tip, StringComparison.Ordinal);
    }

    /// <summary>A stub answers the one question it can. A labelled list of blanks reads as a
    /// broken row rather than an honest one.</summary>
    [Fact]
    public void AStubsHoverIsItsNoteAndNotSixEmptyLabels()
    {
        var tip = GuidePresentation.RowTooltip(Stub());

        Assert.StartsWith(GuidePresentation.StubLead, tip, StringComparison.Ordinal);
        Assert.DoesNotContain("Who:", tip, StringComparison.Ordinal);
        Assert.DoesNotContain("When:", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterDetailNamesTheStepsAndSaysNothingWhenThereAreNone()
    {
        Assert.Equal("after: Stone Amulet, Wind Rune Azia",
            GuidePresentation.AfterDetail(["Stone Amulet", "Wind Rune Azia"]));
        Assert.Equal("", GuidePresentation.AfterDetail([]));
        Assert.Equal("", GuidePresentation.AfterDetail(["", "  "]));
    }

    // ---- the share-back door ---------------------------------------------------------

    [Fact]
    public void TheUrlCarriesTheGuideTheStepAndBothIds()
    {
        var objective = Authored();
        var guide = Guide(objective);

        var body = Body(GuidePresentation.ImproveUrl(guide, objective));

        Assert.Contains(guide.Name, body, StringComparison.Ordinal);
        Assert.Contains(objective.Title, body, StringComparison.Ordinal);
        Assert.Contains("pos-warrior-runed-wind-amulet", body, StringComparison.Ordinal);
        Assert.Contains("stone-amulet", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAuthoredStepsBodyCarriesWhoWhereAndWhat()
    {
        var objective = Authored();

        var body = Body(GuidePresentation.ImproveUrl(Guide(objective), objective));

        Assert.Contains("Who: Keeper of Souls", body, StringComparison.Ordinal);
        Assert.Contains("Where: Plane of Sky - Isle 4", body, StringComparison.Ordinal);
        Assert.Contains("When: Whenever it is up.", body, StringComparison.Ordinal);
        Assert.Contains("How: Kill the named and loot it.", body, StringComparison.Ordinal);
        Assert.Contains("What: Loot Stone Amulet (1).", body, StringComparison.Ordinal);
        Assert.DoesNotContain(GuidePresentation.StubLead, body, StringComparison.Ordinal);
    }

    [Fact]
    public void AStubStepsBodyCarriesItsNoteInstead()
    {
        var objective = Stub();

        var body = Body(GuidePresentation.ImproveUrl(Guide(objective), objective));

        Assert.Contains(GuidePresentation.StubLead, body, StringComparison.Ordinal);
        Assert.Contains("does not say which isle", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBodyLeavesTheReporterABlankLineAndPointsAtTheWikiFirst()
    {
        var objective = Authored();

        var body = Body(GuidePresentation.ImproveUrl(Guide(objective), objective));

        Assert.Contains("What you saw in game:", body, StringComparison.Ordinal);
        // eqlwiki is the source and EQBuddy is the tool that helps it update
        // (David, 2026-08-22) — so the page's own edit link rides the body.
        Assert.Contains(WikiContribution.EditUrl("Warrior Plane of Sky Tests"), body,
            StringComparison.Ordinal);
        Assert.Contains("action=edit", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AStepWithNoSourceStillOpensAndSimplyOffersNoEditLink()
    {
        var objective = Stub();
        var guide = Guide(objective);

        var body = Body(GuidePresentation.ImproveUrl(guide, objective));

        Assert.DoesNotContain("action=edit", body, StringComparison.Ordinal);
        Assert.Contains("What you saw in game:", body, StringComparison.Ordinal);
    }

    /// <summary>The promise, asserted as a negative with the data actually present. The
    /// signature of the bug this forbids is a body that helpfully includes "your character"
    /// or the log line that triggered the step.</summary>
    [Fact]
    public void NothingFromTheLogTheCharacterOrTheInventoryIsInTheBody()
    {
        var objective = Authored();
        var guide = Guide(objective);

        var body = Body(GuidePresentation.ImproveUrl(guide, objective));

        foreach (var leak in new[]
                 {
                     "Dranak", "dranak_freeport", "You have looted", "Server", "Legends",
                     "eqlog", "C:\\", "AppData", "192.168",
                 })
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheDoorIsADiscussionDraftAndNotAPost()
    {
        var objective = Authored();

        var url = GuidePresentation.ImproveUrl(Guide(objective), objective);

        // discussions/new is a FORM the player reads and submits themselves — the same
        // trust shape as the quest report door and FeedbackWindow.
        Assert.StartsWith("https://github.com/DranakCorps-bot/EQBuddy/discussions/new",
            url, StringComparison.Ordinal);
        Assert.Contains("category=q-a", url, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTooltipSaysNothingIsSentUntilYouPost()
    {
        Assert.Contains(GuidePresentation.ImproveLabel, GuidePresentation.ImproveTip,
            StringComparison.Ordinal);
        Assert.Contains("nothing is sent until you post", GuidePresentation.ImproveTip,
            StringComparison.Ordinal);
    }

    /// <summary>The phone spells the door's label in its own JS const, because repeating it
    /// on every guide row would be wire weight for one string. This is what keeps that copy
    /// honest — the two surfaces must not start calling one door two things (#184).</summary>
    [Fact]
    public void ThePhonePageSpellsTheDoorExactlyAsTheDesktopDoes()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        var page = File.ReadAllText(path);

        Assert.Contains(
            $"const IMPROVE_LABEL = \"{GuidePresentation.ImproveLabel}\";",
            page, StringComparison.Ordinal);
    }
}
