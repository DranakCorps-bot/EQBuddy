using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// What a <c>/who</c> says about the character typing it: its level and its EQUIPPED classes,
/// in the game's own words. Verbatim from the Founder's log (2026-09-30):
///
/// <code>
/// Players in EverQuest Legends:
/// ---------------------------
/// [45 SHD/MNK/NEC] Gamed (Iksar) &lt;Debeo Amicitia&gt; ZONE: Nagafen's Lair (soldungb)
/// [ANONYMOUS] Qari
/// [50 WAR/DRU/MNK] Dranak (Ancient Wolf) &lt;Ascendancy&gt; ZONE: Nagafen's Lair (soldungb)
/// There are 9 players in EverQuest Legends.
/// </code>
///
/// <para><b>This is the one line in the log that states the ROSTER.</b> The achievements dump
/// states an unlock history (<see cref="CharacterClasses"/>), inference is a guess, and until
/// this the only roster source was the player's own statement. The level is the LOWEST of the
/// equipped classes (Founder, 2026-09-30) — the same rule <see cref="CharacterLevel.ResolveEquipped"/>
/// answers with, measured in the log: on Sep 11 all three of WAR/DRU/MNK read 50, and the
/// 23–27 dings that followed were a different equipped set.</para>
///
/// <para><b>Refuse rather than guess.</b> Every class code must resolve through
/// <see cref="QuestClassFilter.Canonical"/> or the whole row is dropped — a title
/// (<c>[50 Warlord]</c>) or a code nobody has met is not a class list we can half-read.
/// <c>/anon</c> and <c>/role</c> hide both halves (<c>[ANONYMOUS] Name</c>), so those rows say
/// nothing and parse to nothing.</para>
///
/// Its own file rather than one more regex in <see cref="LogParser"/>, which is a ratchet-watched
/// hotspot (the <see cref="TradeLines"/> precedent). Gated on a literal <c>[</c> + digit before
/// the regex runs.
/// </summary>
public static partial class WhoLines
{
    // Name is one word of letters (EQ names have no spaces); what follows it — race, guild,
    // zone, LFG — is ignored, and a row that ends at the name is admitted too.
    [GeneratedRegex(@"^\[(?<level>\d{1,3}) (?<classes>[A-Za-z ]+(?:/[A-Za-z ]+){0,2})\] (?<name>[A-Za-z]+)(?:\s|$)")]
    private static partial Regex RowRx();

    // Any row of the listing, readable or not: a class row, a title row, an /anon row. Every
    // one of them names a player, so LogWatcher hands it to nothing but the WhoTracker.
    [GeneratedRegex(@"^\[(?:ANONYMOUS|\d{1,3} [A-Za-z ]+(?:/[A-Za-z ]+)*)\] [A-Za-z]+(?:\s|$)")]
    private static partial Regex AnyRowRx();

    /// <summary>True for every row of a /who listing, including the ones <see cref="Parse"/>
    /// refuses (<c>[ANONYMOUS] Qari</c>, <c>[50 Warlord] Name</c>). The header and footer are
    /// not rows — they name nobody.</summary>
    public static bool IsListingRow(string msg) =>
        msg.Length >= 5 && msg[0] == '[' && AnyRowRx().IsMatch(msg);

    /// <summary>The /who row this line is, or null.</summary>
    public static WhoEntryEvent? Parse(DateTime ts, string msg)
    {
        if (msg.Length < 5 || msg[0] != '[' || !char.IsAsciiDigit(msg[1])) return null;
        var r = RowRx().Match(msg);
        if (!r.Success || !int.TryParse(r.Groups["level"].Value, out var level) || level <= 0) return null;
        var classes = new List<string>(CharacterClasses.Max);
        foreach (var code in r.Groups["classes"].Value.Split('/'))
        {
            var cls = QuestClassFilter.Canonical(code.Trim());
            if (cls.Length == 0) return null;
            if (!classes.Contains(cls, StringComparer.OrdinalIgnoreCase)) classes.Add(cls);
        }
        return new WhoEntryEvent(ts, r.Groups["name"].Value, level, classes);
    }
}

/// <summary>The watched character's own /who row: its level (the lowest of its equipped
/// classes) and those classes, stamped with the LOG's time — the same clock
/// <see cref="LevelReading.At"/> uses, because it is weighed against the player's statements.</summary>
public sealed record WhoReading(string Name, int Level, IReadOnlyList<string> Classes, DateTime At);

/// <summary>
/// Keeps the watched character's newest own /who row, and nothing else. Fed every event by
/// <see cref="LogWatcher"/> with the character it is tailing; a row naming anyone else is
/// dropped on arrival (EQBuddy never measures other players). Its own consumer rather than
/// state in <see cref="SessionStats"/> — the row is about the CHARACTER, not the session, and
/// that file is a ratchet-watched hotspot.
/// </summary>
public sealed class WhoTracker
{
    private readonly object _lock = new();
    private WhoReading? _latest;

    public void Observe(GameEvent e, string? character)
    {
        if (e is not WhoEntryEvent w || character is not { Length: > 0 }
            || !string.Equals(w.Name, character, StringComparison.OrdinalIgnoreCase)) return;
        lock (_lock) _latest = new WhoReading(w.Name, w.Level, w.Classes, w.Time);
    }

    /// <summary>The newest own row for <paramref name="character"/>, or null — a row kept for
    /// the previous character after a switch never answers for the next one.</summary>
    public WhoReading? LatestFor(string? character)
    {
        lock (_lock)
            return _latest is { } r && string.Equals(r.Name, character, StringComparison.OrdinalIgnoreCase) ? r : null;
    }
}
