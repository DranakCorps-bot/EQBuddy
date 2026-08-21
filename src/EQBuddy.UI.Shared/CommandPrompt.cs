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
}
