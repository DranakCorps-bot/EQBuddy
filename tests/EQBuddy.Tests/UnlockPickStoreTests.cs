using System.Text.Json;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE UNLOCK PICK: ONE STORE, FILTER SEMANTICS, NARROWED PER SECTION** (DRA-71 D5, Fable
/// plan P11; Founder smoke item 7).
///
/// <para>Three rules carry the whole feature, and each of them is a decision somebody could
/// reasonably have made the other way — which is why each has a test rather than a
/// comment:</para>
///
/// <list type="number">
/// <item><b>Absent means ALL.</b> The sibling store beside it (<c>HelperFactions</c>) means
/// the opposite, deliberately, and picking the wrong one here would empty a tab that has
/// worked since 2026-08-25.</item>
/// <item><b>Unticking the last one REMOVES the key</b>, so "never picked" and "picked nothing"
/// cannot become two spellings of one state.</item>
/// <item><b>A pick that names nothing in a section narrows nothing in it</b> — the rule that
/// keeps one flat list from making a race pick empty the class half.</item>
/// </list>
///
/// <para>The serializer round trip is here because a per-character selection that does not
/// survive a restart is a preference the player has to re-enter, which is the failure the
/// store exists to prevent.</para>
/// </summary>
public class UnlockPickStoreTests
{
    private const string Key = "dranak_erollisi";

    private static UnlockProgress Race(string subject, bool complete = false) =>
        new("Untapped Potential: Races", $"Race Unlock - {subject}", subject, complete, false,
            [new UnlockCriterion(UnlockNeed.MaxFaction,
                $"Get maximum faction with Friends of {subject}.", $"Friends of {subject}", false)]);

    private static UnlockProgress Class(string subject, bool complete = false) =>
        new("Untapped Potential: Classes", $"Class Unlock - {subject}", subject, complete, false,
            [new UnlockCriterion(UnlockNeed.Obtain, "Obtain Something.", "Something", false)]);

    // ---- rule 1: absent means all ------------------------------------------------------

    [Fact]
    public void ACharacterWhoHasNeverPickedHasPickedNothing() =>
        Assert.Empty(UnlockPickStore.Picked(new AppSettings(), Key));

    /// <summary>**Absent = ALL, and this is the assertion that says so.** It is the opposite
    /// of the faction picker's reading one block up in the same room, and the whole argument
    /// for the difference is in <c>AppSettings.UnlockPicks</c>' own summary.</summary>
    [Fact]
    public void NothingPickedNarrowsNothing()
    {
        var races = new[] { Race("Iksar"), Race("Ogre"), Race("Troll") };

        Assert.Equal(3, UnlockPickStore.Narrow(races, []).Count);
        Assert.Equal(0, UnlockPickStore.Hidden(races, []));
    }

    // ---- rule 2: the toggle, and the empty key ------------------------------------------

    [Fact]
    public void PickingAndUnpickingRoundTripsThroughOneStore()
    {
        var settings = new AppSettings();

        UnlockPickStore.Toggle(settings, Key, "Iksar");
        Assert.Equal(["Iksar"], UnlockPickStore.Picked(settings, Key));

        UnlockPickStore.Toggle(settings, Key, "Necromancer");
        Assert.Equal(["Iksar", "Necromancer"], UnlockPickStore.Picked(settings, Key));

        UnlockPickStore.Toggle(settings, Key, "Iksar");
        Assert.Equal(["Necromancer"], UnlockPickStore.Picked(settings, Key));
    }

    /// <summary>Unticking the last one REMOVES the key rather than storing an empty list. Both
    /// halves are asserted, because an empty list would read as "picked nothing" to every
    /// future reader and the two states must not be distinguishable.</summary>
    [Fact]
    public void UntickingTheLastPickRemovesTheKeyRatherThanStoringAnEmptyList()
    {
        var settings = new AppSettings();
        UnlockPickStore.Toggle(settings, Key, "Iksar");
        UnlockPickStore.Toggle(settings, Key, "Iksar");

        Assert.False(settings.UnlockPicks.ContainsKey(Key));
        Assert.Empty(UnlockPickStore.Picked(settings, Key));
    }

    /// <summary>The two files that spell an unlock's name are written by different programs,
    /// so the match is case-insensitive — in the toggle as well as in the filter, or a pick
    /// made from one spelling could never be taken back with the other.</summary>
    [Fact]
    public void TheSpellingIsMatchedWithoutCase()
    {
        var settings = new AppSettings();
        UnlockPickStore.Toggle(settings, Key, "Iksar");
        UnlockPickStore.Toggle(settings, Key, "IKSAR");

        Assert.False(settings.UnlockPicks.ContainsKey(Key));
        Assert.True(UnlockPickStore.IsPicked(["iksar"], "Iksar"));
    }

    /// <summary>One character's plan is not another's. The same install holds a level-8
    /// enchanter being pushed and a level-50 main grinding faction, and a pick that leaked
    /// between them would be the room answering about somebody else.</summary>
    [Fact]
    public void PicksAreKeptPerCharacter()
    {
        var settings = new AppSettings();
        UnlockPickStore.Toggle(settings, Key, "Iksar");
        UnlockPickStore.Toggle(settings, "someone_else", "Ogre");

        Assert.Equal(["Iksar"], UnlockPickStore.Picked(settings, Key));
        Assert.Equal(["Ogre"], UnlockPickStore.Picked(settings, "someone_else"));
    }

    /// <summary>A missing character key writes nothing and throws nothing: the ledger key is
    /// empty until the log names a character, and a room drawn in that window must not be able
    /// to write a pick under "".</summary>
    [Fact]
    public void AnEmptyCharacterKeyIsRefusedRatherThanStored()
    {
        var settings = new AppSettings();
        UnlockPickStore.Toggle(settings, "", "Iksar");
        UnlockPickStore.Toggle(settings, Key, "   ");

        Assert.Empty(settings.UnlockPicks);
    }

    // ---- rule 3: the narrowing is PER SECTION -------------------------------------------

    [Fact]
    public void APickKeepsOnlyWhatItNames()
    {
        var races = new[] { Race("Iksar"), Race("Ogre"), Race("Troll") };

        var kept = UnlockPickStore.Narrow(races, ["Iksar", "Troll"]);

        Assert.Equal(["Iksar", "Troll"], kept.Select(u => u.Subject));
        Assert.Equal(1, UnlockPickStore.Hidden(races, ["Iksar", "Troll"]));
    }

    /// <summary>
    /// **THE RULE THE ONE-LIST DESIGN RESTS ON.** A player working on Iksar has picked no
    /// class, and the Classes half must come back whole — not empty. Without this, one flat
    /// list would make every race pick silently delete the class section of a tab with no
    /// control on screen able to explain where it went.
    /// </summary>
    [Fact]
    public void APickThatNamesNothingInThisSectionNarrowsNothingInIt()
    {
        var classes = new[] { Class("Necromancer"), Class("Paladin") };

        Assert.Equal(2, UnlockPickStore.Narrow(classes, ["Iksar"]).Count);
        Assert.Equal(0, UnlockPickStore.Hidden(classes, ["Iksar"]));
    }

    /// <summary>And the same pick narrows BOTH sections when it names something in each —
    /// which is the state the feature is actually for, and the negative the assertion above
    /// needs to mean anything (trap 39).</summary>
    [Fact]
    public void OnePickNarrowsEverySectionItNames()
    {
        var races = new[] { Race("Iksar"), Race("Ogre") };
        var classes = new[] { Class("Necromancer"), Class("Paladin") };
        string[] picked = ["Iksar", "Necromancer"];

        Assert.Equal(["Iksar"], UnlockPickStore.Narrow(races, picked).Select(u => u.Subject));
        Assert.Equal(["Necromancer"],
            UnlockPickStore.Narrow(classes, picked).Select(u => u.Subject));
    }

    /// <summary>A stored name the dump no longer carries narrows nothing rather than throwing
    /// or emptying the list — an unlock removed from the game's own achievements text should
    /// stop mattering, not break the tab for whoever had ticked it.</summary>
    [Fact]
    public void APickTheDumpNoLongerKnowsIsIgnored()
    {
        var races = new[] { Race("Iksar"), Race("Ogre") };

        Assert.Equal(2, UnlockPickStore.Narrow(races, ["Gnomeling"]).Count);
    }

    /// <summary>A complete unlock is pickable like any other. The engine skips finished ones
    /// on its own, and dropping them here would make the Quests tab — the other reader of this
    /// store — offer a row it then could not filter on.</summary>
    [Fact]
    public void AFinishedUnlockIsStillPickable()
    {
        var races = new[] { Race("Iksar"), Race("Human", complete: true) };

        Assert.Equal(["Human"], UnlockPickStore.Narrow(races, ["Human"]).Select(u => u.Subject));
    }

    // ---- the round trip a restart actually makes ------------------------------------------

    /// <summary>
    /// The pick survives a save and a reload of the profile.
    ///
    /// <para>A standing intent that evaporated at launch would be a preference the player
    /// re-enters every session — the failure the store exists to prevent, and one the
    /// in-memory assertions above cannot see.</para>
    ///
    /// <para><b>Through the SERIALIZER and not through the profile directory</b>: this
    /// assembly runs its collections in parallel, so a test that moved
    /// <c>EQBUDDY_APPDATA</c> would move it for every test running beside it (trap 57) — the
    /// same reason <c>ProfileSplitTests</c> asks its question of a pure method. A reloaded
    /// dictionary also carries the DEFAULT comparer rather than a case-insensitive one, which
    /// is why the read is asserted through the store rather than off the property.</para>
    /// </summary>
    [Fact]
    public void ThePickSurvivesARestart()
    {
        var settings = new AppSettings();
        UnlockPickStore.Toggle(settings, Key, "Iksar");
        UnlockPickStore.Toggle(settings, Key, "Necromancer");

        // The app's own options: several window coordinates default to NaN, which plain
        // System.Text.Json refuses to write — the profile has always been saved this way.
        var opts = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                .AllowNamedFloatingPointLiterals,
        };
        var reloaded = JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(settings, opts), opts)!;

        Assert.Equal(["Iksar", "Necromancer"], UnlockPickStore.Picked(reloaded, Key));
        Assert.True(UnlockPickStore.IsPicked(UnlockPickStore.Picked(reloaded, Key), "iksar"));
    }
}
