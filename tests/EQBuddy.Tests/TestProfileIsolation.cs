using System.Runtime.CompilerServices;

namespace EQBuddy.Tests;

/// <summary>
/// Points every test at a throwaway profile folder before a single test runs.
///
/// Why this exists: AppSettings.Save(), the ledgers, and the learned stores all write
/// under AppPaths.Dir, which is the REAL %AppData%\EQBuddy Evolved unless EQBUDDY_APPDATA
/// says otherwise. A test that constructed settings and called Save() therefore overwrote
/// a live install's settings.json — LogFolder, watch rules, and checklists gone (hit on
/// David's own machine, 2026-08-14, while running the suite). Reviewing each test for
/// stray writes is the fragile fix; making the writes land somewhere harmless is the
/// durable one.
///
/// A module initializer runs before any test type is touched, so the redirect is in place
/// for static initializers too (CompanionPreview reads its marker path once). The folder
/// is left behind deliberately: a failed test's artifacts are evidence, and the OS
/// reclaims temp.
///
/// **THE REDIRECT IS UNCONDITIONAL, AND THAT IS THE 2026-09-07 CHANGE.** It used to read
/// `if (EQBUDDY_APPDATA is set) return;` — "an EQBUDDY_APPDATA already set by the harness
/// or a developer wins; this only fills the gap". That sentence assumed the value in the
/// environment was somebody's decision about THIS RUN, and an inherited variable is not
/// evidence of a decision: an agent seat launched under
/// `EQBUDDY_APPDATA=%AppData%\EQBuddy Evolved` (which is exactly what
/// `scripts/Launch-Evolved-Shell.cmd` and `install-local.ps1 -Evolved` set, and what any
/// shell that has run one of them carries) inherits it into `dotnet test`, the guard steps
/// aside precisely because the value is set, and the suite writes to the live Evolved
/// profile. That is not a hypothetical: it truncated David's Evolved `settings.json` from
/// ~390 KB to ~4.8 KB on 2026-09-07. **The guard failed open in the one environment it was
/// written for**, and it did so silently, because a set variable is indistinguishable from
/// an intended one.
///
/// So the question is no longer "is a profile already chosen?" but "has anyone said, in
/// this run, that the live profile is fine?" — and only <see cref="OptOutVar"/> says it.
/// Anything inherited is displaced and recorded in <see cref="DisplacedVar"/>, so a
/// developer who set it on purpose can see what happened rather than wonder why their
/// directory is empty (trap 42's shape: "present in the environment" and "in effect" are
/// different claims, and only a report separates them).
/// </summary>
internal static class TestProfileIsolation
{
    /// <summary>The profile redirect Core reads (<c>AppPaths.Dir</c>).</summary>
    internal const string ProfileVar = "EQBUDDY_APPDATA";

    /// <summary>
    /// The ONLY way to run this suite against whatever profile the environment names —
    /// including the real one. Exact string <c>"1"</c>, deliberately: "0", "false", "no"
    /// and an accidental empty export must all mean "redirect", because the failure this
    /// exists to prevent is irreversible and the failure it can cause is an inconvenience.
    /// </summary>
    internal const string OptOutVar = "EQBUDDY_ALLOW_LIVE_APPDATA";

    /// <summary>Where an inherited <see cref="ProfileVar"/> went when it was displaced.
    /// Diagnostic only — nothing reads it but the isolation tests and a puzzled human.
    /// </summary>
    internal const string DisplacedVar = "EQBUDDY_APPDATA_DISPLACED";

    /// <summary>
    /// The rule, as a pure function of the opt-out variable, so it can be tested without
    /// an environment: this assembly runs its collections in parallel and a test that
    /// mutated the profile variable would move every other test's profile out from under
    /// it (trap 57).
    /// </summary>
    internal static bool ShouldRedirect(string? optOut) =>
        !string.Equals(optOut, "1", StringComparison.Ordinal);

    [ModuleInitializer]
    internal static void RedirectProfileToTemp()
    {
        if (!ShouldRedirect(Environment.GetEnvironmentVariable(OptOutVar))) return;

        if (Environment.GetEnvironmentVariable(ProfileVar) is { Length: > 0 } inherited)
            Environment.SetEnvironmentVariable(DisplacedVar, inherited);

        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable(ProfileVar, dir);
    }
}
