using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// Character Setup's class chips, and the statement they store.
///
/// <para><b>The Founder's smoke, 2026-09-22.</b> Character Dranak. The achievements
/// dump still yields Paladin · Druid · Warrior — a class he has never played sits in
/// the unlock history — and he set the chips to Warrior, Druid and Monk. The Character
/// section kept Paladin. Two readings produce that, and this file refuses both: a click
/// on a class the line is already showing must take it OFF (chips that start blank turn
/// that click into an add), and a statement that stands must not be united with the
/// dump, the log or the quest picks the next time the editor is built.</para>
///
/// <para>The arithmetic lives in <see cref="ClassStatement"/>. The room is a caller of
/// it; the source guard at the bottom is what stops a second copy growing back in
/// <c>HomeRoom</c> (trap 4).</para>
/// </summary>
public sealed class ClassStatementTests : IDisposable
{
    /// <summary>The dump's first three, in the order the line shows them. Paladin is the
    /// class the Founder has never played.</summary>
    private static readonly string[] Guess = ["Paladin", "Druid", "Warrior"];

    private const string Dranak = "dranak_legends";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* the temp file is the fixture, not the profile */ }
        try { File.Delete(_path + ".rules"); } catch { /* same */ }
    }

    /// <summary>No statement yet: the chips open on the same classes the line is showing,
    /// so there is something to untick. A blank strip cannot remove Paladin — the click
    /// would store it.</summary>
    [Fact]
    public void WithNoStatementTheChipsSeedFromTheGuess()
    {
        var shown = ClassStatement.EditorSelection(
            stated: [], unlocked: Guess, inferred: null, picks: null);

        Assert.Equal(Guess, shown);
        Assert.Contains("Paladin", shown);
    }

    /// <summary>
    /// The gesture. Paladin starts ticked because the dump put it on the line. Unticking
    /// it removes it; ticking Monk puts Monk on. The saved selection is those chips and
    /// not a union with the guess — Paladin is gone even though the dump still names it
    /// and the selection was already at three before the untick.
    /// </summary>
    [Fact]
    public void UntickingPaladinAndTickingMonkReplacesTheGuess()
    {
        var shown = ClassStatement.EditorSelection(
            stated: null, unlocked: Guess, inferred: ["Paladin"], picks: ["Paladin"]);
        Assert.Equal(Guess, shown);

        // A fourth tick on a full seed changes nothing. Monk cannot sneak in by evicting
        // Paladin; the player takes Paladin off first. That is the cap the note announces.
        var refused = ClassStatement.Toggle(shown, "Monk");
        Assert.Equal(Guess, refused);

        var withoutPaladin = ClassStatement.Toggle(shown, "Paladin");
        Assert.Equal(["Druid", "Warrior"], withoutPaladin);
        Assert.DoesNotContain("Paladin", withoutPaladin);

        var saved = ClassStatement.Toggle(withoutPaladin, "Monk");
        Assert.Equal(["Druid", "Warrior", "Monk"], saved);
        Assert.DoesNotContain("Paladin", saved);

        // The editor, rebuilt while the dump and the log still say Paladin.
        var reopened = ClassStatement.EditorSelection(
            stated: saved, unlocked: Guess, inferred: ["Paladin"], picks: ["Paladin"]);
        Assert.Equal(["Druid", "Warrior", "Monk"], reopened);
        Assert.DoesNotContain("Paladin", reopened);
    }

    /// <summary>Ticking only the three classes he plays, starting from an empty selection
    /// (the statement after a clear, before the guess is allowed to seed the next open).
    /// Paladin is not in what he ticked, so it is not in what is stored.</summary>
    [Fact]
    public void TickingWarriorDruidMonkFromEmptyDoesNotKeepPaladin()
    {
        IReadOnlyList<string> selection = [];
        foreach (var cls in new[] { "Warrior", "Druid", "Monk" })
            selection = ClassStatement.Toggle(selection, cls);

        Assert.Equal(["Warrior", "Druid", "Monk"], selection);

        var reopened = ClassStatement.EditorSelection(
            stated: selection, unlocked: Guess, inferred: ["Paladin"], picks: null);
        Assert.Equal(["Warrior", "Druid", "Monk"], reopened);
        Assert.DoesNotContain("Paladin", reopened);

        var (classes, source) = CharacterClasses.Resolve(
            unlocked: Guess, inferred: ["Paladin"], picks: ["Paladin"], stated: selection);
        Assert.Equal(["Warrior", "Druid", "Monk"], classes);
        Assert.Equal(ClassSource.Stated, source);
        Assert.Equal("set by you", CharacterClasses.SourceLabel(source));
    }

    /// <summary>Clearing is the undo. With no statement the guess may seed again — that
    /// is "Let EQBuddy work it out", not a removed class climbing back in over a
    /// statement that still stands.</summary>
    [Fact]
    public void AnEmptyStatementLetsTheGuessSeedAgain()
    {
        var shown = ClassStatement.EditorSelection(
            stated: [], unlocked: Guess, inferred: null, picks: null);

        Assert.False(ClassStatement.HasStatement([]));
        Assert.Equal(Guess, shown);
    }

    /// <summary>
    /// The statement survives the relaunch, and a fresh inference of Paladin does not
    /// put it back on the chips or on the line. This is the fixture the room's own
    /// click cannot reach from E2E — <c>SetStatedClasses</c> has one writer, the chip,
    /// and the suite may not press it — so the persistence is proved here, through the
    /// same store that writer uses.
    /// </summary>
    [Fact]
    public void TheSavedSelectionSurvivesARelaunchAndStillBeatsPaladin()
    {
        var shown = ClassStatement.EditorSelection(
            stated: null, unlocked: Guess, inferred: null, picks: null);
        var saved = ClassStatement.Toggle(ClassStatement.Toggle(shown, "Paladin"), "Monk");

        var store = new QuestLedgerStore(_path);
        store.SetUnlockedClasses(Dranak, Guess);
        store.SetClasses(Dranak, ["Paladin"]);
        store.SetStatedClasses(Dranak, saved);
        store.Flush();

        var reopened = new QuestLedgerStore(_path);
        var stated = reopened.StatedClassesFor(Dranak);
        var unlocked = reopened.UnlockedClassesFor(Dranak);

        Assert.Equal(["Druid", "Warrior", "Monk"], stated);
        Assert.Equal(Guess, unlocked);

        var chips = ClassStatement.EditorSelection(
            stated, unlocked, inferred: ["Paladin"], picks: reopened.ClassesFor(Dranak));
        Assert.Equal(["Druid", "Warrior", "Monk"], chips);
        Assert.DoesNotContain("Paladin", chips);

        var (classes, source) = CharacterClasses.Resolve(
            unlocked, inferred: ["Paladin"], picks: ["Paladin"], stated);
        Assert.Equal(["Druid", "Warrior", "Monk"], classes);
        Assert.Equal(ClassSource.Stated, source);
    }

    /// <summary>
    /// Today's smoke, through the refresh the room actually gets. He set Warrior · Druid ·
    /// Monk. The achievements file still says Paladin · Druid · Warrior — re-reading it is
    /// what <see cref="OutputfileAutoImport.ImportAchievements"/> does, and it must not put
    /// Paladin back on the line or on the chips. The test does not call <c>Flush</c>: a
    /// loot line can wait out the ledger's debounce because the log rebuilds it, and a
    /// statement cannot. Reopening the file is the relaunch.
    /// </summary>
    [Fact]
    public void TheStatementReplacesTheDumpAndSurvivesARefresh()
    {
        var stated = new[] { "Warrior", "Druid", "Monk" };
        var store = new QuestLedgerStore(_path);
        store.SetStatedClasses(Dranak, stated);

        var dir = Directory.CreateTempSubdirectory("eqb-class-refresh");
        try
        {
            var dump = Path.Combine(dir.FullName, "Dranak_legends-Achievements.txt");
            File.WriteAllLines(dump,
            [
                "Untapped Potential: Classes",
                "C\tPrimary Class Unlock - Paladin",
                "C\tPrimary Class Unlock - Druid",
                "C\tPrimary Class Unlock - Warrior",
            ]);

            OutputfileAutoImport.ImportAchievements(
                dump, new AppSettings(), raids: null, store, Dranak);

            Assert.Equal(stated, store.StatedClassesFor(Dranak));
            Assert.Equal(Guess, store.UnlockedClassesFor(Dranak));

            var (live, liveSource) = CharacterClasses.Resolve(
                store.UnlockedClassesFor(Dranak),
                inferred: ["Paladin"],
                picks: ["Paladin"],
                store.StatedClassesFor(Dranak));
            Assert.Equal(stated, live);
            Assert.Equal(ClassSource.Stated, liveSource);
            Assert.DoesNotContain("Paladin", live);

            var chips = ClassStatement.EditorSelection(
                store.StatedClassesFor(Dranak),
                store.UnlockedClassesFor(Dranak),
                inferred: ["Paladin"],
                picks: ["Paladin"]);
            Assert.Equal(stated, chips);
        }
        finally
        {
            dir.Delete(true);
        }

        var reopened = new QuestLedgerStore(_path);
        var saved = reopened.StatedClassesFor(Dranak);
        Assert.Equal(stated, saved);

        var (classes, source) = CharacterClasses.Resolve(
            reopened.UnlockedClassesFor(Dranak),
            inferred: ["Paladin"],
            picks: reopened.ClassesFor(Dranak),
            saved);
        Assert.Equal(stated, classes);
        Assert.Equal(ClassSource.Stated, source);
        Assert.DoesNotContain("Paladin", classes);
    }

    /// <summary>The room paints the chips from <see cref="ClassStatement"/> and stores
    /// what <see cref="ClassStatement.Toggle"/> returns. Selecting from <c>_stated</c>
    /// alone is the blank-strip bug: the line shows the guess, the chips show nothing,
    /// and a click on the class the player wants gone stores it.</summary>
    [Fact]
    public void TheCharacterRoomAsksClassStatementForTheChipsAndTheClick()
    {
        var source = File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            "src", "EQBuddy", "HomeRoom.cs"));

        Assert.Contains("ClassStatement.EditorSelection", source, StringComparison.Ordinal);
        Assert.Contains("ClassStatement.Toggle", source, StringComparison.Ordinal);

        var toggleAt = source.IndexOf("private void ToggleStated(", StringComparison.Ordinal);
        Assert.True(toggleAt > 0, "ToggleStated has been renamed — this guard needs re-aiming");
        var toggleEnd = source.IndexOf("\n    private ", toggleAt + 1, StringComparison.Ordinal);
        var toggle = source[toggleAt..toggleEnd];
        Assert.Contains("ClassStatement.Toggle", toggle, StringComparison.Ordinal);
        Assert.DoesNotContain("new List<string>(_stated)", toggle, StringComparison.Ordinal);
    }
}
