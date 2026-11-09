namespace EQBuddy.Core;

/// <summary>
/// What an item block's <c>Effect:</c> line is an effect OF — <b>read so a weapon row can SAY
/// it procs, and priced by nothing</b> (DRA-241, Helm ruling <c>27302878</c>).
///
/// <para>Six values because six things are true of a stats block, and none of them is a guess.
/// <see cref="Unadmitted"/> is the one that exists so a spelling nobody has measured cannot be
/// silently filed under one of the other five (trap 78: a rule aimed at nothing is green), and
/// like <see cref="WeaponHands.Unadmitted"/> before it, <b>it refuses nothing</b>.</para>
/// </summary>
public enum ItemEffectKind
{
    /// <summary>The block carries no <c>Effect:</c> line and mentions no effect at all — the
    /// ordinary state of an item, and 1,161 of the shipped catalog's 1,649 weapon records.
    /// </summary>
    NoEffect,

    /// <summary><b>A combat proc</b> — the wiki's own <c>(Combat, …)</c> parenthetical, or the
    /// newer template's <c>(Combat Req Level N)</c> (DRA-4196). 382 of the shipped catalog's
    /// weapon records since the 2026-11-02 refresh (378 before it), every one of them wearable.
    /// This is the only value any surface draws, and it draws it as a NAME rather than as a
    /// number.</summary>
    Combat,

    /// <summary><c>(Must Equip)</c>, or the newer template's <c>(Clicky, Must Equip)</c> — a
    /// click effect that wants the item worn. 48 weapon records (47 before the refresh). Not a
    /// proc: it fires because the player pressed it.</summary>
    MustEquip,

    /// <summary><c>(Any Slot)</c> / <c>(Any Slot/Can Equip)</c>, or the newer template's
    /// <c>(Clicky, Can Equip)</c> / <c>(Charge Clicky, Can Equip)</c> — a click effect that works
    /// from the bags. 46 weapon records (47 before the refresh). Not a proc, and not even
    /// conditional on wearing the thing.</summary>
    AnySlot,

    /// <summary><c>(Worn)</c> — a continuous effect that applies while the item is worn. 9
    /// records. Not a proc: nothing triggers it.</summary>
    Worn,

    /// <summary>
    /// An effect line this rule does not admit — <b>reported, never bucketed, and it refuses
    /// nothing</b>.
    ///
    /// <para>SEVEN records in the shipped catalog's weapon half, named one by one in
    /// <c>WeaponProcTests</c>, and they are four different shapes:
    /// <c>Sabertooth Short Bow</c> and <c>Sharp Claws</c> write <c>Combat Effect:</c> with the
    /// word LEFT of the colon; <c>Rod of Understanding</c> says <c>(Proc)</c>;
    /// <c>Blam Stick</c> is a bare <c>Effect:</c> with nothing after it; <c>TornEar Thumper</c>
    /// says <c>(Req Level 30)</c>; <c>Spiroc Wingblade</c> and <c>Trakanon's Tooth</c> carry only
    /// a <c>(Casting Time: …)</c>. The 2026-11-02 refresh (DRA-4190) briefly made it THIRTEEN:
    /// six pages moved to eqlwiki's newer <c>Template:Itembox</c> wording, held here until
    /// admitting a wording was decided as a rule rather than a re-pin. DRA-4196 admitted those
    /// exact spellings (<c>WeaponProcs.AdmittedClickPairs</c>, <c>Combat Req Level N</c>), and
    /// it is the same seven again.</para>
    ///
    /// <para><b>The match is deliberately NOT widened to rescue the two left-of-colon
    /// spellings, and Helm named that refusal by name.</b> They are almost certainly combat
    /// procs — a reader can see it — and that is exactly the temptation trap 66 is about: a
    /// forgiveness rule written to catch two known rows is a rule about a POSITION rather than
    /// about the fact, and the next spelling it admits is one nobody has looked at. The cost is
    /// two weapons whose rows stay silent about a proc they have, which is the cheap direction:
    /// this reading only ever ADDS a sentence, so an unadmitted row is a row that says less,
    /// never a row that says something wrong.</para>
    /// </summary>
    Unadmitted,
}

/// <summary>
/// **THE ONE READER OF A STATS BLOCK'S <c>Effect:</c> LINE** (DRA-241, plan row 1).
///
/// <para><b>It reports a proc and it prices nothing, and that is the whole ruling.</b> D6's done
/// bar asked that weapons compare on *"damage/delay/ratio/hand-restriction/dual-wield/proc"*.
/// Five of the six shipped in DRA-222 D6; proc did not, because comparing means pricing and
/// <b>there is no number anywhere for what a proc is worth</b> — nothing says a Ykesha proc
/// beats +40 Mana, and inventing an exchange rate is what the Gear Locker's lock forbids. Helm
/// ruled ADOPT (a): <b>report only, never price; annotate, never refuse</b>.</para>
///
/// <para><b>This is the FIFTH reader of a block this repo already ships</b>, not a new data
/// source — the shape <see cref="WeaponSkills"/> used for the <c>Skill:</c> line (DRA-222 D6,
/// S7.3). It fetches nothing, adds no data file and un-PARKs no harvest: every number in this
/// file was measured against the committed <c>src/EQBuddy.Core/Data/ItemCatalog.json.gz</c>.</para>
///
/// <para><b>SCOPE IS THE WEAPON RECORD, NOT THE EFFECT LINE</b>, and that is not a tidiness
/// preference. 66 records carry a <c>(Combat)</c> effect and NO <c>DMG:</c> line — they are
/// Rogue poisons (<c>Asp Poison</c>, <c>Basilisk Poison</c>, <c>Crookstinger Poison</c>), a
/// consumable you apply TO a weapon rather than a weapon that procs. A reading scoped to the line
/// would have called all 66 of them weapon procs. So <see cref="Proc"/> asks
/// <see cref="ItemStatsBlock.Dmg"/> first, and the damage question is asked through the block's
/// own parser rather than by grepping for the word. The two used to disagree by one:
/// <c>Keg Mallet</c> spells it <c>Base Dmg: 9</c>, and until DRA-251 the block did not read
/// that as damage, so a textual scan found 1,649 weapons where the shipped code found 1,648.
/// DRA-251 admitted <c>Base Dmg:</c> as an exact second spelling (never a pattern — the four
/// elemental/bane <c>… Dmg:</c> keys stay unread, pinned in
/// <c>ItemStatsBlockDmgCensusTests</c>), so the shipped code now finds 1,649 too.</para>
///
/// <para><b>Measured against the shipped catalog</b> (11,196 records, 1,649 weapons — 1,648
/// with a <c>DMG:</c> line plus <c>Keg Mallet</c>, which has no effect line): 488 of those
/// weapons carry an effect the block mentions at all — <b>378</b>
/// <see cref="ItemEffectKind.Combat"/>, 47 <see cref="ItemEffectKind.MustEquip"/>, 47
/// <see cref="ItemEffectKind.AnySlot"/>, 9 <see cref="ItemEffectKind.Worn"/> and <b>7</b>
/// <see cref="ItemEffectKind.Unadmitted"/>. The 378 name 216 distinct spells, which is the
/// distinct-count telltale saying this is transcription rather than boilerplate (trap 73).
/// Re-taken on the 2026-11-02 refresh with DRA-4196's spellings admitted: 11,383 records, 1,668
/// weapons — 382 Combat, 48 MustEquip, 46 AnySlot, 9 Worn, 7 Unadmitted.
/// <c>WeaponProcTests</c> pins every one of those counts against the committed file, so a
/// refresh that moves one says so.</para>
/// </summary>
public static partial class WeaponProcs
{
    /// <summary>The parentheticals this rule admits, exactly as the wiki writes them, mapped to
    /// what they mean. Matched WHOLE on the first clause of the parenthetical and
    /// case-insensitively — the same shape <see cref="Tradeskills.Match"/> keeps, and for its
    /// reason: a containment test would read <c>(Must Equip)</c> as an <c>Any Slot</c> the day
    /// somebody writes <c>(Any Slot, Must Equip)</c>.</summary>
    private static readonly Dictionary<string, ItemEffectKind> Admitted =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Combat"] = ItemEffectKind.Combat,
            ["Must Equip"] = ItemEffectKind.MustEquip,
            ["Any Slot"] = ItemEffectKind.AnySlot,
            ["Any Slot/Can Equip"] = ItemEffectKind.AnySlot,
            ["Worn"] = ItemEffectKind.Worn,
        };

    /// <summary>
    /// eqlwiki's newer <c>Template:Itembox</c> wording, which leads with the click TYPE and puts
    /// the equip condition SECOND (DRA-4196, Planner ruling under Helm's <c>27302878</c>; found by
    /// the 2026-11-02 refresh, DRA-4190). Matched WHOLE on the first TWO clauses together, in this
    /// order, and only the pairs the shipped catalog's census actually holds — one named record
    /// each in <c>WeaponProcTests</c>. <c>Can Equip</c> is <see cref="ItemEffectKind.AnySlot"/>,
    /// the meaning the older <c>Any Slot/Can Equip</c> already carries; no new value.
    ///
    /// <para><b>Deliberately absent:</b> a type clause with no equip clause after it
    /// (<c>(Clicky, Casting Time: …)</c>, 10 records) says nothing about where it works;
    /// <c>Consumable Clicky</c> never appears with an equip clause in the census, so admitting it
    /// would be a spelling nobody has measured (trap 78); and the reversed order is a different
    /// sentence, not this one.</para>
    /// </summary>
    private static readonly Dictionary<string, ItemEffectKind> AdmittedClickPairs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Clicky, Must Equip"] = ItemEffectKind.MustEquip,
            ["Clicky, Can Equip"] = ItemEffectKind.AnySlot,
            ["Charge Clicky, Can Equip"] = ItemEffectKind.AnySlot,
        };

    /// <summary>What kind of effect this block's <c>Effect:</c> line describes.</summary>
    public static ItemEffectKind Kind(ItemStatsBlock? stats) =>
        stats is null
            ? ItemEffectKind.NoEffect
            : Kind(stats.Effect, stats.MentionsEffect);

    /// <summary>
    /// What kind of effect this line describes.
    ///
    /// <para>The two arguments are the two things the block can say, and the second is what
    /// keeps a spelling from vanishing instead of being reported. An EMPTY
    /// <paramref name="effect"/> means no line the block's own parser admitted; if the block
    /// nevertheless said the word somewhere, that is a shape nobody has read and it answers
    /// <see cref="ItemEffectKind.Unadmitted"/> rather than
    /// <see cref="ItemEffectKind.NoEffect"/>. Silently answering "no effect" there is how the
    /// two <c>Combat Effect:</c> rows would have disappeared without ever being counted.</para>
    /// </summary>
    public static ItemEffectKind Kind(string? effect, bool mentionsEffect = false)
    {
        var line = effect?.Trim() ?? "";
        if (line.Length == 0)
            return mentionsEffect ? ItemEffectKind.Unadmitted : ItemEffectKind.NoEffect;

        var open = line.IndexOf('(');
        if (open < 0) return ItemEffectKind.Unadmitted;
        var close = line.IndexOf(')', open + 1);
        if (close < 0) return ItemEffectKind.Unadmitted;

        // The first clause only. "(Combat, Casting Time: Instant)" is a combat proc and the
        // casting time is the page telling us something this repo does not read.
        var clauses = line[(open + 1)..close].Split(',').Select(c => c.Trim()).ToArray();
        var head = clauses[0];
        if (Admitted.TryGetValue(head, out var kind)) return kind;

        // The newer template's click wording: the TYPE first, the equip condition second.
        if (clauses.Length >= 2
            && AdmittedClickPairs.TryGetValue($"{head}, {clauses[1]}", out kind))
            return kind;

        // `(Combat Req Level 45)` — the newer template's combat proc with its level folded into
        // the clause. The word `Combat` is what makes it a proc: a bare `(Req Level 30)`
        // (TornEar Thumper) stays Unadmitted, because a level is not a trigger.
        if (CombatReqLevelRx().IsMatch(head)) return ItemEffectKind.Combat;

        return ItemEffectKind.Unadmitted;
    }

    // Whole clause, one integer, nothing after it (DRA-4196).
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^Combat Req Level \d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CombatReqLevelRx();

    /// <summary>
    /// **THE PROC A WEAPON ROW MAY NAME**, or "" — the one answer any surface asks for.
    ///
    /// <para>Both halves of the scope live here rather than at the call sites, so "only a weapon
    /// procs, and only a <c>(Combat)</c> effect is a proc" is one fact somebody can test rather
    /// than a pair of conditions each caller has to remember (trap 4). A caller that forgot the
    /// <see cref="ItemStatsBlock.Dmg"/> half would put 66 Rogue poisons on gear rows.</para>
    ///
    /// <para>It returns the spell's NAME and never a score, a rank or a flag with arithmetic
    /// behind it. That is the ruling in the signature.</para>
    /// </summary>
    public static string Proc(ItemStatsBlock? stats) =>
        stats is { Dmg: not null } && Kind(stats) == ItemEffectKind.Combat
            ? ProcName(stats.Effect)
            : "";

    /// <summary>
    /// The spell the effect line names, with the wiki's parenthetical and its trailing
    /// <c>at Level N</c> cut off.
    ///
    /// <para>The level is dropped deliberately: it is the level the PROC fires at, not a level
    /// requirement on the item, and a row reading "procs Ykesha at Level 37" next to a character
    /// who is level 20 would be read as a restriction that does not exist. The catalog states an
    /// item's own level requirement in a different place, and this file makes no claim about
    /// it.</para>
    /// </summary>
    public static string ProcName(string? effect)
    {
        var line = effect?.Trim() ?? "";
        var open = line.IndexOf('(');
        return (open < 0 ? line : line[..open]).Trim();
    }
}
