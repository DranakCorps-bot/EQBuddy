using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// The one place the length of a buff is turned into a countdown.
///
/// Two facts, and they belong together because a duration that skips either one is a
/// number no server ever counted:
///
///  1. <b>Spell Casting Reinforcement</b> lengthens beneficial spells YOU cast, by the
///     rank the character actually owns. It is applied ONCE, to a base length — never to
///     a length that already includes it.
///  2. <b>The server counts ticks, not seconds.</b> Six-second ticks, the same
///     <see cref="BuffTracker.ServerTickSeconds"/> fade-learn already floors an observation
///     to. A stretched length lands mid-tick far more often than not, and the tick you are
///     standing in has not happened yet — so it floors.
///
/// Both were needed to reproduce the owner's own measurements: Shield of Thorns V at
/// 1,350 s base with SCR rank 1 is 1,417.5 s, and what he measured is 23:36 = 1,416 s.
/// Rounding up, or skipping the floor, misses it by a tick and a half.
/// </summary>
public static class BuffDurationModel
{
    /// <summary>Spell Casting Reinforcement, ranks 0-4: +5/15/30/50% on beneficial spells
    /// YOU cast. Index 0 is "no rank" and is deliberately 0 - a character with no SCR gets
    /// the base length back unchanged, including the tick floor, because a base length is
    /// already a whole number of ticks.</summary>
    public static readonly double[] ReinforcementBonus = [0, 0.05, 0.15, 0.30, 0.50];

    public const int MaxReinforcementRank = 4;

    /// <summary>
    /// <paramref name="baseSeconds"/> stretched by <paramref name="rank"/> ranks of Spell
    /// Casting Reinforcement and floored to a whole server tick.
    ///
    /// A rank outside 0-4 returns the base untouched: the AA ledger reports 0 for an
    /// ability it has never seen, and a rank we do not have a number for is not a licence
    /// to guess one (Helm, 2026-09-07: no invented per-rank multipliers).
    /// </summary>
    public static double WithReinforcement(double baseSeconds, int rank)
    {
        if (rank is <= 0 or > MaxReinforcementRank) return baseSeconds;
        var stretched = baseSeconds * (1 + ReinforcementBonus[rank]);
        return Math.Floor(stretched / BuffTracker.ServerTickSeconds) * BuffTracker.ServerTickSeconds;
    }
}

/// <summary>
/// MEASURED durations for spells the log names WITH their rank - "Shield of Thorns V",
/// not "Shield of Thorns".
///
/// <para><b>Why this exists.</b> <see cref="BuffDurationCatalog"/> is harvested from
/// eqlwiki, and an eqlwiki spell page carries ONE duration: the unranked/rank-I number
/// (Shield of Thorns, 15 Min). <see cref="SpellCatalog.BaseName"/> then folds every rank
/// onto that one row, which is right for identity - only one Shield of Thorns can be up on
/// you - and wrong for length, because ranks lengthen buffs. A level-50 Druid casting
/// Shield of Thorns V got a 15-minute countdown for a 23:36 damage shield, so every
/// surface armed off that countdown - the HUD's expiring chicklet, the Buffs card's warn
/// tint, expiring-only mode - fired nearly eight minutes early.</para>
///
/// <para><b>What may go in it.</b> A row is a MEASUREMENT, never a derivation. There is no
/// per-rank multiplier here and there is not going to be one: the two rows below sit at
/// +50% and +25% over their wiki bases, so any formula that fits one misses the other.
/// Helm's standing posture (2026-09-07, #414) is "do not invent ranked durations / mote
/// multipliers", and the shape of this file is how that posture is kept honest rather than
/// merely remembered - every row carries the observation it came from, and
/// <c>RankedBuffDurationTests</c> refuses a row whose <c>baseSeconds</c> does not reproduce
/// its own <c>observedSeconds</c> through <see cref="BuffDurationModel.WithReinforcement"/>
/// at the rank it was measured at.</para>
///
/// <para><b>Curated, never auto-written.</b> The weekly wiki refresh may flag this file; it
/// may not write it. A wrong respawn timer is worse than none and so is a wrong duration -
/// this one silently mis-times an alert instead of showing nothing.</para>
///
/// <para><b>The stored number is the PRE-AA base.</b> The tracker applies the reading
/// character's own SCR rank on top, so a player with rank 3 gets rank 3's length rather
/// than the measurer's rank 1. Storing the observed total instead would bake one
/// character's AAs into everyone's countdown.</para>
/// </summary>
public sealed class RankedBuffDurationLedger
{
    /// <summary>Where a row's number came from. Required: a row with no measurement is a
    /// guess wearing a catalog's clothes, and the guard test says so.</summary>
    public sealed class Measurement
    {
        public string By { get; set; } = "";
        public string On { get; set; } = "";
        public string Character { get; set; } = "";
        /// <summary>The length actually observed, INCLUDING the measurer's SCR.</summary>
        public double ObservedSeconds { get; set; }
        /// <summary>The SCR rank the measurer held when they observed it.</summary>
        public int ReinforcementRank { get; set; }
    }

    public sealed class Entry
    {
        /// <summary>The EXACT ranked name the log writes, e.g. "Shield of Thorns V".</summary>
        public string Name { get; set; } = "";
        /// <summary>Length before any Spell Casting Reinforcement.</summary>
        public double BaseSeconds { get; set; }
        public Measurement? Measured { get; set; }
    }

    private sealed class Root
    {
        public string Note { get; set; } = "";
        public List<Entry> Spells { get; set; } = [];
    }

    private readonly Dictionary<string, Entry> _byName;

    public RankedBuffDurationLedger(IEnumerable<Entry> entries) =>
        _byName = entries.Where(e => e.Name.Length > 0 && e.BaseSeconds > 0)
            .ToDictionary(e => e.Name.Trim(), e => e, StringComparer.OrdinalIgnoreCase);

    public int Count => _byName.Count;
    public IReadOnlyCollection<Entry> Entries => _byName.Values;

    /// <summary>The measured pre-AA length for this EXACT ranked name, or null.
    ///
    /// **Exact, deliberately.** Folding the rank away here would hand rank V's number to a
    /// rank II cast, which is the same defect this file exists to fix, pointing the other
    /// way. A rank nobody has measured falls back to the wiki base, as it always did.</summary>
    public double? BaseSeconds(string rankedSpell) =>
        rankedSpell is { Length: > 0 } && _byName.TryGetValue(rankedSpell.Trim(), out var e)
            ? e.BaseSeconds
            : null;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static RankedBuffDurationLedger LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.RankedBuffDurations.json")
            ?? throw new InvalidOperationException("RankedBuffDurations.json missing from resources");
        var root = JsonSerializer.Deserialize<Root>(stream, JsonOpts)
            ?? throw new InvalidOperationException("RankedBuffDurations.json unreadable");
        return new RankedBuffDurationLedger(root.Spells);
    }

    public static RankedBuffDurationLedger Default { get; } = LoadEmbedded();
}
