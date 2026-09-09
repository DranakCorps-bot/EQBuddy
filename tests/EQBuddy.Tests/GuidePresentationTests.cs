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
        Assert.Contains(objective.What, tip, StringComparison.Ordinal);
        Assert.Contains(objective.Why, tip, StringComparison.Ordinal);
        // Two fewer lines, and no blank one left where they were.
        Assert.Equal(3, tip.Split('\n').Length);
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

    /// <summary>The hover still answers all six — as SENTENCES. Labels are how the DATA is
    /// shaped, not how a person tells you where to go (David, 2026-09-09); the card and the
    /// hover both read as a form until they went.</summary>
    [Fact]
    public void TheHoverAnswersAllSixQuestionsWithoutLabellingThem()
    {
        var o = Authored();
        var tip = GuidePresentation.RowTooltip(o);

        foreach (var label in new[] { "What:", "Who:", "Where:", "When:", "Why:", "How:" })
            Assert.DoesNotContain(label, tip, StringComparison.Ordinal);

        // Who and where fused into one direction sentence; the rest in their own words.
        Assert.Contains("Travel to Plane of Sky - Isle 4, then find Keeper of Souls.",
            tip, StringComparison.Ordinal);
        Assert.Contains(o.What, tip, StringComparison.Ordinal);
        Assert.Contains(o.Why, tip, StringComparison.Ordinal);
        Assert.Contains(o.When, tip, StringComparison.Ordinal);
        Assert.Contains(o.How, tip, StringComparison.Ordinal);
    }

    /// <summary>The direction verb follows the objective type: you do not "find" an NPC you
    /// are meant to talk to, and you do not "travel to" a mob.</summary>
    [Theory]
    [InlineData("Loot", "find")]
    [InlineData("Kill", "fight")]
    [InlineData("TalkToNpc", "speak to")]
    [InlineData("TurnIn", "speak to")]
    public void TheDirectionVerbFollowsWhatTheStepActuallyIs(string type, string verb)
    {
        var o = Authored();
        o.ObjectiveType = type;
        Assert.Contains($", then {verb} Keeper of Souls.",
            GuidePresentation.Directions(o), StringComparison.Ordinal);
    }

    /// <summary>A step answering only one half still reads as a sentence, not a fragment
    /// composed round a blank.</summary>
    [Fact]
    public void DirectionsDropTheHalfTheStepDoesNotHave()
    {
        var whereOnly = Authored();
        whereOnly.Who = "";
        Assert.Equal("Travel to Plane of Sky - Isle 4.", GuidePresentation.Directions(whereOnly));

        var whoOnly = Authored();
        whoOnly.Where = "";
        Assert.Equal("Find Keeper of Souls.", GuidePresentation.Directions(whoOnly));

        var neither = Authored();
        neither.Who = neither.Where = "";
        Assert.Equal("", GuidePresentation.Directions(neither));
    }

    /// <summary>The detail line is suppressed when it restates the instruction — "Kill X on
    /// Isle 5 and loot Y." above "Kill X and loot Y (1)." is one action printed twice, and
    /// that is what made the card read as a form.</summary>
    [Fact]
    public void TheDetailLineOnlyAppearsWhenItSaysSomethingNew()
    {
        var restating = Authored();
        restating.ShortInstruction = "Kill the Keeper of Souls and loot the Stone Amulet.";
        restating.What = "Kill the Keeper of Souls and loot Stone Amulet.";
        Assert.Equal("", GuidePresentation.ExtraDetail(restating));

        var adds = Authored();
        adds.ShortInstruction = "Loot the Stone Amulet.";
        adds.What = "Loot Stone Amulet (1). It is one of the two turn-ins and does not stack.";
        Assert.NotEqual("", GuidePresentation.ExtraDetail(adds));
    }

    /// <summary>What a quest PAYS, for the heading's hover — the question a player scanning a
    /// folded list is actually asking (David, 2026-09-09).</summary>
    [Fact]
    public void TheRewardSummaryNamesWhatYouGetAndWhatItCosts()
    {
        var items = new List<SkyQuestChecklistItem>
        {
            new() { QuestItem = "Stone Amulet" },
            new() { QuestItem = "Wind Rune Azia" },
        };

        Assert.Equal("Rewards the Runed Wind Amulet. Needs Stone Amulet, Wind Rune Azia.",
            GuidePresentation.RewardSummary("Runed Wind Amulet", items));
        // A reward with no pieces recorded still says what it pays.
        Assert.Equal("Rewards the Runed Wind Amulet.",
            GuidePresentation.RewardSummary("Runed Wind Amulet", []));
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

    // ---- the NEXT rule (P1d) ----------------------------------------------------------

    private static Guide TwoStages()
    {
        GuideObjective Step(string id, int order, params string[] prereqs) => new()
        {
            Id = id, Order = order, ObjectiveType = "Loot", Title = id,
            ShortInstruction = "do " + id, Who = "someone", Where = "somewhere",
            What = "do it", Authoring = GuideAuthoring.Authored,
            PrerequisiteObjectiveIds = [.. prereqs],
            Sources = [new GuideSource { Url = "u", Title = "t", RetrievedAt = "2026-09-09" }],
        };
        return new Guide
        {
            Id = "g", Name = "G", ApplicableClasses = ["Warrior"], ZoneNames = ["Plane of Sky"],
            Stages =
            [
                new GuideStage { Id = "s1", Name = "Isle 3", Order = 1,
                    Objectives = [Step("a", 1), Step("b", 2)] },
                new GuideStage { Id = "s2", Name = "Isle 4", Order = 2,
                    Objectives = [Step("c", 1)] },
                new GuideStage { Id = "s3", Name = "Turn in", Order = 3,
                    Objectives = [Step("turn-in", 1, "a", "b", "c")] },
            ],
        };
    }

    private static Func<GuideObjective, bool> In(params string[] ids) =>
        o => ids.Contains(o.Id, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NextIsTheFirstStepInReadingOrderThatIsNotDoneOrSkipped()
    {
        var g = TwoStages();
        Assert.Equal("a", GuidePresentation.NextObjective(g, In(), In())!.Id);
        Assert.Equal("b", GuidePresentation.NextObjective(g, In("a"), In())!.Id);
        Assert.Equal("c", GuidePresentation.NextObjective(g, In("a"), In("b"))!.Id);
    }

    /// <summary>A turn-in whose pieces are not all done is not offered, even though nothing
    /// earlier is left: walking a player to a hand-in they cannot make is worse than saying
    /// nothing. Prerequisites gate it, not position.</summary>
    [Fact]
    public void AStepWhoseePrerequisitesAreNotDoneIsSkippedOverRatherThanOffered()
    {
        var g = TwoStages();

        // Everything but the turn-in is SKIPPED, so the turn-in is next in reading order and
        // still must not be named: skipping "loot it" does not make "hand it in" doable.
        Assert.Null(GuidePresentation.NextObjective(g, In(), In("a", "b", "c")));
        // Done rather than skipped, and it IS offered.
        Assert.Equal("turn-in", GuidePresentation.NextObjective(g, In("a", "b", "c"), In())!.Id);
    }

    [Fact]
    public void AllDoneAndAllSkippedAreDifferentSentences()
    {
        var g = TwoStages();
        Assert.Equal(GuidePresentation.AllDone,
            GuidePresentation.NoNextStep(g, In("a", "b", "c", "turn-in"), In()));
        Assert.Equal(GuidePresentation.AllSkipped,
            GuidePresentation.NoNextStep(g, In(), In("a", "b", "c", "turn-in")));
        // A guide with steps left that are all blocked by skips reads as skipped, because
        // that is what the player chose.
        Assert.Equal(GuidePresentation.AllSkipped,
            GuidePresentation.NoNextStep(g, In("a"), In("b", "c", "turn-in")));
    }

    /// <summary>
    /// The warning fires only when the next step is on a LATER stage while an EARLIER one
    /// still has open work — on Plane of Sky, "you are about to leave this island with
    /// something on it". A warning that is always on is furniture.
    ///
    /// <para>Skipped steps are not stranded work: the player said they are not doing those,
    /// and nagging about them would be arguing with a decision they made.</para>
    ///
    /// <para><b>The fixture needs a BACKWARD prerequisite to reach this at all</b> — see
    /// <see cref="NoShippedSkyGuideCanTriggerTheBeforeLeavingWarningYet"/>.</para>
    /// </summary>
    [Fact]
    public void BeforeLeavingNamesOnlyTheWorkStrandedOnAnEarlierStage()
    {
        var g = TwoStages();
        // "b" (Isle 3) now waits on "c" (Isle 4), so the player is sent forward with work
        // behind them — the only shape that reaches this warning.
        g.Stages[0].Objectives[1].PrerequisiteObjectiveIds = ["c"];

        var next = GuidePresentation.NextObjective(g, In("a"), In())!;
        Assert.Equal("c", next.Id);

        var warning = GuidePresentation.BeforeLeaving(g, next, In("a"), In());
        Assert.StartsWith(GuidePresentation.BeforeLeavingLead, warning, StringComparison.Ordinal);
        Assert.Contains("b", warning, StringComparison.Ordinal);

        // The same shape with "b" SKIPPED is silent: a step the player struck out is not
        // work they are stranding.
        Assert.Equal("", GuidePresentation.BeforeLeaving(g, next, In("a"), In("b")));

        // And a next step on the CURRENT stage never warns, whatever else is open.
        Assert.Equal("", GuidePresentation.BeforeLeaving(
            g, GuidePresentation.NextObjective(g, In(), In())!, In(), In()));
    }

    /// <summary>
    /// <b>No shipped Plane of Sky guide can show that warning today, and that is expected.</b>
    ///
    /// <para>Selection is reading order, and no Sky objective has a prerequisite on a LATER
    /// stage — the isles are walked in order and only the turn-in waits on anything. So the
    /// precondition the signed plan wrote ("the next objective is on another stage and this
    /// stage still has open objectives") cannot occur in this data.</para>
    ///
    /// <para>The rule is kept because Delivery 2 and 3 bring quests that DO reach backward
    /// (a normal quest sending you back to an NPC, an epic step gated on a later drop). This
    /// test exists so nobody reads the missing line as a bug, and so it fails loudly the day
    /// authoring adds a backward prerequisite and the warning starts appearing for real.</para>
    /// </summary>
    [Fact]
    public void NoShippedSkyGuideCanTriggerTheBeforeLeavingWarningYet()
    {
        foreach (var guide in GuideCatalog.Default.Guides)
        {
            var stageOf = guide.Stages
                .SelectMany(s => s.Objectives.Select(o => (o.Id, s.Order)))
                .ToDictionary(x => x.Id, x => x.Order, StringComparer.OrdinalIgnoreCase);

            foreach (var stage in guide.Stages)
                foreach (var objective in stage.Objectives)
                    foreach (var prerequisite in objective.PrerequisiteObjectiveIds)
                        Assert.True(stageOf[prerequisite] <= stage.Order,
                            $"{guide.Id}/{objective.Id} waits on a LATER stage — the "
                            + "before-leaving warning is now reachable and wants a shot");
        }
    }

    [Fact]
    public void TheCardsWhyNamesTheRewardBecauseTheHeadingIsNotBesideIt() =>
        Assert.Equal("Works toward the Runed Wind Amulet.",
            GuidePresentation.CardWhy("Runed Wind Amulet"));

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

        Assert.Contains("Travel to Plane of Sky - Isle 4, then find Keeper of Souls.",
            body, StringComparison.Ordinal);
        Assert.Contains("Whenever it is up.", body, StringComparison.Ordinal);
        Assert.Contains("Kill the named and loot it.", body, StringComparison.Ordinal);
        Assert.Contains("Loot Stone Amulet (1).", body, StringComparison.Ordinal);
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
