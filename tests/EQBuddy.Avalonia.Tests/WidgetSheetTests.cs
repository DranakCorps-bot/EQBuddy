using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using EQBuddy.Core;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// A picture of the Linux/macOS widget, with the cards a change touched actually open.
///
/// **The Avalonia lane had no way to LOOK at its own widget.** `scripts/shoot.ps1` drives
/// the real `EQBuddy.exe` and is Windows-only, so every screenshot-review lesson this repo
/// has paid for — the Gate 2 clipped row (trap 14), the Gate 4 invisible strips (trap 15),
/// the fold headings that rendered as body text (trap 19) — was learned on the WPF side and
/// could not be checked on the side that actually ships to Wine prefixes. Render tests
/// assert that text and controls EXIST; they cannot see that something looks wrong.
///
/// `IconSheetTests` is the precedent and this is the same shape: opt-in, because it writes
/// a file.
///
///   dotnet test tests/EQBuddy.Avalonia.Tests/EQBuddy.Avalonia.Tests.csproj -c Release \
///     --filter FullyQualifiedName~WidgetSheet -e EQBUDDY_SHOOT=1 -e EQBUDDY_SHOOT_OUT=&lt;dir&gt;
///
/// It seeds a snapshot rather than a log: the point is the CARDS, and a card with no rows
/// is a one-line empty state that proves nothing about the rows underneath (trap 22).
/// </summary>
[Collection("avalonia")]
public class WidgetSheetTests : IDisposable
{
    private readonly string _profile =
        Directory.CreateTempSubdirectory("eqbuddy-sheet-").FullName;

    /// <summary>**Isolate the profile, or the picture is of the developer's live session.**
    /// The first run of this test photographed a real character name and server in the
    /// title bar, which is the same failure `scripts/shoot.ps1` has on Windows when the
    /// real EQBuddy is left running — and it would have committed that name to the repo.
    /// A capture surface needs the isolation MORE than an assertion does, because its
    /// whole output is a picture of whatever profile it found.
    ///
    /// Avalonia's EQBUDDY_EXPAND takes only "1" (all cards); the WPF one grew card keys
    /// for Gate 4 and this side never did.</summary>
    public WidgetSheetTests()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", "1");
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              { "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass" }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public void WriteWidgetSheet()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        var window = new MainWindow();
        window.Show();

        var rule = new TrackedRule { Name = "Respawn", Pattern = "placeholder" };
        window.Settings.TrackedRules.Add(rule);
        window.Settings.ShowPetAbilities = true;
        window.Settings.ShowAllAAs = true;

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            CombatSeconds = 120,
            Procs = [("Lifetap Strike", 7, 4200L)],
            PetAbilities = [new SourceDamage("Bite", 12, 4200)],
            AaAbilities =
            [
                new AaAbilityInfo("Spell Casting Mastery", 3, DateTime.Now),
                new AaAbilityInfo("Natural Durability", 1, DateTime.Now),
            ],
            Tracked =
            [
                new TrackedRuleResult(rule.Name, 3,
                    [new NameCount("Haste", 2), new NameCount("Clarity", 1)],
                    3, 3, DateTime.Now, DateTime.Now, "Haste", rule.Id),
            ],
        }, new Dictionary<string, DateTime> { [rule.Id] = DateTime.Now.AddMinutes(8) });

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var dir = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
                  ?? Path.Combine(Path.GetTempPath(), "eqbuddy-widget");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "avalonia-widget.png");
        frame!.Save(path);
        Assert.True(File.Exists(path), $"No sheet written to {path}");

        window.Close();
    }
}
