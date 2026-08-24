namespace EQBuddy.Core;

/// <summary>Where a character's class list came from, worst evidence last. The surfaces
/// print this, because "Warrior · Druid · Monk" means something different depending on
/// whether the game said it or a heuristic guessed it.</summary>
public enum ClassSource
{
    /// <summary>Nothing knows yet — no dump, no qualifying evidence, no picks.</summary>
    Unknown,

    /// <summary>The character's own achievements dump named them. The game's statement.</summary>
    Achievements,

    /// <summary>Read from what the log shows being cast and used.</summary>
    Inferred,

    /// <summary>Only the Quest Tracker's picks had anything to say.</summary>
    Picked,
}

/// <summary>
/// The character's classes, and how we know.
///
/// **The premise this exists to fix.** EQBuddy resolved "which class is this" to a single
/// string, or `""` when two were close — and a Legends character is up to THREE classes at
/// once (David, 2026-08-23: *"you seem to think EQ Legends just lets you have 1 class when
/// in fact you can be 3 at a time"*). Every class-aware surface read that one string: the
/// Quest Tracker's filters, the Gear Locker (#104), the Sky class lens, the next-level
/// unlock list, EQBuddy Mobile. The premise underneath all of them was wrong, which is why
/// this is Core and pure rather than a patch at each seam.
///
/// **Precedence, and the reason for it.** The achievements dump names every class the
/// character holds as a fact the GAME wrote — better evidence than any log heuristic can
/// be, and it has been arriving by itself since 1.98.1's auto-import. Inference is the
/// fallback for players who have never dumped. The Quest Tracker's picks are LAST and are
/// a LENS: #104 established that a player may widen them to help a friend, so picks may
/// add classes but must never be the thing that tells the app what the character IS.
/// Bevel's lock — *"inferred classes in play; never fall back to the Quest Tracker
/// filter"* — becomes satisfiable here for the first time, and is honoured.
///
/// **The dump is a SNAPSHOT, so it unions rather than silences.** A class unlocked after
/// the last dump would otherwise be invisible until the next one, while the log is
/// plainly showing it. So a qualifying inferred class joins a dump-sourced list rather
/// than being suppressed by it — dump entries first, because they are the certain half.
/// </summary>
public static class CharacterClasses
{
    /// <summary>The most classes a character can hold — <see cref="ClassInference.MaxClasses"/>,
    /// which carries the wiki citation. Named through here so a surface reasoning about the
    /// cap does not have to reach into the inference engine for a game fact.</summary>
    public const int Max = ClassInference.MaxClasses;

    /// <param name="unlocked">Complete class unlocks from the achievements dump, primary
    /// first. The game's own statement.</param>
    /// <param name="inferred">Qualifying classes from the log, heaviest first
    /// (<see cref="ClassInference.CurrentClasses"/>).</param>
    /// <param name="picks">The Quest Tracker's picked classes — a lens that may WIDEN the
    /// answer and may never narrow it.</param>
    public static (IReadOnlyList<string> Classes, ClassSource Source) Resolve(
        IReadOnlyList<string>? unlocked,
        IReadOnlyList<string>? inferred,
        IReadOnlyList<string>? picks)
    {
        var classes = new List<string>(Max);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(IReadOnlyList<string>? from)
        {
            if (from is null) return;
            foreach (var cls in from)
            {
                if (classes.Count >= Max) return;
                if (cls is { Length: > 0 } && seen.Add(cls)) classes.Add(cls);
            }
        }

        // The dump leads and the log fills in behind it — see the class note on why a
        // snapshot must not silence live evidence.
        Add(unlocked);
        var source = classes.Count > 0 ? ClassSource.Achievements : ClassSource.Unknown;
        Add(inferred);
        if (source == ClassSource.Unknown && classes.Count > 0) source = ClassSource.Inferred;

        // Picks widen. They also answer alone for a player who has never dumped and whose
        // log shows nothing yet — a brand new session, which is the common case at launch.
        Add(picks);
        if (source == ClassSource.Unknown && classes.Count > 0) source = ClassSource.Picked;

        return (classes, source);
    }

    /// <summary>
    /// How a surface says where the list came from. **ONE table** — Bevel, Helm-signed
    /// 2026-08-23: *"SourceLabel is one table in Core. Do not grow a phone-only string"*,
    /// and *"the phone must not compose a second verb around SourceLabel."*
    ///
    /// The three read in parallel on purpose ("from your …" / "inferred from your …" /
    /// "from your …") because the job is telling a FACT from a GUESS at a glance, and
    /// "inferred" is the word carrying that difference. The third was "your picks" until
    /// Bevel parallelized it.
    ///
    /// **It names a source and nothing else.** No verb, no instruction, no "— pick classes
    /// to override": this labels who the character IS, and the picker is a lens over that
    /// (#104), not a replacement for it. A parenthetical that tells the player to override
    /// their own identity is the #104 error in miniature.
    /// </summary>
    public static string SourceLabel(ClassSource source) => source switch
    {
        ClassSource.Achievements => "from your achievements",
        ClassSource.Inferred => "inferred from your log",
        ClassSource.Picked => "from your picks",
        _ => "",
    };
}
