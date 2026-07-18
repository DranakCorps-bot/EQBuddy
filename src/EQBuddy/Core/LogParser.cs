using System.Globalization;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>Parses raw EverQuest Legends log lines into typed game events.</summary>
public static partial class LogParser
{
    // [Fri Jul 17 18:41:14 2026] message
    [GeneratedRegex(@"^\[(?<ts>[A-Za-z]{3} [A-Za-z]{3} +\d{1,2} \d{2}:\d{2}:\d{2} \d{4})\] (?<msg>.*)$")]
    private static partial Regex LineRx();

    [GeneratedRegex(@"^You have slain (?<target>.+)!$")]
    private static partial Regex YouSlainRx();

    [GeneratedRegex(@"^(?<target>.+) has been slain by (?<killer>.+)!$")]
    private static partial Regex OtherSlainRx();

    [GeneratedRegex(@"^You have been slain by (?<killer>.+)!$")]
    private static partial Regex YouDiedRx();

    // You slash orc pawn for 10 points of damage. (Critical)
    [GeneratedRegex(@"^You (?<verb>slash|hit|kick|bash|pierce|crush|punch|backstab|bite|claw|maul|gore|sting|strike|slice|cleave|smash|rend|slam|frenzy on|frenzies on) (?<target>.+?) for (?<dmg>\d+) points? of damage\.(?<crit> \(Critical\))?$")]
    private static partial Regex MeleeOutRx();

    // You try to slash orc pawn, but miss! / but orc pawn dodges! etc.
    [GeneratedRegex(@"^You try to (?<verb>\w+)(?: on)? (?<target>.+?), but (?<reason>.+)!$")]
    private static partial Regex MeleeMissRx();

    // Orc centurion has taken 10 damage from your Poison Bolt.
    [GeneratedRegex(@"^(?<target>.+?) has taken (?<dmg>\d+) damage from your (?<spell>.+?)\.(?<crit> \(Critical\))?$")]
    private static partial Regex DotOutRx();

    // You hit orc pawn for 20 points of non-melee damage.
    [GeneratedRegex(@"^You hit (?<target>.+?) for (?<dmg>\d+) points? of non-melee damage\.(?<crit> \(Critical\))?$")]
    private static partial Regex NukeOutRx();

    // Orc centurion hits YOU for 4 points of damage.
    [GeneratedRegex(@"^(?<attacker>.+?) (?<verb>hits|slashes|kicks|bashes|pierces|crushes|punches|backstabs|bites|claws|mauls|gores|stings|strikes|slices|cleaves|smashes|rends|slams|frenzies on) YOU for (?<dmg>\d+) points? of damage\.$")]
    private static partial Regex MeleeInRx();

    // Orc centurion tries to hit YOU, but misses! / but YOU dodge!
    [GeneratedRegex(@"^(?<attacker>.+?) tries to (?:\w+)(?: on)? YOU, but (?<reason>.+)!$")]
    private static partial Regex MeleeInMissRx();

    // YOU are burned by orc centurion's flames for 6 points of non-melee damage!
    [GeneratedRegex(@"^YOU are (?<how>.+?) for (?<dmg>\d+) points? of non-melee damage!$")]
    private static partial Regex NonMeleeInRx();

    // You healed Kaybek for 10 hit points by Lifespike. / You healed Kaybek for 7 (10) hit points by Lifespike.
    [GeneratedRegex(@"^You healed (?<target>.+?) for (?<amount>\d+)(?: \((?<attempted>\d+)\))? hit points(?: by (?<spell>.+?))?\.$")]
    private static partial Regex HealOutRx();

    // You have been healed for 30 hit points. / Someone healed you...
    [GeneratedRegex(@"^You have been healed for (?<amount>\d+) (?:hit )?points?(?: of damage)?\.?$")]
    private static partial Regex HealInRx();

    // --You have looted a Mote of Infinitesimal Potential from orc centurion's corpse.--
    [GeneratedRegex(@"^--You have looted an? (?<item>.+?) from (?<source>.+?)'s corpse\.--$")]
    private static partial Regex LootRx();

    // You looted a Crushbone Belt +2 from orc centurion's corpse to create a Crushbone Belt +5
    [GeneratedRegex(@"^You looted an? (?<item>.+?) from (?<source>.+?)'s corpse to create an? (?<result>.+?)\.?$")]
    private static partial Regex LootUpgradeRx();

    // You receive 2 silver and 2 copper from the corpse. | ... as your split.
    [GeneratedRegex(@"^You receive (?<coins>.+?) (?:from the corpse|as your split)\.$")]
    private static partial Regex MoneyRx();

    [GeneratedRegex(@"(?<n>\d+) (?<unit>platinum|gold|silver|copper)")]
    private static partial Regex CoinPartRx();

    // You gain party experience! (0.019%)  |  You gain experience! (0.5%)
    [GeneratedRegex(@"^You gain (?<party>party )?experience!(?: \((?<pct>[\d.]+)%\))?$")]
    private static partial Regex XpRx();

    [GeneratedRegex(@"^You have gained a level! Welcome to level (?<level>\d+)!$")]
    private static partial Regex LevelRx();

    [GeneratedRegex(@"^You have become better at (?<skill>.+?)! \((?<value>\d+)\)$")]
    private static partial Regex SkillUpRx();

    [GeneratedRegex(@"^Your faction standing with (?<faction>.+?) has been adjusted by (?<delta>-?\d+)\.$")]
    private static partial Regex FactionRx();

    [GeneratedRegex(@"^You have entered (?<zone>.+)\.$")]
    private static partial Regex ZoneRx();

    // You have successfully merged two items together to create a new item: Crushbone Belt +5
    [GeneratedRegex(@"^You have successfully merged two items together to create a new item: (?<item>.+?)\.?$")]
    private static partial Regex MergeRx();

    [GeneratedRegex(@"^Your (?<spell>.+?) spell fizzles!$")]
    private static partial Regex FizzleRx();

    // Third-party combat (group members, the player's pet, nearby fights):
    // "Orc centurion hits Lizzid for 4 points of damage." / "Lizzid tries to frenzy on orc centurion, but misses!"
    // "Orc centurion has taken 1 damage from Disease Cloud by Lizzid."
    [GeneratedRegex(@"^(?<attacker>.+?) (?:hits|slashes|kicks|bashes|pierces|crushes|punches|backstabs|bites|claws|mauls|gores|stings|strikes|slices|cleaves|smashes|rends|slams|frenzies on) (?<target>.+?) for (?<dmg>\d+) points? of damage\.$")]
    private static partial Regex ThirdMeleeRx();

    [GeneratedRegex(@"^(?<attacker>.+?) tries to \w+(?: on)? .+?, but .+!$")]
    private static partial Regex ThirdMissRx();

    [GeneratedRegex(@"^(?<target>.+?) has taken (?<dmg>\d+) damage from (?<spell>.+?) by (?<caster>.+?)\.$")]
    private static partial Regex ThirdDotRx();

    // Jibekn told you, 'Attacking orc centurion Master.'
    [GeneratedRegex(@"^(?<pet>\S+) (?:tells|told) you, 'Attacking .+ Master\.'$")]
    private static partial Regex PetClaimRx();

    [GeneratedRegex(@"^Your target resisted the (?<spell>.+?) spell\.$")]
    private static partial Regex ResistRx();

    private const string TsFormat = "ddd MMM d HH:mm:ss yyyy";

    public static bool TryParseTimestamp(string line, out DateTime ts)
    {
        ts = default;
        var m = LineRx().Match(line);
        if (!m.Success) return false;
        var raw = Regex.Replace(m.Groups["ts"].Value, @" {2,}", " ");
        return DateTime.TryParseExact(raw, TsFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out ts);
    }

    /// <summary>Parse one full log line. Returns null for lines we don't track.</summary>
    public static GameEvent? Parse(string line)
    {
        var m = LineRx().Match(line);
        if (!m.Success) return null;
        var rawTs = Regex.Replace(m.Groups["ts"].Value, @" {2,}", " ");
        if (!DateTime.TryParseExact(rawTs, TsFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var ts))
            return null;
        var msg = m.Groups["msg"].Value;

        Match r;

        if ((r = YouDiedRx().Match(msg)).Success)
            return new DeathEvent(ts, r.Groups["killer"].Value);

        if ((r = YouSlainRx().Match(msg)).Success)
            return new KillEvent(ts, Normalize(r.Groups["target"].Value), "You");

        if ((r = OtherSlainRx().Match(msg)).Success)
            return new KillEvent(ts, Normalize(r.Groups["target"].Value), r.Groups["killer"].Value);

        if ((r = MeleeInRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, Normalize(r.Groups["attacker"].Value),
                int.Parse(r.Groups["dmg"].Value), Melee: true);

        if ((r = NonMeleeInRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, Normalize(r.Groups["how"].Value),
                int.Parse(r.Groups["dmg"].Value), Melee: false);

        if ((r = NukeOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell, "Direct spell",
                r.Groups["crit"].Success);

        if ((r = MeleeOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Melee,
                VerbToSkill(r.Groups["verb"].Value), r.Groups["crit"].Success);

        if ((r = DotOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell,
                r.Groups["spell"].Value, r.Groups["crit"].Success);

        if ((r = MeleeMissRx().Match(msg)).Success)
            return new MissEvent(ts, Outgoing: true);

        if ((r = MeleeInMissRx().Match(msg)).Success)
            return new MissEvent(ts, Outgoing: false);

        if ((r = HealOutRx().Match(msg)).Success)
            return new HealEvent(ts, r.Groups["target"].Value,
                int.Parse(r.Groups["amount"].Value),
                r.Groups["spell"].Success ? r.Groups["spell"].Value : "Unknown", Outgoing: true);

        if ((r = HealInRx().Match(msg)).Success)
            return new HealEvent(ts, "You", int.Parse(r.Groups["amount"].Value), "Unknown", Outgoing: false);

        if ((r = LootUpgradeRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, Normalize(r.Groups["source"].Value),
                r.Groups["result"].Value);

        if ((r = LootRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, Normalize(r.Groups["source"].Value), null);

        if ((r = MoneyRx().Match(msg)).Success)
        {
            long copper = 0;
            foreach (Match c in CoinPartRx().Matches(r.Groups["coins"].Value))
            {
                long n = long.Parse(c.Groups["n"].Value);
                copper += c.Groups["unit"].Value switch
                {
                    "platinum" => n * 1000,
                    "gold" => n * 100,
                    "silver" => n * 10,
                    _ => n,
                };
            }
            if (copper > 0) return new MoneyEvent(ts, copper);
        }

        if ((r = XpRx().Match(msg)).Success)
            return new XpEvent(ts,
                r.Groups["pct"].Success ? double.Parse(r.Groups["pct"].Value, CultureInfo.InvariantCulture) : 0,
                r.Groups["party"].Success);

        if ((r = LevelRx().Match(msg)).Success)
            return new LevelEvent(ts, int.Parse(r.Groups["level"].Value));

        if ((r = SkillUpRx().Match(msg)).Success)
            return new SkillUpEvent(ts, r.Groups["skill"].Value, int.Parse(r.Groups["value"].Value));

        if ((r = FactionRx().Match(msg)).Success)
            return new FactionEvent(ts, r.Groups["faction"].Value, int.Parse(r.Groups["delta"].Value));

        if ((r = MergeRx().Match(msg)).Success)
            return new CraftEvent(ts, r.Groups["item"].Value);

        if ((r = FizzleRx().Match(msg)).Success)
            return new FizzleEvent(ts);

        if ((r = ResistRx().Match(msg)).Success)
            return new ResistEvent(ts);

        // Pet announcement and third-party combat (checked last — specific patterns above win).
        if ((r = PetClaimRx().Match(msg)).Success)
            return new PetClaimEvent(ts, r.Groups["pet"].Value);

        if ((r = ThirdMeleeRx().Match(msg)).Success)
            return new ThirdMeleeEvent(ts, r.Groups["attacker"].Value.Trim(),
                Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value));

        if ((r = ThirdDotRx().Match(msg)).Success)
            return new ThirdDotEvent(ts, r.Groups["caster"].Value.Trim(),
                Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value),
                r.Groups["spell"].Value);

        if ((r = ThirdMissRx().Match(msg)).Success)
            return new ThirdMissEvent(ts, r.Groups["attacker"].Value.Trim());

        if ((r = ZoneRx().Match(msg)).Success)
        {
            var zone = r.Groups["zone"].Value;
            // Filter non-zone "You have entered ..." flavor messages (e.g. "an area where levitation ...").
            if (!zone.StartsWith("an area", StringComparison.OrdinalIgnoreCase) &&
                !zone.Contains("area where", StringComparison.OrdinalIgnoreCase))
                return new ZoneEvent(ts, zone);
        }

        return null;
    }

    /// <summary>
    /// Capitalize creature names consistently and strip leading articles so
    /// "orc pawn", "An orc pawn", and "Orc pawn" all count as the same mob.
    /// </summary>
    public static string Normalize(string name)
    {
        name = name.Trim();
        foreach (var article in (string[])["a ", "an ", "the "])
        {
            if (name.Length > article.Length &&
                name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                name = name[article.Length..];
                break;
            }
        }
        if (name.Length == 0) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string VerbToSkill(string verb) => verb switch
    {
        "frenzy on" or "frenzies on" => "Frenzy",
        "hit" => "Hit",
        _ => char.ToUpperInvariant(verb[0]) + verb[1..],
    };
}
