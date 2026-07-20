using EQBuddy.Core;

namespace EQBuddy.Tests;

public class EqConfigTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eqbuddy-test-").FullName;
    private string LogsDir => Path.Combine(_root, "Logs");
    private string IniPath => Path.Combine(_root, "eqclient.ini");

    public EqConfigTests() => Directory.CreateDirectory(LogsDir);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void EnableLoggingFlipsLogAndPreservesEverythingElse()
    {
        File.WriteAllLines(IniPath,
        [
            "[Defaults]",
            "Log=0",
            "SomeSetting=5",
            "[Options]",
            "LogOutOverParcelLimitConfirm=1",
        ]);

        Assert.True(EqConfig.EnsureLoggingEnabled(LogsDir, ignoreGameCheck: true));

        var lines = File.ReadAllLines(IniPath);
        Assert.Contains("Log=1", lines);
        Assert.Contains("SomeSetting=5", lines);
        Assert.Contains("LogOutOverParcelLimitConfirm=1", lines);   // "Log"-prefixed key untouched
        Assert.DoesNotContain("Log=0", lines);
    }

    [Fact]
    public void EnableLoggingIsIdempotent()
    {
        File.WriteAllLines(IniPath, ["[Defaults]", "Log=1"]);
        Assert.False(EqConfig.EnsureLoggingEnabled(LogsDir, ignoreGameCheck: true));
    }

    [Fact]
    public void TruncatesOnlyStaleLogs()
    {
        var stale = Path.Combine(LogsDir, "eqlog_Old_freeport.txt");
        var fresh = Path.Combine(LogsDir, "eqlog_New_qeynos.txt");
        File.WriteAllText(stale, new string('x', 5000));
        File.WriteAllText(fresh, "recent line");
        File.SetLastWriteTime(stale, DateTime.Now.AddHours(-2));

        var count = EqConfig.TruncateStaleLogs(LogsDir, TimeSpan.FromMinutes(60), ignoreGameCheck: true);

        Assert.Equal(1, count);
        Assert.Equal(0, new FileInfo(stale).Length);
        Assert.Equal("recent line", File.ReadAllText(fresh));
    }
}
