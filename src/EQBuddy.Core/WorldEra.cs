namespace EQBuddy.Core;

/// <summary>
/// **WHAT ERA THE WORLD IS CURRENTLY AT — one curated fact, and it starts ABSENT**
/// (DRA-180, plan P2).
///
/// <para>The Founder's Replace list cited Kael Drakkel, Icewell Keep and Veeshan's Peak to a
/// level-29 in a world that has not reached them. Those rows passed the band gate correctly —
/// Kael's published band is <c>30-60+</c>, and 30 − 29 = 1, well inside
/// <see cref="Recommendations.GearBandReachAbove"/> — because a level band cannot express an
/// expansion. Kael really does hold level-30 giants; it holds them in an era the server has
/// not opened. The missing axis is WHEN a place exists, and this is the one value that
/// answers it.</para>
///
/// <para><b>It is CURATED and hand-committed, like every other curated catalog in this repo,
/// and the weekly refresh may only FLAG drift.</b> A machine that could write this could
/// silently advance the world past what the server has actually opened, and the failure would
/// be invisible: rows would simply start appearing. The refresh's census of
/// <c>{{... Era}}</c> templates is evidence about ZONES, never about the world's clock.</para>
///
/// <para><b>ABSENT is the shipped state and it is not a hole.</b> Nothing in this repo stated
/// the world's era before DRA-180 and this slice deliberately does not invent one: guessing
/// would be trap 73 with a single word instead of a paragraph, and the guess would be the one
/// input that decides whether a real place is refused. While <see cref="Current"/> is empty
/// the era gate stands down whole and product behaviour is exactly what it was, which is what
/// lets the engine, its guards and its words all land green before anyone has answered
/// anything. <c>D5</c> is the slice that sets it, from named evidence, in a one-value commit
/// citing its source.</para>
///
/// <para><b>The value must be spelled as <see cref="QuestEraLadder.Eras"/> spells it.</b> An
/// era word this repo cannot rank is one the gate cannot compare against, and
/// <see cref="Recommendations"/> stands the era arm down rather than guessing at it — the same
/// refusal <see cref="ZoneEras.Source.Refused"/> makes on the zone side, for the same
/// reason.</para>
/// </summary>
public static class WorldEra
{
    /// <summary>
    /// The era the world has reached, spelled as <see cref="QuestEraLadder.Eras"/> spells it,
    /// or <b>empty for ABSENT</b> — nobody has told EQBuddy yet.
    ///
    /// <para>Empty is the shipped value. Setting it is D5's whole job and is a curated,
    /// hand-committed change that cites its evidence in <see cref="Source"/>.</para>
    /// </summary>
    public const string Current = "";

    /// <summary>
    /// Where <see cref="Current"/> came from, in words — the eqlwiki page, the Founder's
    /// statement, whatever actually settled it. Empty while <see cref="Current"/> is.
    ///
    /// <para><b>It ships beside the value rather than in a commit message</b> because a bare
    /// era word is a claim a surface will state as fact to a player, and the same discipline
    /// the band gate follows (quote eqlwiki's own row, never a paraphrase) applies to the one
    /// number the whole gate hangs on.</para>
    /// </summary>
    public const string Source = "";

    /// <summary>Whether the world's era has been stated at all. <b>The gate's stand-down
    /// condition</b> — asked rather than re-deriving <c>Length == 0</c> at each reader, so
    /// there is one answer to "do we know" (trap 4).</summary>
    public static bool Known => Current.Length != 0;
}
