using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// What the buff-set surfaces (the ⏳ Buffs breakout, and the Options editor) need
/// from the app shell. Member names mirror the WPF MainWindow surface one-for-one,
/// so wiring is "MainWindow implements IBuffSetHost" — same idiom as IZoneHost.
///
/// WPF reaches all of this through a plain MainWindow reference. That works there
/// because both windows live in one assembly with one owner; here the breakout has
/// deliberately taken its dependencies as hooks. Twelve hooks would be noise, so
/// this is the interface those hooks collapse into.
/// </summary>
public interface IBuffSetHost
{
    AppSettings Settings { get; }
    StatsSnapshot CurrentSnapshot();

    /// <summary>Per-character storage key; empty until today's log names the character,
    /// which is the one state every buff-set surface has to degrade honestly for.</summary>
    string BuffSetKey { get; }
    string BuffSetCharacterName { get; }

    /// <summary>The active class combination, and whether it was picked or inferred —
    /// every surface that shows the combination says which source it came from.</summary>
    (IReadOnlyList<string> Classes, bool Picked) BuffSetClassSource(StatsSnapshot s);

    /// <summary>The same combination with its REAL source rather than a picked/not
    /// boolean — the dump, the log or the picks (<see cref="CharacterClasses.Resolve"/>).
    /// A surface that prints where the classes came from needs the three-way answer: with
    /// only a boolean it has to guess "inferred" for everything that is not a pick, and a
    /// dump-sourced trio then reads as a guess (Bevel, Helm-signed 2026-08-23).</summary>
    (IReadOnlyList<string> Classes, ClassSource Source) ClassSourceFor(StatsSnapshot s);

    List<string> AssembledBuffSet(IReadOnlyList<string> classes);
    List<(string Class, List<BuffSetEntryState> Entries)> BuffSetSectionStates(StatsSnapshot s, DateTime now);
    List<BuffSuggestion> BuffSuggestionsFor(StatsSnapshot s, List<string> assembled);
    BuffLossLog BuffLosses { get; }
    IReadOnlyCollection<string> SeenBuffCasts();

    void AcceptBuffSuggestion(BuffSuggestion sug);
    void DismissBuffSuggestion(BuffSuggestion sug);

    /// <summary>An edit landed: repaint the card, Options, and the breakout at once.
    /// A change that waits for the next tick reads as a silent no-op.</summary>
    void OnBuffSetEdited();
}
