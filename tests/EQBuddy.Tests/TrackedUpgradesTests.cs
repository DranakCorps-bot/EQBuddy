using System.Text.Json.Nodes;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **A DECISION THE ANSWERS ARE NOT ALLOWED TO LOSE** — the tracked upgrade goal (DRA-216 D4,
/// S12; plan §3 Q3).
///
/// <para>Every row here is one of the four claims the slice makes: a goal is its own object and
/// outlives the sweep that offered it, it is a BASE item and never a "+N", it ends only when
/// the player says so, and it survives a restart. The last one is asserted through the real
/// serializer rather than through the store's own dictionary, because "it is in memory" and
/// "it will be there tomorrow" are different claims and only the second is S18.3.</para>
/// </summary>
// The S18.3 row below writes and reads the shared throwaway profile's settings.json, so this
// file runs serially with every other test that does — the 1-in-3 flake of 2026-08-22, which
// presents in somebody else's file weeks later. See SettingsFileCollection.
[Collection(SettingsFileCollection.Name)]
public class TrackedUpgradesTests
{
    private const string Me = "erollisi|Dranak";
    private const string Someone = "erollisi|Aluvia";
    private static readonly DateTime Tuesday = new(2026, 9, 15, 20, 14, 0, DateTimeKind.Local);

    /// <summary>An offer as the engine makes one: an item, and the worn thing it beats. The
    /// anchor is what keeps this room off the best-in-slot side of the line, so every fixture
    /// here has one.</summary>
    private static GearUpgradeFact Offer(
        string item, string over = "Shiny Brass Shield +6", string slot = "SECONDARY") =>
        new(item, over, slot, "AC", 12, ["a gnoll pup"]);

    private static AppSettings Fresh() => new();

    // ---- the goal is its own object, and it outlives the offer ------------------------

    /// <summary>
    /// **THE WHOLE POINT OF THE SLICE.** A goal is tracked from an offer, and it is still
    /// there when the offer is not — a different dump, a different pick, a refused zone, a
    /// catalog refresh. The plan's answer to Q3 is exactly this: a tracked goal outlives the
    /// sweep, so it cannot BE a swept row.
    /// </summary>
    [Fact]
    public void AGoalOutlivesTheOfferThatMadeIt()
    {
        var settings = Fresh();
        Assert.True(TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday));

        // The sweep is re-run against a character wearing something else entirely; nothing in
        // this profile now produces that offer.
        var swept = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, [], [], null, [], includeQuests: false);
        Assert.Empty(swept.Upgrades);

        var goal = Assert.Single(TrackedUpgradeStore.For(settings, Me));
        Assert.Equal("Blade of Carnage", goal.Item);
        Assert.Equal("Shiny Brass Shield +6", goal.Over);
        Assert.Equal("SECONDARY", goal.Slot);
        Assert.Equal(Tuesday, goal.TrackedAt);
    }

    /// <summary>The anchor is carried because it is in the sentence — an upgrade with nothing
    /// behind it is a claim about the game rather than about this character.</summary>
    [Fact]
    public void TheWornItemItBeatsIsKeptAsTheDumpSpelledIt()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(
            settings, Me, Offer("Blade of Carnage", over: "Rusty Short Sword +3"), Tuesday);

        var goal = Assert.Single(TrackedUpgradeStore.For(settings, Me));
        // The "+3" survives UNFOLDED on this side: it is what the player reads on their own
        // character sheet. Nothing does arithmetic with it — see the row's own sentence.
        Assert.Equal("Rusty Short Sword +3", goal.Over);
        Assert.Contains("Rusty Short Sword +3", HelperPresentation.TrackedRow(goal));
    }

    // ---- base items only (S8/S9 PARKED by Helm) ---------------------------------------

    /// <summary>
    /// **A "+N" IS FOLDED, SO ONE ITEM IS ONE GOAL.** S8 is parked and this repo holds no
    /// enhanced-item stats at all, so a goal naming a tier would name an object EQBuddy cannot
    /// describe. The fold is the ONE seam every other item key in this profile goes through.
    /// </summary>
    [Fact]
    public void ATierIsFoldedOffSoOneItemIsOneGoal()
    {
        var settings = Fresh();
        Assert.True(TrackedUpgradeStore.Track(settings, Me, Offer("Crushbone Belt +5"), Tuesday));
        // The same item, offered again under its base name a week later.
        Assert.False(TrackedUpgradeStore.Track(
            settings, Me, Offer("Crushbone Belt"), Tuesday.AddDays(7)));

        var goal = Assert.Single(TrackedUpgradeStore.For(settings, Me));
        Assert.Equal("Crushbone Belt", goal.Item);
        Assert.DoesNotContain('+', goal.Item);
        Assert.True(TrackedUpgradeStore.IsTracked(settings, Me, "Crushbone Belt +5"));
        Assert.True(TrackedUpgradeStore.IsTracked(settings, Me, "Crushbone Belt"));
    }

    /// <summary>Re-tracking keeps the FIRST decision's stamp. "Yes, I still want that" is not
    /// "start the clock again", and the stamp is the one field nothing else can recover.</summary>
    [Fact]
    public void TrackingSomethingTwiceKeepsTheFirstDecisionsStamp()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday);
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday.AddDays(7));

        Assert.Equal(Tuesday, Assert.Single(TrackedUpgradeStore.For(settings, Me)).TrackedAt);
    }

    /// <summary>The name a curated alias answers for is the name the CATALOG is looked up
    /// under, so a goal and its item cannot be keyed differently (the Founder's bow: the game
    /// spells it <c>Deterioriated</c> and eqlwiki <c>Deteriorated</c>).</summary>
    [Fact]
    public void TheKeyIsTheSameOneTheCatalogIsLookedUpUnder()
    {
        foreach (var name in new[] { "Blade of Carnage", "Crushbone Belt +5", "Lute +1" })
            Assert.Equal(EqlWikiItemService.NormalizeTitle(name), TrackedUpgradeStore.Key(name));
    }

    // ---- it ends when the player says so, and only then --------------------------------

    /// <summary>
    /// **THERE IS NO COMPLETION CONDITION, AND THAT IS HELM'S PARK RATHER THAN AN OMISSION**
    /// (<c>HELM.md</c> 2026-09-19: S12.3 is parked with S8/S9). Untracking is the only way out,
    /// and the block's own sentence says so on both surfaces.
    /// </summary>
    [Fact]
    public void AGoalEndsOnlyWhenThePlayerUntracksIt()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday);

        Assert.True(TrackedUpgradeStore.Untrack(settings, Me, "Blade of Carnage"));
        Assert.Empty(TrackedUpgradeStore.For(settings, Me));
        // "Never tracked anything" and "untracked the last one" are ONE state — the idiom every
        // per-character selection in this profile keeps.
        Assert.False(settings.TrackedUpgrades.ContainsKey(Me));
        // And untracking something that is not tracked changes nothing rather than throwing.
        Assert.False(TrackedUpgradeStore.Untrack(settings, Me, "Blade of Carnage"));
    }

    /// <summary>The player's own words for what the list does NOT claim. Both halves are
    /// load-bearing: nothing ticks itself off, and EQBuddy has no numbers for a "+N" — a list
    /// implying either would be acted on.</summary>
    [Fact]
    public void TheBlocksOwnSentenceCarriesBothParks()
    {
        Assert.Contains("until you untrack", HelperPresentation.TrackedNote);
        Assert.Contains("does not tick one off", HelperPresentation.TrackedNote);
        Assert.Contains("+N", HelperPresentation.TrackedNote);
    }

    /// <summary>One control, two states, and the label MOVES — a toggle whose words did not
    /// change is a control that lies about what it just did (trap 17's sibling).</summary>
    [Fact]
    public void TheControlSaysWhichWayItIsAbout()
    {
        Assert.NotEqual(HelperPresentation.TrackLabel(true), HelperPresentation.TrackLabel(false));
        Assert.Contains("Blade of Carnage", HelperPresentation.TrackTip(false, "Blade of Carnage"));
        Assert.Contains("Blade of Carnage", HelperPresentation.TrackTip(true, "Blade of Carnage"));
        Assert.NotEqual(
            HelperPresentation.TrackTip(true, "x"), HelperPresentation.TrackTip(false, "x"));
    }

    /// <summary>The toggle's DIRECTION is the store's decision and not a host's, so the two
    /// surfaces cannot disagree about what one click does (trap 4).</summary>
    [Fact]
    public void TheToggleDecidesItsOwnDirection()
    {
        var settings = Fresh();
        var offer = Offer("Blade of Carnage");

        Assert.True(TrackedUpgradeStore.Toggle(settings, Me, offer, Tuesday));
        Assert.True(TrackedUpgradeStore.IsTracked(settings, Me, "Blade of Carnage"));
        Assert.False(TrackedUpgradeStore.Toggle(settings, Me, offer, Tuesday.AddMinutes(1)));
        Assert.Empty(TrackedUpgradeStore.For(settings, Me));
    }

    // ---- whose goal it is ---------------------------------------------------------------

    /// <summary>Per character, like every other selection in this room: one install holds a
    /// level-8 alt and a main, and they are not going after the same thing.</summary>
    [Fact]
    public void OneCharactersGoalIsNeverAnothers()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday);

        Assert.Empty(TrackedUpgradeStore.For(settings, Someone));
        Assert.False(TrackedUpgradeStore.IsTracked(settings, Someone, "Blade of Carnage"));
    }

    /// <summary>No character, no goal — and no throw. A room asked before the log has named
    /// anybody is a real state on every other store here.</summary>
    [Fact]
    public void NoCharacterAndNoOfferAreAnsweredRatherThanThrown()
    {
        var settings = Fresh();
        Assert.False(TrackedUpgradeStore.Track(settings, "", Offer("Blade of Carnage"), Tuesday));
        Assert.False(TrackedUpgradeStore.Track(settings, Me, null, Tuesday));
        Assert.False(TrackedUpgradeStore.Track(settings, Me, Offer(""), Tuesday));
        Assert.False(TrackedUpgradeStore.Track(null, Me, Offer("x"), Tuesday));
        Assert.Empty(TrackedUpgradeStore.For(settings, Me));
        Assert.Empty(TrackedUpgradeStore.For(null, Me));
        Assert.False(TrackedUpgradeStore.IsTracked(settings, Me, ""));
    }

    // ---- the order is decided, not inherited --------------------------------------------

    /// <summary>
    /// Newest first, with the item name breaking a tie.
    ///
    /// <para>The order is the store's rather than the insertion order's, because the two are
    /// only the same until something is removed — and a stable tiebreak is what stops two goals
    /// tracked in the same tick from swapping places between two paints and making a surface
    /// look as though it re-ranked something.</para>
    /// </summary>
    [Fact]
    public void TheNewestGoalIsFirstAndATieIsBrokenByName()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday);
        TrackedUpgradeStore.Track(settings, Me, Offer("Wurmslayer"), Tuesday.AddDays(2));
        TrackedUpgradeStore.Track(settings, Me, Offer("Ancient Bone Bracer"), Tuesday.AddDays(2));

        Assert.Equal(
            ["Ancient Bone Bracer", "Wurmslayer", "Blade of Carnage"],
            TrackedUpgradeStore.For(settings, Me).Select(t => t.Item));
    }

    // ---- S18.3: it survives a restart ---------------------------------------------------

    /// <summary>
    /// **IT COMES BACK AFTER A RESTART** (S18.3) — through the REAL
    /// <see cref="AppSettings.Save"/> and <see cref="AppSettings.Load"/>, because a record with
    /// a positional constructor is a shape this profile has not held before, "it is in the
    /// dictionary" is not the claim, and the app's own serializer options are the ones that
    /// have to carry it. The profile is the test one — <c>TestProfileIsolation</c> redirects
    /// <c>EQBUDDY_APPDATA</c> unconditionally, so nothing here can reach a player's.
    /// </summary>
    [Fact]
    public void AGoalSurvivesTheProfileBeingWrittenAndReadBack()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(
            settings, Me, Offer("Blade of Carnage", over: "Rusty Short Sword +3"), Tuesday);
        settings.Save();

        var reloaded = AppSettings.Load();

        var goal = Assert.Single(TrackedUpgradeStore.For(reloaded, Me));
        Assert.Equal("Blade of Carnage", goal.Item);
        Assert.Equal("Rusty Short Sword +3", goal.Over);
        Assert.Equal("SECONDARY", goal.Slot);
        Assert.Equal(Tuesday, goal.TrackedAt);
        Assert.True(TrackedUpgradeStore.IsTracked(reloaded, Me, "Blade of Carnage"));
    }

    /// <summary>
    /// **THE ROOM FOR A REQUIRED RANK AND AN EXALTATION, PINNED TO A GUARD RATHER THAN TO A
    /// COMMENT** (Fable's last-look on PR #712, the one note it left).
    ///
    /// <para>This card's done bar asks that S8/S9 can be un-parked — a required rank, a required
    /// exaltation — without a schema migration, and that is true today only because
    /// <see cref="AppSettings"/>' serializer options leave <c>UnmappedMemberHandling</c> at the
    /// default, which IGNORES a member this build has never heard of, and because a positional
    /// record fills an absent parameter with its default. Neither of those is anything in D4's
    /// diff: the claim rested on reading the options, so a later tightening to
    /// <c>Disallow</c> — a plausible, well-meant change — would break the bar with every test
    /// still green.</para>
    ///
    /// <para><b>Both directions, because a migration-free field has to survive both.</b> An
    /// OLDER build reading a profile a NEWER one wrote: the unknown members are dropped and the
    /// rest of the row still loads. A NEWER build reading an OLDER profile: the parameter that
    /// is not in the file comes back as its default and the row is not refused — which is
    /// exactly what a future <c>RequiredRank</c> does to every profile written before it.</para>
    ///
    /// <para>The row from the future is written BY HAND on purpose: this build cannot serialize
    /// a field it does not have, so staging one is the only way to ask the question.</para>
    /// </summary>
    [Fact]
    public void AStoredGoalSurvivesAFieldThisBuildHasNeverHeardOf()
    {
        var settings = Fresh();
        TrackedUpgradeStore.Track(settings, Me, Offer("Blade of Carnage"), Tuesday);
        settings.Save();

        var path = AppPaths.File("settings.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        var rows = json["TrackedUpgrades"]![Me]!.AsArray();
        // What a post-S8 build would have written over this row.
        rows[0]!["RequiredRank"] = "+5";
        rows[0]!["RequiredExaltation"] = "Exalted";
        // …and a row carrying ONLY what a build older than the new field wrote.
        rows.Add(new JsonObject { ["Item"] = "Wurmslayer" });
        File.WriteAllText(path, json.ToJsonString());

        var goals = TrackedUpgradeStore.For(AppSettings.Load(), Me);

        var carried = Assert.Single(goals, g => g.Item == "Blade of Carnage");
        Assert.Equal("Shiny Brass Shield +6", carried.Over);
        Assert.Equal("SECONDARY", carried.Slot);
        Assert.Equal(Tuesday, carried.TrackedAt);

        var sparse = Assert.Single(goals, g => g.Item == "Wurmslayer");
        Assert.Equal(default, sparse.TrackedAt);
    }

    /// <summary>A profile nobody has tracked anything in holds no key at all — an install that
    /// never touches this feature carries nothing for it, which is the other half of the
    /// "empty REMOVES the key" rule above.</summary>
    [Fact]
    public void AProfileThatHasNeverTrackedAnythingHoldsNothing()
    {
        Assert.Empty(Fresh().TrackedUpgrades);
        Assert.Empty(TrackedUpgradeStore.For(Fresh(), Me));
    }

    /// <summary>A hand-edited profile is the one way a subjectless row can arrive, and it is
    /// dropped rather than drawn: a goal with no item is not a goal.</summary>
    [Fact]
    public void AStoredRowWithNoItemIsDropped()
    {
        var settings = Fresh();
        settings.TrackedUpgrades[Me] =
        [
            new TrackedUpgrade("", "PRIMARY", "Rusty Short Sword", Tuesday),
            new TrackedUpgrade("Blade of Carnage", "PRIMARY", "Rusty Short Sword", Tuesday),
        ];

        Assert.Equal(
            ["Blade of Carnage"], TrackedUpgradeStore.For(settings, Me).Select(t => t.Item));
    }

    // ---- the row's own words -------------------------------------------------------------

    /// <summary>The row names what you are going after, what it was offered against, and when
    /// you decided. The date is ABSOLUTE: a relative one drifts on a clock, and every surface
    /// that folds this sentence into a repaint key would then move on its own (trap 8).</summary>
    [Fact]
    public void TheRowNamesTheGoalTheAnchorAndTheDay()
    {
        var row = HelperPresentation.TrackedRow(
            new TrackedUpgrade("Blade of Carnage", "PRIMARY", "Rusty Short Sword +3", Tuesday));

        Assert.Contains("Blade of Carnage", row);
        Assert.Contains("Rusty Short Sword +3", row);
        Assert.Contains("primary", row);
        Assert.Contains(Tuesday.ToString("d MMM yyyy"), row);
        Assert.DoesNotContain("ago", row);
    }

    /// <summary>A goal with no anchor stored still draws — it just says less. Nothing here
    /// invents a worn item to fill the clause.</summary>
    [Fact]
    public void AGoalWithNoAnchorDrawsTheGoalAndNothingInvented()
    {
        var row = HelperPresentation.TrackedRow(
            new TrackedUpgrade("Blade of Carnage", "", "", Tuesday));

        Assert.Contains("Blade of Carnage", row);
        Assert.DoesNotContain("to replace", row);
    }

    // ---- the engine carries it, and does not rank on it ---------------------------------

    /// <summary>
    /// **THE ONE ASSEMBLY POINT CARRIES IT, AND THE RANKING DOES NOT WEIGH IT.**
    ///
    /// <para>Carried, so the room and the phone cannot read the store at two different moments
    /// (trap 33). Not weighed, because a tracked goal is a decision rather than evidence — a
    /// ranking that quietly moved toward whatever was clicked last is not something any engine
    /// in this file was asked for.</para>
    /// </summary>
    [Fact]
    public void TheRankingCarriesTheGoalsWithoutRankingOnThem()
    {
        IReadOnlyList<SessionRow> sessions =
        [
            new SessionRow(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(6),
                21600, 21600, "ended", "Lower Guk", 0, 240, 5000, 0, 0, 0, "", "")
                { Id = 1 },
        ];
        IReadOnlyList<MobSummary> pool =
            [new MobSummary("a froglok tad", 300, 300, 30, 0, 0, []) { Zone = "Lower Guk" }];

        var bare = new HelperInputs(
            ZoneHistory.Fold(sessions, pool), pool, null, [], [], [], [], false,
            [], [], null, default);
        var withGoals = bare with
        {
            Tracked = [new TrackedUpgrade("Blade of Carnage", "PRIMARY", "Rusty Sword", Tuesday)],
        };

        var before = Recommendations.Rank(bare, [HelperGoal.LevelUp]);
        var after = Recommendations.Rank(withGoals, [HelperGoal.LevelUp]);

        Assert.Equal(
            before.Top.Select(HelperPresentation.Headline),
            after.Top.Select(HelperPresentation.Headline));
        Assert.Single(withGoals.Tracked);
    }
}
