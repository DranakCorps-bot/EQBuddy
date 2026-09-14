namespace EQBuddy.UI.Shared;

/// <summary>
/// What a surface says when it needs the player to run an in-game command and CANNOT
/// hand the command over — the phone.
///
/// David answered this directly on 2026-08-20, asked as its own question: a phone's
/// clipboard cannot paste into the game running on the PC, so a ⧉ copy button on
/// EQBuddy Mobile is a control that lies about what it does. Selectable text is honest —
/// you are looking at the phone and typing on the PC, which is what the second screen is
/// for — and it still satisfies the standing rule that a surface naming a command must
/// hand the exact text over (David, 2026-08-14).
///
/// The desktops keep their buttons. This is the same fact wearing the affordance the
/// device can actually keep, and it comes off <see cref="GameCommands"/> like every
/// other copy source, so the phone can never show a command the PC has stopped using.
/// </summary>
/// <param name="Lead">The line above the command block.</param>
/// <param name="Command">The exact text, from <see cref="GameCommands"/>, never a literal.</param>
/// <param name="Note">What happens after — a command with no next step is half an
/// instruction, which is the defect this whole change is about.</param>
public sealed record CommandPrompt(string Lead, string Command, string Note);

/// <summary>One prompt per surface that needs one. Curated on purpose, and paired with
/// <c>GameCommandsTests.EverySurfaceThatNeedsACommandHandsItOver</c>: a list is code a
/// compiler cannot check, so the list and the assertion are written together.</summary>
public static class CommandPrompts
{
    /// <summary>Says "on your PC" out loud. The player is holding the device that cannot
    /// run it, and telling them to type a command without saying where is the same defect
    /// as telling them to import a file without saying how.</summary>
    public const string Lead = "Type this in game on your PC:";

    /// <summary>The gear checklist auto-ticks from the inventory dump.</summary>
    public static readonly CommandPrompt GearInventory = new(
        Lead, GameCommands.OutputfileInventory,
        "EQBuddy on your PC picks the file up by itself and ticks off whatever your "
        + "bags and bank already hold.");

    /// <summary>The Raids surface marks clears from before EQBuddy off the achievements
    /// dump — a two-step: type it in game, then import the file on the PC.</summary>
    public static readonly CommandPrompt RaidsAchievements = new(
        Lead, GameCommands.OutputfileAchievements,
        "EQBuddy on your PC reads it by itself and marks clears from before EQBuddy.");

    // ---- the Helper, on the phone (DRA-71 D9) -------------------------------------------
    //
    // THREE, and they are the same three the desktop room's own list carries
    // (GameCommandsTests.SurfacesNeedingACommand has three rows against HelperRoom.cs): the
    // Helper needs the dumps its OWN engines read, and a surface that copied every command
    // in the app would be a launcher rather than an answer. The NOTES differ from the
    // surfaces above because what happens next differs — a dump that fills the gear
    // checklist is not a dump that lets a room start ranking — and a command with no next
    // step is half an instruction.

    /// <summary>Work on Faction cannot rank a standing the log never sees.</summary>
    public static readonly CommandPrompt HelperFaction = new(
        Lead, GameCommands.OutputfileFaction,
        "EQBuddy on your PC picks the file up by itself, and the Helper starts ranking "
        + "factions and which of your own kills move them.");

    /// <summary>Unlock Classes and Unlock Races are the game's own record.</summary>
    public static readonly CommandPrompt HelperAchievements = new(
        Lead, GameCommands.OutputfileAchievements,
        "EQBuddy on your PC reads it by itself, and the Helper starts ranking the unlocks "
        + "you are closest to.");

    /// <summary>Farm Gear anchors its sweep on what you are WEARING, which comes out of the
    /// inventory dump and nowhere else.</summary>
    public static readonly CommandPrompt HelperInventory = new(
        Lead, GameCommands.OutputfileInventory,
        "EQBuddy on your PC picks the file up by itself, and the Helper starts looking for "
        + "upgrades over what you are wearing.");
}
