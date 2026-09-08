using System.Runtime.CompilerServices;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>What the module initializer decided to do about the environment it found.</summary>
internal enum IsolationChoice
{
    /// <summary>Nothing had set EQBUDDY_APPDATA, so the redirect filled the gap. The
    /// ordinary case — CI, <c>check.ps1</c> and a plain <c>dotnet test</c> all land here.
    /// </summary>
    FilledTheGap,

    /// <summary>Something HAD set EQBUDDY_APPDATA and it was overridden anyway, because the
    /// opt-in below was not given. This is the hardening.</summary>
    OverrodePreset,

    /// <summary>The opt-in was given and the preset pointed at a real player profile, so it
    /// was refused. An opt-in may move the suite to another throwaway directory; it may not
    /// point it at the data this whole file exists to protect.</summary>
    RefusedPlayerProfile,

    /// <summary>The opt-in was given for a preset that is not a player profile, so the
    /// caller's directory was left alone.</summary>
    KeptPresetByOptIn,
}

/// <summary>
/// Points every test at a throwaway profile folder before a single test runs.
///
/// Why this exists: AppSettings.Save(), the ledgers, and the learned stores all write
/// under AppPaths.Dir, which is the REAL profile directory unless EQBUDDY_APPDATA says
/// otherwise. A test that constructed settings and called Save() therefore overwrote a
/// live install's settings.json — LogFolder, watch rules, and checklists gone (hit on
/// David's own machine, 2026-08-14, while running the suite). Reviewing each test for
/// stray writes is the fragile fix; making the writes land somewhere harmless is the
/// durable one.
///
/// A module initializer runs before any test type is touched, so the redirect is in
/// place for static initializers too (CompanionPreview reads its marker path once). The
/// folder is left behind deliberately: a failed test's artifacts are evidence, and the OS
/// reclaims temp.
///
/// **A PRE-SET EQBUDDY_APPDATA NO LONGER WINS, and that is this file's second lesson.**
/// It used to: the initializer opened with <c>if (EQBUDDY_APPDATA is set) return;</c>, on
/// the reasoning that a harness or a developer who had named a directory meant it. But the
/// variable is INHERITED, so what actually names it is whatever shell the suite happens to
/// be started from — and on 2026-09-07 that was a session exporting
/// <c>%AppData%\EQBuddy Evolved</c> to drive the real app. The suite ran, isolation opted
/// out of itself in silence, and David's live Evolved settings.json went from ~390 KB to
/// ~4.9 KB TWICE in one day. The guard did not fail; it was asked politely to stand aside
/// by a variable nobody in the room had typed.
///
/// So the default is now to isolate ALWAYS, and honouring a preset is an explicit,
/// deliberate act:
///
/// <list type="bullet">
/// <item><c>EQBUDDY_APPDATA_ALLOW_PRESET=1</c> — "keep my override". Anything else (unset,
/// <c>0</c>, empty, <c>true</c>) means isolate, because a half-spelled opt-in must fail
/// towards the player's data being safe.</item>
/// <item><b>Even with the opt-in, a preset naming a real player profile is refused</b> —
/// <c>%AppData%\EQBuddy Evolved</c> or <c>%AppData%\EQBuddy</c>. Running THIS suite against
/// a live profile is never a thing anyone wants; it is the accident, and an opt-in flag is
/// not a reason to hand back the one hole it was written to close.</item>
/// </list>
///
/// <see cref="Decide"/> is a pure function of the environment for the same reason
/// <c>AppPaths.IsProductOwned</c> is: this assembly runs its collections in parallel, so a
/// test that mutated EQBUDDY_APPDATA to exercise a branch would move every other test's
/// profile out from under it (trap 57). The initializer applies what it decides and records
/// it in <see cref="Choice"/>, so a test can also assert what happened in THIS process.
/// </summary>
internal static class TestProfileIsolation
{
    /// <summary>The opt-in, spelled once. Documented in the class summary above.</summary>
    internal const string AllowPresetVar = "EQBUDDY_APPDATA_ALLOW_PRESET";

    /// <summary>The one value that means "keep my override". A typo isolates.</summary>
    internal const string AllowPresetValue = "1";

    /// <summary>The temp folder every throwaway profile is created under. Named so the live
    /// assertions can say "the suite's profile is in here" rather than "it is somewhere in
    /// temp".</summary>
    internal const string TempRoot = "eqbuddy-tests";

    /// <summary>What was decided at module-init time, for the tests that assert this
    /// process is actually isolated rather than that the rule reads correctly.</summary>
    internal static IsolationChoice Choice { get; private set; }

    /// <summary>Whatever EQBUDDY_APPDATA said before the redirect ran — usually null.
    /// </summary>
    internal static string? PresetAtStartup { get; private set; }

    [ModuleInitializer]
    internal static void RedirectProfileToTemp()
    {
        PresetAtStartup = Environment.GetEnvironmentVariable("EQBUDDY_APPDATA");
        Choice = Decide(
            PresetAtStartup,
            Environment.GetEnvironmentVariable(AllowPresetVar),
            PlayerProfiles);

        if (Choice == IsolationChoice.KeptPresetByOptIn) return;

        var dir = NewProfileDir();
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", dir);
    }

    /// <summary>The directories that hold a real player's data on this machine. Both lines,
    /// because an Evolved build's <c>AppPaths.ProductDir</c> is only one of them and the v1
    /// profile is just as much somebody's install.</summary>
    internal static IReadOnlyList<string> PlayerProfiles =>
        [AppPaths.ProductDir, AppPaths.LegacyDir];

    /// <summary>A fresh throwaway profile path. Not created here — <see cref="Decide"/> and
    /// this stay free of side effects so both can be exercised without leaving directories
    /// behind.</summary>
    internal static string NewProfileDir() =>
        Path.Combine(Path.GetTempPath(), TempRoot, Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The rule, as a pure function of the environment. See the class summary for why it is
    /// separated and for what each answer means.
    /// </summary>
    internal static IsolationChoice Decide(
        string? preset, string? allowPreset, IReadOnlyList<string> playerProfiles)
    {
        if (preset is not { Length: > 0 }) return IsolationChoice.FilledTheGap;
        if (!string.Equals(allowPreset, AllowPresetValue, StringComparison.Ordinal))
            return IsolationChoice.OverrodePreset;
        return playerProfiles.Any(p => SameDirectory(preset, p))
            ? IsolationChoice.RefusedPlayerProfile
            : IsolationChoice.KeptPresetByOptIn;
    }

    /// <summary>Two spellings of one directory — a trailing separator, a different case, a
    /// relative segment — are the same directory. The comparison <c>AppPaths.IsProductOwned</c>
    /// makes, for the same reason: neither spelling may be a second profile.</summary>
    internal static bool SameDirectory(string? a, string? b)
    {
        if (a is not { Length: > 0 } || b is not { Length: > 0 }) return false;
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // An unusable path is not a match. Never throw from the question that decides
            // whether the suite is isolated — the safe answer is reached either way, since
            // an unmatched preset is one we override.
            return false;
        }
    }
}
