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
    /// screen. Ask 2 (#217) widens it to the full archive, and this line is what has to
    /// change when it does.</summary>
    [Fact]
    public void Scope_line_names_the_character_and_says_this_session_only()
    {
        var text = WikiPackPresentation.ScopeLine(
            "Dranak", "freeport", new DateTime(2026, 8, 19, 18, 22, 0));

        Assert.Contains("This session only", text);
        Assert.Contains("Dranak (freeport)", text);
        Assert.Contains("18:22", text);
        Assert.Contains("earlier sessions are not counted", text);
    }

    [Fact]
    public void Scope_line_survives_an_unknown_character_and_no_session_start()
    {
        var text = WikiPackPresentation.ScopeLine("", "", null);

        Assert.Contains("This session only", text);
        Assert.Contains("this character", text);
        Assert.DoesNotContain("since", text);
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
}
