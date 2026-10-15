using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// **The built <c>EQBuddy.exe --mcp</c>, launched the way an MCP client launches it**
/// (DRA-1446 D1, <c>docs/plans/DRA-1446.md</c>). This is the measurement decision 1 rests on:
/// that a WinExe reads and writes the pipes its parent hands it, so the release can keep
/// shipping ONE signed executable. <c>CharacterCardMcpTests</c> proves the protocol in memory;
/// only the real exe can prove the ENTRY POINT — that <c>--mcp</c> leaves before WPF exists,
/// opens no window, writes no settings, and exits when the client closes stdin.
///
/// <para>Launched through <see cref="IsolatedLaunchPolicy"/> (trap 69) into a throwaway profile
/// with one seeded card. Nothing here asserts the screen: the window claim is the process's own
/// <see cref="Process.MainWindowHandle"/>, never the dump hook, which <c>--mcp</c> never reaches.
/// Build first — <c>dotnet build EQBuddy.slnx -c Release</c> (trap 64).</para>
/// </summary>
public class CharacterCardMcpLaunchTests
{
    private static readonly TimeSpan ExitBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Handshake and a read over real pipes; then stdin closes and the process is gone within
    /// five seconds, having never owned a window or written <c>settings.json</c>.
    /// Prove-fail: drop the <c>--mcp</c> branch from <c>Program.Main</c> — the app starts its
    /// window, answers nothing on stdout and the first reply times out.
    /// </summary>
    [Fact]
    public void TheBuiltExeServesACardOverStdioAndLeavesNoWindowAndNoSettings()
    {
        Assert.True(File.Exists(AppHarness.ExePath),
            $"EQBuddy.exe not built at {AppHarness.ExePath}. Run `dotnet build EQBuddy.slnx -c Release` first.");
        // Taken although --mcp opens nothing: if it ever did, it must not land on a desktop a
        // shoot batch is photographing (trap 61).
        ScreenLock.Acquire();

        var root = Directory.CreateTempSubdirectory("eqbuddy-e2e-mcp-").FullName;
        try
        {
            var profile = Directory.CreateDirectory(Path.Combine(root, "profile")).FullName;
            var folder = Directory.CreateDirectory(Path.Combine(profile, CharacterCardWriter.FolderName)).FullName;
            var cardText = CharacterCardPresentation.Title + "\n\n" + CharacterCardPresentation.Provenance
                           + "\n\n- Character: Testchar\n- Server: test\n- Marker: e2e-mcp-card\n";
            File.WriteAllText(Path.Combine(folder, CharacterCardWriter.FileNameFor("Testchar", "test")!), cardText);
            var before = Entries(profile);

            var psi = new ProcessStartInfo(AppHarness.ExePath, "--mcp")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardInputEncoding = new UTF8Encoding(false),
                CreateNoWindow = true,
            };
            IsolatedLaunchPolicy.PinChildProfile(psi.Environment, profile);
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                var init = Exchange(process, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"e2e","version":"1"}}}""");
                Assert.Equal(1, init.GetProperty("id").GetInt32());
                Assert.Equal(CharacterCardMcp.ProtocolVersion, init.GetProperty("result").GetProperty("protocolVersion").GetString());
                process.StandardInput.Write("""{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n");

                var list = Exchange(process, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_character_cards","arguments":{}}}""");
                Assert.Contains("\"character\":\"Testchar\"", Text(list), StringComparison.Ordinal);

                var read = Exchange(process, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"read_character_card","arguments":{"character":"testchar"}}}""");
                Assert.False(read.GetProperty("result").GetProperty("isError").GetBoolean());
                Assert.Equal(CharacterCardMcp.Labelled(cardText), Text(read));

                // Alive, served three requests, and never owned a window.
                process.Refresh();
                Assert.False(process.HasExited, "the server exited before stdin closed");
                Assert.Equal(IntPtr.Zero, process.MainWindowHandle);

                var closedAt = Stopwatch.StartNew();
                process.StandardInput.Close();
                Assert.True(process.WaitForExit(ExitBudget),
                    $"EQBuddy.exe --mcp was still running {ExitBudget.TotalSeconds:0} s after stdin closed");
                Assert.True(closedAt.Elapsed <= ExitBudget);
                Assert.Equal(0, process.ExitCode);
                Assert.Equal("", process.StandardOutput.ReadToEnd());   // nothing but the three replies
                Assert.Equal("", stderr.Result);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }

            // Wrote nothing: no settings.json, no lock, no error.log, no history — the profile is
            // exactly the one card the test seeded.
            Assert.False(File.Exists(Path.Combine(profile, "settings.json")), "--mcp wrote settings.json");
            Assert.Equal(before, Entries(profile));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static List<string> Entries(string dir) =>
        [.. Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

    /// <summary>Writes one request line and reads one reply line, with a timeout so a server
    /// that never answers (an exe that started its window instead) fails the row rather than
    /// hanging the suite.</summary>
    private static JsonElement Exchange(Process process, string request)
    {
        process.StandardInput.Write(request + "\n");
        process.StandardInput.Flush();
        var line = process.StandardOutput.ReadLineAsync();
        Assert.True(line.Wait(ReplyTimeout), $"no reply within {ReplyTimeout.TotalSeconds:0} s to: {request}");
        Assert.NotNull(line.Result);
        return JsonDocument.Parse(line.Result!).RootElement.Clone();
    }

    private static string Text(JsonElement response) =>
        response.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
}
