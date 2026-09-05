using System.Text;
using System.Text.RegularExpressions;

namespace EQBuddy.E2E;

/// <summary>
/// The screen mutex, checked from the side a green run never reaches.
///
/// **It launches nothing** — like <c>IconGeometryTests</c>, it lives in this project
/// because the thing it is about lives here. The suite's own run holds the real lock by
/// the time any of this executes, so every case here works on a throwaway path; what is
/// under test is the CONTRACT (the share modes, the refusal, the holder line), not the
/// desktop.
///
/// **Two of these are cross-file, and that is the point.** The lock is a rendezvous
/// between a C# harness and a PowerShell script that share nothing but a filename and a
/// share mode. Neither half has a compiler that can see the other, which is trap 53's
/// shape exactly — a literal agreed in two places, with one of them free to drift for six
/// days before anybody notices.
/// </summary>
public class ScreenLockTests
{
    private static string ShootScript => File.ReadAllText(
        Path.Combine(AppHarness.RepoRoot, "scripts", "shoot.ps1"));

    /// <summary>The one fact both halves must agree on. `shoot.ps1` builds the path with
    /// `Join-Path ([IO.Path]::GetTempPath()) 'eqbuddy-screen.lock'`; this asserts the
    /// filename is still that, and that C# resolves the same %TEMP% root — an agreement no
    /// compiler on either side can check.</summary>
    [Fact]
    public void TheLockPathIsTheSameFileShootPs1Opens()
    {
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "eqbuddy-screen.lock"),
            ScreenLock.Path);
        Assert.Matches(
            @"Join-Path \(\[IO\.Path\]::GetTempPath\(\)\) 'eqbuddy-screen\.lock'",
            ShootScript);
    }

    /// <summary>The share mode is half the contract: the holder opens Write/Read so a
    /// refused seat can still READ the holder line. Open it exclusively on either side and
    /// the refusal becomes anonymous — technically correct, and useless to the person who
    /// has to decide whether to wait.</summary>
    [Fact]
    public void ShootPs1StillOpensTheLockWriteWithShareRead()
    {
        Assert.Matches(
            @"\[IO\.FileMode\]::OpenOrCreate,\s*[\r\n ]*\[IO\.FileAccess\]::Write, \[IO\.FileShare\]::Read",
            ShootScript);
    }

    [Fact]
    public void TakingItTwiceRefusesTheSecondCaller()
    {
        var path = ThrowawayLockPath();
        using var first = ScreenLock.Take(path);

        var refused = Assert.Throws<IOException>(() => ScreenLock.Take(path));

        // The pid is the actionable half — "the screen is busy" is not something a reader
        // can do anything with, and "pid 21804, four minutes ago" is.
        Assert.Contains($"pid {Environment.ProcessId}", refused.Message);
        Assert.Contains("tests/EQBuddy.E2E", refused.Message);
        Assert.Contains("EQBUDDY_SCREEN_FORCE=1", refused.Message);
    }

    /// <summary>The holder line must be readable WHILE it is held — that is the whole
    /// reason for FileShare.Read, and it is the case a test that released first would miss.
    /// Read here the way `shoot.ps1` reads it (`Get-Content -Raw`), not the way this
    /// assembly writes it.</summary>
    [Fact]
    public void TheHolderLineIsReadableByAnotherProcessWhileHeld()
    {
        var path = ThrowawayLockPath();
        using var held = ScreenLock.Take(path);

        using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var line = new StreamReader(read, Encoding.UTF8).ReadToEnd().Trim();

        Assert.StartsWith($"pid {Environment.ProcessId} | ", line);
        Assert.EndsWith(" | tests/EQBuddy.E2E", line);
        // ASCII only, deliberately: `shoot.ps1` reads this back with Get-Content, which
        // under Windows PowerShell 5.1 decodes as the ANSI code page (trap 54). A holder
        // line that arrives as mojibake is a line nobody trusts.
        Assert.All(line, c => Assert.InRange(c, ' ', '~'));
    }

    /// <summary>A previous run's stale bytes must not be reported as the live holder. The
    /// file is never deleted (deleting it is a race in itself), so the take truncates —
    /// `SetLength(0)` on both sides.</summary>
    [Fact]
    public void TakingItOverwritesAPreviousRunsHolderLineRatherThanAppending()
    {
        var path = ThrowawayLockPath();
        File.WriteAllText(path, new string('x', 4096));

        using (var _ = ScreenLock.Take(path)) { }

        var line = File.ReadAllText(path);
        Assert.DoesNotContain("xxxx", line);
        Assert.StartsWith($"pid {Environment.ProcessId} | ", line);
    }

    /// <summary>Releasing gives it back — which is what makes the process-exit hook, and a
    /// second run of this suite, work at all.</summary>
    [Fact]
    public void ReleasingItLetsTheNextCallerTakeIt()
    {
        var path = ThrowawayLockPath();
        ScreenLock.Take(path).Dispose();
        using var second = ScreenLock.Take(path);
        Assert.True(second.CanWrite);
    }

    /// <summary>`AppHarness.Launch` must ASK — the guard is the call, and nothing else in
    /// the suite would fail if the line were deleted. Trap 20's shape: the thing to look
    /// for is what is no longer there.</summary>
    [Fact]
    public void LaunchTakesTheScreenBeforeItStartsTheApp()
    {
        // Comments stripped first, and that is not fastidiousness: the prove-fail run for
        // this test commented the call out, and a raw text scan went on finding the words
        // and reported the guard intact. A check that a disabled call satisfies is trap
        // 34's shape — it reads as coverage while being blind to the way it will actually
        // be lost.
        var harness = Uncommented(File.ReadAllText(
            Path.Combine(AppHarness.RepoRoot, "tests", "EQBuddy.E2E", "AppHarness.cs")));
        var launch = harness.IndexOf("public void Launch()", StringComparison.Ordinal);
        Assert.True(launch >= 0, "AppHarness.Launch no longer exists under that name");

        var acquire = harness.IndexOf("ScreenLock.Acquire();", launch, StringComparison.Ordinal);
        var start = harness.IndexOf("Process.Start(psi)", launch, StringComparison.Ordinal);
        Assert.True(acquire >= 0, "AppHarness.Launch no longer takes the screen lock");
        Assert.True(acquire < start,
            "AppHarness.Launch takes the screen lock AFTER starting the app — the window " +
            "it is guarding is already on screen by then.");
    }

    /// <summary>Trap 57, held open. The suite shares one desktop, so it must not run its
    /// classes in parallel — and the constraint belongs at the assembly, not in a
    /// hand-kept list of `[Collection]` attributes that stops covering the set the day a
    /// sixth class is added (trap 30). `ShellHostTests` is the class that proved it: it
    /// launches a real always-on-top app and carried no attribute.</summary>
    [Fact]
    public void TheSuiteIsSerializedAtTheAssemblyRatherThanPerClass()
    {
        var info = Uncommented(File.ReadAllText(
            Path.Combine(AppHarness.RepoRoot, "tests", "EQBuddy.E2E", "AssemblyInfo.cs")));
        Assert.Matches(
            @"\[assembly: *CollectionBehavior\(DisableTestParallelization *= *true\)\]", info);
    }

    /// <summary>Line comments removed, so a commented-out guard reads as the absence it
    /// is. Crude on purpose — these two files carry no string literal holding a `//`.</summary>
    private static string Uncommented(string source) => Regex.Replace(source, @"//.*", "");

    /// <summary>
    /// The rendezvous itself, across the process boundary it actually spans: PowerShell
    /// opens the file with `shoot.ps1`'s own call, and this side must be refused by it and
    /// able to READ who holds it.
    ///
    /// **The two regex checks above are necessary and not sufficient.** They prove the two
    /// halves still SAY the same thing; only this proves the operating system agrees — that
    /// Write/Read on one side and Write/Read on the other really do exclude, and that the
    /// stamp written by one runtime is legible to the other. A guard between two languages
    /// that has never been run between two languages is a hypothesis.
    /// </summary>
    [Fact]
    public void APowerShellHolderRefusesThisSideAndIsNamedInTheRefusal()
    {
        var path = ThrowawayLockPath();
        // Opens exactly as shoot.ps1 does, stamps it, then blocks on stdin so this side
        // controls the release — no sleep, so nothing here is a bet on timing.
        var script =
            "$s = [IO.File]::Open('" + path.Replace("'", "''") + "', " +
            "[IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::Read); " +
            "$b = [Text.Encoding]::UTF8.GetBytes(\"pid $PID | $(Get-Date -Format o) | " +
            "scripts/shoot.ps1\"); $s.Write($b, 0, $b.Length); $s.Flush(); " +
            "Write-Host 'HELD'; [Console]::ReadLine() | Out-Null; $s.Dispose()";

        using var holder = StartPowerShell(script);
        Assert.Equal("HELD", holder.StandardOutput.ReadLine()?.Trim());
        try
        {
            var refused = Assert.Throws<IOException>(() => ScreenLock.Take(path));
            Assert.Contains($"pid {holder.Id} | ", refused.Message);
            Assert.Contains("scripts/shoot.ps1", refused.Message);
        }
        finally
        {
            holder.StandardInput.WriteLine();
            holder.WaitForExit(15_000);
            if (!holder.HasExited) holder.Kill(entireProcessTree: true);
        }

        // And it comes back: the holder released, so this side can take it.
        using var mine = ScreenLock.Take(path);
        Assert.True(mine.CanWrite);
    }

    /// <summary>pwsh 7 is what the scripts assume; Windows PowerShell 5.1 runs this
    /// four-line open identically and is always present, so a machine without pwsh gets
    /// the check rather than a silent skip (a skipped guard reads as coverage — trap 34).
    /// The `shoot.ps1` a real collision comes from may be running under either.</summary>
    private static System.Diagnostics.Process StartPowerShell(string script)
    {
        foreach (var host in new[] { "pwsh", "powershell" })
        {
            var psi = new System.Diagnostics.ProcessStartInfo(host)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);
            try { if (System.Diagnostics.Process.Start(psi) is { } p) return p; }
            catch (System.ComponentModel.Win32Exception) { }   // not on PATH; try the next
        }
        throw new InvalidOperationException(
            "Neither pwsh nor powershell is on PATH — this repo's scripts require one " +
            "(see CLAUDE.md, 'Tooling notes'), and the screen lock's other half is a script.");
    }

    /// <summary>A per-test file under the test host's temp dir, cleaned by the OS. Never
    /// the real lock: this suite is holding that one by the time any of these run.</summary>
    private static string ThrowawayLockPath() =>
        Path.Combine(Path.GetTempPath(), $"eqbuddy-screen-test-{Guid.NewGuid():N}.lock");
}
