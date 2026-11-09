namespace EQBuddy.UI.Shared;

/// <summary>
/// Review-only fold for the While you're here block (DRA-4211).
///
/// <para><c>EQBUDDY_WHILE_HERE=shut</c> starts the block folded, the same state the
/// player's own fold control reaches. Unset, or any other value, leaves the player
/// default (open). There is no settings key: a block about where you are now has no
/// state worth persisting, and this hook must not invent one. A shipping run never
/// sets the variable.</para>
/// </summary>
public static class WhileHereReview
{
    public const string Variable = "EQBUDDY_WHILE_HERE";

    /// <summary>The only value that starts the block folded. Exact, so a nearby spelling
    /// leaves the block open rather than folding a player's window. The departure notice
    /// stays: it sits outside the fold on purpose.</summary>
    public const string Shut = "shut";

    /// <summary>The only value that draws nothing. The Sky guide shots need it: folding
    /// still leaves the departure notice, and that notice pushed Island 6 out of
    /// <c>shell-quests-sky-island</c> (DRA-4211).</summary>
    public const string Off = "off";

    public static bool StartShut(string? value) => value == Shut;

    public static bool StartHidden(string? value) => value == Off;
}
