using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-81's star-restore migration through the REAL <see cref="AppSettings.Load"/>, against
/// a settings.json that actually exists on disk.
///
/// **`HudStatPromotionMigrationTests` calls `ApplyMigrations(hadFile)` directly, and that
/// is a different claim.** It proves the pass is correct given the right argument; this
/// proves the argument arrives. Trap 42's distinction — "present in the build" and "in
/// effect at runtime" are not the same thing — and it is exactly the gap that would ship
/// here: the restore must run for a player who HAS a profile, and every player the Founder
/// LOCK is about does.
///
/// **The population that matters is the SA-1-promoted one**, because that is every file
/// written since. It is also trap 76's shape: the state the broken build persisted is the
/// state the repair has to work from, so a repair that could only fire on a pre-SA-1 file
/// would reach nobody at all.
///
/// [Collection] because these write settings.json on the assembly's shared throwaway
/// profile, and `AppSettings.Load(` is one of the writers that guard names.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class HudStatPromotionLoadTests : IDisposable
{
    private static string SettingsPath => AppPaths.File("settings.json");

    public HudStatPromotionLoadTests() => Delete();
    public void Dispose() => Delete();

    private static void Delete()
    {
        try { File.Delete(SettingsPath); } catch { /* best effort */ }
    }

    /// <summary>Write a stored profile the way a real one is stored — through
    /// <see cref="AppSettings.Save"/>, so every property is present, which is what a profile
    /// on disk looks like. The default shape here is PRE-SA-1: the promoted keys are still
    /// stars and the two breakout kinds are gated by them.</summary>
    private static void WriteStoredProfile(Action<AppSettings> configure)
    {
        var stored = new AppSettings
        {
            MiniStats = ["kills", "dps", "xp"],
            DisabledBreakouts = [],
            // Everything SA-1 and DRA-81 added is absent, exactly as it is in a 1.x file.
            HudStatsPromoted = false,
            HudStatStarsRestored = false,
        };
        configure(stored);
        stored.Save();
    }

    /// <summary>A profile as SA-1 left it — promoted, three keys gone. The Founder's file,
    /// and everybody else's.</summary>
    private static void WritePromotedProfile(Action<AppSettings>? configure = null) =>
        WriteStoredProfile(s =>
        {
            s.MiniStats = ["kills"];
            s.DisabledBreakouts = ["Healing"];
            s.HudStatsPromoted = true;
            configure?.Invoke(s);
        });

    /// <summary>
    /// **THE FOUNDER'S LAUNCH.** A profile SA-1 promoted opens with the three stars back,
    /// so the HPS they wanted is on the row and the box that controls it is ticked.
    ///
    /// Asserted through <see cref="HudGlanceStars.From"/> as well as through the list,
    /// because "the file has the key" and "the bar draws the slot" are different claims and
    /// only the second is what the smoke was about.
    /// </summary>
    [Fact]
    public void LoadingAPromotedProfileGivesTheThreeStarsBack()
    {
        WritePromotedProfile();

        var loaded = AppSettings.Load();

        Assert.True(loaded.HudStatStarsRestored);
        Assert.Equal(["kills", "dps", "hps", "xp"], loaded.MiniStats);
        Assert.Equal(new HudGlanceStars(Dps: true, Hps: true, Xp: true, Pet: false),
            HudGlanceStars.From(loaded));
    }

    /// <summary>…and it does not disturb the windows SA-1 already settled. A promoted
    /// profile's absent stars mean "SA-1 took them", not "the player turned them off", so
    /// the pre-SA-1 branch must not run over it and close a Damage window that has been
    /// open ever since.</summary>
    [Fact]
    public void LoadingAPromotedProfileLeavesItsWindowsAlone()
    {
        WritePromotedProfile(s => s.DisabledBreakouts = []);

        var loaded = AppSettings.Load();

        Assert.Empty(loaded.DisabledBreakouts);
    }

    /// <summary>A pre-SA-1 profile gets both halves in one launch: the star it was holding
    /// is carried into <c>DisabledBreakouts</c> BEFORE the keys land, and then the keys
    /// land. "dps" was starred, so the Damage window stays available; "hps" was not, so
    /// Healing is written off explicitly — the window's tick is its whole switch now.</summary>
    [Fact]
    public void LoadingAPreSA1ProfileCarriesItsStarsAndThenRestoresThem()
    {
        WriteStoredProfile(_ => { });

        var loaded = AppSettings.Load();

        Assert.True(loaded.HudStatsPromoted);
        Assert.True(loaded.HudStatStarsRestored);
        Assert.DoesNotContain("Damage", loaded.DisabledBreakouts);
        Assert.Contains("Healing", loaded.DisabledBreakouts);
        Assert.Contains("hps", loaded.MiniStats);
    }

    /// <summary>…and the other star state, because a migration that only ever got one
    /// answer right would pass the test above by accident.</summary>
    [Fact]
    public void AStoredProfileWithNeitherStarKeepsBothWindowsClosed()
    {
        WriteStoredProfile(s => s.MiniStats = ["kills"]);

        var loaded = AppSettings.Load();

        Assert.Contains("Damage", loaded.DisabledBreakouts);
        Assert.Contains("Healing", loaded.DisabledBreakouts);
    }

    [Fact]
    public void AStoredProfileWithBothStarsKeepsBothWindowsOpen()
    {
        WriteStoredProfile(s => s.MiniStats = ["kills", "dps", "hps"]);

        var loaded = AppSettings.Load();

        Assert.DoesNotContain("Damage", loaded.DisabledBreakouts);
        Assert.DoesNotContain("Healing", loaded.DisabledBreakouts);
    }

    /// <summary>The second launch: <see cref="AppSettings.Load"/> persists what the
    /// migration changed, so the next one reads a restored profile and must leave it alone.
    /// Trap 55's shape reached through the real file rather than through a hand-called
    /// chain — and here the re-run would OPEN two windows, because after run one every star
    /// is present.</summary>
    [Fact]
    public void TheSecondLaunchDoesNotOpenWindowsThePlayerHadClosed()
    {
        WriteStoredProfile(s => s.MiniStats = ["kills"]);

        AppSettings.Load();
        var second = AppSettings.Load();
        var third = AppSettings.Load();

        Assert.Contains("Damage", second.DisabledBreakouts);
        Assert.Contains("Healing", second.DisabledBreakouts);
        Assert.Contains("Damage", third.DisabledBreakouts);
        Assert.Contains("Healing", third.DisabledBreakouts);
    }

    /// <summary>…and it does not keep re-adding the keys either: a list rather than a set,
    /// so a pass without its flag would grow "dps,dps,dps" across launches and the bar would
    /// have to decide what that meant.</summary>
    [Fact]
    public void TheSecondLaunchDoesNotDuplicateTheRestoredKeys()
    {
        WritePromotedProfile();

        AppSettings.Load();
        var second = AppSettings.Load();

        Assert.Equal(["kills", "dps", "hps", "xp"], second.MiniStats);
    }

    /// <summary>A profile that has never been saved is born restored and keeps the row a
    /// fresh install has always had — DPS and the XP rate, no HPS. The case `hadFile` exists
    /// to tell apart, asserted HERE through Load because the whole value of the distinction
    /// is that Load can actually make it.</summary>
    [Fact]
    public void AProfileWithNoFileAtAllIsBornRestored()
    {
        Delete();

        var loaded = AppSettings.Load();

        Assert.True(loaded.HudStatStarsRestored);
        Assert.Equal(["dps", "xp", "kills"], loaded.MiniStats);
        Assert.DoesNotContain("hps", loaded.MiniStats);
        Assert.DoesNotContain("Damage", loaded.DisabledBreakouts);
        Assert.Contains("Healing", loaded.DisabledBreakouts);
    }
}
