using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// WHILE YOU'RE HERE on the phone (DRA-42 D1): the SAME answer the Guide room draws, arranged by
/// the same <see cref="WhileHerePresentation.Groups"/> and worded by the same file — and a page
/// that draws every field it is sent while spelling none of the sentences itself.
///
/// <para>The page half is DRA-84 D5's lesson: a sentence the page is SENT but never DRAWS passes
/// every projection test there is, so the fields the page must read are a curated must-list
/// beside the forbid-scan (trap 34 — the forbid alone cannot see a missing thing).</para>
/// </summary>
public sealed class WhileHereSurfaceParityTests
{
    private static WhileHereAnswer Busy() => new(
        "West Commonlands", WhileHereState.Answered,
        Required: [.. Enumerable.Range(1, 8).Select(i =>
            new WhileHereStep("Armor of Ro Quests", $"s{i}", $"Collect piece {i}", ["a", "b", "c", "d"]))],
        Relevant: [new WhileHereStep("Bear Hide Armor", "b1", "Collect Low Quality Bear Skin", [])],
        Optional: [.. Enumerable.Range(1, 7).Select(i => $"Quest {i}")],
        UnplacedTracked: 3);

    [Fact]
    public void ThePhoneCarriesTheRoomsGroupsCapsAndSentencesWordForWord()
    {
        var answer = Busy();
        var phone = CompanionProjection.BuildWhileHere(answer)!;
        var room = WhileHerePresentation.Groups(answer);

        Assert.Equal(WhileHerePresentation.HeadingFor(answer), phone.Heading);
        Assert.Equal(WhileHerePresentation.SourceNote, phone.Note);
        Assert.Null(phone.Empty);
        Assert.Equal(WhileHerePresentation.Unplaced(3), phone.Unplaced);
        Assert.Equal(room.Count, phone.Groups.Count);
        for (var i = 0; i < room.Count; i++)
        {
            Assert.Equal(room[i].Label, phone.Groups[i].Label);
            Assert.Equal(room[i].More, phone.Groups[i].More);
            Assert.Equal(room[i].Rows.Select(r => (r.Title, r.Detail)),
                phone.Groups[i].Rows.Select(r => (r.Title, r.Detail)));
        }
        // The caps are the room's, and each SAYS what it held back (trap 50).
        Assert.Equal(WhileHerePresentation.StepsPerGroup, phone.Groups[0].Rows.Count);
        Assert.Equal(WhileHerePresentation.MoreSteps(8 - WhileHerePresentation.StepsPerGroup), phone.Groups[0].More);
        Assert.Equal(WhileHerePresentation.OptionalShown, phone.Groups[2].Rows.Count);
        Assert.Equal(WhileHerePresentation.MoreQuests(7 - WhileHerePresentation.OptionalShown), phone.Groups[2].More);
        Assert.Contains("and 1 more on its page", phone.Groups[0].Rows[0].Detail);
    }

    [Fact]
    public void AnEmptyStateCarriesItsTrailingCountsOnBothSurfaces()
    {
        // Review item 1 on #985: the phone drew "nothing open here" AND the unplaced count while
        // the desktop drew the first alone. Both now walk TrailingLines in every state.
        var answer = new WhileHereAnswer("Crushbone", WhileHereState.NothingOpenHere,
            [], [], [], UnplacedTracked: 3, Filtered: 2);
        var phone = CompanionProjection.BuildWhileHere(answer)!;
        Assert.NotNull(phone.Empty);
        Assert.Equal(WhileHerePresentation.TrailingLines(answer),
            new[] { phone.Unplaced, phone.Filtered }.OfType<string>());
        Assert.Equal(2, WhileHerePresentation.TrailingLines(answer).Count);
    }

    [Fact]
    public void AnEmptyStateRidesTheWireAndNoAnswerDrawsNoBlock()
    {
        var unknown = CompanionProjection.BuildWhileHere(WhileHereAnswer.None)!;
        Assert.Equal(WhileHerePresentation.Empty(WhileHereAnswer.None), unknown.Empty);
        Assert.Empty(unknown.Groups);

        Assert.Null(CompanionProjection.BuildWhileHere(null));
    }

    [Fact]
    public void ADoneStepMovesTheQuestsFingerprint()
    {
        // Trap 72: a step leaving the block moves no count the rest of the print carries.
        var before = Section(Busy());
        var after = Section(Busy() with { Required = Busy().Required.Skip(1).ToList() });
        Assert.NotEqual(
            CompanionProjection.SectionFingerprints(new CompanionSnapshot { Quests = before })[CompanionSurfaces.Quests],
            CompanionProjection.SectionFingerprints(new CompanionSnapshot { Quests = after })[CompanionSurfaces.Quests]);
    }

    private static CompanionQuestsSection Section(WhileHereAnswer answer) => new(
        Tabs: [], CatalogStamp: "", Catalog: null, Mine: [], MineMore: 0,
        Owned: new Dictionary<string, int>(), Tracked: [], Hidden: [],
        Completed: new Dictionary<string, int>(), Classes: [], InferredClass: null,
        CharacterClasses: null, ClassSourceLabel: null,
        Epics: new CompanionChecklistSection(0, 0, []),
        Sky: new CompanionChecklistSection(0, 0, []),
        Guides: [], GuidesMore: 0,
        WhileHere: CompanionProjection.BuildWhileHere(answer));

    [Fact]
    public void ThePageSpellsNoneOfTheBlocksWordsAndDrawsEveryField()
    {
        var html = File.ReadAllText(Path.Combine(SrcRoot(), "EQBuddy.Companion", "Web", "index.html"));

        var sentences = new List<string>
        {
            WhileHerePresentation.HeadingNoZone,
            WhileHerePresentation.SourceNote,
            WhileHerePresentation.MoreSteps(2),
            WhileHerePresentation.MoreQuests(2),
            WhileHerePresentation.Unplaced(2),
            WhileHerePresentation.Filtered(2),
        };
        sentences.AddRange(Enum.GetValues<WhileHereGroup>().Select(WhileHerePresentation.GroupLabel));
        foreach (var state in Enum.GetValues<WhileHereState>())
            if (WhileHerePresentation.Empty(WhileHereAnswer.None with { State = state, Zone = "Crushbone" }) is { } e)
                sentences.Add(e);
        foreach (var sentence in sentences)
            Assert.DoesNotContain(sentence, html, StringComparison.Ordinal);

        // The must-list: every field the wire carries is READ by the page. A page that drew
        // the rows and dropped `g.more` would swallow the cap's sentence, and one that
        // dropped `w.unplaced` would let a tracked step vanish — D5's failure, twice.
        foreach (var field in new[] { "d.whileHere", "w.groups", "g.rows" })
            Assert.Contains(field, html, StringComparison.Ordinal);

        // Every SENTENCE field must be DRAWN as an element's text, not merely mentioned: the
        // first cut read `w.note` into a `title=` hover, which a phone cannot show and which a
        // bare Contains passed (review item 3 on #985, trap 35).
        foreach (var field in new[]
                 {
                     "w.heading", "w.note", "w.empty", "g.label", "r.title", "r.detail",
                     "g.more", "w.unplaced", "w.filtered",
                 })
            Assert.True(DrawnAsText(html, field), $"the page never draws {field} as a visible line");
        Assert.DoesNotMatch(@"\.title\s*=\s*w\.", html);
    }

    /// <summary>The page draws <paramref name="field"/> as an element's text:
    /// <c>el("div", "cls", field)</c>.</summary>
    private static bool DrawnAsText(string html, string field) =>
        System.Text.RegularExpressions.Regex.IsMatch(html,
            @"el\(\s*""[a-z]+""\s*,\s*""[a-z]+""\s*,\s*" + System.Text.RegularExpressions.Regex.Escape(field) + @"\s*\)");

    [Fact]
    public void TheDrawnAsTextCheckRefusesAHoverOnlyField()
    {
        // The must-list's committed negative (trap 78): the pre-fix shape does not pass.
        Assert.False(DrawnAsText("const out = [el(\"div\", \"hh\", w.heading)];\nout[0].title = w.note;", "w.note"));
        Assert.True(DrawnAsText("el(\"div\", \"hs\", w.note)", "w.note"));
    }

    private static string SrcRoot() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");
}
