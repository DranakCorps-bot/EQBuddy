using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The unlock picker's words, which BOTH surfaces read (DRA-71 D5, plan P11).
///
/// <para>The face is <see cref="PickerFace"/>'s rule with a new noun, so what is asserted here
/// is the noun and the two budgets — not the rule, which <c>PickerFaceTests</c> already owns.
/// The two budgets are the point: the Quests window's face SHARES its row (the #184 geometry)
/// and the Helper's owns one, and a single spelling of the cap would have put #184 straight
/// back on whichever surface lost the argument.</para>
/// </summary>
public class UnlockPickReadoutTests
{
    private static UnlockProgress Unlock(
        string subject, bool complete, params UnlockCriterion[] criteria) =>
        new("Untapped Potential: Races", $"Race Unlock - {subject}", subject, complete, false,
            criteria);

    private static UnlockCriterion Work(bool done = false) =>
        new(UnlockNeed.MaxFaction, "Get maximum faction with Someone.", "Someone", done);

    private static UnlockCriterion NotWork() =>
        new(UnlockNeed.Derived, "This achievement will autocomplete when you unlock Human.", "", false);

    // ---- the face ----------------------------------------------------------------------

    [Fact]
    public void NothingPickedReadsAsAnyUnlock() =>
        Assert.Equal("Any unlock", UnlockPickReadout.Face([], 30, PickerFace.MaxChars));

    /// <summary>One pick is always its own name, whatever the budget — the rule the class face
    /// has always had ("Cleric", not "1 class").</summary>
    [Fact]
    public void OnePickIsNamed() =>
        Assert.Equal("Necromancer",
            UnlockPickReadout.Face(["Necromancer"], 30, PickerFace.MaxChars));

    /// <summary>Ticking every row says so by name. The offer is never capped here — unlike the
    /// faction picker's, whose list is hundreds long and whose face is therefore never told
    /// how many exist — so "All unlocks" is a claim this face may make.</summary>
    [Fact]
    public void TickingEveryRowSaysAllUnlocks() =>
        Assert.Equal("All unlocks",
            UnlockPickReadout.Face(["Iksar", "Ogre"], 2, PickerFace.MaxChars));

    /// <summary>
    /// **THE TWO BUDGETS, AND THE SAME TWO PICKS.** On the Quests window's shared row the face
    /// counts; in the Helper's own column it names. That is the whole difference between the
    /// two callers, and asserting them together is what stops one of them from quietly
    /// adopting the other's number.
    /// </summary>
    [Fact]
    public void TheSharedRowCountsWhereTheHelpersOwnRowNames()
    {
        string[] picked = ["Iksar", "Necromancer"];

        Assert.Equal("2 unlocks", UnlockPickReadout.Face(picked, 30, PickerFace.MaxChars));
        Assert.Equal("Iksar · Necromancer",
            UnlockPickReadout.Face(picked, 30, HelperPresentation.FaceChars));
    }

    /// <summary>And the roomier budget is still a budget: three of the longest names is 40
    /// characters and counts, which is the cap doing its job rather than failing at it.</summary>
    [Fact]
    public void EvenTheRoomierBudgetCountsPastItsWidth() =>
        Assert.Equal("3 unlocks", UnlockPickReadout.Face(
            ["Necromancer", "Shadow Knight", "Half Elf"], 30, HelperPresentation.FaceChars));

    // ---- the rows ----------------------------------------------------------------------

    /// <summary>Three states and no fourth, because <c>UnlockProgress.Score</c> has three. The
    /// last is Half Elf's — only Derived criteria, so nothing to count — and "0 of 0" there
    /// would read as a stalled checklist.</summary>
    [Fact]
    public void ARowSaysHowFarAlongItIs()
    {
        Assert.Equal("Iksar — 1 of 2 done",
            UnlockPickReadout.Row(Unlock("Iksar", false, Work(done: true), Work())));
        Assert.Equal("Human — unlocked",
            UnlockPickReadout.Row(Unlock("Human", true, Work(done: true))));
        Assert.Equal("Half Elf", UnlockPickReadout.Row(Unlock("Half Elf", false, NotWork())));
    }

    // ---- what the filter withheld ------------------------------------------------------

    /// <summary>A surviving filter says what it held back (trap 50) — and the player did this
    /// one, so the note names the way back rather than apologising.</summary>
    [Fact]
    public void TheHiddenNoteCountsAndSaysTheWayBack()
    {
        Assert.Contains("13 more unlocks are hidden", UnlockPickReadout.HiddenNote(13));
        Assert.Contains("Untick", UnlockPickReadout.HiddenNote(13));
        Assert.Contains("1 more unlock is hidden", UnlockPickReadout.HiddenNote(1));
    }

    /// <summary>Zero draws NOTHING rather than "0 hidden" — both "nothing picked" and "picked
    /// only from the other section" land here, and neither hid anything. Every equality
    /// assertion deserves its negative (trap 39).</summary>
    [Fact]
    public void NothingHiddenSaysNothing()
    {
        Assert.Equal("", UnlockPickReadout.HiddenNote(0));
        Assert.Equal("", UnlockPickReadout.HiddenNote(-1));
    }

    /// <summary>The note over the picker states the OFF-state in words. It is the one sentence
    /// that makes "absent = all" visible rather than documented, and a filter whose empty
    /// reading is a mystery is a filter people avoid touching.</summary>
    [Fact]
    public void TheNoteSaysWhatNothingPickedMeans() =>
        Assert.Contains("Nothing picked means", UnlockPickReadout.Note);
}
