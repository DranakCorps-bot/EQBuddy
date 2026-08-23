using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Raids surface: every raid target the game's own achievements list names, per zone,
/// with the personal record — witnessed kills with dates, or the imported Conqueror
/// achievement for clears from before EQBuddy. No difficulty tiers on the imported ones:
/// neither the log nor the dump names the instance tier, and a badge the data cannot back
/// would be decoration rather than information.
///
/// **Lifted out of <c>MainWindow</c> for PR A** (Fable 5, 2026-08-22), the last and largest
/// of the five Progress rooms to move. It takes the ledger as a <c>Func</c> rather than the
/// ledger itself, for the same reason its WPF twin does: the ledger is rebuilt when the
/// followed character changes, so a captured instance would go stale and start answering
/// for whoever was logged in when the window opened.
/// </summary>
/// <param name="lastImport">The widget's last <c>/outputfile achievements</c> auto-import.
/// This surface is where that report belongs — it is the one that ASKS the player to run
/// the command, in both its empty and its populated state. Wired earlier the same day,
/// after two days in which the property was written and nothing read it (trap 43); the
/// wrapper panel that first carried it in <c>MainWindow</c> dies here, which is what Fable
/// asked for so it could not survive as a second home for the report.</param>
internal sealed class RaidsCardView : IWidgetCard
{
    private readonly StackPanel _panel = new();
    private readonly StackPanel _body = new();
    private readonly Func<RaidKillLedger> ledger;
    private readonly ImportReportView _importReport;

    public RaidsCardView(Func<RaidKillLedger> ledger, Func<AutoImportOutcome?> lastImport)
    {
        this.ledger = ledger;
        // An Undo un-marks bosses, so the report has to be able to repaint the rows.
        _importReport = new ImportReportView(lastImport, RenderRows);
        // ABOVE the rows, and outside them. Outside because RenderRows clears the rows
        // panel wholesale; above because a report appended after 21 boss rows sits below
        // the fold behind a scrollbar — trap 44, caught by the second WPF screenshot.
        _body.Children.Add(_importReport.Body);
        _body.Children.Add(_panel);
    }

    public string Key => "raids";

    public Control Body => _body;

    /// <summary>"2 / 21" — what the theme's tab strip carries. Computed here so the surface
    /// and its badge cannot disagree about how many targets are cleared.</summary>
    public string Header =>
        $"{ledger().DefeatedCount()} / {RaidTargetCatalog.Default.BossCount}";

    public void Render(StatsSnapshot snapshot)
    {
        RenderRows();
        _importReport.Render();
    }

    private void RenderRows()
    {
        var defeated = ledger().DefeatedCount();
        var catalog = RaidTargetCatalog.Default;
        if (defeated == 0)
        {
            _panel.Children.Clear();
            _panel.Children.Add(CardParts.EmptyLine(
                "Nothing defeated yet — kills your log witnesses land here, and importing " +
                $"{GameCommands.OutputfileAchievements} marks clears from before EQBuddy."));
            _panel.Children.Add(CopyAchievementsCmd());
            return;
        }

        _panel.Children.Clear();
        foreach (var zone in catalog.Zones)
        {
            var records = zone.Bosses.Select(b => (Boss: b, Rec: ledger().For(b))).ToList();
            var done = records.Count(x => x.Rec is { } r && (r.Kills > 0 || r.AchievementComplete));
            _panel.Children.Add(new TextBlock
            {
                Text = $"{zone.Zone} — {done}/{zone.Bosses.Length}",
                FontSize = DesignSystem.Size(Role.Caption), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 1),
                Foreground = done == zone.Bosses.Length ? AppTheme.GoodBrush : AppTheme.AccentBrush,
            });
            foreach (var (boss, rec) in records)
            {
                var cleared = rec is { } rr && (rr.Kills > 0 || rr.AchievementComplete);
                var badge = rec?.HighestDifficulty() is { } hd ? $"D{hd} · " : "";
                var detail = rec switch
                {
                    { Kills: > 0 } k =>
                        $"{badge}{(k.Kills > 1 ? $"×{k.Kills} · " : "")}last {k.LastKill:MMM d}",
                    { AchievementComplete: true } => "cleared (from achievements)",
                    _ => "",
                };
                // A fixed lane for the mark, so the boss names line up whether or not
                // one is cleared — mirrors the WPF twin. The "·" stays as TEXT (the
                // ratchet allows it): it holds the column open and is not an icon.
                var row = new Grid { Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition(
                    new GridLength(DesignTokens.IconInlineHit)));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                if (cleared)
                {
                    var tick = DesignSystem.Icon("Check", "GoodBrush", DesignTokens.IconInline);
                    tick.HorizontalAlignment = HorizontalAlignment.Left;
                    row.Children.Add(tick);
                }
                else row.Children.Add(DesignSystem.Text(DesignTokens.TypeRole.BodySecondary, "·"));
                var bossText = new TextBlock
                {
                    Text = $"{boss}{(detail.Length > 0 ? $" — {detail}" : "")}",
                    FontSize = DesignTokens.Spec(DesignTokens.TypeRole.BodySecondary).Size,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = cleared ? AppTheme.TextBrush : AppTheme.DimBrush,
                };
                Grid.SetColumn(bossText, 1);
                row.Children.Add(bossText);
                if (rec is { TierKills.Count: > 0 } tk)
                    ToolTip.SetTip(row, "Kills by difficulty: " + string.Join(" · ",
                        new[] { "d4", "d3", "d2", "d1", "d0", "open", "instance", "unknown" }
                            .Where(k => tk.TierKills.ContainsKey(k))
                            .Select(k => $"{(k.StartsWith('d') ? k.ToUpperInvariant() : k)} ×{tk.TierKills[k]}"))
                        + (tk.Kills > tk.TierKills.Values.Sum()
                            ? $" · {tk.Kills - tk.TierKills.Values.Sum()} earlier kill(s) predate tier tracking"
                            : ""));
                _panel.Children.Add(row);
            }
        }
        _panel.Children.Add(new TextBlock
        {
            Text = "Kills count when your log sees the boss die; import " +
                $"{GameCommands.OutputfileAchievements} to mark older clears.",
            FontSize = DesignSystem.Size(Role.Metadata), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0),
            Foreground = AppTheme.DimBrush,
        });
        _panel.Children.Add(CopyAchievementsCmd());
    }

    /// <summary>The Raids surface names the achievements dump in both its empty and its
    /// populated state, so both offer the one-click copy (David, 2026-08-14) — every
    /// surface that names a command hands it over without retyping.</summary>
    private static Button CopyAchievementsCmd() => DesignSystem.CopyCommandButton(
        GameCommands.OutputfileAchievements,
        "Copies the command — paste it into the game's chat and the game " +
        "writes its achievements dump beside its own folders; right-click → " +
        "Data & imports → Import achievements… reads it.");
}
