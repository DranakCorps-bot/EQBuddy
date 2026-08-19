using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Folding the five Progress-theme cards into one — step 5 of docs/Themes.md's recipe,
/// and the step the plan itself names as *where silent data loss lives*.
///
/// A migration runs once, on a profile nobody can inspect afterwards, and its failures are
/// exactly the kind a player reports as "the update moved my stuff" months later. So the
/// cases below are the ones that would produce that report: a dragged card losing its
/// slot, a hidden card coming back, a visible card disappearing, and the migration running
/// twice.
/// </summary>
public class ProgressSectionFoldTests
{
    private static AppSettings With(IEnumerable<string> order, params string[] hidden)
    {
        var s = new AppSettings();
        s.SectionOrder.Clear();
        s.SectionOrder.AddRange(order);
        s.HiddenSections.Clear();
        foreach (var h in hidden) s.HiddenSections.Add(h);
        return s;
    }

    [Fact]
    public void The_five_cards_become_one()
    {
        var s = With(["combat", "progress", "money", "motes", "faction", "raids", "loot"]);

        Assert.True(s.MigrateProgressSections());
        Assert.Equal(["combat", "progress", "loot"], s.SectionOrder);
    }

    /// <summary>A player who dragged Money to the top finds the THEME at the top — not
    /// appended to the bottom of their list.</summary>
    [Fact]
    public void The_theme_lands_in_the_first_slot_any_of_its_cards_held()
    {
        var s = With(["money", "combat", "loot", "progress"]);

        s.MigrateProgressSections();

        Assert.Equal("progress", s.SectionOrder[0]);
        Assert.Equal(["progress", "combat", "loot"], s.SectionOrder);
    }

    /// <summary>Hiding one of five must not hide the theme. Showing a card that was hidden
    /// is one click to undo; hiding one the player wanted is invisible.</summary>
    [Fact]
    public void One_hidden_card_does_not_hide_the_theme()
    {
        var s = With(["progress", "money", "motes", "faction", "raids"], "motes");

        s.MigrateProgressSections();

        Assert.DoesNotContain("progress", s.HiddenSections);
        Assert.DoesNotContain("motes", s.HiddenSections);
    }

    /// <summary>But someone who hid ALL of them was saying "I don't want this", and the
    /// theme should honour it rather than resurrect five cards as one.</summary>
    [Fact]
    public void Hiding_every_absorbed_card_hides_the_theme()
    {
        var s = With(["progress", "money", "motes", "faction", "raids", "combat"],
            "progress", "money", "motes", "faction", "raids");

        s.MigrateProgressSections();

        Assert.Contains("progress", s.HiddenSections);
        Assert.Equal(["progress", "combat"], s.SectionOrder);
    }

    /// <summary>Hidden state is judged against the cards the profile ACTUALLY had. A
    /// profile carrying only Raids, hidden, has hidden everything this theme owns.</summary>
    [Fact]
    public void Hidden_state_is_judged_against_the_cards_the_profile_actually_had()
    {
        var s = With(["combat", "raids"], "raids");

        s.MigrateProgressSections();

        Assert.Contains("progress", s.HiddenSections);
    }

    /// <summary>A profile that never had any of these cards must not acquire a hidden one —
    /// otherwise the theme is born invisible and nothing on screen explains why.</summary>
    [Fact]
    public void A_profile_with_none_of_these_cards_gains_nothing()
    {
        var s = With(["combat", "loot"]);

        Assert.False(s.MigrateProgressSections());
        Assert.Equal(["combat", "loot"], s.SectionOrder);
        Assert.Empty(s.HiddenSections);
    }

    /// <summary>Migrations run on every load. The second run must be a no-op, or a profile
    /// drifts a little further every launch.</summary>
    [Fact]
    public void Running_it_twice_changes_nothing_the_second_time()
    {
        var s = With(["combat", "progress", "money", "motes", "faction", "raids"], "money");

        Assert.True(s.MigrateProgressSections());
        var order = s.SectionOrder.ToList();
        var hidden = s.HiddenSections.ToList();

        Assert.False(s.MigrateProgressSections());
        Assert.Equal(order, s.SectionOrder);
        Assert.Equal(hidden, s.HiddenSections);
    }

    /// <summary>Cards this theme does not own keep their places, including relative order.</summary>
    [Fact]
    public void Cards_outside_the_theme_are_untouched()
    {
        var s = With(["loot", "money", "combat", "raids", "healing"], "healing");

        s.MigrateProgressSections();

        Assert.Equal(["loot", "progress", "combat", "healing"], s.SectionOrder);
        Assert.Contains("healing", s.HiddenSections);
    }

    /// <summary>The fold must never leave a duplicate, which would render the theme twice.</summary>
    [Fact]
    public void A_profile_already_carrying_the_theme_key_does_not_duplicate_it()
    {
        var s = With(["progress", "money", "raids"]);

        s.MigrateProgressSections();

        Assert.Single(s.SectionOrder, k => k == "progress");
    }
}
