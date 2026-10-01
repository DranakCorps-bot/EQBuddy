namespace EQBuddy.Core;

/// <summary>
/// Where a loot line says an item came FROM - a creature's corpse, or the one source that is
/// not a creature: a dungeon crawl's <see cref="RewardChest"/> (discussion #679, joeymavity;
/// its duplicate #957, LoZZoL).
///
/// <para>Every loot shape the game writes for a corpse it also writes for the chest, with
/// "from Reward Chest" where "from X's corpse" would be. Verbatim, from the reporter's log and
/// from David's own (2026-09-20):</para>
/// <code>
/// You looted 10 Mote of Major Potential from Reward Chest and stored it in your currency
/// You looted a Bronze Knuckles +4 from Reward Chest and sold it for 2 gold.
/// --You have looted 2 Mote of Major Potential from Reward Chest.--
/// You looted a Pristine Studded Leather Tunic +4 from Reward Chest to create a Pristine Studded Leather Tunic +9
/// </code>
/// <para>All four loot regexes in <see cref="LogParser"/> required <c>'s corpse</c>, so every
/// one of those lines parsed as nothing: the motes never reached the Motes card, the quest
/// ledger never saw the item, and the auto-sale's coin never reached vendor income.
/// <see cref="From"/> is the one fragment the four share, so the next non-corpse source is one
/// alternation here rather than four edits there. The chest is matched as a LITERAL, not as
/// "anything that is not a corpse": it is the only non-corpse source anyone has measured, and
/// a looser rule would admit shapes nobody has read.</para>
///
/// <para><b>The chest IS loot and is NOT a creature.</b> Its items count like any other loot
/// (session loot, motes, the quest ledger, vendor income). But it never becomes a row in the
/// per-creature drop ledger (<see cref="StatsSnapshot.Mobs"/> → <see cref="MobHistory"/>, the
/// Drops card, the wiki contribution pack): it has no kills to rate a drop against, no zone
/// (a creature's zone is stamped by its KILL, so every crawl's chest would pool into one
/// zone-less "creature"), and offering it to eqlwiki as a mob would spend a lookup on a page
/// that describes nothing. <see cref="IsCreature"/> is the one answer, applied where the
/// snapshot projects its creatures.</para>
/// </summary>
public static class LootSources
{
    /// <summary>The source a dungeon crawl's reward loot names, exactly as the game prints it
    /// (and exactly as <see cref="LogParser.Normalize"/> leaves it).</summary>
    public const string RewardChest = "Reward Chest";

    /// <summary>The "from ..." clause every loot regex shares: a creature's corpse, or the
    /// reward chest. Both arms fill the same <c>source</c> group.</summary>
    internal const string From = @"from (?:(?<source>.+?)'s corpse|(?<source>" + RewardChest + "))";

    /// <summary>Is this loot source a creature that belongs in the per-creature drop ledger?
    /// False only for <see cref="RewardChest"/>.</summary>
    public static bool IsCreature(string source) =>
        !string.Equals(source, RewardChest, StringComparison.OrdinalIgnoreCase);
}
