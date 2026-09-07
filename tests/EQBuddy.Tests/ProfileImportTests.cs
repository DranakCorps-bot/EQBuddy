using System.Text.Json;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The one-time EQBuddy 1.x → EQBuddy Evolved profile import (Fable's transition plan §2,
/// TR-1, Helm-signed).
///
/// **Every negative in here was prove-failed**, per trap 62: an assertion that something
/// did NOT happen reads exactly like coverage when the rule it names has been deleted, and
/// this file is full of them — refuse while 1.x is running, refuse into a non-empty
/// profile, never offer twice, never move the source. Each was run with its rule removed
/// from <c>ProfileImport</c> and each went red; the counts are in the PR body.
///
/// It uses REAL directories rather than a filesystem abstraction, deliberately. The bugs
/// this class exists to prevent are all about what is on disk after a kill — a staging
/// directory committed half way, a marker written before the files, a lock held by another
/// process — and none of those is expressible against a fake.
/// </summary>
public class ProfileImportTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eqbuddy-import-").FullName;
    private string Source => Path.Combine(_root, "EQBuddy");
    private string Target => Path.Combine(_root, "EQBuddy Evolved");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>A plausible v1 profile: settings, a ledger, a database, a wiki cache
    /// subdirectory and the transient files that must not travel.</summary>
    private void SeedLegacy(string? version = "1.99.18")
    {
        Directory.CreateDirectory(Source);
        // The same options AppSettings.Save uses: NaN is a legitimate value in a profile
        // ("not placed yet" window positions) and the default serializer refuses it — trap
        // 23's rule, seeded through the shape the app actually writes.
        File.WriteAllText(Path.Combine(Source, "settings.json"),
            JsonSerializer.Serialize(new AppSettings { LastSeenVersion = version ?? "" },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    NumberHandling = System.Text.Json.Serialization
                        .JsonNumberHandling.AllowNamedFloatingPointLiterals,
                }));
        File.WriteAllText(Path.Combine(Source, "quest-ledger.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(Source, "settings.json.bak"), "{}");
        File.WriteAllText(Path.Combine(Source, "history.db"), "sqlite");
        Directory.CreateDirectory(Path.Combine(Source, "wiki"));
        File.WriteAllText(Path.Combine(Source, "wiki", "asp.json"), "{}");
        File.WriteAllText(Path.Combine(Source, "debug.txt"), "tick=3");
        File.WriteAllText(Path.Combine(Source, "error.log"), "boom");
        File.WriteAllText(Path.Combine(Source, "door.trigger"), "open");
        File.WriteAllText(Path.Combine(Source, "instance.lock"), "");
        File.WriteAllText(Path.Combine(Source, "show.request"), "");
        File.WriteAllText(Path.Combine(Source, "settings.json.corrupt"), "\0\0\0");
    }

    private static bool NotLocked(string _) => false;
    private static bool Locked(string _) => true;

    private ProfileImportOffer Inspect(bool productOwned = true, Func<string, bool>? locked = null) =>
        ProfileImport.Inspect(Source, Target, productOwned, locked ?? NotLocked);

    private ProfileImportResult Run(ProfileImportOffer offer, Func<string, bool>? locked = null) =>
        ProfileImport.Run(offer, true, locked ?? NotLocked);

    // ---- the offer -----------------------------------------------------------------

    [Fact]
    public void AV1ProfileBesideAnEmptyEvolvedOneIsOffered()
    {
        SeedLegacy();

        var offer = Inspect();

        Assert.Equal(ProfileImportBlock.None, offer.Block);
        Assert.True(offer.CanImport);
        Assert.Equal("1.99.18", offer.LegacyVersion);
    }

    /// <summary>**The gate that keeps every harness on this machine safe.** shoot.ps1, the
    /// E2E suite and this very test project all redirect EQBUDDY_APPDATA to a temp
    /// directory; an import running there would copy a real player's v1 profile into a
    /// throwaway one on every screenshot batch.</summary>
    [Fact]
    public void AnIsolatedProfileIsNeverOfferedAnImport()
    {
        SeedLegacy();

        Assert.Equal(ProfileImportBlock.NotTheProductProfile,
            Inspect(productOwned: false).Block);
    }

    [Fact]
    public void AProfileImportingItselfIsRefused()
    {
        SeedLegacy();

        // A 1.x build: the product directory IS the v1 one, so source and target are the
        // same folder. Nothing to import, and a copy would be a self-overwrite.
        var offer = ProfileImport.Inspect(Source, Source, productOwnedProfile: true, NotLocked);

        Assert.Equal(ProfileImportBlock.NotTheProductProfile, offer.Block);
    }

    [Fact]
    public void NoV1ProfileMeansNothingIsSaid()
    {
        Directory.CreateDirectory(Source);   // a folder with no settings.json in it

        var offer = Inspect();

        Assert.Equal(ProfileImportBlock.NoLegacyProfile, offer.Block);
        Assert.False(offer.WorthSaying);
    }

    /// <summary>Trap 47's rule, and the one refusal the player can clear — so it is the one
    /// that gets SAID rather than swallowed. v1 writes settings.json whole-file and
    /// unflushed (trap 65, on a binary we cannot patch) and holds history.db open: a live
    /// copy is a corrupt import wearing a progress bar.</summary>
    [Fact]
    public void ARunningV1RefusesTheImportAndSaysSo()
    {
        SeedLegacy();

        var offer = Inspect(locked: Locked);

        Assert.Equal(ProfileImportBlock.LegacyIsRunning, offer.Block);
        Assert.False(offer.CanImport);
        Assert.True(offer.WorthSaying);
    }

    /// <summary>There is no merge path, ever: merging two settings.json files is trap 13
    /// with extra steps. The refusal NAMES what is there, because "no" with no reason is
    /// the silent no-op this repo treats as broken.</summary>
    [Fact]
    public void ANonEmptyEvolvedProfileRefusesAndNamesWhatIsThere()
    {
        SeedLegacy();
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "settings.json"), "{}");

        var offer = Inspect();

        Assert.Equal(ProfileImportBlock.TargetNotEmpty, offer.Block);
        Assert.Contains("settings.json", offer.TargetContents);
    }

    /// <summary>
    /// **The running app's OWN files must not make the target look occupied.** By the time
    /// the question is asked, this process holds <c>instance.lock</c> in the target and may
    /// have written <c>error.log</c> and <c>debug.txt</c>; counting those would make "empty"
    /// a state no live EQBuddy can be in, and the offer would be blocked on every machine
    /// by the very process making it.
    /// </summary>
    [Fact]
    public void TheImportingAppsOwnTransientFilesDoNotMakeTheTargetOccupied()
    {
        SeedLegacy();
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "instance.lock"), "");
        File.WriteAllText(Path.Combine(Target, "error.log"), "started");
        File.WriteAllText(Path.Combine(Target, "debug.txt"), "tick=1");

        Assert.Equal(ProfileImportBlock.None, Inspect().Block);
    }

    /// <summary>Trap 55's shape: a one-time step that is not marked one-time undoes the
    /// player on every launch. The marker is what marks it, and it is written for a DECLINE
    /// as well as for a copy.</summary>
    [Fact]
    public void TheOfferNeverRepeatsAfterAnImport()
    {
        SeedLegacy();
        Assert.True(Run(Inspect()).Imported);

        Assert.Equal(ProfileImportBlock.AlreadyAnswered, Inspect().Block);
    }

    [Fact]
    public void TheOfferNeverRepeatsAfterADecline()
    {
        SeedLegacy();
        ProfileImport.Decline(Inspect());

        Assert.Equal(ProfileImportBlock.AlreadyAnswered, Inspect().Block);
        Assert.Equal("declined", ProfileImport.ReadMarker(Target)!.Decision);
        // And nothing was copied by saying no.
        Assert.False(File.Exists(Path.Combine(Target, "settings.json")));
    }

    // ---- the copy ------------------------------------------------------------------

    [Fact]
    public void TheImportCopiesTheWholeProfileIncludingSubdirectories()
    {
        SeedLegacy();

        var result = Run(Inspect());

        Assert.True(result.Imported);
        Assert.True(File.Exists(Path.Combine(Target, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Target, "quest-ledger.json")));
        Assert.True(File.Exists(Path.Combine(Target, "history.db")));
        Assert.True(File.Exists(Path.Combine(Target, "wiki", "asp.json")));
        // ProfileJson's net comes across: a profile that arrives without its .bak arrives
        // without the thing that repairs it after a torn write (trap 65).
        Assert.True(File.Exists(Path.Combine(Target, "settings.json.bak")));
    }

    /// <summary>The transient list, asserted by NAME rather than by count — a count would
    /// go on passing if the list swapped one entry for another. <c>instance.lock</c> is the
    /// mandatory one: the importing app holds the target's own lock open with
    /// <c>FileShare.None</c>, so copying over it fails the import outright.</summary>
    [Theory]
    [InlineData("debug.txt")]
    [InlineData("error.log")]
    [InlineData("door.trigger")]
    [InlineData("instance.lock")]
    [InlineData("show.request")]
    [InlineData("settings.json.corrupt")]
    public void TransientFilesDoNotComeAcross(string name)
    {
        SeedLegacy();

        Assert.True(Run(Inspect()).Imported);

        Assert.False(File.Exists(Path.Combine(Target, name)));
    }

    /// <summary>**COPY, NEVER MOVE** — <c>LEGACY-V1.md</c>'s "your profile is yours" made
    /// mechanical. Undo is structural: clear the Evolved profile and v1 still has
    /// everything. Asserted over the whole source tree rather than on one file, because
    /// "never moves" is a claim about all of it.</summary>
    [Fact]
    public void TheV1ProfileIsLeftExactlyAsItWas()
    {
        SeedLegacy();
        var before = Snapshot(Source);

        Assert.True(Run(Inspect()).Imported);

        Assert.Equal(before, Snapshot(Source));
    }

    [Fact]
    public void TheMarkerRecordsTheSourceItsVersionAndTheManifest()
    {
        SeedLegacy();

        var result = Run(Inspect());
        var marker = ProfileImport.ReadMarker(Target)!;

        Assert.Equal("imported", marker.Decision);
        Assert.Equal(Source, marker.Source);
        Assert.Equal("1.99.18", marker.SourceVersion);
        Assert.Equal(result.Files, marker.Files);
        Assert.Contains(marker.Entries, e => e.Name == "settings.json");
        Assert.Contains(marker.Entries, e => e is { Name: "wiki", IsDirectory: true, Files: 1 });
    }

    /// <summary>A source whose settings carry no version does not get an invented one — the
    /// report says so rather than printing a number nobody wrote.</summary>
    [Fact]
    public void AnUnversionedV1ProfileImportsWithNoVersionRatherThanAGuess()
    {
        SeedLegacy(version: null);

        Assert.True(Run(Inspect()).Imported);

        Assert.True(string.IsNullOrEmpty(ProfileImport.ReadMarker(Target)!.SourceVersion));
    }

    /// <summary>
    /// The consent question is drawn in the palette the player's EQBuddy 1.x is wearing,
    /// which is read out of the SOURCE profile because this product has none of its own yet
    /// — that ordering is the whole import. Null when there is nothing to read, so the
    /// caller keeps its default rather than being handed a guess.
    /// </summary>
    [Fact]
    public void ThePlayersOwnV1ThemeIsReadableBeforeAnythingOfOursIsLoaded()
    {
        SeedLegacy();
        File.WriteAllText(Path.Combine(Source, "settings.json"), "{\"Theme\":\"Turquoise\"}");

        Assert.Equal("Turquoise", ProfileImport.LegacyTheme(Source));
        Assert.Null(ProfileImport.LegacyTheme(Path.Combine(_root, "nothing-here")));
    }

    /// <summary>**Stage, verify, commit — and the verify is the step that has teeth.** A
    /// settings.json that will not parse is #385's zero-filled file; carrying it across
    /// would hand the Evolved profile the corruption AND spend the one-time offer on it.
    /// Nothing is committed, no marker is written, and the offer comes back.</summary>
    [Fact]
    public void AnUnreadableSettingsFileAbortsTheWholeImportAndLeavesTheOfferOpen()
    {
        SeedLegacy();
        File.WriteAllBytes(Path.Combine(Source, "settings.json"), new byte[64]);   // zeros

        var result = Run(Inspect());

        Assert.False(result.Imported);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(Path.Combine(Target, "quest-ledger.json")));
        Assert.False(File.Exists(Path.Combine(Target, ProfileImport.MarkerFileName)));
        Assert.Equal(ProfileImportBlock.None, Inspect().Block);
    }

    /// <summary>The staging directory is gone whether the import worked or not — a profile
    /// that keeps a second copy of itself in a dot-folder is a profile whose next
    /// "is it empty" question answers wrongly.</summary>
    [Fact]
    public void TheStagingDirectoryDoesNotSurviveEitherOutcome()
    {
        SeedLegacy();
        Assert.True(Run(Inspect()).Imported);
        Assert.False(Directory.Exists(Path.Combine(Target, ProfileImport.StagingDirName)));

        Directory.Delete(Target, recursive: true);
        File.WriteAllBytes(Path.Combine(Source, "settings.json"), new byte[64]);
        Assert.False(Run(Inspect()).Imported);
        Assert.False(Directory.Exists(Path.Combine(Target, ProfileImport.StagingDirName)));
    }

    /// <summary>**<see cref="ProfileImport.Run"/> re-asks rather than trusting the offer it
    /// is handed.** The caller holds that offer across a dialog a player can leave open, and
    /// "1.x is running" is exactly the fact that changes inside that window — trap 47's rule
    /// with the polarity that matters: the slow path must not be the one that skips the
    /// check.</summary>
    [Fact]
    public void AnOfferThatWentStaleWhileTheQuestionWasOnScreenIsRefusedAtTheLastMoment()
    {
        SeedLegacy();
        var offer = Inspect();
        Assert.True(offer.CanImport);

        var result = ProfileImport.Run(offer, productOwnedProfile: true, Locked);

        Assert.False(result.Imported);
        Assert.False(File.Exists(Path.Combine(Target, "settings.json")));
    }

    /// <summary>The real lock, not a lambda: <c>ProfileImport</c> takes the probe as an
    /// argument so Core does not need a second spelling of the lock's filename (trap 4), and
    /// this is the assertion that the spelling the app passes actually answers.</summary>
    [Fact]
    public void TheRealSingleInstanceLockIsWhatAnsweredIsRunning()
    {
        SeedLegacy();
        Assert.False(SingleInstance.IsHeldByAnotherCopy(Source));

        using (SingleInstance.TryClaim(Source))
        {
            Assert.True(SingleInstance.IsHeldByAnotherCopy(Source));
            Assert.Equal(ProfileImportBlock.LegacyIsRunning,
                ProfileImport.Inspect(Source, Target, true,
                    SingleInstance.IsHeldByAnotherCopy).Block);
        }

        Assert.False(SingleInstance.IsHeldByAnotherCopy(Source));
    }

    /// <summary>Every relative path in the source, with its bytes — the shape "nothing
    /// moved" is a claim about.</summary>
    private static IReadOnlyList<string> Snapshot(string dir) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(p => $"{Path.GetRelativePath(dir, p)}:{new FileInfo(p).Length}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
}
