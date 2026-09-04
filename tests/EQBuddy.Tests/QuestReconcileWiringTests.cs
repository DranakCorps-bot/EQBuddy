namespace EQBuddy.Tests;

/// <summary>
/// #241's reconcile only runs when a widget wires <c>SessionStats.InventoryDumpResolver</c>
/// beside <c>QuestStore</c> — SessionStats itself has no idea where the log folder or the
/// followed character live, so a lane that forgets the resolver silently keeps DasGud's bug
/// (have-counts drift from the ledger forever, because the dump is never read). No unit test
/// inside Core can see a missing call in a widget's constructor; this is the
/// <c>CompanionSnapshotArgumentTests</c> shape applied to the same class of miss.
///
/// **It scanned two lanes until E-2 (2026-09-04), and the reason survives the second one.**
/// The original framing was #122/#152 — a fix that lands on one lane and never reaches the
/// other. What is left is the half that was never about lanes: a constructor is where
/// wiring goes missing, nothing in Core can see it, and the widget's constructor is edited
/// on every theme change.
/// </summary>
public sealed class QuestReconcileWiringTests
{
    private static readonly (string Ui, string File)[] Widgets =
    [
        ("WPF", Path.Combine("EQBuddy", "MainWindow.xaml.cs")),
    ];

    [Theory]
    [InlineData("WPF")]
    public void EveryWidgetWiresTheResolverBesideTheQuestStore(string ui)
    {
        var (_, relative) = Widgets.Single(w => w.Ui == ui);
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var path = Path.Combine(src, relative);
        Assert.True(File.Exists(path), $"{relative} moved — update this test's paths.");
        var text = File.ReadAllText(path);

        Assert.Contains("_stats.QuestStore = QuestLedger;", text);
        Assert.Contains("_stats.InventoryDumpResolver =", text);

        // Order matters not at all functionally (both are set before the watcher starts),
        // but the resolver must exist on the SAME _stats the reconcile will read from —
        // this at least proves the wiring is not dead code cut from the constructor path.
        var questStoreLine = IndexOfLine(text, "_stats.QuestStore = QuestLedger;");
        var resolverLine = IndexOfLine(text, "_stats.InventoryDumpResolver =");
        var watcherLine = IndexOfLine(text, "_watcher = new LogWatcher(_stats);");
        Assert.True(questStoreLine < watcherLine && resolverLine < watcherLine,
            $"{ui}: the resolver or QuestStore is wired AFTER the watcher starts tailing — " +
            "a dump announced before that point would reconcile against nothing.");
    }

    /// <summary>The resolver must hand back the SAME finder <c>InventoryFile.FindLatest</c>
    /// has always used for the Gear half — a second inventory reader is exactly the
    /// trap-4 shape (one fact, two sources) this feature exists to avoid.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void TheResolverReusesInventoryFileFindLatest(string ui, string file)
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var text = File.ReadAllText(Path.Combine(src, file));

        var resolverAt = text.IndexOf("_stats.InventoryDumpResolver =", StringComparison.Ordinal);
        Assert.True(resolverAt >= 0, $"{ui}: resolver assignment not found.");
        var snippet = text.Substring(resolverAt, Math.Min(400, text.Length - resolverAt));
        Assert.Contains("InventoryFile.FindLatest", snippet);
        Assert.Contains("OutputfileKind.Inventory", snippet);
    }

    public static TheoryData<string, string> Rows()
    {
        var data = new TheoryData<string, string>();
        foreach (var (ui, file) in Widgets) data.Add(ui, file);
        return data;
    }

    private static int IndexOfLine(string text, string needle)
    {
        var i = text.IndexOf(needle, StringComparison.Ordinal);
        return i < 0 ? int.MaxValue : i;
    }
}
