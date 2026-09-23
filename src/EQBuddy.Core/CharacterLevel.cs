namespace EQBuddy.Core;

/// <summary>Where a character's level came from. The surfaces print this, because
/// "Level 30" means something different depending on whether the game announced it or the
/// player typed it.</summary>
public enum LevelSource
{
    /// <summary>Nothing knows yet — the log has never announced a ding for this character
    /// and nobody has stated one.</summary>
    Unknown,

    /// <summary>The log said so: <c>"You have gained a level! Welcome to level N!"</c>.
    /// The game's own statement, stamped with the moment the line was written.</summary>
    Observed,

    /// <summary>The player told EQBuddy, on the Character room (DRA-71 D3).</summary>
    Stated,
}

/// <summary>One claim about the character's level and WHEN that claim was made.</summary>
/// <param name="Level">The number. Always &gt; 0 — see <see cref="CharacterLevel.Reading"/>,
/// which is the only way to make one and refuses the rest.</param>
/// <param name="At">
/// When the claim was made, in **LOCAL time on this machine**.
///
/// <para><b>THE CLOCK IS THE WHOLE DESIGN AND IT IS EASY TO GET WRONG.</b> An observed
/// reading's stamp is the LOG's timestamp, which the parser reads out of the game's own
/// <c>[Sun Sep 13 12:41:39 2026]</c> prefix and hands over as a local <see cref="DateTime"/>.
/// A stated reading's stamp is the player's wall clock at the moment they pressed the
/// button. <see cref="CharacterLevel.Resolve"/> compares the two directly, so they must be
/// the same clock, and the repo has a sibling that is the other one:
/// <c>QuestLedgerStore.GuideProgress.LastUpdated</c> is deliberately <b>UTC</b> and says so
/// in its own comment, because nothing ever compares it to a log line. Stamping a statement
/// in UTC here would make every statement look up to a day fresher or staler than the ding
/// it is being weighed against, depending on the player's offset — and it would look
/// perfectly correct in the one timezone the author happened to test in.</para>
/// </param>
public readonly record struct LevelReading(int Level, DateTime At);

/// <summary>
/// The character's level, and how we know — the one answer every surface reads.
/// </summary>
/// <param name="Level">The resolved number, or 0 when nothing knows.</param>
/// <param name="Source">Which claim won.</param>
/// <param name="At">When the winning claim was made. <see cref="DateTime.MinValue"/> when
/// unknown, and also when the winner is a level stored before stamps existed — see
/// <see cref="CharacterLevel"/>.</param>
public readonly record struct ResolvedLevel(int Level, LevelSource Source, DateTime At)
{
    // Backing fields rather than positional parameters, so `default(ResolvedLevel)` — which
    // HelperInputs takes as a default argument — stays EQUAL to Unknown: a positional string
    // would be null in `default` and "" in Unknown, two states for one absence.
    private readonly string? _lowestClass;
    private readonly string? _unknownClass;

    /// <summary>Set when the number is the LOWEST of two or more equipped classes' own levels
    /// (DRA-356, DRA-352 D4) — the class that answered. Empty when one class or none was
    /// weighed.</summary>
    public string LowestClass { get => _lowestClass ?? ""; init => _lowestClass = value; }

    /// <summary>Set when an equipped class has no level of its own yet, so the answer FELL
    /// BACK to the character's single remembered pair — the class that has none. Unknown is
    /// never guessed (trap 73); the surface says which class it could not weigh.</summary>
    public string UnknownClass { get => _unknownClass ?? ""; init => _unknownClass = value; }

    /// <summary>Nothing knows yet. The state a brand-new profile is in, and a real answer
    /// rather than an error: a surface draws what it can and says the rest is unknown.</summary>
    public static readonly ResolvedLevel Unknown = new(0, LevelSource.Unknown, DateTime.MinValue);

    /// <summary>Is there a number to reason about at all? Read as a guard by everything
    /// that would otherwise do arithmetic against 0.</summary>
    public bool Known => Level > 0 && Source != LevelSource.Unknown;
}

/// <summary>
/// **ONE LEVEL, TWO WRITERS, ORDERED BY TIME** (DRA-71 D3, plan P3; Founder smoke item 2).
///
/// <para><b>The fresher claim wins, and that is the entire rule.</b> DRA-66 settled the
/// class version of this question as *"a statement never beats the game's own dump"*,
/// because an achievements dump is a snapshot the game wrote and a player who disagrees with
/// it has a stale dump. A level is not that shape. The log announces a level at the ding and
/// never again, so the game's claim is a MOMENT rather than a standing record — and the
/// Founder's own case is the one that proves a statement has to be able to win: a Legends
/// character holds up to three classes at once, and the level the log printed belongs to
/// whatever was equipped when it printed. So the carry-over is *"a statement never beats
/// FRESHER game truth"*: a ding after your statement wins, and your statement after the ding
/// wins.</para>
///
/// <para><b>It does not rank the sources at all, on purpose.</b> A precedence table —
/// "observed beats stated" or the reverse — is the thing that would have to be re-argued
/// every time a new writer arrives, and both versions are wrong half the time here. Time is
/// the only ordering that is a FACT about the two claims rather than an opinion about
/// them.</para>
///
/// <para><b>An unstamped stored level is the oldest thing there is</b>, and that is the
/// correct migration. Profiles written before this slice carry
/// <c>CharacterLedger.Level</c> with no <c>LevelAt</c>, which resolves as
/// <see cref="DateTime.MinValue"/>: it still answers when it is the only claim, and it
/// yields to any statement the player makes afterwards. The alternative — treating an
/// unstamped level as NOW — would have made every existing profile's stored number
/// unbeatable until the next ding.</para>
///
/// <para><b>Per-class levels are UN-PARKED by DRA-356 (DRA-352 D4, Founder-directed
/// 2026-09-23), and the park's premise still stands:</b> no log line and no
/// <c>/outputfile</c> dump carries a level per class. The source is not the game — it is the
/// player's own statement plus the equipped-set join. A ding is written to every class
/// equipped when it printed (<see cref="CharacterClasses.Resolve"/>'s answer), RAISE-ONLY; a
/// statement raises every equipped class below it and LOWERS only the class(es) at the
/// current minimum (or with no memory). The character's level is then the MINIMUM over the
/// equipped classes (<see cref="ResolveEquipped"/>) — the Founder's rule: Warrior 50 with a
/// newly equipped class at 17 is a level-17 character. An equipped class with no memory is
/// never guessed: the answer falls back to the single pair above and says which class it
/// could not weigh.</para>
///
/// <para><b>Known cost, stated rather than hidden:</b> the equipped set is the one EQBuddy
/// holds when it READS the ding, not when the game printed it. A live ding is the same
/// moment; a first-launch replay of an old ding lands on today's classes. Raise-only keeps
/// that from ever lowering a class.</para>
/// </summary>
public static class CharacterLevel
{
    /// <summary>The highest level the Character room's picker offers (DRA-356). Legends' cap
    /// is 60 through the top of today's era ladder (<see cref="QuestEraLadder.Eras"/> ends at
    /// Luclin, a level-60 era; <c>SpellLevelCatalog</c> cites the same cap). A later era that
    /// raises it moves this one number.</summary>
    public const int MaxLevel = 60;

    /// <summary>
    /// Make a reading, or answer null for one there is no claim behind.
    ///
    /// <para>Zero and negative levels are not claims — <c>LevelFor</c> answers 0 for "never
    /// seen" and the editor refuses an unparseable box — so they become null here rather
    /// than travelling on as a number some later comparison would treat as a level.</para>
    /// </summary>
    public static LevelReading? Reading(int level, DateTime at) =>
        level > 0 ? new LevelReading(level, at) : null;

    /// <summary>
    /// The resolved level: the fresher of the two claims.
    /// </summary>
    /// <param name="observed">What the log announced, stamped with the LOG's own time.</param>
    /// <param name="stated">What the player set, stamped with their wall clock. **On an
    /// exact tie the statement wins** — a player who typed a level in the same second the
    /// game announced one was there for both, and the number they typed is the one they
    /// meant. It is written down rather than left to <c>&gt;=</c> because a tie-break nobody
    /// named is a tie-break nobody can check.</param>
    public static ResolvedLevel Resolve(LevelReading? observed, LevelReading? stated)
    {
        if (stated is { } s && observed is { } o)
            return o.At > s.At
                ? new ResolvedLevel(o.Level, LevelSource.Observed, o.At)
                : new ResolvedLevel(s.Level, LevelSource.Stated, s.At);
        if (stated is { } only) return new ResolvedLevel(only.Level, LevelSource.Stated, only.At);
        if (observed is { } log) return new ResolvedLevel(log.Level, LevelSource.Observed, log.At);
        return ResolvedLevel.Unknown;
    }

    /// <summary>
    /// **The character's level over its equipped classes** (DRA-356, DRA-352 D4): the
    /// MINIMUM of each equipped class's own fresher-wins answer.
    /// </summary>
    /// <param name="equipped">The classes equipped now, in <see cref="CharacterClasses.Resolve"/>'s
    /// order. Null or empty ⇒ the single pair answers, exactly as before per-class memory
    /// existed.</param>
    /// <param name="perClass">Each class's own observed/stated pair. A class absent here, or
    /// present with neither reading, has no memory.</param>
    /// <param name="observed">The character's single observed reading (the legacy pair).</param>
    /// <param name="stated">The character's single stated reading (the legacy pair).</param>
    public static ResolvedLevel ResolveEquipped(
        IReadOnlyList<string>? equipped,
        Func<string, (LevelReading? Observed, LevelReading? Stated)> perClass,
        LevelReading? observed, LevelReading? stated)
    {
        var single = Resolve(observed, stated);
        if (equipped is null || equipped.Count == 0) return single;

        ResolvedLevel? lowest = null;
        var lowestClass = "";
        foreach (var cls in equipped)
        {
            var (o, s) = perClass(cls);
            var mine = Resolve(o, s);
            // The minimum is UNKNOWN when any equipped class has no level of its own — a
            // class nothing has spoken for could be level 1. Fall back to the one pair the
            // character has always had, and name the class (trap 73: never guessed).
            if (!mine.Known) return single with { UnknownClass = cls };
            if (lowest is null || mine.Level < lowest.Value.Level)
            {
                lowest = mine;
                lowestClass = cls;
            }
        }
        return equipped.Count > 1 ? lowest!.Value with { LowestClass = lowestClass } : lowest!.Value;
    }

    /// <summary>
    /// How a surface says WHICH classes the number stands for — the class half of the level's
    /// source, one table beside <see cref="SourceLabel"/> for the same ruling. Empty when the
    /// number is the character's single pair with nothing to add.
    /// </summary>
    public static string BasisLabel(ResolvedLevel level) =>
        level.LowestClass.Length > 0
            ? $"the lowest of your equipped classes ({level.LowestClass})"
            : level.UnknownClass.Length > 0
                ? $"no level known yet for {level.UnknownClass}"
                : "";

    /// <summary>
    /// How a surface says where the level came from. **ONE TABLE**, the same rule
    /// <see cref="CharacterClasses.SourceLabel"/> carries — Bevel, Helm-signed 2026-08-23:
    /// *"SourceLabel is one table in Core. Do not grow a phone-only string"*, and *"the phone
    /// must not compose a second verb around SourceLabel."*
    ///
    /// <para>It names a SOURCE and nothing else: no verb, no instruction, no "— set it
    /// yourself to correct this". The two read in parallel ("from your …" / "set by …")
    /// because the job is telling a fact from a statement at a glance.</para>
    /// </summary>
    public static string SourceLabel(LevelSource source) => source switch
    {
        LevelSource.Observed => "from your log's ding lines",
        // The same three words the class line uses for the same fact, deliberately: a
        // player who has stated both should read one voice, not two.
        LevelSource.Stated => "set by you",
        _ => "",
    };
}
