namespace EQBuddy.UI.Shared;

/// <summary>
/// The exact in-game slash commands EQBuddy asks the player to run (David,
/// 2026-08-14: every surface that names one offers a one-click ⧉ copy). One
/// definition per command so a button and the prose beside it can never drift —
/// copy sources reference these, never their own literals (pinned by test).
/// </summary>
public static class GameCommands
{
    /// <summary>Makes the game write &lt;name&gt;_&lt;server&gt;-Inventory.txt beside its
    /// own folders — read by the quest tracker's held tab, the Inventory window,
    /// and the Gear Locker.</summary>
    public const string OutputfileInventory = "/outputfile inventory";

    /// <summary>Makes the game write its achievements dump — the import that
    /// pre-marks Sky rewards and raid clears from before EQBuddy.</summary>
    public const string OutputfileAchievements = "/outputfile achievements";
    /// <summary>Singular, and the file it writes is plural with the class code in the
    /// middle. Both verified from the game's own usage line and announcement in Hateborne's
    /// log, 2026-08-25 — no public documentation covers this dump.</summary>
    public const string OutputfileFaction = "/outputfile faction";

    /// <summary>The map's breadcrumb social, one command per social-editor line:
    /// /loc drops a position into the log, /doability 1 keeps the key doing what
    /// it already did.</summary>
    public const string LocSocialLine1 = "/loc";

    /// <inheritdoc cref="LocSocialLine1"/>
    public const string LocSocialLine2 = "/doability 1";

    /// <summary>Both social lines, newline-separated — the game's social editor
    /// takes one line per slot, so the paste happens line by line.</summary>
    public static readonly string LocSocial =
        LocSocialLine1 + Environment.NewLine + LocSocialLine2;
}
