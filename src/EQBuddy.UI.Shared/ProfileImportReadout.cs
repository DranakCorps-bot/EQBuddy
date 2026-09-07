using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The words the v1 → Evolved profile import says, and the two sentences it builds from a
/// manifest — Fable's transition plan §2 (TR-1), Helm-signed.
///
/// It is here rather than in the dialog for this repo's standing reason: the WPF layer has
/// no unit tests, so a sentence built inline in a window is a sentence nothing can check —
/// and every sentence in this file is a promise about a player's data.
///
/// **Bevel owns the polish.** The transition plan sequences a one-pager after the plan was
/// signed: first-run import consent wording, the Windows-only gate messaging, the
/// dual-install page and the release-page transition section. What is here is product copy
/// that says the true thing in the right order; the file to edit when that lands is this
/// one, and nothing in the dialog changes with it.
///
/// **The consent MODEL is not copy and is not Bevel's to move**: opt-in, default-checked,
/// one click to accept, visible to decline. That is door D2 on the plan's §6 — David's,
/// consequence #8 — and TR-1 builds it as the stated assumption while the door stays open.
/// Flipping it to auto-import-with-notice is one boolean's default and one paragraph here.
/// </summary>
public static class ProfileImportReadout
{
    /// <summary>The window's title, and it carries what it is about rather than being a
    /// second bare "EQBuddy" in the same process (trap 24 — `-OwnerPid` cannot separate two
    /// windows of ONE process, and the shell already names its room for this reason).
    /// </summary>
    public const string WindowTitle = "EQBuddy — Import from EQBuddy 1.x";

    public const string Headline = "You already have EQBuddy 1.x";

    /// <summary>
    /// What is about to happen, in the order a player needs it: what we found, what we
    /// would do with it, and — first among the reassurances, because it is the one that
    /// makes the answer safe either way — that the 1.x profile is not touched.
    /// </summary>
    public const string Lead =
        "EQBuddy Evolved keeps its own settings, history and quest progress, separate from "
        + "EQBuddy 1.x. It can start from a copy of what 1.x already knows about you.";

    /// <summary>The tick-box. Default-CHECKED (D2's stated assumption) and visible to
    /// decline: the player is told what will happen and accepts it by continuing, which is
    /// what "opt-in, default-checked" means here.</summary>
    public const string Consent = "Bring my EQBuddy 1.x settings and history over";

    /// <summary>Under the tick-box, and it is the half that makes a decline safe: nothing
    /// is moved, nothing is deleted, and 1.x goes on working exactly as it does now. This
    /// is <c>LEGACY-V1.md</c>'s public promise said to the one player it applies to, at the
    /// moment it applies.</summary>
    public const string CopyNeverMove =
        "Your EQBuddy 1.x profile is copied, never moved — it is left exactly as it is, and "
        + "EQBuddy 1.x keeps working. Nothing is sent anywhere.";

    /// <summary>The alternative, named rather than implied. "Start fresh" is a real answer
    /// and it has to look like one, or a default-checked box is a decision nobody made.
    /// </summary>
    public const string StartFresh =
        "Leave it unticked to start fresh. EQBuddy Evolved begins with nothing and learns "
        + "from this point on; your 1.x profile is still there either way.";

    /// <summary>One way out, like Setup's. The act is described by what the tick-box says,
    /// so the button does not need to carry the decision twice.</summary>
    public const string Continue = "Continue";

    /// <summary>What EQBuddy 1.x running looks like from here, said by name — the plan's
    /// own wording. It is the one refusal a player can clear, so it says how.</summary>
    public const string LegacyRunningHeadline = "Close EQBuddy 1.x first";

    public const string LegacyRunning =
        "EQBuddy 1.x is running and is writing to the profile we would be copying, so this "
        + "is not the moment to read it. Close EQBuddy 1.x and start EQBuddy Evolved again "
        + "and the offer comes back. Nothing has been copied and nothing has changed.";

    /// <summary>
    /// "3 files · 4.2 MB" — the size of the thing being agreed to, so "settings and history"
    /// is not the only thing on offer. Bytes are rendered at one decimal from MB up: a
    /// player is judging scale, and "4,404,019 bytes" is not a scale.
    /// </summary>
    public static string Volume(int files, long bytes) =>
        $"{files} file{(files == 1 ? "" : "s")} · {Size(bytes)}";

    public static string Size(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024 / 1024:0.0} GB"
        : bytes >= 1024L * 1024 ? $"{bytes / 1024d / 1024:0.0} MB"
        : bytes >= 1024 ? $"{bytes / 1024d:0} KB"
        : $"{bytes} bytes";

    /// <summary>The heading over the manifest.</summary>
    public const string ManifestHeadline = "What would come over";

    /// <summary>One manifest row. Directories roll their contents up (a wiki cache is
    /// thousands of files and naming them all is not information), files stand alone.
    /// </summary>
    public static string ManifestRow(ProfileImportEntry entry) =>
        entry.IsDirectory
            ? $"{entry.Name} — {entry.Files} file{(entry.Files == 1 ? "" : "s")}, {Size(entry.Bytes)}"
            : $"{entry.Name} — {Size(entry.Bytes)}";

    /// <summary>
    /// **THE REPORT, and it exists because an import nobody is told about is
    /// indistinguishable from an import that never ran** (trap 43 — a value with a producer
    /// and no consumer, which is how an achievements dump silently ticked a player's
    /// checklist for two days). It is read off the marker rather than off a variable the
    /// importing code kept, so it survives the restart that the import's own ordering makes
    /// inevitable: the copy happens before the settings load, and the first surface capable
    /// of showing anything comes up after it.
    /// </summary>
    public static string ReportHeadline(ProfileImportMarker marker) =>
        marker.Decision == "imported"
            ? "Your EQBuddy 1.x profile came over"
            : "You started fresh";

    public static string Report(ProfileImportMarker marker) =>
        marker.Decision == "imported"
            ? $"{Volume(marker.Files, marker.Bytes)} copied from EQBuddy 1.x"
                + (marker.SourceVersion is { Length: > 0 } v ? $" (v{v})" : "")
                + $" on {marker.WhenUtc.ToLocalTime():d MMMM}. "
                + "Your 1.x profile is untouched and EQBuddy 1.x still works."
            : "EQBuddy Evolved is keeping its own settings and history, and your EQBuddy "
                + "1.x profile is untouched.";

    /// <summary>Beside the report, and it is the plan's "start-fresh alternative beside
    /// it": the undo is structural, so say what it is rather than offering a button that
    /// would have to delete a player's profile to work.</summary>
    public const string ReportUndo =
        "To start over instead, close EQBuddy and delete the EQBuddy Evolved folder in "
        + "%AppData%. Nothing there is your only copy — EQBuddy 1.x still has all of it.";
}
