namespace EQBuddy.Core;

/// <summary>
/// Where EQBuddy keeps settings, history, and logs.
///
/// **The Evolved/v1 split is a PRODUCT property, and until TR-1 it was a LAUNCHER one.**
/// This class hardcoded <c>"EQBuddy"</c>; the <c>EQBuddy Evolved</c> directory existed only
/// because <c>scripts/install-local.ps1 -Evolved</c> and
/// <c>scripts/Launch-Evolved-Shell.cmd</c> set <c>EQBUDDY_APPDATA</c> before starting it. So
/// any door that bypassed those two scripts — a published Evolved exe, double-clicked — ran
/// Evolved **on the v1 profile**, and <c>AppSettings.ApplyMigrations</c> rewrote it on the
/// first load. That is trap 42's shape (present in the script ≠ in effect at runtime)
/// standing in front of a player's data, and closing it is TR-1's first change, before any
/// transition surface exists (Fable's transition plan §0.2 / §7, Helm-signed).
///
/// **The two scripts keep working unchanged**, and that is by construction rather than by
/// luck: they set <c>EQBUDDY_APPDATA</c> to <c>%AppData%\EQBuddy Evolved</c>, which is the
/// path this file now derives on its own, so the override and the default agree.
/// <see cref="IsProductOwnedProfile"/> is the question that difference matters for — a
/// redirect somewhere ELSE (a test, <c>shoot.ps1</c>, <c>tests/EQBuddy.E2E</c>) is an
/// isolated profile, and nothing about the v1 transition may happen inside one.
///
/// <c>EQBUDDY_APPDATA</c> still overrides, unchanged and deliberately: it is what keeps the
/// suite off the real profile (the module initializer that exists because a test once
/// overwrote David's live <c>settings.json</c>).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// The major version at which EQBuddy becomes the Evolved line and takes its own
    /// profile directory.
    ///
    /// **One number, one home.** <c>UI.Shared</c>'s
    /// <c>LegacyPlatformUpdatePolicy.WindowsOnlyMajor</c> is the same fact from the update
    /// channel's side — "the major at which EQBuddy stops being cross-platform" — and it is
    /// now DEFINED as this constant rather than spelled again. Two spellings of one number
    /// is trap 4 with a version in it: they would agree today and disagree the first time
    /// one of them moved. UI.Shared references Core, so the dependency runs the only
    /// direction it can.
    /// </summary>
    public const int EvolvedMajor = 2;

    /// <summary>The v1 line's directory name. Reserved to v1 forever — the same rule the
    /// transition plan §3 gives <c>EQBuddySetup.exe</c>, for the same reason: already-shipped
    /// v1 binaries identify their profile by this name and cannot be patched.</summary>
    public const string LegacyDirName = "EQBuddy";

    /// <summary>The Evolved line's directory name. Spelled here ONCE; the two local scripts
    /// derive the same string from <c>%APPDATA%</c> and say in their own comments that they
    /// move when this does.</summary>
    public const string EvolvedDirName = "EQBuddy Evolved";

    /// <summary>Which profile directory a build of this major version owns. Pure, so the
    /// rule is unit-testable without an assembly version or an environment.</summary>
    public static string DirNameFor(int major) =>
        major >= EvolvedMajor ? EvolvedDirName : LegacyDirName;

    /// <summary>
    /// This build's major version — asked of CORE'S OWN assembly rather than of the entry
    /// assembly.
    ///
    /// The product line is a property of the PRODUCT, not of whatever process happens to be
    /// hosting it: under <c>dotnet test</c> the entry assembly is the test host, and its
    /// version has nothing to say about which line EQBuddy is. Core's assembly version is
    /// <c>Directory.Build.props</c>' <c>&lt;Version&gt;</c> — the same number
    /// <c>EQBuddy.exe</c> reports — which is the fact the whole split keys on.
    /// </summary>
    public static int ProductMajor { get; } =
        typeof(AppPaths).Assembly.GetName().Version?.Major ?? 0;

    /// <summary>Is this the Evolved line? Named rather than inlined so a caller never tests
    /// the number itself.</summary>
    public static bool IsEvolvedLine => ProductMajor >= EvolvedMajor;

    private static string Roaming =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>The explicit redirect, or null. Read fresh every time: a test changes it
    /// between cases, and a cached answer would be the isolation guard quietly failing
    /// open.</summary>
    private static string? OverrideDir =>
        Environment.GetEnvironmentVariable("EQBUDDY_APPDATA") is { Length: > 0 } custom
            ? custom
            : null;

    /// <summary>Where this build of EQBuddy keeps a player's profile when nothing has
    /// redirected it — <c>%AppData%\EQBuddy Evolved</c> on the 2.x line,
    /// <c>%AppData%\EQBuddy</c> on 1.x.</summary>
    public static string ProductDir => Path.Combine(Roaming, DirNameFor(ProductMajor));

    /// <summary>The v1 line's profile, wherever this build's own profile is. The IMPORT
    /// source (see <see cref="ProfileImport"/>), and never written to by anything here.
    /// </summary>
    public static string LegacyDir => Path.Combine(Roaming, LegacyDirName);

    public static string Dir => OverrideDir ?? ProductDir;

    /// <summary>
    /// Is the profile in force the one this PRODUCT owns — as opposed to an isolated one a
    /// harness pointed us at?
    ///
    /// **The transition only ever happens inside a product-owned profile**, and this is the
    /// line that says so. <c>shoot.ps1</c>, <c>tests/EQBuddy.E2E</c> and the unit suite all
    /// redirect to a temp directory; an import that ran there would copy a real player's v1
    /// profile into a throwaway one on every screenshot batch — the capture-surface failure
    /// this repo already learned the expensive way (a sheet that photographed David's live
    /// profile), with a whole-directory copy behind it instead of a picture.
    ///
    /// An override that POINTS AT the product directory is not a redirect at all: that is
    /// what <c>install-local.ps1 -Evolved</c> and <c>Launch-Evolved-Shell.cmd</c> set, and
    /// they are how David runs Evolved. Comparing full paths rather than strings, so a
    /// trailing slash or a different case is still the same directory.
    /// </summary>
    public static bool IsProductOwnedProfile => IsProductOwned(OverrideDir);

    /// <summary>
    /// The rule behind <see cref="IsProductOwnedProfile"/>, as a pure function of the
    /// override.
    ///
    /// Separated so it can be TESTED without setting an environment variable: this
    /// assembly's suite runs its collections in parallel, and a test that mutated
    /// <c>EQBUDDY_APPDATA</c> would move every other test's profile out from under it —
    /// trap 57's shape (one shared thing, several parallel writers, and the test that FAILS
    /// is never the test that is wrong).
    /// </summary>
    public static bool IsProductOwned(string? overrideDir)
    {
        if (overrideDir is not { Length: > 0 }) return true;
        return SameDirectory(overrideDir, ProductDir);
    }

    /// <summary>
    /// Is this directory one of the two live player profiles on this machine —
    /// Evolved (<see cref="ProductDir"/>) or v1 (<see cref="LegacyDir"/>)?
    ///
    /// Automated launches (E2E, <c>shoot.ps1</c>, drag-verify) refuse both. An
    /// empty or unusable path is not a live profile: never throw from a path
    /// question that gates a write.
    /// </summary>
    public static bool IsLivePlayerDirectory(string? path) =>
        SameDirectory(path, ProductDir) || SameDirectory(path, LegacyDir);

    /// <summary>Two spellings of one directory — trailing separator, case,
    /// relative segments — are the same directory. The comparison
    /// <see cref="IsProductOwned"/> already made, extracted so the live-profile
    /// refuse cannot drift from it (trap 4).</summary>
    public static bool SameDirectory(string? a, string? b)
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
            // An unusable path is not a match. Never throw from a path question
            // that gates a copy or a refuse.
            return false;
        }
    }

    public static string File(string name) => Path.Combine(Dir, name);
}
