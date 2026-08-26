using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// What the Wiki contribution pack window says before you copy it (#217, Frankthetankk).
///
/// The rule these exist to hold: "we could not read the page" and "the page already has
/// it" are different facts and must never be worded alike. <see cref="WikiContribution.BuildExport"/>
/// already respected that in the pasted text; the window is a second surface making the
/// same claim, and the point of putting the decisions in UI.Shared is that it cannot
/// drift from the pack it is describing.
/// </summary>
public class WikiPackPresentationTests
{
    private static MobSummary Mob(string name, int kills, params string[] loot) =>
        new(name, kills, kills, 8.0, 1.0, 0,
            loot.Select(i => new MobLoot(i, 1, 50.0)).ToList());

    /// <summary>A wiki page that exists and lists the given drops.</summary>
    private static MobLookupResult Page(params string[] drops) =>
        new(new MobInfo
        {
            IsCreaturePage = true,
            Name = "x",
            PageTitle = "x",
            Drops = drops.Select(d => (d, "")).ToList(),
        }, ItemLookupState.Cached, DateTime.UtcNow);

    private static readonly MobLookupResult NoPage =
        new(null, ItemLookupState.NotFound, null);

    private static readonly MobLookupResult Offline =
        new(null, ItemLookupState.Offline, null);

    private static WikiPackPresentation.Pack Build(
        params (MobSummary Mob, MobLookupResult? Lookup)[] obs) =>
        WikiPackPresentation.Build(
            obs.Select(o => new WikiContribution.MobObservation(o.Mob, o.Lookup)));

    [Fact]
    public void Missing_page_is_the_biggest_contribution_and_leads()
    {
        var pack = Build(
            (Mob("a decaying skeleton", 12, "Bone Chips"), Page("Rusty Sword")),
            (Mob("Chief Goonda", 3, "Goonda's Club", "Silk"), NoPage));

        Assert.Equal(WikiPackPresentation.RowKind.PageMissing, pack.Rows[0].Kind);
        Assert.Equal("Chief Goonda", pack.Rows[0].Creature);
        Assert.Equal(2, pack.Rows[0].Contributions);
        Assert.Equal(1, pack.PagesMissing);
        Assert.Equal(1, pack.NewDrops);
        Assert.Equal(3, pack.Contributions);
        Assert.Equal(2, pack.Creatures);
    }

    [Fact]
    public void Page_with_no_loot_makes_everything_news()
    {
        var pack = Build((Mob("a puma", 20, "Pelt", "Claw", "Meat"), Page()));

        Assert.Equal(WikiPackPresentation.RowKind.PageHasNoLoot, pack.Rows[0].Kind);
        Assert.Equal(3, pack.Rows[0].Contributions);
        Assert.Equal(1, pack.PagesWithoutLoot);
    }

    [Fact]
    public void Drops_the_wiki_already_lists_produce_no_row()
    {
        var pack = Build((Mob("an asp", 11, "Snake Fang"), Page("Snake Fang")));

        Assert.Empty(pack.Rows);
        Assert.Equal(0, pack.Contributions);
        Assert.Equal(1, pack.KnownDrops);
    }

    /// <summary>The rule the whole surface exists to protect. An unread page must never
    /// be counted as a contribution, and must never vanish silently either.</summary>
    [Fact]
    public void Unread_page_is_pending_not_a_contribution_and_never_reads_as_nothing_new()
    {
        var pack = Build((Mob("a griffon", 4, "Griffon Feather"), Offline));

        Assert.Equal(WikiPackPresentation.RowKind.Pending, Assert.Single(pack.Rows).Kind);
        Assert.Equal(0, pack.Contributions);
        Assert.Equal(0, pack.Creatures);
        Assert.Equal(1, pack.PendingCreatures);
        Assert.False(WikiPackPresentation.CanCopy(pack));

        var empty = WikiPackPresentation.EmptyText(pack);
        Assert.Contains("not the same as", empty);
        Assert.DoesNotContain("already on eqlwiki", empty);
    }

    /// <summary>A null lookup is "never looked up", which is the same claim as offline.</summary>
    [Fact]
    public void Never_looked_up_is_also_pending()
    {
        var pack = Build((Mob("a bat", 2, "Bat Wing"), null));

        Assert.Equal(WikiPackPresentation.RowKind.Pending, Assert.Single(pack.Rows).Kind);
        Assert.Equal(1, pack.PendingCreatures);
    }

    [Fact]
    public void Pending_sinks_below_every_real_contribution()
    {
        var pack = Build(
            (Mob("a bat", 2, "Bat Wing"), Offline),
            (Mob("a rat", 9, "Rat Ear"), Page("Tail")));

        Assert.Equal(WikiPackPresentation.RowKind.NewToPage, pack.Rows[0].Kind);
        Assert.Equal(WikiPackPresentation.RowKind.Pending, pack.Rows[1].Kind);
    }

    [Fact]
    public void Creatures_with_no_loot_at_all_are_not_rows()
    {
        var pack = Build((Mob("a rat", 5), Page()));

        Assert.Empty(pack.Rows);
        Assert.Equal(0, pack.Contributions);
    }

    /// <summary>"The wiki already has everything" is good news and must not read as a
    /// failure to find anything.</summary>
    [Fact]
    public void Empty_because_the_wiki_is_complete_says_so()
    {
        var pack = Build((Mob("an asp", 11, "Snake Fang", "Venom Sac"), Page("Snake Fang", "Venom Sac")));

        var text = WikiPackPresentation.EmptyText(pack);
        Assert.Contains("already on eqlwiki", text);
        Assert.Contains("good shape", text);
    }

    [Fact]
    public void Empty_because_nothing_was_looted_says_that_instead()
    {
        var pack = Build();

        Assert.Contains("No loot recorded", WikiPackPresentation.EmptyText(pack));
    }

    /// <summary>A complete wiki plus an unread page must still surface the unread one —
    /// otherwise the reassuring message hides a contribution the player actually has.</summary>
    [Fact]
    public void Complete_wiki_still_reports_what_was_not_checked()
    {
        var pack = Build(
            (Mob("an asp", 11, "Snake Fang"), Page("Snake Fang")),
            (Mob("a griffon", 4, "Griffon Feather"), Offline));

        var text = WikiPackPresentation.EmptyText(pack);
        Assert.Contains("already on eqlwiki", text);
        Assert.Contains("haven't been checked", text);
    }

    [Fact]
    public void Copy_is_offered_only_when_there_is_something_to_paste()
    {
        Assert.False(WikiPackPresentation.CanCopy(Build()));
        Assert.True(WikiPackPresentation.CanCopy(
            Build((Mob("Chief Goonda", 3, "Club"), NoPage))));
    }

    [Fact]
    public void Headline_counts_items_and_creatures_and_singularizes()
    {
        var one = Build((Mob("Chief Goonda", 3, "Club"), NoPage));
        Assert.Equal("1 item across 1 creature the wiki doesn't have",
            WikiPackPresentation.Headline(one));

        var many = Build(
            (Mob("Chief Goonda", 3, "Club", "Silk"), NoPage),
            (Mob("a rat", 9, "Rat Ear"), Page("Tail")));
        Assert.Equal("3 items across 2 creatures the wiki doesn't have",
            WikiPackPresentation.Headline(many));

        Assert.Equal("Nothing to contribute yet", WikiPackPresentation.Headline(Build()));
    }

    [Fact]
    public void Breakdown_names_only_the_categories_present()
    {
        var pack = Build(
            (Mob("Chief Goonda", 3, "Club"), NoPage),
            (Mob("a rat", 9, "Rat Ear"), Page("Tail")));

        var text = WikiPackPresentation.Breakdown(pack);
        Assert.Contains("1 with no wiki page", text);
        Assert.Contains("1 with new drops", text);
        Assert.DoesNotContain("lists no loot", text);
    }

    /// <summary>The reason this is a window and not a menu command: the scope has to be on
    /// screen. Ask 2 (#217) widened it to the full archive, and this line is what makes
    /// the pooling decisions honest — it names everyone pooled and the date span, never a
    /// policy sentence.</summary>
    [Fact]
    public void Scope_line_names_everyone_pooled_and_the_span()
    {
        var scope = new PoolScope(["Dranak", "Flossie"], ["freeport"], 3,
            new DateTime(2026, 7, 30), new DateTime(2026, 8, 19));
        var text = WikiPackPresentation.ScopeLine(scope, kills: 12, creatures: 4);

        Assert.Contains("12 kills of 4 creatures across 3 sessions", text);
        Assert.Contains("Dranak and Flossie on freeport", text);
        Assert.Contains("2026-07-30", text);
        // The old single-session wording is gone with the single-session scope.
        Assert.DoesNotContain("This session only", text);
    }

    [Fact]
    public void Scope_line_survives_an_empty_pool_and_says_why_it_is_empty()
    {
        var text = WikiPackPresentation.ScopeLine(
            new PoolScope([], [], 0, null, null), kills: 0, creatures: 0);

        Assert.Contains("No sessions with kills yet", text);
    }

    [Fact]
    public void Scope_line_calls_a_today_only_pool_today_rather_than_a_degenerate_span()
    {
        var scope = new PoolScope(["Dranak"], ["freeport"], 1, DateTime.Today.AddHours(5), DateTime.Now);
        var text = WikiPackPresentation.ScopeLine(scope, kills: 3, creatures: 2);

        Assert.Contains("· today", text);
        Assert.DoesNotContain("→", text);
    }

    /// <summary>Both desktops read these; a divergence would be two different products.</summary>
    [Theory]
    [InlineData(WikiPackPresentation.RowKind.PageMissing)]
    [InlineData(WikiPackPresentation.RowKind.PageHasNoLoot)]
    [InlineData(WikiPackPresentation.RowKind.NewToPage)]
    [InlineData(WikiPackPresentation.RowKind.Pending)]
    public void Every_kind_has_a_label_and_a_tip(WikiPackPresentation.RowKind kind)
    {
        Assert.NotEmpty(WikiPackPresentation.KindLabel(kind));
        Assert.NotEmpty(WikiPackPresentation.KindTip(kind));
    }

    // ---------------- the re-check (#226) ----------------

    /// <summary>The button re-reads the creatures the pack CLAIMS something for, or could
    /// not read — and never one whose page already has everything. That bound is the
    /// etiquette: flagged creatures, not the whole session.</summary>
    [Fact]
    public void Recheck_targets_are_the_flagged_and_unread_creatures_never_the_fully_known()
    {
        var obs = new[]
        {
            new WikiContribution.MobObservation(Mob("Chief Goonda", 3, "Goonda's Club"), NoPage),          // missing page
            new WikiContribution.MobObservation(Mob("an asp", 11, "Snake Fang"), Page("Snake Fang")),      // fully known
            new WikiContribution.MobObservation(Mob("a puma", 2, "Puma Skin", "Chunk of Meat"), Page("Chunk of Meat")), // new to page
            new WikiContribution.MobObservation(Mob("a zombie", 1, "Zombie Skin"), Offline),               // unread
            new WikiContribution.MobObservation(Mob("a ghoul", 4), Page("Bone Chips")),                    // no loot at all
        };

        var targets = WikiPackPresentation.RecheckTargets(obs);

        Assert.Equal(["Chief Goonda", "a puma", "a zombie"], targets);
        Assert.Equal("Re-check 3 pages", WikiPackPresentation.RecheckLabel(targets.Count));
        Assert.Equal("Re-check 1 page", WikiPackPresentation.RecheckLabel(1));
        Assert.Equal("Nothing to re-check", WikiPackPresentation.RecheckLabel(0));
        Assert.True(WikiPackPresentation.CanRecheck(3, running: false));
        Assert.False(WikiPackPresentation.CanRecheck(3, running: true));
        Assert.False(WikiPackPresentation.CanRecheck(0, running: false));
        Assert.Equal("checking 7 of 9\u2026", WikiPackPresentation.RecheckProgress(inFlight: 3, total: 9));
    }

    /// <summary>What the window shows must be what the clipboard gets — they read the same
    /// observations, so a creature counted here has a block there.</summary>
    [Fact]
    public void Rows_agree_with_the_export_they_describe()
    {
        var obs = new[]
        {
            new WikiContribution.MobObservation(Mob("Chief Goonda", 3, "Goonda's Club"), NoPage),
            new WikiContribution.MobObservation(Mob("an asp", 11, "Snake Fang"), Page("Snake Fang")),
        };

        var pack = WikiPackPresentation.Build(obs);
        var export = WikiContribution.BuildExport(obs, "Dranak", "freeport", "gfaydark", DateTime.Now);

        Assert.Contains("Chief Goonda", export);
        Assert.Equal("Chief Goonda", Assert.Single(pack.Rows).Creature);
    }
    /// <summary>A session whose only loot was motes must not be told "no loot recorded this
    /// session yet" — the player looted plenty; there is simply nothing a creature page
    /// should carry. Saying otherwise reads as EQBuddy having missed the kills (Fable 5,
    /// v1.99.5 review).</summary>
    [Fact]
    public void AMoteOnlySessionIsToldWhyRatherThanToldItLootedNothing()
    {
        var pack = Build((Mob("a puma", 6, "Mote of Potential"), Page()));

        var text = WikiPackPresentation.EmptyText(pack);
        Assert.Contains("motes", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No loot recorded", text);
        Assert.Equal(0, pack.Contributions);
    }

    // ---- the rare-only row (Bevel, Helm-signed 2026-08-23): a contribution that is
    // not loot. The gap: a rare-conned named whose page already had every drop produced
    // NOTHING — dropped for exactly the creature most likely to be a known named. ----

    private static MobSummary RareMob(string name, int kills, int considers, int rare,
        params string[] loot) =>
        Mob(name, kills, loot) with { Considers = considers, RareConsiders = rare };

    [Fact]
    public void ARareConnedNamedWithAllKnownLootEarnsItsOwnRow()
    {
        var pack = Build((RareMob("Magus Rokyl", 1, 2, 2, "Rokyl's Robe"),
            Page("Rokyl's Robe")));

        var row = Assert.Single(pack.Rows);
        Assert.Equal(WikiPackPresentation.RowKind.RareConfirmed, row.Kind);
        Assert.Equal(0, row.Contributions);
        Assert.Equal("rare on all 2 /considers", row.Note);
        Assert.Equal(1, pack.RareOnlyCreatures);
        // The headline counts it (the ruling's own words) — not "Nothing to contribute".
        Assert.Equal("1 rare-spawn confirmation for the wiki",
            WikiPackPresentation.Headline(pack));
        Assert.Contains("1 confirmed rare via /consider", WikiPackPresentation.Breakdown(pack));
        // And the paste exists, so Copy is live.
        Assert.True(WikiPackPresentation.CanCopy(pack));
    }

    [Fact]
    public void AMoteOnlyRareConIsARareRowNotANothingSuggestable()
    {
        var pack = Build((RareMob("Lesser blade fiend", 1, 1, 1, "Mote of Potential"),
            Page()));

        Assert.Equal(WikiPackPresentation.RowKind.RareConfirmed,
            Assert.Single(pack.Rows).Kind);
        Assert.Equal(0, pack.NothingSuggestableCreatures);
    }

    /// <summary>An unread page gets no claim of any kind — rare included. Pending wins,
    /// exactly as it does for loot.</summary>
    [Fact]
    public void AnUnreadPageStaysPendingEvenWhenTheConSaidRare()
    {
        var pack = Build((RareMob("Magus Rokyl", 1, 2, 2, "Rokyl's Robe"), Offline));

        Assert.Equal(WikiPackPresentation.RowKind.Pending, Assert.Single(pack.Rows).Kind);
        Assert.Equal(0, pack.RareOnlyCreatures);
    }

    /// <summary>A lore article must not be offered a description edit any more than a
    /// loot table — the wrong-article row wins and no rare paste is suggested.</summary>
    [Fact]
    public void AWrongArticleStaysWrongArticleEvenWhenTheConSaidRare()
    {
        var lore = new MobLookupResult(new MobInfo
        {
            IsCreaturePage = false,
            Name = "Innoruk",
            PageTitle = "Innoruk (Lore)",
        }, ItemLookupState.Cached, DateTime.UtcNow);
        var pack = Build((RareMob("Innoruk", 1, 1, 1, "Shard"), lore));

        Assert.Equal(WikiPackPresentation.RowKind.NotACreaturePage,
            Assert.Single(pack.Rows).Kind);
        Assert.Equal(0, pack.RareOnlyCreatures);
    }

    /// <summary>A mixed pack keeps the item sentence honest: the rare-only creature is
    /// its own clause, never inflating "N items across M creatures".</summary>
    [Fact]
    public void AMixedPackNamesItemsAndRareConfirmationsSeparately()
    {
        var pack = Build(
            (Mob("Chief Goonda", 3, "Goonda's Club"), NoPage),
            (RareMob("Magus Rokyl", 1, 2, 2, "Rokyl's Robe"), Page("Rokyl's Robe")));

        Assert.Equal(1, pack.Creatures);
        Assert.Equal("1 item across 1 creature the wiki doesn't have · 1 rare-spawn confirmation",
            WikiPackPresentation.Headline(pack));
        // Sorted below the loot contributions, above wrong-article and pending.
        Assert.Equal(WikiPackPresentation.RowKind.PageMissing, pack.Rows[0].Kind);
        Assert.Equal(WikiPackPresentation.RowKind.RareConfirmed, pack.Rows[1].Kind);
    }

    /// <summary>The rare row and the export agree — a row here has a section there, with
    /// the ADD instruction and both counts. And the negative: a rare-conned creature is
    /// NOT in the export when its page was never read.</summary>
    [Fact]
    public void TheRareRowHasARareSectionInTheExport()
    {
        var obs = new[]
        {
            new WikiContribution.MobObservation(
                RareMob("Magus Rokyl", 1, 2, 1, "Rokyl's Robe"), Page("Rokyl's Robe")),
        };
        var export = WikiContribution.BuildExport(obs, "Dranak", "freeport", "gfaydark", DateTime.Now);

        Assert.Contains("rare spawn confirmed via /consider ===", export);
        Assert.Contains("ADD this to the description field", export);
        Assert.Contains(WikiContribution.RareSpawnDescription, export);
        Assert.Contains("1 of your 2 /considers", export);   // both counts, said once
        Assert.DoesNotContain("Nothing confirmed new yet", export);
        Assert.DoesNotContain("already on the wiki. Nothing to contribute", export);

        var unread = new[]
        {
            new WikiContribution.MobObservation(
                RareMob("Magus Rokyl", 1, 2, 1, "Rokyl's Robe"), Offline),
        };
        Assert.DoesNotContain("rare spawn confirmed",
            WikiContribution.BuildExport(unread, "Dranak", "freeport", "gfaydark", DateTime.Now));
    }
}
