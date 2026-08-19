using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The merged unlock list — AA rows then spell rows, and the per-row lookups that depend
/// on which half a name came from.
///
/// These rules lived as statics on <c>MainWindow</c>, where nothing could test them and
/// the Progress breakout had to reach into the widget class to call them. The rule worth
/// pinning is the one that makes the merge non-trivial: **only the SET knows which kind a
/// row is**, so a name appearing in both halves must be resolved per set rather than by a
/// global lookup.
/// </summary>
public class LevelUnlockRowsTests
{
    private static AaCatalogEntry Aa(string name) => new()
    {
        Name = name, Category = "General", Class = "Warrior", LevelRequirement = 12,
    };

    private static LevelUnlockSet Set(string[] aas, string[] spells) =>
        new([.. aas.Select(Aa)],
            [.. spells.Select(sp => new SpellUnlock(sp, ["Warrior"]))]);

    [Fact]
    public void Aa_rows_lead_and_spell_rows_follow_in_one_list()
    {
        var rows = LevelUnlockRows.Rows(Set(["Natural Durability"], ["Shield of Words"])).ToList();

        Assert.Equal(["Natural Durability", "Shield of Words"], rows.Select(r => r.Name));
        Assert.All(rows, r => Assert.NotEmpty(r.Value));
    }

    /// <summary>The value column is what tells the two kinds apart on screen — there is no
    /// heading between them — so a spell row and an AA row must never read alike.</summary>
    [Fact]
    public void The_value_column_distinguishes_a_spell_row_from_an_aa_row()
    {
        var rows = LevelUnlockRows.Rows(Set(["Natural Durability"], ["Shield of Words"])).ToList();

        Assert.NotEqual(rows[0].Value, rows[1].Value);
        Assert.Contains("spell", rows[1].Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_empty_set_produces_no_rows()
    {
        Assert.Empty(LevelUnlockRows.Rows(LevelUnlockSet.Empty));
    }

    /// <summary>THE rule. Resolution is per SET: the same name is a spell in one set and an
    /// AA in another, and a global lookup would answer one of them wrongly.</summary>
    [Fact]
    public void Which_half_a_name_came_from_is_answered_by_the_set_not_globally()
    {
        const string shared = "Ambidexterity";

        Assert.True(LevelUnlockRows.IsSpell(Set([], [shared]), shared));
        Assert.False(LevelUnlockRows.IsSpell(Set([shared], []), shared));
    }

    [Fact]
    public void Matching_a_name_ignores_case()
    {
        Assert.True(LevelUnlockRows.IsSpell(Set([], ["Shield of Words"]), "shield of words"));
    }

    /// <summary>A spell row opens its own wiki page; an AA row opens the one AA page,
    /// because the wiki has no per-ability pages to open.</summary>
    [Fact]
    public void A_spell_row_opens_its_own_page_and_an_aa_row_opens_the_aa_page()
    {
        var set = Set(["Natural Durability"], ["Shield of Words"]);

        Assert.Equal("Shield of Words", LevelUnlockRows.WikiPageFor(set, "Shield of Words"));
        Assert.Equal(LevelUnlockRows.AaWikiPage,
            LevelUnlockRows.WikiPageFor(set, "Natural Durability"));
    }

    /// <summary>A name in neither half is not a spell, so it falls to the AA page rather
    /// than opening a wiki page that does not exist.</summary>
    [Fact]
    public void An_unknown_name_falls_back_to_the_aa_page()
    {
        Assert.Equal(LevelUnlockRows.AaWikiPage,
            LevelUnlockRows.WikiPageFor(LevelUnlockSet.Empty, "Nothing At All"));
    }

    [Fact]
    public void The_tooltip_lookup_is_also_resolved_per_set()
    {
        var asSpell = LevelUnlockRows.Tooltip(Set([], ["Shield of Words"]));
        var asAa = LevelUnlockRows.Tooltip(Set(["Shield of Words"], []));

        // Whatever the catalogs say, the two paths must not be the same lookup — the spell
        // side consults the spell catalog and the AA side the AA catalog.
        Assert.NotSame(asSpell, asAa);
        // And neither may throw on a name the catalogs have never heard of.
        Assert.Null(LevelUnlockRows.Tooltip(LevelUnlockSet.Empty)("Nothing At All"));
    }
}
