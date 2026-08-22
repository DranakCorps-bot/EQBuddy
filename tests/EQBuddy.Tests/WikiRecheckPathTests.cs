using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The re-check must not delete the cache it may need to fall back on (#226).
///
/// Found by Fable 5's H4 last-look of the executed diff, AFTER it shipped in 1.99.1.
/// Both windows called <c>Forget</c> and then <c>LookupAsync(bypassCache: true)</c>.
/// <c>LookupAsync</c> reads the cache at the top, so the delete left the offline fallback
/// nothing to return: a failed re-check came back <c>Offline</c> instead of
/// <c>StaleCache</c>, the memo stored it, <c>Classify</c> turned it into <c>Unknown</c>,
/// and a lit ✦ vanished while the pack row dropped to Pending. That is the exact failure
/// the plan's #217 paragraph forbade, on the surface built to honour it.
///
/// The Core contract was right the whole time and <c>AFailedBypassReturnsTheStaleRead…</c>
/// passed throughout, because it never called <c>Forget</c> first — the window defeated a
/// correct Core. So the guard belongs HERE, on the call sites, and it is a source scan for
/// the same reason <see cref="CompanionSnapshotArgumentTests"/> is one: the WPF layer has
/// no unit tests at all (docs/TestPlan.md §5), and the bug lives entirely in which calls a
/// window makes in what order.
///
/// Reachable only with the wiki unreachable, which is why neither the suite nor the staged
/// screenshot saw it — and why a review of the executed diff is worth its cost (H4).
/// </summary>
public sealed class WikiRecheckPathTests
{
    [Fact]
    public void NeitherWindowDeletesTheCacheOnTheRecheckPath()
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

        foreach (var relative in new[]
        {
            Path.Combine("EQBuddy", "MainWindow.xaml.cs"),
            Path.Combine("EQBuddy.Avalonia", "MainWindow.cs"),
        })
        {
            var path = Path.Combine(src, relative);
            Assert.True(File.Exists(path), $"{relative} moved — update this test's paths.");
            var text = File.ReadAllText(path);

            // The re-check exists at all: a guard that passes because the feature is gone
            // would be worse than no guard (trap 34).
            Assert.Contains("RecheckMobLookup", text);
            // Each window spells the bypass its own way — WPF through its own wrapper's
            // `bypass:`, Avalonia straight through to Core's `bypassCache:`. Assert the
            // FACT (a re-check asks past the cache), not one spelling of it.
            Assert.True(text.Contains("bypass: true") || text.Contains("bypassCache: true"),
                $"{relative}: nothing on the re-check path asks past the cache — the "
                + "feature is gone, which a guard must never read as passing (trap 34).");

            // …and nowhere in either file does the re-check delete the cache first.
            var body = Body(text, "RecheckMobLookup");
            Assert.False(body.Contains("Forget("),
                $"{relative}: RecheckMobLookup calls Forget — that deletes the file the "
                + "offline fallback reads, so a failed re-check reports Offline and the ✦ "
                + "it was meant to refresh disappears (#226, found by H4 after 1.99.1). "
                + "A bypass overwrites the file on success; the delete buys nothing.");
        }
    }

    /// <summary>The braces-balanced body of the named method, so the assertion reads the
    /// re-check and not the whole 4k-line file.</summary>
    private static string Body(string text, string method)
    {
        var at = text.IndexOf($"void {method}(", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{method} not found — update this test.");
        var open = text.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[open..(i + 1)];
        }
        throw new Xunit.Sdk.XunitException($"{method} body is unbalanced — update this test.");
    }
}
