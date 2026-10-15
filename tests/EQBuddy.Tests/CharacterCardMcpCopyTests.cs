using System.Reflection;
using System.Text.Json;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The Options "Connect an AI app" block's words and snippet** (DRA-1446 D2,
/// <c>docs/plans/DRA-1446.md</c>; card DRA-1839). The snippet is the line a player pastes into an
/// AI app, so the claim worth testing is that the app can READ it: it parses as JSON, it names
/// the exe it was given, and its argument is the one <c>Program</c> answers to. The launched-app
/// half — that the box draws the exe actually running — is the E2E
/// <c>CharacterCardMcpLaunchTests.TheOptionsSnippetNamesTheExeThatIsRunning</c>.
/// </summary>
public sealed class CharacterCardMcpCopyTests
{
    /// <summary>
    /// <b>The snippet parses and names the exe and <c>--mcp</c>.</b> A path with spaces, a quote
    /// mark and a non-ASCII letter: the JSON writer escapes what JSON needs and nothing reads
    /// the backslashes back wrong. Prove-failed by building the snippet with string
    /// interpolation instead of the writer: the backslashes are not escaped and the parse
    /// throws.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Users\Zoë O'Neil\AppData\Local\EQBuddy Evolved\publish\EQBuddy.exe")]
    [InlineData(@"D:\Games\EQBuddy\EQBuddy.exe")]
    public void TheSnippetParsesAsJsonAndNamesTheExeAndTheSwitch(string exe)
    {
        var snippet = CharacterCardMcpCopy.ConfigSnippet(exe);

        using var doc = JsonDocument.Parse(snippet);
        var server = doc.RootElement.GetProperty("mcpServers").GetProperty(CharacterCardMcpCopy.ServerKey);
        Assert.Equal(exe, server.GetProperty("command").GetString());
        var args = server.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList();
        Assert.Equal(["--mcp"], args);
        // Exactly one server: the snippet registers EQBuddy and nothing else.
        Assert.Single(doc.RootElement.GetProperty("mcpServers").EnumerateObject());
    }

    /// <summary>
    /// <b>It names the RUNNING exe when handed the running exe</b> — the view passes
    /// <c>Environment.ProcessPath</c>, so this is the same call with the test host's.
    /// </summary>
    [Fact]
    public void TheSnippetNamesTheRunningProcess()
    {
        var running = Environment.ProcessPath!;
        using var doc = JsonDocument.Parse(CharacterCardMcpCopy.ConfigSnippet(running));
        Assert.Equal(running, doc.RootElement.GetProperty("mcpServers")
            .GetProperty(CharacterCardMcpCopy.ServerKey).GetProperty("command").GetString());
    }

    /// <summary>
    /// <b>One spelling of the switch</b> (trap 4): the snippet's argument and the server's
    /// constant are the same string, and <c>Program</c> reads that constant rather than its own
    /// literal. Prove-failed by giving <c>Program.McpSwitch</c> its own <c>"--mcp"</c> again.
    /// </summary>
    [Fact]
    public void ProgramReadsTheSameSwitchTheSnippetWrites()
    {
        Assert.Equal("--mcp", CharacterCardMcp.Switch);
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "src", "EQBuddy", "Program.cs"));
        Assert.Contains("McpSwitch = CharacterCardMcp.Switch;", program, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--mcp\"", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The disclosure is drawn from <see cref="CharacterCardCopy"/>, never copied</b> (plan D2,
    /// trap 4): no string constant in <see cref="CharacterCardMcpCopy"/> repeats it, and the
    /// Options view draws <c>CharacterCardCopy.Lead</c> twice — the slice-1 block and the
    /// connection block. Prove-failed by deleting the second <c>Para(CharacterCardCopy.Lead)</c>.
    /// </summary>
    [Fact]
    public void TheConnectBlockRepeatsTheDisclosureFromItsOneProducer()
    {
        var consts = typeof(CharacterCardMcpCopy)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        Assert.NotEmpty(consts);
        Assert.DoesNotContain(consts, c => c.Contains("sends it nowhere", StringComparison.Ordinal));

        var view = File.ReadAllText(Path.Combine(RepoRoot(), "src", "EQBuddy", "SettingsCharacterCardView.cs"));
        Assert.Equal(2, CountOf(view, "Para(CharacterCardCopy.Lead)"));
        Assert.Contains("CharacterCardMcpCopy.ConfigSnippet(Environment.ProcessPath", view, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string needle)
    {
        var n = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + 1, StringComparison.Ordinal))
            n++;
        return n;
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
