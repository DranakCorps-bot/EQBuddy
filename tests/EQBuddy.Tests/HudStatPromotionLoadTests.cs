using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The SA-1 promotion migration through the REAL <see cref="AppSettings.Load"/>, against a
/// settings.json that actually exists on disk.
///
/// **`HudStatPromotionMigrationTests` calls `ApplyMigrations(hadFile)` directly, and that
/// is a different claim.** It proves the pass is correct given the right argument; this
/// proves the argument arrives. Trap 42's distinction — "present in the build" and "in
/// effect at runtime" are not the same thing — and it is exactly the gap that would have
/// shipped here: the migration must run for a player who HAS a profile, and every one of
/// them does.
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
    /// <see cref="AppSettings.Save"/>, so every property is present, which is what a 1.x
    /// profile on disk looks like.</summary>
    private static void WriteStoredProfile(Action<AppSettings> configure)
    {
        var stored = new AppSettings
        {
            // A pre-SA-1 profile: the promoted keys are still stars, and the two breakout
            // kinds are gated by them rather than by the disabled list.
            MiniStats = ["kills", "dps", "xp"],
            DisabledBreakouts = [],
            // Everything SA-1 added is absent, exactly as it is in a 1.x file.
            HudStatsPromoted = false,
        };
        configure(stored);
        stored.Save();
    }

    /// <summary>The whole point: a player WITH a profile gets the promotion, and the star
    /// they were holding is carried into <c>DisabledBreakouts</c> before it is stripped.
    ///
    /// "dps" was starred, so the Damage window stays available; "hps" was not, so Healing
    /// is written off explicitly — the tick is its whole switch now, and without this the
    /// window would start appearing on the next minimize for somebody who never asked for
    /// it.</summary>
    [Fact]
    public void LoadingAStoredProfileRunsThePromotion()
    {
        WriteStoredProfile(_ => { });

        var loaded = AppSettings.Load();

        Assert.True(loaded.HudStatsPromoted);
        Assert.Equal(["kills"], loaded.MiniStats);
        Assert.DoesNotContain("Damage", loaded.DisabledBreakouts);
        Assert.Contains("Healing", loaded.DisabledBreakouts);
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
    /// migration changed, so the next one reads a promoted profile and must leave it
    /// alone. Trap 55's shape reached through the real file rather than through a
    /// hand-called chain.</summary>
    [Fact]
    public void TheSecondLaunchDoesNotCloseWindowsThePlayerKept()
    {
        WriteStoredProfile(s => s.MiniStats = ["kills", "dps", "hps"]);

        AppSettings.Load();
        var second = AppSettings.Load();
        var third = AppSettings.Load();

        Assert.DoesNotContain("Damage", second.DisabledBreakouts);
        Assert.DoesNotContain("Healing", second.DisabledBreakouts);
        Assert.DoesNotContain("Damage", third.DisabledBreakouts);
        Assert.DoesNotContain("Healing", third.DisabledBreakouts);
    }

    /// <summary>A profile that has never been saved is born promoted and keeps the Damage
    /// window a fresh install has always had — the case `hadFile` exists to tell apart.
    /// It is asserted HERE, through Load, because the whole value of the distinction is
    /// that Load can actually make it.</summary>
    [Fact]
    public void AProfileWithNoFileAtAllIsBornPromoted()
    {
        Delete();

        var loaded = AppSettings.Load();

        Assert.True(loaded.HudStatsPromoted);
        Assert.Equal(["kills"], loaded.MiniStats);
        Assert.DoesNotContain("Damage", loaded.DisabledBreakouts);
        Assert.Contains("Healing", loaded.DisabledBreakouts);
    }
}
