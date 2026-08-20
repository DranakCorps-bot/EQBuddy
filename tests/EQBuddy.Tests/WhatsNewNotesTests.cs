using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The MOVED badge (David, 2026-08-20: *"please explicitly note, maybe in a different
/// color, when things move from accessing one way to another"*).
///
/// The popup is the only place a near-silent auto-update announces itself, and a
/// relocation is the one note a player cannot afford to skim: the alternative to reading
/// it is going to look for a card, not finding it, and concluding EQBuddy deleted the
/// feature. That is #219 exactly.
/// </summary>
public sealed class WhatsNewNotesTests
{
    [Fact]
    public void AnUnmarkedNoteIsAnOrdinaryChange()
    {
        var note = WhatsNewNotes.Parse("Spawn timers no longer learn a respawn you did not watch.");

        Assert.Equal(WhatsNewKind.Change, note.Kind);
        Assert.Equal("", note.Label);
        Assert.Equal("Spawn timers no longer learn a respawn you did not watch.", note.Text);
    }

    [Fact]
    public void TheMarkerIsStrippedSoNoUiEverDrawsIt()
    {
        var note = WhatsNewNotes.Parse("MOVED: the Loot card is a tab in Gear & Loot now.");

        Assert.Equal(WhatsNewKind.Moved, note.Kind);
        Assert.Equal("MOVED", note.Label);
        // The badge IS the marker. Leaving it in the sentence too would render as
        // "MOVED  MOVED: the Loot card…".
        Assert.Equal("the Loot card is a tab in Gear & Loot now.", note.Text);
    }

    /// <summary>A marker with no sentence after it is a typo, not a move — drawn as an
    /// ordinary note rather than a badge pointing at nothing.</summary>
    [Fact]
    public void AMarkerWithNothingBehindItIsNotAMove()
    {
        Assert.Equal(WhatsNewKind.Change, WhatsNewNotes.Parse("MOVED:").Kind);
        Assert.Equal(WhatsNewKind.Change, WhatsNewNotes.Parse("MOVED:   ").Kind);
        Assert.Equal(WhatsNewKind.Change, WhatsNewNotes.Parse(null).Kind);
    }

    /// <summary>Every note ever written before the marker existed still reads correctly.
    /// The marker is a string prefix rather than a JSON field precisely so that a schema
    /// change never has to be migrated across the whole history — this is that claim,
    /// checked against the shipped file rather than asserted.</summary>
    [Fact]
    public void EveryShippedNoteParsesAndOnlyDeliberateOnesAreMoves()
    {
        var all = WhatsNewCatalog.Load();
        Assert.NotEmpty(all);

        foreach (var entry in all)
            foreach (var note in WhatsNewNotes.ParseAll(entry))
                Assert.False(string.IsNullOrWhiteSpace(note.Text),
                    $"EQBuddy {entry.Version} has a highlight that parses to nothing.");

        // The badge only keeps its force while it means one thing, so this is a deliberate
        // count rather than "some exist": a release that starts marking ordinary changes as
        // moves fails here and gets read again.
        var moves = all.SelectMany(WhatsNewNotes.ParseAll)
            .Where(n => n.Kind == WhatsNewKind.Moved)
            .ToList();
        Assert.Equal(3, moves.Count);
        Assert.All(moves, m => Assert.DoesNotContain(WhatsNewNotes.MovedMarker, m.Text,
            StringComparison.OrdinalIgnoreCase));
    }
}
