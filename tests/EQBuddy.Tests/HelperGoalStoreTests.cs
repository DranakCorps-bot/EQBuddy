using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Helper's two per-character selections.
///
/// <para><b>Writer and reader in the same slice</b> (trap 20). A setting that only READERS
/// touch has cost this repo three player-facing bugs, every one of them the same event: a
/// surface folded into another, the DATA survived the move and the WRITE path did not.
/// <c>DeadSettingTests</c> catches that from the outside; these rows are what say the write
/// path does the right thing while it exists.</para>
/// </summary>
/// <remarks>
/// In the serial settings collection because <see cref="TheSelectionSurvivesASaveAndLoad"/>
/// writes the shared profile's <c>settings.json</c> — the 1-in-3 flake of 2026-08-22, and
/// <c>SettingsFileCollectionTests</c> is the guard that names a file which forgot.
/// </remarks>
[Collection(SettingsFileCollection.Name)]
public class HelperGoalStoreTests
{
    private const string Dranak = "erollisi|Dranak";
    private const string Alt = "erollisi|Smallwizard";

    [Fact]
    public void AGoalTogglesOnAndOffAgain()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.LevelUp);
        Assert.Equal([HelperGoal.LevelUp], HelperGoalStore.Goals(settings, Dranak));

        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.LevelUp);
        Assert.Empty(HelperGoalStore.Goals(settings, Dranak));
    }

    /// <summary>
    /// **One person's alts are not one player's plan.** A level-8 enchanter being pushed and
    /// a level-50 main grinding faction want different answers from the same install, which
    /// is why the key is the character and not the profile.
    /// </summary>
    [Fact]
    public void SelectionsAreScopedToTheCharacter()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.WorkOnFaction);
        HelperGoalStore.Toggle(settings, Alt, HelperGoal.LevelUp);

        Assert.Equal([HelperGoal.WorkOnFaction], HelperGoalStore.Goals(settings, Dranak));
        Assert.Equal([HelperGoal.LevelUp], HelperGoalStore.Goals(settings, Alt));
    }

    /// <summary>
    /// **An empty selection REMOVES the key rather than storing an empty list.** "Never
    /// picked" and "picked nothing" are the same state here — both mean weigh everything —
    /// and two spellings of one state is a distinction a later reader would eventually act
    /// on.
    /// </summary>
    [Fact]
    public void TurningTheLastGoalOffLeavesNoRowBehind()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.LevelUp);
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.LevelUp);
        Assert.False(settings.HelperGoals.ContainsKey(Dranak));

        HelperGoalStore.ToggleFaction(settings, Dranak, "Dark Bargainers");
        HelperGoalStore.ToggleFaction(settings, Dranak, "Dark Bargainers");
        Assert.False(settings.HelperFactions.ContainsKey(Dranak));
    }

    /// <summary>Read back in the enum's own order, not the click order, so the "serves" line
    /// under a recommendation reads the same way the chip strip does however the player
    /// clicked.</summary>
    [Fact]
    public void GoalsComeBackInTheFoundersOrderAndNotInClickOrder()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.Achievements);
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.LevelUp);
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.FarmGear);

        Assert.Equal([HelperGoal.LevelUp, HelperGoal.FarmGear, HelperGoal.Achievements],
            HelperGoalStore.Goals(settings, Dranak));
    }

    /// <summary>
    /// **Names, not ordinals, and an unknown one is skipped.** An enum's numeric value is a
    /// promise about declaration order nobody is keeping: inserting a tenth goal in the
    /// Founder's list would silently re-point every stored selection. A goal later removed
    /// should stop mattering, not break the room for whoever had ticked it.
    /// </summary>
    [Fact]
    public void AStoredGoalNameNobodyRecognisesIsSkippedRatherThanThrowing()
    {
        var settings = new AppSettings();
        settings.HelperGoals[Dranak] = ["LevelUp", "TameDragons", "workonfaction"];

        // And the surviving two are matched case-insensitively, because a hand-edited
        // settings.json is a real thing a power user does.
        Assert.Equal([HelperGoal.LevelUp, HelperGoal.WorkOnFaction],
            HelperGoalStore.Goals(settings, Dranak));
    }

    /// <summary>Factions are stored in the DUMP's own spelling. The achievements text and the
    /// faction dump disagree about four of these names and <c>FactionNames.Resolve</c> is
    /// what reconciles them at read time — normalising here would bake one source's spelling
    /// into the player's profile where nothing could correct it later.</summary>
    [Fact]
    public void AFactionIsStoredExactlyAsTheDumpSpelledIt()
    {
        var settings = new AppSettings();
        HelperGoalStore.ToggleFaction(settings, Dranak, "Dark Bargainers");
        Assert.Equal(["Dark Bargainers"], HelperGoalStore.Factions(settings, Dranak));

        // Toggling by a differently-cased spelling still removes it — the player clicked one
        // chip, and a second row for "dark bargainers" would be a pick they cannot undo.
        HelperGoalStore.ToggleFaction(settings, Dranak, "dark bargainers");
        Assert.Empty(HelperGoalStore.Factions(settings, Dranak));
    }

    /// <summary>No character key yet — the log has named nobody, or the ledger has not keyed
    /// them. Writing under "" would put one player's goals on every future character; reading
    /// answers empty, which means "weigh everything" and is the right first screen.</summary>
    [Fact]
    public void WithNoCharacterKeyNothingIsWrittenAndNothingIsRead()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, "", HelperGoal.LevelUp);
        HelperGoalStore.ToggleFaction(settings, "", "Dark Bargainers");

        Assert.Empty(settings.HelperGoals);
        Assert.Empty(settings.HelperFactions);
        Assert.Empty(HelperGoalStore.Goals(settings, ""));
        Assert.Empty(HelperGoalStore.Factions(settings, ""));
    }

    /// <summary>A blank faction name is refused. It would render as an unlabelled chip that
    /// selects nothing — an affordance that opens nothing, one level down from the rail's own
    /// rule.</summary>
    [Fact]
    public void ABlankFactionIsRefused()
    {
        var settings = new AppSettings();
        HelperGoalStore.ToggleFaction(settings, Dranak, "   ");
        Assert.Empty(settings.HelperFactions);
    }

    /// <summary>The selections survive a save/load round trip, which is the whole promise of
    /// "a standing intent rather than a session lens": a chip strip that reset every launch
    /// would be a control you operate instead of a preference you hold.</summary>
    [Fact]
    public void TheSelectionSurvivesASaveAndLoad()
    {
        var settings = new AppSettings();
        HelperGoalStore.Toggle(settings, Dranak, HelperGoal.WorkOnFaction);
        HelperGoalStore.ToggleFaction(settings, Dranak, "Dark Bargainers");
        settings.Save();

        var loaded = AppSettings.Load();
        Assert.Equal([HelperGoal.WorkOnFaction], HelperGoalStore.Goals(loaded, Dranak));
        Assert.Equal(["Dark Bargainers"], HelperGoalStore.Factions(loaded, Dranak));
    }
}
