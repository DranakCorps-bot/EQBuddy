using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// The one-time EQBuddy 1.x → EQBuddy Evolved profile import, driven through the REAL
/// EQBuddy.exe (Fable's transition plan §8, TR-1).
///
/// **Core's own tests prove the rules; only a launched app can prove the WIRING.** The
/// import happens in <c>App.OnStartup</c>, before <c>AppSettings.Load</c> and before any
/// window exists — an ordering that is the whole design (see <c>ProfileImportStartup</c>)
/// and that no unit test can observe. Trap 42's shape is exactly what these two scenarios
/// exclude: present in the build and never in effect.
/// </summary>
public class ProfileImportTests
{
    /// <summary>
    /// A v1 profile beside an empty Evolved one, consent given: the copy happens, the
    /// marker is written, and the report reaches the first-run Setup screen.
    ///
    /// **The strongest assertion in here is not one of the migration keys — it is that the
    /// app WORKS.** The harness's settings were moved into the fake v1 profile
    /// (<see cref="AppHarness.StageV1Profile"/>), so the log folder, the window position
    /// and every seeded flag exist only in the file the import copied. <c>Launch()</c> waits
    /// for the fixture to replay into a live session, and that is impossible unless the
    /// settings the app loaded are the imported ones — which is the half a
    /// <c>migrationImported=1</c> alone could not tell from a copy that landed somewhere
    /// nothing reads.
    /// </summary>
    [Fact]
    public void AV1ProfileIsImportedAtStartupAndTheSetupScreenReportsIt()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            // Setup has no address of its own, and the imported profile carries
            // SetupDismissed — so the hook is the only way to reach the screen that
            // carries the report (trap 22).
            ["EQBUDDY_SETUP"] = "1",
        });
        app.StageV1Profile(consent: "accept");
        app.Launch();

        app.WaitForDump("migrationOffered", 1, "the app to have asked about the v1 profile");
        app.WaitForDump("migrationImported", 1, "the v1 profile to have been imported");
        app.WaitForDump("migrationRefusedReason", "none", "nothing to have refused the import");

        // The files themselves, on disk, in the Evolved profile — including the one the app
        // would never have created for itself.
        Assert.True(File.Exists(Path.Combine(app.ProfileDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(app.ProfileDir, "quest-ledger.json")));

        // COPY, NEVER MOVE: the source is still whole. This is LEGACY-V1.md's public
        // promise, asserted against the real binary rather than against Core.
        Assert.True(File.Exists(Path.Combine(app.V1ProfileDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(app.V1ProfileDir, "quest-ledger.json")));

        // The marker, written LAST — so its presence is also the assertion that the commit
        // finished.
        var marker = ProfileImport.ReadMarker(app.ProfileDir);
        Assert.NotNull(marker);
        Assert.Equal("imported", marker!.Decision);
        Assert.True(marker.Files >= 2);

        // And the REPORT reached a surface (trap 43). An import that changed a player's
        // whole profile and said nothing is indistinguishable from one that never ran, and
        // an absent control photographs as an unremarkable panel (trap 29) — so this is
        // asserted from a launched app rather than from a screenshot.
        app.WaitForDump("shellSetupImportReport", 1,
            "the first-run Setup screen to report what came over");
    }

    /// <summary>
    /// The refusal, and it is the one a real machine is most likely to hit: an Evolved
    /// profile that already holds something. There is no merge path, ever — merging two
    /// settings.json files is trap 13 with extra steps — so the app must not ask, must not
    /// copy, and must SAY which rule said no.
    ///
    /// **A negative assertion needs to name the moment it is true at** (trap 62). This one
    /// is safe where an ordinary "and nothing happened" would not be: the three keys are
    /// decided once, before any window exists, so a dump that exists at all is a dump
    /// written after the decision — and <c>Launch()</c> does not return until the app has
    /// replayed the fixture, which is many ticks later.
    /// </summary>
    [Fact]
    public void AnOccupiedEvolvedProfileIsNeverAskedAndNeverImportedInto()
    {
        using var app = new AppHarness();
        app.StageV1ProfileBesideAnOccupiedOne();
        app.Launch();

        app.WaitForDump("migrationRefusedReason", "targetNotEmpty",
            "the import to name the rule that refused it");
        Assert.Equal(0, app.DumpValue("migrationOffered"));
        Assert.Equal(0, app.DumpValue("migrationImported"));
        Assert.False(File.Exists(Path.Combine(app.ProfileDir, ProfileImport.MarkerFileName)),
            "a refusal must not spend the one-time offer: no marker, so a player who "
            + "clears the Evolved profile is asked again.");
    }

    /// <summary>
    /// **The gate every other test in this suite depends on**: with no source override, an
    /// isolated EQBUDDY_APPDATA profile is never offered an import — so a suite run, a
    /// screenshot batch or a local dev launch can never copy a real player's v1 profile
    /// into a throwaway one. It is asserted rather than assumed because the cost of it
    /// being wrong is silent and enormous, and because every other launch in this file
    /// overrides the source specifically to get PAST it.
    /// </summary>
    [Fact]
    public void AnIsolatedProfileWithNoSourceOverrideIsNeverOfferedAnImport()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("migrationRefusedReason", "notTheProductProfile",
            "an isolated test profile to be refused the import by name");
        Assert.Equal(0, app.DumpValue("migrationOffered"));
    }
}
