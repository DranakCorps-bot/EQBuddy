namespace EQBuddy.UI.Shared;

/// <summary>
/// One row on a widget card: a name on the left, a value hard right (Gate 5b).
///
/// Every card draws this same row, and before this each one drew it slightly differently —
/// `FillList` in the WPF widget, a near-copy in the Avalonia widget, and a fresh one inside
/// every surface lifted out. That is the shape the whole rework exists to consolidate, and
/// it is worth doing before twelve more cards each grow their own.
///
/// Deliberately DATA and framework-free, so a card's presentation module can be tested
/// with no window at all — which is the point of Gate 5b. Nothing here is a control; the
/// flags below say what a row IS, and each UI decides what that looks like.
/// </summary>
/// <param name="Name">The left column. Trimmed with an ellipsis when it doesn't fit, and
/// carrying its full text on hover (#182).</param>
/// <param name="Value">The right column.</param>
/// <param name="Indent">This row hangs under the one above it — a drop beneath the
/// creature that dropped it. A real margin from the spacing scale, never leading spaces
/// in <paramref name="Name"/>: a proportional font renders those differently at every zoom
/// level, and nothing can assert them.</param>
/// <param name="Note">A muted parenthetical after the name — "(Foraged)", "(Merged)". A
/// separate run, so a click still looks the base name up.</param>
/// <param name="Item">This names a game ITEM rather than a creature, a faction or a
/// sentence. Items are clickable (the wiki popup), hoverable (cached stats) and can carry
/// the quest badge; nothing else is any of those. One flag rather than three booleans
/// threaded through every call, because they are always the same three together.</param>
/// <param name="ValueInk">A <see cref="ThemePalettes"/> key for the value, when the value
/// carries STATE — faction gains read Good, losses read Bad. Null takes the row default.
/// A key rather than a colour, so a card can never go off-palette.</param>
/// <param name="Tip">Hover text for THIS row, beating the caller's name-keyed tooltip
/// lookup. Every list until #240 had unique names, so a <c>Func&lt;string, string?&gt;</c>
/// was enough; the Level-ups list does not — dying back a level and re-dinging it writes
/// "Level 24" twice, and the two rows have different gaps to report. A lookup keyed on
/// something that is not unique answers the same thing for both, silently and only for the
/// player it happened to.</param>
public sealed record CardRow(
    string Name,
    string Value,
    bool Indent = false,
    string? Note = null,
    bool Item = false,
    string? ValueInk = null,
    string? Tip = null);
