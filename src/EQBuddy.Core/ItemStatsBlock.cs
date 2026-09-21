using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// The wiki's in-game stats block ("Slot: PRIMARY", "AC: 10", "STR: +15 WIS: +15"),
/// parsed into numbers the Gear Locker can compare (#104). The block is what the
/// game shows in the item window, transcribed by wiki editors — so parsing is a
/// whitelist over observed shapes and anything unreadable simply stays null:
/// a Locker row honestly missing a number beats one wearing a guessed one.
/// </summary>
public sealed partial class ItemStatsBlock
{
    /// <summary>Equip slots exactly as the block prints them (PRIMARY, HEAD, EAR…).</summary>
    public List<string> Slots { get; init; } = [];
    public int? Ac { get; init; }
    public int? Dmg { get; init; }
    public int? Delay { get; init; }
    public int? Hp { get; init; }
    public int? Mana { get; init; }
    /// <summary>Named attributes and saves (STR/STA/…/SvFire), signed as printed.</summary>
    public Dictionary<string, int> Attributes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Empty = usable by all (the block prints "Class: ALL" or nothing).</summary>
    public List<string> Classes { get; init; } = [];
    public string Skill { get; init; } = "";
    public bool Magic { get; init; }
    public bool Lore { get; init; }
    public bool NoDrop { get; init; }

    /// <summary>
    /// The block's own <c>Effect:</c> line, VERBATIM from after the colon — "" when the block
    /// carries none (DRA-241). Read by <see cref="WeaponProcs"/> and by nothing else.
    ///
    /// <para>Kept as the page's own string rather than pre-split into a name and a kind, for the
    /// reason every other transcription in this repo is: the parenthetical is the wiki's own
    /// sentence and the one thing separating a combat proc from a click effect, so the field
    /// carrying it should be the thing a human can check against the page.</para>
    /// </summary>
    public string Effect { get; init; } = "";

    /// <summary>
    /// The block says the word "Effect" SOMEWHERE, however spelled — the flag that stops an
    /// UNREADABLE effect line from reading as an ABSENT one (DRA-241).
    ///
    /// <para>It exists for two committed records. <c>Sabertooth Short Bow</c> and
    /// <c>Sharp Claws</c> write <c>Combat Effect:</c>, with the word left of the colon, so
    /// <see cref="Effect"/> is empty for them and <see cref="ItemEffectKind.NoEffect"/> would be
    /// a false answer the reading had no way to notice. With this they answer
    /// <see cref="ItemEffectKind.Unadmitted"/> instead — counted, named in a test, and drawn by
    /// nothing.</para>
    /// </summary>
    public bool MentionsEffect { get; init; }

    /// <summary>DMG per point of delay — the honest one-number weapon comparison.</summary>
    public double? Ratio => Dmg is { } d && Delay is { } dl && dl > 0 ? (double)d / dl : null;

    /// <summary>True when the item goes in a worn slot at all — spell scrolls, quest
    /// pieces and containers have no Slot line and no place in the Locker.</summary>
    public bool Wearable => Slots.Count > 0;

    private static readonly HashSet<string> AttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "STR", "STA", "AGI", "DEX", "WIS", "INT", "CHA",
        "SV FIRE", "SV COLD", "SV MAGIC", "SV DISEASE", "SV POISON",
    };

    public static ItemStatsBlock Parse(IEnumerable<string> statsLines)
    {
        var slots = new List<string>();
        var classes = new List<string>();
        var attrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int? ac = null, dmg = null, delay = null, hp = null, mana = null;
        var skill = "";
        var effect = "";
        bool magic = false, lore = false, noDrop = false, mentionsEffect = false;

        foreach (var line in statsLines)
        {
            ReadEffect(line, ref effect, ref mentionsEffect);

            magic |= line.Contains("MAGIC ITEM", StringComparison.OrdinalIgnoreCase);
            lore |= line.Contains("LORE ITEM", StringComparison.OrdinalIgnoreCase);
            noDrop |= line.Contains("NO DROP", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("NO TRADE", StringComparison.OrdinalIgnoreCase);

            if (SlotRx().Match(line) is { Success: true } slotM)
                slots.AddRange(slotM.Groups[1].Value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (ClassRx().Match(line) is { Success: true } classM)
            {
                var names = classM.Groups[1].Value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!names.Any(c => c.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
                    classes.AddRange(names);
            }
            if (SkillRx().Match(line) is { Success: true } skillM)
                skill = skillM.Groups[1].Value.Trim();

            foreach (Match m in PairRx().Matches(line))
            {
                var key = Regex.Replace(m.Groups["key"].Value.Trim(), @"\s+", " ").ToUpperInvariant();
                var value = int.Parse(m.Groups["val"].Value);
                switch (key)
                {
                    case "AC": ac = value; break;
                    case "DMG": dmg = value; break;
                    case "ATK DELAY": case "DELAY": delay = value; break;
                    case "HP": hp = value; break;
                    case "MANA": mana = value; break;
                    default:
                        if (AttributeKeys.Contains(key)) attrs[key] = value;
                        break;
                }
            }
        }

        return new ItemStatsBlock
        {
            Slots = slots, Classes = classes, Attributes = attrs,
            Ac = ac, Dmg = dmg, Delay = delay, Hp = hp, Mana = mana,
            Skill = skill, Magic = magic, Lore = lore, NoDrop = noDrop,
            Effect = effect, MentionsEffect = mentionsEffect,
        };
    }

    // "Slot: PRIMARY SECONDARY" — the block's own word list.
    [GeneratedRegex(@"^Slot:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SlotRx();
    // "Class: PAL WAR" / "Class: ALL"
    [GeneratedRegex(@"^Class(?:es)?:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ClassRx();
    // "Skill: 1H Slashing Atk Delay: 26" — the skill runs until the Delay key or
    // the end of the line; a greedier cut ("1H") lied about two-word skills.
    [GeneratedRegex(@"Skill:\s*(.+?)(?=\s+(?:Atk\s+)?Delay:|\s*$)", RegexOptions.IgnoreCase)]
    private static partial Regex SkillRx();
    /// <summary>
    /// **THE ONE PLACE THE <c>Effect:</c> LINE IS READ** (DRA-241; trap 4).
    ///
    /// <para>Called from <see cref="Parse"/> and from <see cref="ItemCatalog.Record.ToStatsBlock"/>,
    /// which is why it is a method rather than four lines inside the loop. That record builds its
    /// block from the promoter's own COLUMNS and never parses the text, so the catalog — the only
    /// source any of this ships against — would carry an empty <see cref="Effect"/> on all 11,196
    /// records if this rule lived only in <see cref="Parse"/>. A second copy over there would be
    /// two producers of one fact, and the one that drifts is always the one nobody is looking
    /// at.</para>
    /// </summary>
    internal static void ReadEffect(string line, ref string effect, ref bool mentionsEffect)
    {
        // The word ANYWHERE, so a spelling this parser cannot read is REPORTED as unadmitted
        // rather than silently becoming "no effect" — see MentionsEffect.
        mentionsEffect |= line.Contains("Effect", StringComparison.OrdinalIgnoreCase);
        // FIRST admitted line wins: four records carry a plain `Effect:` AND a `Focus Effect:`
        // (the Staff of Elemental Mastery set), and the plain one is the line this repo reads.
        if (effect.Length == 0 && EffectRx().Match(line) is { Success: true } m)
            effect = m.Groups[1].Value.Trim();
    }

    /// <summary>The same rule over a whole block's raw text, for the one caller that holds the
    /// block as a string rather than as lines.</summary>
    internal static (string Effect, bool MentionsEffect) ReadEffect(string? statsText)
    {
        var effect = "";
        var mentions = false;
        foreach (var line in (statsText ?? "").Split('\n'))
            ReadEffect(line, ref effect, ref mentions);
        return (effect, mentions);
    }

    // "Effect: Ykesha (Combat, Casting Time: Instant) at Level 37" (DRA-241).
    //
    // **ANCHORED AT THE LINE START, AND THAT IS THE REFUSAL HELM NAMED.** Two committed records
    // write "Combat Effect:" with the word left of the colon, and this does not match them — by
    // design. Admitting a word before the key would be a rule about a POSITION rather than about
    // the fact (trap 66), and the next spelling it swept in is one nobody has measured. They are
    // reported as ItemEffectKind.Unadmitted instead, and a test names both.
    [GeneratedRegex(@"^\s*Effect:\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex EffectRx();
    // "AC: 10" · "STR: +15" · "Atk Delay: 26" · "SV FIRE: +5" — several per line.
    [GeneratedRegex(@"(?<key>[A-Za-z][A-Za-z ]{0,9}?):\s*(?<val>[+-]?\d+)\b")]
    private static partial Regex PairRx();
}
