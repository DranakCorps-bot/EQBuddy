using System;
using System.Collections.Generic;
using System.Linq;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #252 (TiconaX, 1.99.15): *"The cards always reset to having 2 cards open even though I
/// have hidden all of them. Gear &amp; loot and + Motes."*
///
/// Both halves were the SAME defect wearing two costumes — a card-fold migration that kept
/// running after its fold was over, because something else kept handing it a key it thought
/// it still owned:
///
/// <list type="bullet">
/// <item><b>Motes:</b> it stopped being a folded card on 2026-08-21 and
/// <c>ProgressSurface.AbsorbedCardKeys</c> was not told, so the fold saw a LIVE catalog key
/// in every profile's <c>SectionOrder</c> and judged itself stale on every launch.</item>
/// <item><b>Gear &amp; Loot:</b> <c>ApplyDefaultGearSection</c> re-created the <c>gear</c>
/// key on every launch and <c>MigrateLootSections</c> absorbed it again on every launch —
/// and the re-hide rule (<c>hidden &gt;= present</c>) counted a "gear" that no player could
/// ever have hidden, because it has no row in Options.</item>
/// </list>
///
/// **Why the existing tests could not see either one.** <c>ProgressSectionFoldTests</c> and
/// the loot fold's own tests each call ONE migration, once, and each is genuinely idempotent
/// when called that way. The bug lived in the chain — in what two migrations do to each
/// other across a restart — so the only test shape that can catch it is one that runs the
/// whole of <see cref="AppSettings.ApplyMigrations"/> twice and asks whether the second pass
/// is silent. That is trap 49's lesson with different actors: a suite is only as complete as
/// the model it encodes, and the model here was "one migration at a time".
/// </summary>
public class SectionFoldIdempotenceTests
{
    /// <summary>Every card the app offers a player today, which is the list a fold must
    /// never quietly rewrite.</summary>
    private static string[] CatalogKeys =>
        [.. OverlaySections.Catalog.Select(c => c.Key)];

    /// <summary>A profile that has already been through every fold — the state the
    /// overwhelming majority of installs are actually in, and the one #252 was reported
    /// from. <paramref name="hidden"/> is what the player unticked in Options.</summary>
    private static AppSettings Current(params string[] hidden) => new()
    {
        SectionOrder = [.. CatalogKeys],
        HiddenSections = [.. hidden],
        // Both one-shots have long since fired on a 1.99.x profile.
        MotesCardOffered = true,
        MotesCardRestored = true,
    };

    // ---- the report ----

    /// <summary>#252 itself, in one line: hide everything, restart, and it stays hidden.
    ///
    /// The pre-fix tree fails this on exactly two keys — "loot" and "motes" — which is the
    /// two cards TiconaX named, in the words he used for them.</summary>
    [Fact]
    public void Hiding_every_card_survives_a_restart()
    {
        var settings = Current(CatalogKeys);

        settings.ApplyMigrations(hadFile: true);

        Assert.Equal([.. CatalogKeys.Order()], [.. settings.HiddenSections.Order()]);
    }

    /// <summary>The same thing across THREE launches, because a migration that alternates
    /// would pass a single-restart check. Each pass re-reads what the last one wrote, the
    /// way `Load` does.</summary>
    [Fact]
    public void Hiding_every_card_survives_several_restarts()
    {
        var settings = Current(CatalogKeys);

        for (var launch = 0; launch < 3; launch++) settings.ApplyMigrations(hadFile: true);

        foreach (var key in CatalogKeys)
            Assert.Contains(key, settings.HiddenSections);
    }

    /// <summary>Hiding ONE card is the ordinary case, and the two the report names get their
    /// own row so a regression says which card came back rather than "a list differs".</summary>
    [Theory]
    [InlineData("loot")]
    [InlineData("motes")]
    [InlineData("progress")]
    [InlineData("combat")]
    // "misc" had a row here until 2026-09-05: the World card was cut (HUD subtraction cut
    // 2) and its key now has no card, so hiding it is not a thing a player can do. The
    // migration REMOVING it is asserted in OptionsViewModelTests, beside the same claim
    // for "quests" — this list is only for keys that are still cards.
    public void A_single_hidden_card_stays_hidden_across_a_restart(string key)
    {
        var settings = Current(key);

        settings.ApplyMigrations(hadFile: true);
        settings.ApplyMigrations(hadFile: true);

        Assert.Contains(key, settings.HiddenSections);
        Assert.Contains(key, settings.SectionOrder);
    }

    // ---- the structural guards ----

    /// <summary>The rule the whole file exists for. A migration chain that still has
    /// something to do on its SECOND run is a migration chain that will still have something
    /// to do on the tenth, and #252 is what "something to do" turned out to mean.
    ///
    /// It also costs a settings SAVE per launch, which is trap 13's loaded gun: a save
    /// rewrites the entire file from the snapshot taken at startup.</summary>
    [Theory]
    [MemberData(nameof(ProfileShapes))]
    public void The_second_pass_changes_nothing(string name, AppSettings settings)
    {
        settings.ApplyMigrations(hadFile: true);
        var order = string.Join(",", settings.SectionOrder);
        var hidden = string.Join(",", settings.HiddenSections.Order());

        Assert.False(settings.ApplyMigrations(hadFile: true),
            $"{name}: the migration chain still reports work to do on its second run. " +
            "Every launch will rewrite settings.json, and whatever it is redoing is being " +
            "done to a profile that already had it done (#252).");
        Assert.Equal(order, string.Join(",", settings.SectionOrder));
        Assert.Equal(hidden, string.Join(",", settings.HiddenSections.Order()));
    }

    public static TheoryData<string, AppSettings> ProfileShapes()
    {
        var data = new TheoryData<string, AppSettings>();
        data.Add("fresh install", new AppSettings());
        data.Add("current, nothing hidden", Current());
        data.Add("current, everything hidden", Current(CatalogKeys));
        data.Add("current, the two #252 names hidden", Current("loot", "motes"));
        // Profiles frozen before each fold, which are the ones the folds still exist FOR.
        data.Add("pre Gear & Loot fold", new AppSettings
        {
            SectionOrder = ["combat", "loot", "gear", "tracked"],
            HiddenSections = ["loot", "gear"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        });
        data.Add("pre Progress fold", new AppSettings
        {
            SectionOrder = ["combat", "progress", "money", "faction", "raids", "tracked"],
            HiddenSections = ["money"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        });
        data.Add("pre Quests fold", new AppSettings
        {
            SectionOrder = ["combat", "sky", "epic", "tracked"],
            HiddenSections = ["sky", "epic"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        });
        // A 1.99.x profile frozen before HUD subtraction cut 2 — the shape EVERY existing
        // install is in, since "misc" has been in SectionOrder since long before the card
        // was called World. Hidden as well as present, because a removal that only handles
        // one of the two lists leaves the other holding a key with no card (#252's shape).
        data.Add("pre World cut", new AppSettings
        {
            SectionOrder = ["combat", "misc", "tracked"],
            HiddenSections = ["misc"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        });
        return data;
    }

    /// <summary>The premise check, and the cheap one that would have prevented all of this.
    ///
    /// A theme's absorbed list is a statement about cards that NO LONGER EXIST. The moment a
    /// key on it is a card again — which is exactly what David did for Motes on 2026-08-21 —
    /// the fold has a live key to chew on and it will chew on it every launch. The theme's
    /// OWN key is the one legitimate entry (it is the slot the fold lands in).
    ///
    /// This is the guard that generalises: Bevel has an open ask about giving Faction its
    /// card back (#251, skwayb), and if that lands with this list unedited, #252 returns
    /// under a different card's name.</summary>
    [Theory]
    [MemberData(nameof(Folds))]
    public void No_fold_absorbs_a_key_that_is_still_a_card(
        string theme, string themeKey, IReadOnlyList<string> absorbed)
    {
        var live = absorbed
            .Where(k => !k.Equals(themeKey, StringComparison.OrdinalIgnoreCase))
            .Where(k => CatalogKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(live.Count == 0,
            $"{theme} claims to absorb {string.Join(", ", live)}, but OverlaySections.Catalog " +
            "still offers that as its own card. A fold only ever names cards that are GONE — " +
            "a live key makes FoldThemeSections judge itself stale on every launch and strip " +
            "the card out of HiddenSections each time (#252). If a card came back, take it " +
            "off the absorbed list in the same change.");
    }

    public static TheoryData<string, string, IReadOnlyList<string>> Folds() => new()
    {
        { "ProgressSurface", ProgressSurface.ThemeCardKey, ProgressSurface.AbsorbedCardKeys },
        { "LootSurface", LootSurface.ThemeCardKey, LootSurface.AbsorbedCardKeys },
        { "CreatureSurface", CreatureSurface.ThemeCardKey, CreatureSurface.AbsorbedCardKeys },
        // WorldSurface's row went on 2026-09-05 with the World card (HUD subtraction cut 2)
        // — and it went because the guard above is exactly what says it had to. A fold may
        // only name keys that are NO LONGER CARDS; "misc" stopped being one, and rather
        // than leave a fold naming a dead key, the two constants were deleted. That theme
        // was the one whose absorbed list no `FoldThemeSections` call ever read (it
        // absorbed one card and kept that card's key), so this table was their only reader
        // and the row could not outlive them.
    };

    /// <summary>The other half of the same premise: nothing in the chain may CREATE a key
    /// that is not a card. <c>ApplyDefaultGearSection</c> did (it inserted "gear" after the
    /// fold had removed "gear" from the catalog), and that phantom is what the loot fold ate
    /// every launch. A key with no catalog row also throws in
    /// <c>OptionsViewModel.Cards</c>'s `First(...)` if it ever reaches there unfiltered.</summary>
    [Theory]
    [MemberData(nameof(ProfileShapes))]
    public void Migrations_never_invent_a_key_that_is_not_a_card(string name, AppSettings settings)
    {
        // A subset check, not an equality one: REMOVING a stale key is exactly what a fold is
        // for, and the pre-fold profiles above arrive carrying several. What must never
        // happen is a key appearing that was not there before.
        var before = settings.SectionOrder.Except(CatalogKeys, StringComparer.OrdinalIgnoreCase).ToList();

        settings.ApplyMigrations(hadFile: true);

        var invented = settings.SectionOrder
            .Except(CatalogKeys, StringComparer.OrdinalIgnoreCase)
            .Except(before, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.True(invented.Count == 0,
            $"{name}: migrations added {string.Join(", ", invented)} to SectionOrder, and no " +
            "card in OverlaySections.Catalog has that key. It can never draw anything, and a " +
            "fold that lists it will absorb it again on the next launch (#252).");
    }

    // ---- the folds still do their job ----

    /// <summary>The fix removed a migration and shortened a list, so these say the folds are
    /// still folds. A profile frozen before the Gear &amp; Loot fold, with BOTH old cards
    /// hidden, must arrive with one hidden card and not two visible ones.</summary>
    [Fact]
    public void The_gear_and_loot_fold_still_folds_a_genuinely_old_profile()
    {
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "loot", "gear", "tracked"],
            HiddenSections = ["loot", "gear"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        };

        Assert.True(settings.ApplyMigrations(hadFile: true));

        Assert.DoesNotContain("gear", settings.SectionOrder);
        Assert.Contains("loot", settings.SectionOrder);
        Assert.Contains("loot", settings.HiddenSections);
    }

    /// <summary>And the same profile with the two old cards VISIBLE keeps them visible — the
    /// fold's conservative half, which matters more than the other one because a card that
    /// silently disappears is invisible while one that reappears is a click.</summary>
    [Fact]
    public void The_gear_and_loot_fold_keeps_a_visible_pair_visible()
    {
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "loot", "gear", "tracked"],
            MotesCardOffered = true,
            MotesCardRestored = true,
        };

        settings.ApplyMigrations(hadFile: true);

        Assert.Contains("loot", settings.SectionOrder);
        Assert.DoesNotContain("loot", settings.HiddenSections);
    }

    /// <summary>Motes leaving the Progress absorbed list must not cost a pre-2026-08-19
    /// profile its slot: the key stays exactly where the player had it, and it is the Motes
    /// migration — not the Progress fold — that decides whether it shows.</summary>
    [Fact]
    public void An_ancient_profile_keeps_its_motes_card_in_its_own_slot()
    {
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "money", "motes", "faction", "tracked"],
            MiniStats = ["motes"],   // starred: WasWatchingMotes, so the restore speaks for it
        };

        settings.ApplyMigrations(hadFile: true);

        Assert.Contains("motes", settings.SectionOrder);
        Assert.DoesNotContain("motes", settings.HiddenSections);
        // The Progress fold still swallowed the cards that ARE still folded.
        Assert.DoesNotContain("money", settings.SectionOrder);
        Assert.DoesNotContain("faction", settings.SectionOrder);
        Assert.Contains("progress", settings.SectionOrder);
    }

    /// <summary>The Motes card still ships hidden to a profile that never asked for it —
    /// removing it from the fold must not turn it on for everybody, which is the taller
    /// widget the 2026-08-21 migration exists to prevent.</summary>
    [Fact]
    public void Motes_still_arrives_hidden_for_a_profile_that_never_starred_it()
    {
        var settings = new AppSettings { SectionOrder = [.. CatalogKeys] };

        settings.ApplyMigrations(hadFile: true);

        Assert.Contains("motes", settings.HiddenSections);
        // And one-shot: a player who then shows it is never re-hidden.
        settings.HiddenSections.Remove("motes");
        settings.ApplyMigrations(hadFile: true);
        Assert.DoesNotContain("motes", settings.HiddenSections);
    }
}
