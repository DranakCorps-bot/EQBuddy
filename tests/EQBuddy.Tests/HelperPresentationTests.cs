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
    /// <remarks>
    /// **DRA-71 D4 widened it, and the additions are the words a THROUGHPUT feature invites.**
    ///
    /// <para>The first list was written against a recommender that knew your experience rate
    /// and your deaths. D4 gives it your damage, your healing, your fight length and your
    /// downtime — and the sentence somebody writes in good faith with those in hand is not
    /// "this camp is safe", it is *"trivial for your output"* or *"too tough at your level"*
    /// or *"a comfortable camp for a character with your healing"*. HOME-006's actual text is
    /// about exactly this: <i>"do not claim a camp is safe merely because the player's DPS is
    /// high."</i> So the vocabulary the new numbers would be spent on is banned in the same
    /// slice that adds them, and the prove-fail below is fed those sentences rather than the
    /// old ones.</para>
    ///
    /// <para>"Hard", "tough" and "trivial" are the ones worth naming out loud, because they
    /// are the words a reasonable person reaches for when they have a difficulty-shaped
    /// number and no difficulty model. EQBuddy has no mob-HP model and no con-colour scale;
    /// the permitted shape is a measurement with its scope, and the comparison is against
    /// this character's own pooled figures, never against a claim about the place.</para>
    /// </remarks>
    private static readonly string[] Forbidden =
    [
        "safe", "safer", "safest", "safely", "safety", "unsafe",
        "easy", "easier", "easiest", "easily",
        "dangerous", "danger", "deadly", "lethal", "risky",
        "survivable", "survivability", "forgiving", "harmless", "brutal", "punishing",
        // DRA-71 D4 (plan P7): the throughput-vs-difficulty vocabulary.
        "hard", "harder", "hardest", "tough", "tougher", "toughest",
        "trivial", "trivially", "comfortable", "comfortably", "manageable",
        "overwhelming", "overmatched", "outmatched", "outclassed", "punishes",
        "efficient", "efficiently", "optimal", "suboptimal", "underperforming",
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
            AssertClean(HelperPresentation.GearWithheld(n), $"GearWithheld({n})");
            AssertClean(HelperPresentation.DropOffersWithheld(n), $"GearWhoWithheld({n})");
        }

        // DRA-149 D2's unread sentence, at every shape it has: silent, one, exactly the name
        // cap, and past it. It names ITEMS rather than places, which is a different way into
        // the same temptation — "nothing better in that slot" is a claim about the game, and
        // this sentence's whole subject is EQBuddy's own catalog.
        foreach (var unread in new IReadOnlyList<string>[]
                 {
                     [],
                     ["Deterioriated Ancient Faydark Longbow +2"],
                     ["Shiny Brass Shield +6", "Lute +1", "Mystery Pauldrons"],
                     [
                         "Shiny Brass Shield +6", "Lute +1", "Mystery Pauldrons",
                         "Cracked Staff +3", "Bronze Greaves",
                     ],
                 })
            AssertClean(HelperPresentation.UnreadWorn(unread), $"UnreadWorn({unread.Count})");

        // **DRA-84 D2's refusal sentence, at the values that would most tempt an adjective.**
        // "eqlwiki lists its creatures at 5-20 and you are 30" is a place somebody would
        // reasonably write "too easy now" or "too tough" — the band gate is a difficulty-shaped
        // rule with no difficulty model behind it, which is exactly the D4 lesson one slice on.
        // Both arms, a single-level band, an OPEN top, and past the name cap.
        foreach (var refusals in new IReadOnlyList<GearBandRefusal>[]
                 {
                     [],
                     [new("Crushbone", 5, 20, "5-20", 30, GearBandArm.TopUnder)],
                     [new("Plane of Sky", 50, null, "50+", 12, GearBandArm.BottomOver)],
                     [new("Befallen", 12, 12, "12", 40, GearBandArm.TopUnder)],
                     [
                         new("Crushbone", 5, 20, "5-20", 30, GearBandArm.TopUnder),
                         new("Plane of Sky", 50, null, "50+", 30, GearBandArm.BottomOver),
                         new("Najena", 8, 35, "8-35", 60, GearBandArm.TopUnder),
                         new("Befallen", 7, 25, "7-25", 60, GearBandArm.TopUnder),
                     ],
                 })
            AssertClean(HelperPresentation.BandRefused(refusals, HelperPresentation.BandRefusedUpgrades),
                $"GearBandRefused({refusals.Count})");

        foreach (var (min, max) in new (int, int?)[] { (5, 20), (12, 12), (50, null) })
            AssertClean(HelperPresentation.BandPhrase(min, max), $"BandPhrase({min},{max})");

        // **DRA-180 D3's per-anchor sentence, and it is the one most likely to grow an
        // adjective.** It is the only line on this screen that says "nothing here beats what
        // you are wearing", which is one word away from "your gear is good" — a claim about
        // the player rather than about the catalog. Every cause combination, singular and
        // plural, plus the cap line.
        foreach (var anchor in new GearAnchorRemoved[]
                 {
                     new("Deteriorated Ancient Faydark Longbow", "RANGE", 2, 0, 2, 0),
                     new("The Baron's Blade", "PRIMARY", 1, 1, 0, 0),
                     new("Rusty Helm", "HEAD", 1, 0, 0, 1),
                     new("Rusty Helm", "HEAD", 3, 1, 1, 1),
                     new("Rusty Helm", "HEAD", 8, 4, 3, 1),
                 })
            AssertClean(HelperPresentation.AnchorAllRemoved(anchor),
                $"AnchorAllRemoved({anchor.Anchor}/{anchor.Found})");

        foreach (var held in new[] { 0, 1, 5 })
            AssertClean(HelperPresentation.AnchorsNotNamed(held), $"AnchorsNotNamed({held})");

        foreach (var maxed in new[] { false, true })
            AssertClean(
                HelperPresentation.FactionChip(new FactionsFile.Standing(1, "Faydark Rangers", 1200, maxed ? 0 : 800)),
                $"FactionChip(maxed:{maxed})");
    }

    /// <summary>
    /// **The sweep reaches <see cref="LevelReadout"/> too** (DRA-71 D3).
    ///
    /// <para>The Helper draws two of that file's sentences — the level it ranked with, and
    /// what it says when it has none — so the ban has to follow them there. A vocabulary guard
    /// that stopped at the file it was written for is the first thing that goes wrong when a
    /// producer moves out to be shared, and this one moved out on the day it landed.</para>
    ///
    /// <para>An outgrown zone is the most likely place the forbidden words would ever arrive
    /// in good faith — "you have outgrown this camp, it is too easy now" is the sentence
    /// somebody writes — so the discount's own line is swept at several bands rather than at
    /// the one the fixture builder happens to pick.</para>
    /// </summary>
    [Fact]
    public void NoLevelSentenceUsesSafetyVocabulary()
    {
        var seen = 0;
        foreach (var f in typeof(LevelReadout).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is string s) { AssertClean(s, $"LevelReadout.{f.Name}"); seen++; }
        Assert.True(seen >= 6,
            $"Only {seen} LevelReadout constants were reached — the reflection has stopped "
            + "finding them. Check the type, not the assertion.");

        foreach (var source in Enum.GetValues<LevelSource>())
        {
            var level = new ResolvedLevel(source == LevelSource.Unknown ? 0 : 30, source,
                new DateTime(2026, 9, 12, 20, 0, 0));
            AssertClean(LevelReadout.Line(level), $"LevelReadout.Line({source})");
            AssertClean(LevelReadout.UsedByHelper(level), $"LevelReadout.UsedByHelper({source})");
        }

        foreach (var (min, max, level) in new[] { (8, 12, 60), (1, 4, 11), (45, 50, 60) })
            AssertClean(
                HelperPresentation.Why(new ZoneOutgrownFact("Lower Guk", min, max, level, 200)),
                $"Why(ZoneOutgrownFact {min}-{max} at {level})");
    }

    /// <summary>
    /// **The discount's sentence reports two measurements and predicts nothing.**
    ///
    /// <para>Both bounds, the kill count that is the band's denominator, and the level it is
    /// being compared against — everything a player needs to disagree with it. What is NOT in
    /// it is a verdict: no "too low", no "move on", no estimate of what an hour there would
    /// pay now. This repo has no XP curve, eqlwiki publishes none, and a number invented to
    /// fill that gap would be trap 73's shape with arithmetic instead of prose.</para>
    /// </summary>
    [Fact]
    public void TheOutgrownSentenceNamesTheBandTheKillsAndYourLevel()
    {
        var line = HelperPresentation.Why(new ZoneOutgrownFact("Lower Guk", 8, 12, 50, 200));

        Assert.Contains("8", line);
        Assert.Contains("12", line);
        Assert.Contains("200", line);
        Assert.Contains("50", line);
        // Personal evidence, so no estimate label — it is a measurement of this player's own
        // /consider lines and their own stated or observed level (HOME-004).
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, line);
        foreach (var predicted in new[] { "should", "will", "expect", "instead" })
            Assert.DoesNotContain(predicted, line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **The Helper names the level it used, and says which of the two writers it came
    /// from** (plan P4).
    ///
    /// <para>A ranking that quietly weighed a number the player disagrees with, and never
    /// said which, is the shape that makes somebody distrust the whole room. The source rides
    /// with it through <c>CharacterLevel.SourceLabel</c> — the one table — rather than being
    /// re-phrased here, which is the rule that stopped the phone from growing a second verb
    /// around the class version of it.</para>
    /// </summary>
    [Fact]
    public void TheHelperDisclosesTheLevelItRankedWithAndWhereItCameFrom()
    {
        var stated = LevelReadout.UsedByHelper(
            new ResolvedLevel(28, LevelSource.Stated, DateTime.Today));
        Assert.Contains("28", stated);
        Assert.Contains(CharacterLevel.SourceLabel(LevelSource.Stated), stated);

        var observed = LevelReadout.UsedByHelper(
            new ResolvedLevel(31, LevelSource.Observed, DateTime.Today));
        Assert.Contains("31", observed);
        Assert.Contains(CharacterLevel.SourceLabel(LevelSource.Observed), observed);

        // Unknown says what the room DID anyway rather than apologising, and names no number
        // at all — a guessed level disclosed as a fact would be worse than none.
        var unknown = LevelReadout.UsedByHelper(ResolvedLevel.Unknown);
        Assert.Equal(LevelReadout.HelperUnknown, unknown);
        Assert.DoesNotContain("0", unknown);
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
    // DRA-71 D4: the four sentences a THROUGHPUT feature invites. Every one of them is what
    // somebody writes when they have your dps, your fight length and no difficulty model —
    // which is HOME-006's own worked example ("do not claim a camp is safe merely because the
    // player's DPS is high") arriving through the new numbers rather than the old ones.
    [InlineData("Trivial for your output — your fights here run 6 seconds.")]
    [InlineData("These creatures are too tough for your damage right now.")]
    [InlineData("A comfortable camp for a character putting out 60 a second.")]
    [InlineData("Your output here is underperforming; pick somewhere more efficient.")]
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
        // DRA-71 D4's four, at the values that would most tempt an adjective: an output well
        // under the baseline, fights at twice the usual length, and most of a sitting idle.
        // The widened ban has to pass all of them, or it is catching everything and proving
        // nothing.
        Assert.Null(Hit(HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 12.4, 0, 12.4, 61.8, 4200, 6))));
        Assert.Null(Hit(HelperPresentation.Why(
            new ZoneCadenceFact("Lower Guk", 96, 480, 41))));
        Assert.Null(Hit(HelperPresentation.Why(
            new ZoneDowntimeFact("Lower Guk", 0.81, 9, 21.5))));
        Assert.Null(Hit(HelperPresentation.Why(new ZoneTierFact("Najena 4 (Refined)", 4))));
    }

    // ---- the D4 sentences (plan P7) --------------------------------------------------------

    /// <summary>
    /// **The throughput line reports three measurements and draws no conclusion from
    /// them** — Founder smoke item 3, answered without a difficulty model because there is
    /// none.
    ///
    /// <para>What is in it: the damage, how long the fighting lasted, the pooled figure and
    /// the number of zones that figure rests on. What is NOT in it is a verdict — no
    /// judgement on whether the camp suits the character, and no prediction of what an hour
    /// there would pay. A player reading "12.4 here against your usual 61.8" has the whole
    /// finding; EQBuddy has no mob-HP model with which to draw a conclusion from it.</para>
    /// </summary>
    [Fact]
    public void TheThroughputSentenceNamesYourOutputItsScopeAndYourOwnBaseline()
    {
        var line = HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 12.4, 0, 12.4, 61.8, 4200, 6));

        Assert.Contains("12.4", line);
        Assert.Contains("61.8", line);
        Assert.Contains("6 zones", line);
        // The scope: 4,200 combat seconds said the way a person would say it.
        Assert.Contains("1.2 hours", line);
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, line);
        foreach (var verdict in new[] { "should", "better", "instead", "expect", "recommend" })
            Assert.DoesNotContain(verdict, line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **Healing is drawn only when healing is a real part of what this character did** —
    /// and the trace case is the one the staged shot caught.
    ///
    /// <para>At <c>Hps &gt; 0</c> the fixture's WARRIOR came back saying "You healed 0.1 a
    /// second", because a log with regen ticks in it is not a log with zero healing. The
    /// sentence was correct, the number was real, and it was still furniture on a character
    /// who does not heal (trap 23: the picture is what found it, and no assertion in the repo
    /// could have). <c>HelperPresentation.HealingClauseShare</c> is the fix and this is the
    /// row that pins it — both arms, because a threshold with only the passing side tested
    /// would move without anything noticing.</para>
    /// </summary>
    [Fact]
    public void TheHealingClauseAppearsForAHealerAndNotForATraceOfRegen()
    {
        var healer = HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 8.1, 44.2, 52.3, 61.8, 4200, 6));
        Assert.Contains("44.2", healer);
        Assert.Contains("healed", healer);

        // The staged shot's own numbers: 13.3 damage a second and 0.1 healing, which is
        // under a hundredth of the output.
        var warrior = HelperPresentation.Why(
            new ZoneThroughputFact("West Commonlands", 13.3, 0.1, 13.4, 0, 4680, 0));
        Assert.DoesNotContain("healed", warrior);

        // And a character with no healing at all, which is the arm that was always right.
        Assert.DoesNotContain("healed", HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 61.0, 0, 61.0, 0, 4200, 0)));
    }

    /// <summary>
    /// **The baseline comparison is drawn only when there is something to notice** — the
    /// second thing the staged shot caught.
    ///
    /// <para>The first take read "your damage and healing together run 13.2 a second; here,
    /// 13.4": a whole line spent saying a zone is exactly average. Same lesson as the cadence
    /// clause, which already went silent when the two rounded to the same words, and the same
    /// one the downtime line is built on — a line that never varies tells a player nothing,
    /// and the primary figure is on screen either way.</para>
    /// </summary>
    [Fact]
    public void TheBaselineClauseIsSilentWhenTheZoneIsSimplyAverage()
    {
        // The staged shot's own numbers.
        Assert.DoesNotContain("Across the", HelperPresentation.Why(
            new ZoneThroughputFact("Kithicor Forest", 13.3, 0, 13.4, 13.2, 4680, 2)));

        // Well under, and well over, both say so.
        Assert.Contains("Across the 6 zones", HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 12.4, 0, 12.4, 61.8, 4200, 6)));
        Assert.Contains("Across the 6 zones", HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 90.0, 0, 90.0, 61.8, 4200, 6)));
    }

    /// <summary>
    /// **A ZONE IS NEVER MARKED DOWN IN SILENCE, AND THIS IS THE ROW THAT KEEPS IT TRUE
    /// ACROSS TWO FILES.**
    ///
    /// <para>The discount threshold lives in Core and the clause threshold lives here, and
    /// the rule that matters spans them: any zone whose output fires
    /// <c>Recommendations.ThroughputShortfall</c> must be far enough from its baseline to
    /// clear <c>HelperPresentation.BaselineClauseGap</c>, or the ranking would move and the
    /// explanation would be the sentence that got suppressed. Asserted as the RELATIONSHIP
    /// between the two numbers rather than as two numbers, because "0.6 and 0.1" is a fact
    /// about today and "the discount is well outside the silence band" is the rule.</para>
    /// </summary>
    [Fact]
    public void AnOutputThatFiresTheDiscountAlwaysClearsTheSilenceBand()
    {
        Assert.True(1 - Recommendations.ThroughputShortfall > HelperPresentation.BaselineClauseGap,
            $"a zone at {Recommendations.ThroughputShortfall:P0} of its baseline is inside the "
            + $"{HelperPresentation.BaselineClauseGap:P0} band where the comparison goes silent — "
            + "so a discounted zone could be ranked down with nothing on screen saying why.");

        // And the worked case: exactly at the shortfall threshold, the clause is drawn.
        var atThreshold = 61.8 * Recommendations.ThroughputShortfall;
        Assert.Contains("Across the", HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", atThreshold, 0, atThreshold, 61.8, 4200, 6)));
    }

    /// <summary>
    /// **A single measured zone gets NO comparison clause.**
    ///
    /// <para>A baseline folded from one zone is that zone, so the clause would read "your
    /// average is exactly this" — a tautology printed as a finding. The engine passes 0 for
    /// the baseline in that state and this is where the 0 becomes silence.</para>
    /// </summary>
    [Fact]
    public void WithOneMeasuredZoneTheThroughputSentenceComparesItWithNothing()
    {
        var line = HelperPresentation.Why(
            new ZoneThroughputFact("Lower Guk", 42.0, 0, 42.0, 0, 3600, 0));

        Assert.Contains("42.0", line);
        Assert.DoesNotContain("Across", line);
        Assert.DoesNotContain("zones", line);
    }

    /// <summary>The cadence line gained its own baseline clause in D4, and it is silent both
    /// when there is nothing to compare against and when the two round to the same words —
    /// "they run 41 sec; you average 41 sec" is a line spent saying nothing.</summary>
    [Fact]
    public void TheCadenceSentenceComparesWithYourOwnAverageOrSaysNothing()
    {
        var slower = HelperPresentation.Why(new ZoneCadenceFact("Lower Guk", 96, 480, 41));
        Assert.Contains("1.6 min", slower);
        Assert.Contains("41 sec", slower);

        Assert.DoesNotContain("Everywhere",
            HelperPresentation.Why(new ZoneCadenceFact("Lower Guk", 96, 480)));
        Assert.DoesNotContain("Everywhere",
            HelperPresentation.Why(new ZoneCadenceFact("Lower Guk", 41.2, 480, 41.4)));
    }

    /// <summary>
    /// **The downtime line says WHAT was measured and never WHY.**
    ///
    /// <para>The active figure counts two-minute stretches that contained an event, so
    /// medding, travelling, a bank trip and a corpse run are one thing to it. Naming a cause
    /// would be inventing the half the log did not record (trap 73) — and every one of those
    /// causes is a different thing for a player to act on.</para>
    /// </summary>
    [Fact]
    public void TheDowntimeSentenceNamesTheShareAndNoCauseForIt()
    {
        var line = HelperPresentation.Why(new ZoneDowntimeFact("Lower Guk", 0.81, 9, 21.5));

        Assert.Contains("81%", line);
        Assert.Contains("9 sessions", line);
        Assert.Contains("21.5 hours", line);
        foreach (var cause in new[] { "med", "travel", "bank", "corpse", "waiting", "afk" })
            Assert.DoesNotContain(cause, line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **The tier line says it was the player's OWN zone line that recorded it**, not that
    /// the zone is anything. It is the one difficulty word the game itself states, read where
    /// the session already stored it — nothing was looked up, so there is no estimate label,
    /// and it is not an adjective about the place.
    /// </summary>
    [Fact]
    public void TheTierSentenceAttributesTheTierToYourOwnLog()
    {
        var line = HelperPresentation.Why(new ZoneTierFact("Najena 4 (Refined)", 4));

        Assert.Contains("D4", line);
        Assert.Contains("your own zone line", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, line);

        // Every tier the game states gets a badge, D0 included — a base instance is a real
        // observation and "D0" is what InstanceTier calls it.
        for (var tier = 0; tier <= 4; tier++)
            Assert.Contains($"D{tier}", HelperPresentation.Why(new ZoneTierFact("Najena", tier)));
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

    // ---- DRA-149 D2: the unread worn sentence ------------------------------------------

    /// <summary>
    /// **The Founder's bow, said out loud** — the sentence names it under the spelling the game
    /// printed, counts it, and puts the subject on EQBuddy's catalog rather than on the game.
    /// </summary>
    [Fact]
    public void TheUnreadSentenceNamesTheItemTheDumpNamed()
    {
        var said = HelperPresentation.UnreadWorn(["Deterioriated Ancient Faydark Longbow +2"]);

        Assert.Contains("Deterioriated Ancient Faydark Longbow +2", said);
        // Singular, because a sentence that says "1 things" reads as a bug in the sentence and
        // makes a player doubt the number beside it.
        Assert.Contains("1 thing you are wearing", said);
        Assert.DoesNotContain("1 things", said);
        // The subject. "That item does not exist" would be a claim about the world; what
        // happened is that a name matched nothing EQBuddy ships.
        Assert.Contains("EQBuddy has never read about", said);
        Assert.Contains("item pages EQBuddy ships", said);
    }

    /// <summary>A cap that hid how much it was hiding is the silence this sentence exists to
    /// end (trap 50). Three are named, the REST are counted, and the leading number is the
    /// WHOLE count rather than the named one.</summary>
    [Fact]
    public void TheUnreadSentenceNamesThreeAndCountsTheRest()
    {
        var said = HelperPresentation.UnreadWorn(
            ["First Thing", "Second Thing", "Third Thing", "Fourth Thing", "Fifth Thing"]);

        Assert.Equal(3, HelperPresentation.UnreadWornNamed);
        Assert.Contains("5 things you are wearing", said);
        Assert.Contains("First Thing, Second Thing, Third Thing, and 2 more", said);
        Assert.DoesNotContain("Fourth Thing", said);
        // Exactly at the cap it counts nothing, rather than saying "and 0 more".
        Assert.DoesNotContain("more",
            HelperPresentation.UnreadWorn(["First Thing", "Second Thing", "Third Thing"]));
    }

    /// <summary>Nothing unread says NOTHING — the committed negative, so a caption that had
    /// learned to fire always fails here rather than over a room with nothing wrong in
    /// it.</summary>
    [Fact]
    public void AnUnreadSentenceWithNothingToSayIsSilent()
    {
        Assert.Empty(HelperPresentation.UnreadWorn([]));
        Assert.Empty(HelperPresentation.UnreadWorn(null!));
    }

    /// <summary>
    /// **The unreadable-dump gap must not ask for the dump again.**
    ///
    /// <para>That is the whole reason it is not <see cref="GoalGapReason.NoInventoryDump"/>: the
    /// player has run the command, and telling them to run it produces the same unreadable rows
    /// forever. The two sentences are asserted against each other so a later edit cannot quietly
    /// converge them.</para>
    /// </summary>
    [Fact]
    public void TheUnreadableDumpGapDoesNotAskForAnotherDump()
    {
        var unreadable = HelperPresentation.Gap(
            new GoalGap(HelperGoal.FarmGear, GoalGapReason.NothingWornIsReadable));
        var missing = HelperPresentation.Gap(
            new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoInventoryDump));

        Assert.DoesNotContain("Run the inventory command", unreadable);
        Assert.Contains("Run the inventory command", missing);
        Assert.NotEqual(missing, unreadable);
        // It says the dump ARRIVED, which is the fact that makes the two different.
        Assert.Contains("your inventory dump is here", unreadable);
        Assert.Contains("eqlwiki", unreadable);
    }

    /// <summary>The item door is a SEARCH and says so: it exists because the name matched no
    /// page, so "open its page" would promise the thing that just failed. And like every wiki
    /// door it says EQBuddy fetches nothing.</summary>
    [Fact]
    public void TheItemWikiDoorOffersASearchAndFetchesNothing()
    {
        var tip = HelperPresentation.DoorTip(new HelperDoor(
            HelperDoorKind.WikiItem, "Deterioriated Ancient Faydark Longbow +2"));

        Assert.Contains("Search eqlwiki", tip);
        Assert.Contains("Deterioriated Ancient Faydark Longbow +2", tip);
        Assert.Contains("never fetches", tip);
        Assert.Equal("eqlwiki", HelperPresentation.DoorLabel(HelperDoorKind.WikiItem));
        // A page is not a room — the null arm is a real answer, and a caller that read it as
        // "no door" would drop the only affordance this caption has.
        Assert.Null(HelperPresentation.AddressFor(HelperDoorKind.WikiItem));
    }

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

    // ---- the professions block (DRA-71 D8) -------------------------------------------

    /// <summary>
    /// **Unknown is not a zero, and the sentence names the line it is waiting for.**
    ///
    /// <para>"You are at 0" is false for everybody who crafted before EQBuddy was watching, and
    /// it is the number a reader that only checked for presence would print. The permitted
    /// shape is the one the whole Helper keeps: say what has not been seen, and name what would
    /// feed it — here, the game's own skill-up line, which is the only thing a player can
    /// act on.</para>
    /// </summary>
    [Fact]
    public void AnUnseenProfessionSaysSoRatherThanPrintingAZero()
    {
        var line = HelperPresentation.ProfessionStanding(
            new TradeskillStanding(Tradeskill.Baking, 0, default));

        Assert.Contains("Baking", line);
        Assert.DoesNotContain("0", line);
        // And the explanation is NOT on the row — it is the block's, said once. Eight rows
        // repeating one thirty-word sentence is what the first staged shot of this block came
        // back as, and distinct-count is the tell in prose exactly as it is in data (trap 73).
        Assert.DoesNotContain("become better at", line);
        Assert.Contains("become better at", HelperPresentation.ProfessionLearnNote);
    }

    /// <summary>A known standing reports the number and the day, and predicts nothing about
    /// what it would take to raise it — this repo has no skill curve and eqlwiki publishes
    /// none.</summary>
    [Fact]
    public void AKnownStandingNamesTheNumberAndTheDay()
    {
        var line = HelperPresentation.ProfessionStanding(new TradeskillStanding(
            Tradeskill.Blacksmithing, 122, new DateTime(2026, 9, 7, 21, 14, 3)));

        Assert.Contains("122", line);
        Assert.Contains("Sep 7", line);
        Assert.DoesNotContain("trivial", line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A row with a value it cannot date still reads as a sentence — an archived
    /// snapshot from before DRA-71 D8 carries no moment, and a surface that printed
    /// "on Jan 1" for it would be inventing one.</summary>
    [Fact]
    public void AStandingWithNoMomentDoesNotInventADate()
    {
        var line = HelperPresentation.ProfessionStanding(
            new TradeskillStanding(Tradeskill.Pottery, 40, default));

        Assert.Contains("40", line);
        Assert.DoesNotContain("Jan 1", line);
        Assert.EndsWith(".", line);
    }

    /// <summary>The watch control's two states are different words, and both say what they
    /// are. A control that read the same either way would make a second click look like it
    /// did something.</summary>
    [Fact]
    public void TheWatchPresetHasTwoDistinctLabels()
    {
        Assert.NotEqual(
            HelperPresentation.WatchPresetLabel(true), HelperPresentation.WatchPresetLabel(false));
        Assert.Contains("skill-ups", HelperPresentation.WatchPresetLabel(true));
        Assert.Contains("skill-ups", HelperPresentation.WatchPresetLabel(false));
    }

    /// <summary>
    /// **The park note carries the measurement, and the measurement is what makes it honest.**
    ///
    /// <para>"EQBuddy does not rank this yet" is a shrug. "Of the 11,197 item pages it has
    /// read, 14 say which profession an ingredient belongs to" is a survey result a player —
    /// or a reporter — can argue with, and it is the number that decides when the parked
    /// arithmetic reopens. Pinned here so a later edit cannot quietly drop it back to the
    /// shrug.</para>
    ///
    /// <para><b>AND PINNED TO THE SURVEY RATHER THAN TO ITSELF.</b> This test used to assert
    /// the sentence contained the literal <c>"10,957"</c> — which is the sentence quoting
    /// itself. It stayed GREEN through the DRA-84 D3 weekly refresh (#626), which moved the
    /// real count to 11,197 and left a player-facing survey result its own committed report
    /// contradicts. A guard that can only catch somebody DELETING a number, never the number
    /// going wrong, is trap 34's shape sitting on trap 4's: the report and this literal were
    /// two producers of one fact. Both numbers now come out of <c>items-catalog-report.md</c>,
    /// which <c>itemcatalog-build</c> rewrites on every refresh, so the NEXT refresh that
    /// moves either one reddens here instead of reaching a player.</para>
    /// </summary>
    [Fact]
    public void TheParkNoteNamesTheCoverageItMeasured()
    {
        var (pages, recipes) = RecipeSurvey();

        Assert.Contains(pages.ToString("N0"), HelperPresentation.ProfessionsFarmNote);
        Assert.Contains(recipes.ToString("N0"), HelperPresentation.ProfessionsFarmNote);
    }

    /// <summary>
    /// **And the pin above can actually fail** — the committed negative, because "the sentence
    /// contains a number" was green for no reason before (trap 78). The stale text below is
    /// what really shipped on `main` between #626 and this change.
    /// </summary>
    [Fact]
    public void AParkNoteQuotingTheSurveyItWasWrittenAgainstIsWhatThisCatches()
    {
        const string stale =
            "EQBuddy does not rank where to farm materials yet. Of the 10,957 item pages it "
            + "has read, 14 say which profession an ingredient belongs to.";

        var (pages, _) = RecipeSurvey();

        Assert.DoesNotContain(pages.ToString("N0"), stale);
    }

    /// <summary>
    /// **THE PARK'S OWN NUMBER MUST NOT BE THE ONE ON SCREEN** (DRA-149 D3).
    ///
    /// <para>The sentence this replaced was measured on <c>[[Category:…]]</c> — "14 of 11,197
    /// pages name a profession" — and it was TRUE the whole time it was wrong to print: it
    /// answered a different question from the one the block asks. The report carries both
    /// columns, so the committed negative is that the profession-CATEGORY count is not what the
    /// farming sentence quotes; a future edit that reached for the familiar number would go red
    /// here rather than telling players EQBuddy cannot do a thing it now does.</para>
    /// </summary>
    [Fact]
    public void TheFarmNoteQuotesTheRecipesColumnAndNotTheCategoriesOne()
    {
        var (_, naming) = CategorySurvey();
        var (_, recipes) = RecipeSurvey();

        Assert.NotEqual(naming, recipes);
        Assert.DoesNotContain(
            $"{naming:N0} say which profession", HelperPresentation.ProfessionsFarmNote);
    }

    /// <summary>The recipes column's two numbers, read from the same committed report the
    /// category survey is read from and for the same reason: the sentence must not be the only
    /// producer of a number it states as a measurement (trap 4). A parse that yields nothing is
    /// itself a failure (trap 78).</summary>
    private static (int Pages, int Recipes) RecipeSurvey()
    {
        var report = Report();

        var pages = Regex.Match(report,
            @"item pages with at least one Category:\s*[\d,]+\s+of\s+([\d,]+)");
        var recipes = Regex.Match(report,
            @"pages with a recipes field:\s*([\d,]+)");

        Assert.True(pages.Success && recipes.Success,
            "items-catalog-report.md did not yield the recipe survey numbers — "
            + "itemcatalog-build's report format moved and this guard stopped measuring "
            + "anything.");

        return (int.Parse(pages.Groups[1].Value.Replace(",", "")),
                int.Parse(recipes.Groups[1].Value.Replace(",", "")));
    }

    /// <summary>The committed promoter report, which both surveys above read.</summary>
    private static string Report()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(
            repoRoot, "scripts", "harvests", "eqlwiki", "items-catalog-report.md"));
    }

    /// <summary>The two numbers the promoter's own survey printed, read from the committed
    /// report. A parse that yields nothing is itself a failure — a guard whose input silently
    /// went missing measures nothing at all (trap 78).</summary>
    private static (int Pages, int Naming) CategorySurvey()
    {
        var report = Report();

        var pages = Regex.Match(report,
            @"item pages with at least one Category:\s*[\d,]+\s+of\s+([\d,]+)");
        var naming = Regex.Match(report,
            @"categories naming a PROFESSION:\s*([\d,]+)\s+pages");

        Assert.True(pages.Success && naming.Success,
            "items-catalog-report.md did not yield the two survey numbers — itemcatalog-build's "
            + "report format moved and this guard stopped measuring anything.");

        return (int.Parse(pages.Groups[1].Value.Replace(",", "")),
                int.Parse(naming.Groups[1].Value.Replace(",", "")));
    }

    /// <summary>The face may say "all" here and may not on the faction picker beside it —
    /// this offer is the whole curated eight and nothing is capped away.</summary>
    [Fact]
    public void TheProfessionFaceMaySayAllBecauseNothingIsCapped()
    {
        var all = Tradeskills.All.Select(p => p.Name).ToArray();

        Assert.Equal("Any profession", HelperPresentation.ProfessionFace([], all.Length));
        Assert.Equal("All professions", HelperPresentation.ProfessionFace(all, all.Length));
        Assert.Equal("Pottery", HelperPresentation.ProfessionFace(["Pottery"], all.Length));
    }

    /// <summary>Every profession sentence goes through the same ban as the rest of the
    /// room — the sweep that could not see them is the sweep that stops covering the feature
    /// somebody adds next.</summary>
    [Fact]
    public void NoProfessionSentenceUsesSafetyVocabulary()
    {
        foreach (var skill in Enum.GetValues<Tradeskill>())
        {
            AssertClean(HelperPresentation.WatchRuleName(skill), $"WatchRuleName({skill})");
            foreach (var standing in new[]
                     {
                         new TradeskillStanding(skill, 0, default),
                         new TradeskillStanding(skill, 122, new DateTime(2026, 9, 7)),
                     })
            {
                AssertClean(HelperPresentation.ProfessionRow(standing), $"ProfessionRow({skill})");
                AssertClean(HelperPresentation.ProfessionStanding(standing),
                    $"ProfessionStanding({skill})");
            }
        }

        foreach (var watching in new[] { false, true })
            AssertClean(HelperPresentation.WatchPresetLabel(watching), $"WatchPresetLabel({watching})");

        AssertClean(HelperPresentation.ProfessionFace(["Pottery", "Baking"], 8), "ProfessionFace");
        AssertClean(HelperPresentation.ProfessionLearnNote, "ProfessionLearnNote");
    }

    /// <summary>
    /// **THE DEFERRAL IS GONE AND NOTHING MAY SAY OTHERWISE** (DRA-149 D3, plan P4).
    ///
    /// <para>This test used to assert that Farm Materials' deferral named the half that was
    /// missing — "not ranking WHERE to farm the materials yet". D3 built that half, so the
    /// sentence had to leave with the shape, and the FLIP is what gets asserted now: an engine
    /// that answers must not also carry a sentence telling players it does not. The pairing
    /// guard beside this one (<c>EveryDeferredGoalNamesTheRoomThatAnswersItToday</c>) covers
    /// the other direction — a Deferred goal with no sentence.</para>
    ///
    /// <para><b>The block's own note is asserted here too, because that is where the promise
    /// moved.</b> The professions block still explains itself; what it says changed from "this
    /// is not ranked" to where the rows below it came from and what has no page at all.</para>
    /// </summary>
    [Fact]
    public void FarmMaterialsNoLongerCarriesADeferralSentence()
    {
        Assert.Equal(HelperGoalShape.Answered, Recommendations.ShapeFor(HelperGoal.FarmMaterials));
        Assert.Empty(HelperPresentation.NotAnsweredYet(HelperGoal.FarmMaterials));
        Assert.Null(HelperPresentation.NotAnsweredDoor(HelperGoal.FarmMaterials));

        var note = HelperPresentation.ProfessionsFarmNote;
        Assert.Contains("recipe", note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not rank", note, StringComparison.OrdinalIgnoreCase);
        // The honest gap MOVED rather than closed, and the sentence still carries it — bought,
        // foraged and crafted ingredients have no drop page, which is the whole of Fletching.
        Assert.Contains("foraged", note, StringComparison.OrdinalIgnoreCase);
    }

    // ---- DRA-180 D3: the per-anchor sentence's subject rules --------------------------

    /// <summary>
    /// **THE SUBJECT IS EQBUDDY'S CATALOG, NEVER THE GAME** — the never-BiS lock, restated for
    /// the one sentence on this screen that could most easily break it.
    ///
    /// <para>"Nothing in reach beats this item's base" is a claim about what EQBuddy has READ
    /// and what its own gates did with it. It is one careless edit away from "this is the best
    /// bow in the game", which the catalog cannot support and which the Gear Locker has refused
    /// to say since #104. The sentence must lead with what EQBuddy found, name its own reading
    /// as the source, and never generalise past it.</para>
    /// </summary>
    [Fact]
    public void ThePerAnchorSentenceSpeaksAboutWhatEqbuddyReadAndNotAboutTheGame()
    {
        var said = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Deteriorated Ancient Faydark Longbow", "RANGE", 2, 0, 2, 0));

        // It leads with the FINDING. "EQBuddy has read about 2 better base items" is the
        // evidence that the sweep ran, and an empty screen's whole problem was that it was
        // indistinguishable from one that had not.
        Assert.Contains("EQBuddy has read about", said);
        Assert.Contains("2 better base items", said);
        // It names the worn item, because a caption about a zone does not answer a question
        // about a bow — which is the entire reason this record exists.
        Assert.Contains("Deteriorated Ancient Faydark Longbow", said);
        Assert.Contains("range", said);
        // It cites eqlwiki as the source of the numbers that removed them.
        Assert.Contains("eqlwiki", said);

        // And it makes no claim about the game or about the player.
        foreach (var forbidden in new[]
                 {
                     "best in slot", "best-in-slot", "your gear is", "nothing in the game",
                     "no better", "you are well equipped", "perfect",
                 })
            Assert.DoesNotContain(forbidden, said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **A CAUSE THAT REMOVED NOTHING IS NOT NAMED** (plan D3: a sentence about a gate that did
    /// not run is furniture).
    ///
    /// <para>This is live on every shipped build, not a hypothetical: <c>WorldEra.Current</c> is
    /// empty, so the era gate stands down, so <c>LaterContent</c> is 0 for every player today.
    /// A sentence reading "0 come from content eqlwiki dates later than…" would be EQBuddy
    /// explaining a rule it did not apply.</para>
    /// </summary>
    [Fact]
    public void OnlyTheCausesThatRemovedSomethingAppearInThePerAnchorSentence()
    {
        var bandOnly = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 2, 0, 2, 0));
        Assert.Contains("creature levels outside yours", bandOnly);
        Assert.DoesNotContain("later than the era", bandOnly);
        Assert.DoesNotContain("name nothing that drops them", bandOnly);

        var eraOnly = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 2, 2, 0, 0));
        Assert.Contains("later than the era", eraOnly);
        Assert.DoesNotContain("creature levels outside yours", eraOnly);

        // All three, and the list reads as a sentence rather than as three sentences.
        var all = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 3, 1, 1, 1));
        Assert.Contains("later than the era", all);
        Assert.Contains("creature levels outside yours", all);
        Assert.Contains("name nothing that drops them", all);
        Assert.Contains(", ", all);
        Assert.Contains(" and ", all);

        // Nothing found at all is not this sentence's subject — NoCatalogUpgrade owns it.
        Assert.Equal("", HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 0, 0, 0, 0)));
    }

    /// <summary>Singular and plural are both written out, because "1 better base items" is the
    /// tell that a sentence was never read at the value it will most often be produced at.
    /// </summary>
    [Fact]
    public void ThePerAnchorSentenceReadsAtOneAsWellAsAtMany()
    {
        var one = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 1, 0, 1, 0));
        Assert.Contains("1 better base item ", one);
        Assert.Contains("left it out", one);
        Assert.Contains("1 drops only where", one);

        var many = HelperPresentation.AnchorAllRemoved(
            new GearAnchorRemoved("Rusty Helm", "HEAD", 4, 0, 4, 0));
        Assert.Contains("4 better base items", many);
        Assert.Contains("left every one out", many);
        Assert.Contains("4 drop only where", many);

        // The cap line is silent at zero and says the number otherwise (trap 50).
        Assert.Equal("", HelperPresentation.AnchorsNotNamed(0));
        Assert.Contains("1 more worn item ", HelperPresentation.AnchorsNotNamed(1));
        Assert.Contains("5 more worn items", HelperPresentation.AnchorsNotNamed(5));
    }
}

