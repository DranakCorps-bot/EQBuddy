using System.IO;
using System.Text;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The entry point, hand-written so <c>--mcp</c> can leave before WPF exists** (DRA-1446 D1,
/// <c>docs/plans/DRA-1446.md</c> decision 2). <c>App.xaml</c> is still the generated
/// <c>ApplicationDefinition</c> and its generated <c>Main</c> still compiles; the csproj's
/// <c>StartupObject</c> picks this one instead.
///
/// <para><b><c>--mcp</c> starts no window and touches no state</b>: it never constructs
/// <see cref="App"/>, so there is no single-instance lock, no settings load or save (trap 13),
/// no log watcher, companion server, telemetry clock or history database. It serves
/// <see cref="CharacterCardMcp"/> over the inherited stdin/stdout — a WinExe still reads and
/// writes the pipes its parent hands it — and returns when stdin closes. stdout carries the
/// protocol and nothing else; a failure goes to stderr.</para>
///
/// <para>Every other launch does exactly what the generated <c>Main</c> did, so
/// <c>App.OnStartup</c> sees the same arguments it always has.</para>
/// </summary>
public static class Program
{
    public const string McpSwitch = "--mcp";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, McpSwitch, StringComparison.OrdinalIgnoreCase)))
            return ServeMcp();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static int ServeMcp()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            using var input = new StreamReader(Console.OpenStandardInput(), utf8);
            using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
            CharacterCardMcp.Serve(input, output, CharacterCardMcp.DefaultFolder, CancellationToken.None);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                using var error = new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true };
                error.WriteLine($"EQBuddy --mcp: {ex.Message}");
            }
            catch { /* no stderr to report to */ }
            return 1;
        }
    }
}
