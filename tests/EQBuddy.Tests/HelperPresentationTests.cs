using System.Reflection;
using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **HOME-006 AS A BUILD STEP, plus the rest of the Helper's words.**
///
/// <para>The requirement is one sentence: <i>"Do not claim a camp is safe merely because the
/// player's DPS is high. Use actual survival/history signals where available. If
/// insufficient, say so."</i> The plan's D4 turns that into a refusal rather than a caveat —
/// no generated sentence may claim a place is safe, easy or survivable — and a refusal that
/// lives in a doc comment lasts one author. This is the guard.</para>
///
/// <para><b>It sweeps ASSEMBLED sentences and not only constants</b>, which is the whole
/// difference between this and a reflection over <c>const</c> fields. Every word the Helper
/// can put on screen arrives through one of a handful of methods, most of them
/// interpolations; a scan that read only the literals would pass over
/// <c>$"{zone} is a comfortable camp"</c> forever. So every switch arm is walked with a real
/// argument and the RESULT is what is checked.</para>
///
/// <para><b>And it prove-fails.</b> Trap 78 is a detector whose pattern list parsed into one
/// empty string and reported clean on the commit that mangled 63,782 characters. A ban that
/// has never been seen to fire is a ban aimed at nothing, so a planted sentence is fed to the
/// same scanner and has to be caught.</para>
/// </summary>
public class HelperPresentationTests
{
    /// <summary>
    /// The vocabulary HOME-006 forbids, in both directions.
    ///
    /// <para><b>"Dangerous" is banned as well as "safe", and that is not symmetry for its own
    /// sake.</b> The requirement is about not making survivability claims the evidence does
    /// not support, and "this camp is dangerous" is exactly as unsupported as its opposite —
    /// EQBuddy has your deaths and your downtime, which are facts about what happened to YOU,
    /// and nothing at all about what a place is like. The permitted shape is
    /// <see cref="ZoneDeathsFact"/>: a count, with its scope, and no adjective.</para>
    ///
    /// <para>Whole words, so "Fashioned" does not trip "hard" and a zone called "Erudin
    /// Palace" does not trip anything at all.</para>
    /// </summary>
    private static readonly string[] Forbidden =
    [
        "safe", "safer", "safest", "safely", "safety", "unsafe",
        "easy", "easier", "easiest", "easily",
        "dangerous", "danger", "deadly", "lethal", "risky",
        "survivable", "survivability", "forgiving", "harmless", "brutal", "punishing",
    ];

    private static void AssertClean(string text, string where)
    {
        if (string.IsNullOrEmpty(text)) return;
        var hit = Hit(text);
        Assert.True(hit is null,
            $"{where} says \"{hit}\": {text}{Environment.NewLine}{Environment.NewLine}"
            + "HOME-006 forbids claiming a camp is safe, easy or survivable — and forbids the "
            + "opposite claim for the same reason: EQBuddy has your deaths and your downtime, "
            + "which are facts about what happened to YOU and not about what a place is like. "
            + "The permitted shape is a count with its scope and no adjective "
            + "(ZoneDeathsFact). If a new sentence genuinely needs one of these words, that is "
            + "a decision for the plan and not for this file.");
    }

    /// <summary>The scanner, named so the prove-fail below can point it at a planted
    /// sentence rather than at a second copy of the regex (trap 33 inside a test file).</summary>
    private static string? Hit(string text) =>
        Forbidden.FirstOrDefault(w => Regex.IsMatch(text, $@"\b{Regex.Escape(w)}\b",
            RegexOptions.IgnoreCase));

    // ---- 1. the ban ----------------------------------------------------------------------

    /// <summary>Every constant. The room's chrome, the headings, the notes, the whole-room
    /// empty.</summary>
    [Fact]
    public void NoConstantOnTheHelperUsesSafetyVocabulary()
    {
        var seen = 0;
        foreach (var f in typeof(HelperPresentation)
                     .GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is string s) { AssertClean(s, $"HelperPresentation.{f.Name}"); seen++; }
            else if (f.GetValue(null) is RoomEmptyMessage m)
            {
                AssertClean(m.Heading, $"HelperPresentation.{f.Name}.Heading");
                AssertClean(m.Explanation, $"HelperPresentation.{f.Name}.Explanation");
                seen++;
            }
        }
        // The liveness half. A rename that emptied the reflection would otherwise pass in
        // silence, which is a guard reading as coverage while seeing nothing.
        Assert.True(seen >= 6,
            $"Only {seen} Helper string constants were reached — the reflection has stopped "
            + "finding them. Check the type, not the assertion.");
    }

    /// <summary>Every ASSEMBLED sentence: the goal labels and tips, the why-lines, the gaps,
    /// the deferred notes, the caps, the door labels and tips. This is the tier a constant
    /// sweep cannot reach.</summary>
    [Fact]
    public void NoAssembledHelperSentenceUsesSafetyVocabulary()
    {
        foreach (var goal in Recommendations.All)
        {
            AssertClean(HelperPresentation.GoalLabel(goal), $"GoalLabel({goal})");
            AssertClean(HelperPresentation.GoalTip(goal), $"GoalTip({goal})");
            AssertClean(HelperPresentation.NotAnsweredYet(goal), $"NotAnsweredYet({goal})");
            foreach (var reason in Enum.GetValues<GoalGapReason>())
                AssertClean(HelperPresentation.Gap(new GoalGap(goal, reason)), $"Gap({goal}, {reason})");
        }

        foreach (var type in HelperMustListTests.Subtypes())
            AssertClean(HelperPresentation.Why(HelperMustListTests.Build(type)), $"Why({type.Name})");

        foreach (var kind in Enum.GetValues<HelperDoorKind>())
        {
            AssertClean(HelperPresentation.DoorLabel(kind), $"DoorLabel({kind})");
            AssertClean(HelperPresentation.DoorTip(new HelperDoor(kind, "Lower Guk")), $"DoorTip({kind})");
        }

        foreach (var kind in Enum.GetValues<RecommendationKind>())
            AssertClean(
                HelperPresentation.Headline(new Recommendation(kind, "Lower Guk", "", [], [], [], 0, 0)),
                $"Headline({kind})");

        for (var n = 0; n <= 2; n++)
        {
            AssertClean(HelperPresentation.Cap(n), $"Cap({n})");
            AssertClean(HelperPresentation.WithheldWhy(n), $"WithheldWhy({n})");
            AssertClean(HelperPresentation.FactionPickerCapNote(n), $"FactionPickerCapNote({n})");
        }

        foreach (var maxed in new[] { false, true })
            AssertClean(
                HelperPresentation.FactionChip(new FactionsFile.Standing(1, "Faydark Rangers", 1200, maxed ? 0 : 800)),
                $"FactionChip(maxed:{maxed})");
    }

    /// <summary>
    /// **THE PROVE-FAIL.** The scanner is pointed at a sentence of exactly the shape the ban
    /// exists to stop — the one somebody would write in good faith, because it sounds
    /// helpful — and has to catch it. Without this row the two above are a guard that has
    /// never been observed to fire (trap 78).
    /// </summary>
    [Theory]
    [InlineData("Lower Guk is a safe camp for your level.")]
    [InlineData("Your damage makes this an easy pull.")]
    [InlineData("This zone is dangerous at your level.")]
    [InlineData("Highly survivable for a character with your healing.")]
    public void TheBanCatchesTheSentenceSomebodyWouldWriteInGoodFaith(string sentence) =>
        Assert.NotNull(Hit(sentence));

    /// <summary>The negative that keeps the prove-fail honest: a real Helper sentence must
    /// pass the same scanner, or the ban would be catching everything and proving
    /// nothing.</summary>
    [Fact]
    public void TheBanDoesNotCatchTheSentencesTheHelperActuallySays()
    {
        Assert.Null(Hit(HelperPresentation.Why(
            new ZoneDeathsFact("Lower Guk", 4, 9))));
        Assert.Null(Hit(HelperPresentation.Why(
            new ZoneXpRateFact("Lower Guk", 8.2, 14, 21.5))));
    }

    // ---- 2. the sentences say what they claim --------------------------------------------

    /// <summary>
    /// **HOME-003's scope.** A personal rate names the denominator it rests on, because the
    /// player is the only person who can tell whether fourteen sessions in Befallen is a lot.
    /// </summary>
    [Fact]
    public void APersonalRateNamesWhatItRestsOn()
    {
        var line = HelperPresentation.Why(new ZoneXpRateFact("Lower Guk", 8.24, 14, 21.5));
        Assert.Contains("8.2", line);
        Assert.Contains("14", line);
        Assert.Contains("of your sessions", line);
        Assert.Contains("21.5 hours", line);
    }

    /// <summary>A sitting under an hour is said in minutes, because "0.4 hours" is a number
    /// nobody has said out loud about an evening.</summary>
    [Fact]
    public void ShortHistoriesAreSaidInMinutes() =>
        Assert.Contains("30 minutes",
            HelperPresentation.Why(new ZoneXpRateFact("Befallen", 6, 1, 0.5)));

    /// <summary>
    /// **One stored session gets its OWN sentence, not a plural switch.**
    ///
    /// <para>The first staged screenshot of this room read "across 1 of your session"
    /// (trap 23: a shot whose words you did not predict has not been reviewed). "Across N of
    /// your sessions" is a sentence that needs an N there are others beside; at one it is
    /// simply a different sentence, and pretending otherwise is what a lone plural toggle in
    /// the middle of an interpolation does.</para>
    /// </summary>
    [Fact]
    public void OneStoredSessionReadsAsOneSessionAndNotAsAPartialPlural()
    {
        var one = HelperPresentation.Why(new ZoneXpRateFact("West Commonlands", 14.5, 1, 1.1));
        Assert.Contains("from 1 stored session", one);
        Assert.DoesNotContain("of your session", one);

        var many = HelperPresentation.Why(new ZoneXpRateFact("West Commonlands", 14.5, 9, 11.1));
        Assert.Contains("across 9 of your sessions", many);
    }

    /// <summary>A fight length under a minute is seconds. HOME-002's own worked example says
    /// "Your avg kill: 47 sec".</summary>
    [Fact]
    public void FightLengthReadsTheWayThePrdsExampleDoes()
    {
        Assert.Contains("47 sec",
            HelperPresentation.Why(new ZoneCadenceFact("Lower Guk", 47, 120)));
        Assert.Contains("2.5 min",
            HelperPresentation.Why(new ZoneCadenceFact("Kael Drakkel", 150, 8)));
    }

    /// <summary>The deaths line is a COUNT with its scope and no adjective — the one shape
    /// HOME-006 permits.</summary>
    [Fact]
    public void TheDeathsLineIsACountAndNotAJudgement()
    {
        var line = HelperPresentation.Why(new ZoneDeathsFact("Sebilis", 1, 3));
        Assert.Contains("1 time", line);
        Assert.Contains("3 sessions", line);
        Assert.DoesNotContain("should", line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A pass-through sentence is drawn exactly as its own producer wrote it.
    /// Re-wording it here would be a second answer to one arithmetic — the failure
    /// <c>UnlockGuidance</c>'s own comment names, and the newer copy is always the one that
    /// goes stale.</summary>
    [Fact]
    public void APassThroughSentenceIsNotRephrased()
    {
        const string text = "≈340 more kills of a froglok tad in Lower Guk at +5 each — an "
            + "estimate from your own log, not a target.";
        Assert.Equal(text, HelperPresentation.Why(new WordedFact(text, Evidence.Personal)));
    }

    /// <summary>Singular and plural both land, in the three places a count is drawn. "1
    /// answers" is the kind of slip nobody notices in review and everybody notices on
    /// screen.</summary>
    [Fact]
    public void CountsReadCorrectlyAtOne()
    {
        Assert.Contains("1 more answer matched", HelperPresentation.Cap(1));
        Assert.Contains("2 more answers matched", HelperPresentation.Cap(2));
        Assert.Contains("1 more reason", HelperPresentation.WithheldWhy(1));
        Assert.Contains("2 more reasons", HelperPresentation.WithheldWhy(2));
        Assert.Contains("1 more faction", HelperPresentation.FactionPickerCapNote(1));
        Assert.Contains("2 more factions", HelperPresentation.FactionPickerCapNote(2));
    }

    /// <summary>A cap that held nothing back says NOTHING. A sentence reading "0 more
    /// answers matched your goals" is furniture that makes the list look truncated when it is
    /// complete.</summary>
    [Fact]
    public void ACapThatHeldNothingBackIsSilent()
    {
        Assert.Empty(HelperPresentation.Cap(0));
        Assert.Empty(HelperPresentation.WithheldWhy(0));
        Assert.Empty(HelperPresentation.FactionPickerCapNote(0));
        Assert.Empty(HelperPresentation.Cap(-1));
    }

    /// <summary>The headline names the KIND for anything that is not a place, so "Dark Elf"
    /// is not read as a zone to travel to.</summary>
    [Fact]
    public void ANonPlaceHeadlineSaysWhatKindOfThingItIs()
    {
        Assert.Equal("Lower Guk", HelperPresentation.Headline(
            new Recommendation(RecommendationKind.Zone, "Lower Guk", "Lower Guk", [], [], [], 0, 0)));
        Assert.Contains("unlock", HelperPresentation.Headline(
            new Recommendation(RecommendationKind.Unlock, "Dark Elf", "", [], [], [], 0, 0)));
        Assert.Contains("faction", HelperPresentation.Headline(
            new Recommendation(RecommendationKind.Faction, "Faydark Rangers", "", [], [], [], 0, 0)));
    }

    /// <summary>The "serves" line is the cross-domain chain said out loud (HOME-005), and it
    /// is silent when there is no chain to name.</summary>
    [Fact]
    public void TheServesLineNamesEveryGoalTheRowAnswers()
    {
        var two = new Recommendation(RecommendationKind.Zone, "Lower Guk", "Lower Guk",
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction], [], [], 0, 0);
        Assert.Equal("Level Up · Work on Faction", HelperPresentation.Serves(two));
        Assert.Empty(HelperPresentation.Serves(two with { Goals = [] }));
    }

    /// <summary>The World door names the zone when it has one, because the World room follows
    /// the zone you are IN — a tip that promised to open a map of somewhere else would be an
    /// affordance the room cannot honour (trap 35's shape).</summary>
    [Fact]
    public void TheWorldDoorIsHonestAboutWhichZoneItWillShow()
    {
        var named = HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.World, "Lower Guk"));
        Assert.Contains("Lower Guk", named);
        Assert.Contains("when you get there", named);
        Assert.DoesNotContain("Lower Guk",
            HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.World, "")));
    }

    /// <summary>The wiki tip says what EQBuddy does NOT do. The request policy toward a third
    /// party that can notice us is the Founder's call, and every surface that offers a wiki
    /// link says the same thing about it.</summary>
    [Fact]
    public void TheWikiDoorSaysEqbuddyFetchesNothing() =>
        Assert.Contains("never fetches",
            HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.WikiFaction, "Faydark Rangers")));

    /// <summary>A gap whose answer is a file the game writes ASKS for it. The command itself
    /// is handed over by the room (<c>GameCommandsTests.SurfacesNeedingACommand</c>); the
    /// sentence has to name what is missing, or the ⧉ beside it is a button with no
    /// question.</summary>
    [Theory]
    [InlineData(GoalGapReason.NoFactionDump, "faction command")]
    [InlineData(GoalGapReason.NoAchievementsDump, "achievements command")]
    public void AMissingDumpIsAskedForByName(GoalGapReason reason, string expected) =>
        Assert.Contains(expected,
            HelperPresentation.Gap(new GoalGap(HelperGoal.WorkOnFaction, reason)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>"Nothing left" is not worded as a gap in the data. A finished job and a
    /// missing file are different answers and a player can act on only one of them.</summary>
    [Fact]
    public void AFinishedJobIsNotWordedAsMissingData()
    {
        var done = HelperPresentation.Gap(
            new GoalGap(HelperGoal.UnlockRaces, GoalGapReason.NothingLeftToDo));
        Assert.Contains("finished", done);
        Assert.DoesNotContain("command", done, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The values line, on the surface that most needs it: the Helper is the room
    /// that looks most like it might be comparing you to somebody.</summary>
    [Fact]
    public void TheSourceNoteSaysNobodyElseIsMeasured() =>
        Assert.Contains("never looks at anyone else's play", HelperPresentation.SourceNote);
}
