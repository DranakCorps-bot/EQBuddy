using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **THE "NO LONGER ON THE WIDGET" LIST — the shape of it, and the two ways it can rot.**
///
/// `OverlaySections.AbsorbedTitles` answers "where did my card go" by hanging the old names
/// under the card that ABSORBED them, and it cannot answer for a card that was SUBTRACTED:
/// Quests and World did not merge into anything, so there is no surviving row to hang a note
/// on. Six names had no row on this screen at all — a known cost recorded in `Catalog`'s own
/// comments when each cut shipped, ruled on by Bevel (I-11 §4) and Helm-signed 2026-09-05.
/// `OverlaySections.Retired` is the fix, keyed by the OLD TITLE.
///
/// **Two failure modes, and this file exists for both.** A row that describes a card which is
/// still live is trap 55 exactly — the shape that cost #252, where two hand-maintained lists
/// described one fold and only one of them was told when Motes came back. And a cut that
/// ships with no row is the gap re-opening silently, seven more times, because I-8 has seven
/// more cards queued behind Surface A.
/// </summary>
public sealed class RetiredCardsTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    /// <summary>
    /// **A row may only name a card that is no longer a card** — by key AND by title. This is
    /// trap 55's rule ("a fold may only name keys that are no longer cards") applied to the
    /// other list, and it is checked against `Catalog` rather than against either comment,
    /// for the reason `SectionFoldIdempotenceTests` gives: a comment is what was true when
    /// somebody last read it.
    /// </summary>
    [Fact]
    public void NoRetiredRowDescribesACardThatIsStillOnTheWidget()
    {
        foreach (var gone in OverlaySections.Retired)
        {
            Assert.DoesNotContain(OverlaySections.Catalog, c => c.Key == gone.Key);
            Assert.DoesNotContain(OverlaySections.Catalog, c => c.Title == gone.Title);
            // And the names it answers for cannot be live cards either. "Motes" is the worked
            // example of why: it left AbsorbedTitles the day it became a card again, and a
            // note that sends someone into a window to find a card sitting two rows above is
            // worse than saying nothing.
            foreach (var name in gone.Answered)
                Assert.DoesNotContain(OverlaySections.Catalog, c => c.Title == name);
        }
    }

    /// <summary>
    /// The two lists must not both claim one name. `AbsorbedTitles` says "it is a tab in THIS
    /// card"; this list says "it is not on the widget at all" — a name in both is one of the
    /// two lying, and the screen shows them four inches apart.
    /// </summary>
    [Fact]
    public void NoNameIsClaimedByBothTheAbsorbedNoteAndTheRetiredList()
    {
        var absorbed = string.Join(" · ",
            OverlaySections.Catalog
                .Select(c => OverlaySections.AbsorbedNote(c.Key))
                .Where(n => n is not null));

        foreach (var gone in OverlaySections.Retired)
        {
            Assert.DoesNotContain(gone.Title, absorbed, StringComparison.Ordinal);
            foreach (var name in gone.Answered)
                Assert.DoesNotContain(name, absorbed, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **The door has to be real for a player who has configured nothing** — trap 59, which
    /// is the check that saved cut 1: Quests' "second way in" was a hotkey, and
    /// `HotkeyManager` binds nothing by default, so the card's ⧉ was the only entrance a
    /// default profile had. A row here names a CONTEXT-MENU header, and this asserts that
    /// header is still in `MainWindow.xaml` verbatim. A menu row renamed without this list
    /// being told would leave the screen telling people to choose something that is not
    /// there — which is the same defect as the missing row, one step further on.
    /// </summary>
    [Fact]
    public void EveryRetiredRowNamesAContextMenuRowThatStillExists()
    {
        var menu = File.ReadAllText(
            Path.Combine(Repo, "src", "EQBuddy", "MainWindow.xaml"));

        foreach (var gone in OverlaySections.Retired)
        {
            Assert.Contains($"Header=\"{gone.MenuHeader}\"", menu, StringComparison.Ordinal);
            Assert.Contains(gone.MenuHeader, gone.Line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **The line says the OLD place and the NEW one** — CLAUDE.md's "X is now Y", which is a
    /// public promise made in 1.99.6's What's-new rather than a style preference. #219, #227
    /// and #228 were all one complaint arriving three times, and each release note named the
    /// destination without naming the origin.
    /// </summary>
    [Fact]
    public void EveryLineNamesTheOldPlaceTheNewPlaceAndTheWayIn()
    {
        foreach (var gone in OverlaySections.Retired)
        {
            Assert.StartsWith($"{gone.Title} is now {gone.Now}", gone.Line, StringComparison.Ordinal);
            foreach (var name in gone.Answered)
                Assert.Contains(name, gone.Line, StringComparison.Ordinal);
            Assert.Contains("Right-click EQBuddy", gone.Line, StringComparison.Ordinal);
            Assert.EndsWith(".", gone.Line, StringComparison.Ordinal);
            Assert.NotEmpty(gone.Key);
        }

        Assert.Equal(OverlaySections.Retired.Count,
            OverlaySections.Retired.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The sentence has to read correctly for one name, several, and none. "Gear are tabs in
    /// here now" shipped for a fortnight on the note above, and a line whose whole job is to
    /// be believed by someone hunting a vanished card cannot afford to look wrong.
    /// </summary>
    [Fact]
    public void TheSentenceIsGrammaticalForOneNameSeveralAndNone()
    {
        Assert.Equal(
            "Gear is now the Gear window — Bags is a tab in it. Right-click EQBuddy and choose “Gear…”.",
            new OverlaySections.RetiredCard("gear", "Gear", "the Gear window", "Gear…", ["Bags"]).Line);

        Assert.Equal(
            "Gear is now the Gear window — Bags · Wishlist are tabs in it. "
            + "Right-click EQBuddy and choose “Gear…”.",
            new OverlaySections.RetiredCard("gear", "Gear", "the Gear window", "Gear…",
                ["Bags", "Wishlist"]).Line);

        // A card that answered for nothing but itself still gets a row — Quests was very
        // nearly this case, and "it is gone and here is where" is the whole point.
        Assert.Equal(
            "Gear is now the Gear window. Right-click EQBuddy and choose “Gear…”.",
            new OverlaySections.RetiredCard("gear", "Gear", "the Gear window", "Gear…", []).Line);
    }

    /// <summary>
    /// **The other direction, which is the one that goes quiet: a cut that ships with no
    /// row.** Both subtractions that have shipped are named here explicitly, by the words a
    /// player scans for. A future cut adds its own row — and until this assertion is edited
    /// alongside it, nothing else in the suite would notice the omission.
    /// </summary>
    [Fact]
    public void BothShippedSubtractionsHaveTheirRow()
    {
        var quests = OverlaySections.Retired.Single(r => r.Key == "quests");
        Assert.Equal("Quests", quests.Title);
        Assert.Equal(["Sky Quest", "Epics"], quests.Answered);

        // "misc" is the oldest key in the file — the Travels & Deaths card's own name, kept
        // through the World fold so nobody's slot moved, and it left with the card.
        var world = OverlaySections.Retired.Single(r => r.Key == "misc");
        Assert.Equal("World", world.Title);
        Assert.Equal(["Travels & Deaths", "Zone map", "Travel route", "Spawn timers"], world.Answered);
    }

    /// <summary>
    /// A retired key must actually be REMOVED from a profile, not merely dropped from the
    /// catalog: `OptionsViewModel.Cards` looks every key up with `First(...)`, and a key with
    /// no catalog row is exactly what #252 was made of. The migration chain is asked as a
    /// whole (`ApplyMigrations`), because that is the level at which #252 lived.
    /// </summary>
    [Fact]
    public void EveryRetiredKeyIsRemovedFromAProfileByTheMigrationChain()
    {
        var settings = new AppSettings
        {
            SectionOrder = [.. OverlaySections.Retired.Select(r => r.Key), "combat"],
            HiddenSections = [.. OverlaySections.Retired.Select(r => r.Key)],
        };

        settings.ApplyMigrations(hadFile: true);

        foreach (var gone in OverlaySections.Retired)
        {
            Assert.DoesNotContain(gone.Key, settings.SectionOrder);
            Assert.DoesNotContain(gone.Key, settings.HiddenSections);
        }
    }

    /// <summary>
    /// The heading and the blurb are player words. The Evolved shell's rooms are deliberately
    /// unmentioned — `EQBUDDY_SHELL` is the only way into one today, so naming a room here
    /// would send a player looking for something they cannot open, which is the mirror of the
    /// defect this list exists to fix.
    /// </summary>
    [Fact]
    public void TheListPointsOnlyAtDoorsAPlayerActuallyHas()
    {
        Assert.Equal("No longer on the widget", OverlaySections.RetiredHeading);
        Assert.Contains("window", OverlaySections.RetiredBlurb, StringComparison.Ordinal);

        foreach (var gone in OverlaySections.Retired)
            Assert.DoesNotContain("room", gone.Line, StringComparison.OrdinalIgnoreCase);
    }
}
