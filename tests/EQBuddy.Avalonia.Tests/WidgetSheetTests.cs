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

    /// <summary>**Pin the palette, or the capture is not of the theme you asked for.**
    /// `AppTheme`'s brushes are static singletons shared by every test in this collection,
    /// and `AppThemeTests.EveryCatalogThemeAppliesCleanly` walks the whole catalog — so
    /// whichever theme ran last leaks into whatever renders next. The first companion
    /// capture came back in someone else's turquoise while `settings.json` said
    /// ParchmentBrass: a real palette, correctly applied, and not the one under review.
    /// Same shape as trap 23 — a picture of a state that is genuinely rendered and is not
    /// the state the shot is about.</summary>
    private static void PinTheme() => AppTheme.Apply("ParchmentBrass");

    [AvaloniaFact]
    public void WriteWidgetSheet()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        PinTheme();
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

    /// <summary>A picture of the EQBuddy Mobile pairing window (#208), which is entirely
    /// new on this lane and is the one surface here with a control that is neither text
    /// nor a box — the QR code a phone has to scan.
    ///
    /// It turns the host ON first, on a port of its own. A pairing window with the
    /// feature off is an intro paragraph and a tick box: a real state, and one that says
    /// nothing about the surface underneath it — trap 22, the same reason `tracked-card`
    /// seeds rules. Opt-in like its sibling above, so `check.ps1` never binds a socket.
    ///
    /// **Predicted before running** (trap 23): a code, a URL beginning
    /// <c>http://</c> and ending <c>#</c>+token, "No device connected yet.", and one
    /// tick box per entry in <c>CompanionSurfaces.All</c>, all ticked — a fresh profile
    /// hides nothing.</summary>
    [AvaloniaFact]
    public void WriteCompanionSheet()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        PinTheme();
        var main = new MainWindow();
        main.Show();
        // Port 0, not the shipped 47859: the server resolves an ephemeral port and reports
        // the real one back through PairingUrl (`CompanionServer.Start`), which is the
        // same trick `CompanionServerTests` uses. A fixed port would collide with the
        // developer's own EQBuddy Mobile if it happens to be on — and a bind failure here
        // photographs as a bug in the window rather than as a busy port.
        main.Settings.CompanionPort = 0;
        main.Companion.SetEnabled(true);

        var window = new CompanionWindow(main.Companion);
        window.Show();
        Assert.True(main.Companion.Running, main.Companion.LastError ?? "host did not start");

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var dir = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
                  ?? Path.Combine(Path.GetTempPath(), "eqbuddy-widget");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "avalonia-companion.png");
        frame!.Save(path);
        Assert.True(File.Exists(path), $"No sheet written to {path}");

        main.Companion.SetEnabled(false);   // give the port back before the next test
        window.Close();
        main.Close();
    }
}
