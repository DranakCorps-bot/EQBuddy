namespace EQBuddy.Core;

/// <summary>
/// Items EQ Legends stores in the CURRENCY tab rather than in bags - which an
/// <c>/outputfile inventory</c> dump never lists, so a dump can neither confirm nor deny
/// holding one.
///
/// **Wind Runes, since 2026-09-16** (Hateborne's log: every rune looted from that day on
/// reads "...and stored it in your currency"; eleven of the fifteen seen so far). The ledger
/// also LEARNS this per item from the loot line itself (<see cref="QuestLedgerStore.Entry.OffDump"/>),
/// but only once the running app has read such a line - and a player's current log may
/// hold none, while every scan since the change has already recorded their runes as zero.
/// This rule is what keeps a scan from acting on that zero before the log has taught it.
///
/// Game behaviour, not quest data: a rule about where an item is stored, which the wiki
/// does not carry. Widen it only on evidence of the same kind - the game's own "stored it
/// in your currency" line.
/// </summary>
public static class CurrencyItems
{
    public static bool IsKnown(string itemName) =>
        QuestCatalog.BaseItemName(itemName).StartsWith("Wind Rune ", StringComparison.OrdinalIgnoreCase);
}
