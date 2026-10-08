using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// **The character card file, from a launched app** (DRA-1288 D2, <c>docs/plans/DRA-1288.md</c>).
/// <c>CharacterCardWriterTests</c> proves the writer's rules with a stated clock; only the real
/// EQBuddy.exe can prove the WIRING — that the once-a-second tick reaches the writer, that it
/// reads the stores the app actually keeps, and that the file lands inside the profile.
///
/// <para>Nothing here asserts the screen. The dump says how many files were published and where
/// the followed one is; the test reads THAT file and asserts the relationship between a log line
/// and a line of the card.</para>
/// </summary>
public class CharacterCardTests
{
    /// <summary>
    /// **Off by default, and off writes nothing.** Prove-failed by defaulting
    /// <c>AppSettings.CharacterCardEnabled</c> to true: <c>characterCardEnabled</c> reads 1.
    /// </summary>
    [Fact]
    public void TheDefaultProfileWritesNoCard()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("characterCardEnabled", 0, "the character card to be OFF on a default profile");
        // A few ticks after the log is read, still nothing — off reads nothing and writes nothing.
        var tick = app.DumpValue("tick");
        app.WaitForDumpAtLeast("tick", tick + 3, "three more ticks with the card off");
        Assert.Equal(0, app.DumpValue("characterCardWrites"));
        Assert.Equal("none", app.DumpText("characterCardPath"));
        Assert.False(Directory.Exists(Path.Combine(app.ProfileDir, CharacterCardWriter.FolderName)));
    }

    /// <summary>
    /// **Switched on: a ding in the log reaches the card's level line.** Seed the setting on,
    /// wait for the first card, append a level-up, wait for <c>characterCardWrites</c> to
    /// advance, then read the file the dump names and assert its level line says 12.
    /// Prove-failed by dropping the runtime's tick call from <c>MainWindow.RefreshUi</c>:
    /// <c>characterCardWrites</c> never leaves 0.
    /// </summary>
    [Fact]
    public void ADingInTheLogReachesTheCardFile()
    {
        using var app = new AppHarness(settings => settings.CharacterCardEnabled = true);
        app.Launch();

        app.WaitForDumpAtLeast("characterCardWrites", 1, "the first card to be written once the log is read");
        var path = Uri.UnescapeDataString(app.DumpText("characterCardPath"));
        // Isolation (trap 68): the card is inside this run's throwaway profile.
        Assert.StartsWith(Path.Combine(app.ProfileDir, CharacterCardWriter.FolderName),
            path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CharacterCardWriter.FileNameFor(AppHarness.Character, AppHarness.Server),
            Path.GetFileName(path));
        Assert.DoesNotContain("Level 12", LevelLine(path), StringComparison.Ordinal);

        var before = app.DumpValue("characterCardWrites");
        app.AppendLogLines("You have gained a level! Welcome to level 12!");

        app.WaitForDumpAtLeast("characterCardWrites", before + 1, "the card to be rewritten after the ding");
        Wait.Until(() => LevelLine(path).Contains("Level 12", StringComparison.Ordinal),
            TimeSpan.FromSeconds(30), "the card's level line to say 12",
            () => $"level line: {LevelLine(path)}");
    }

    private static string LevelLine(string path) =>
        WholeFilePublish.Read(path).Split('\n')
            .FirstOrDefault(l => l.StartsWith("- Level: ", StringComparison.Ordinal)) ?? "(no level line)";
}
