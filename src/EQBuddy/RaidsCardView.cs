using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Raids surface: every raid target the game's own achievements list names, per
/// zone, with the personal record — witnessed kills with dates, or the imported
/// Conqueror achievement for clears from before EQBuddy. No difficulty tiers on
/// purpose: neither the log nor the dump names the instance tier, and a badge the data
/// can't back would be decoration, not information.
///
/// Lifted out of <c>MainWindow</c> for the PROGRESS THEME (docs/Themes.md), and it was
/// the last of that theme's five cards still inline — Gate 5b had already put the other
/// four on the <see cref="IWidgetCard"/> seam, which is why Progress went first of the
/// six themes rather than Alerts, which the plan listed first.
///
/// It takes the ledger as a <c>Func</c> rather than the widget: the ledger is rebuilt
/// when the followed character changes, so a captured instance would go stale and start
/// answering for whoever was logged in when the window opened. Everything else it needs
/// is a catalog or a token, so it reaches for <see cref="ICardContext"/> not at all —
/// its rows name BOSSES, not items, and there is nothing here to click through to.
/// </summary>
internal sealed class RaidsCardView(Func<RaidKillLedger> ledger) : IWidgetCard
{
    private readonly StackPanel _panel = new();

    public string Key => "raids";

    public UIElement Body => _panel;

    /// <summary>Rendered row count, for the <c>EQBUDDY_EXPAND</c> dump E2E asserts on.
    /// Pinned at 29 on the fixture ledger BEFORE this lift — 6 zone headings, 21 boss
    /// rows, the provenance note and the copy button — so the move has a number to be
    /// checked against rather than a claim to be believed.</summary>
    public int RowCount => _panel.Children.Count;

    /// <summary>"2 / 21" — what the card header carried, and what the theme's tab strip
    /// carries now. Computed here so the surface and its badge cannot disagree about how
    /// many targets are cleared.</summary>
    public string Header =>
        $"{ledger().DefeatedCount()} / {RaidTargetCatalog.Default.BossCount}";

    /// <summary>How many targets are cleared — for the launcher card's one-line summary,
    /// which has to carry the glance five card headers used to.</summary>
    public int DefeatedCount => ledger().DefeatedCount();

    public void Render(StatsSnapshot snapshot)
    {
        var raids = ledger();
        var defeated = raids.DefeatedCount();
        var catalog = RaidTargetCatalog.Default;

        _panel.Children.Clear();
        if (defeated == 0)
        {
            _panel.Children.Add(CardParts.EmptyLine(
                "Nothing defeated yet — kills your log witnesses land here, and importing " +
                $"{GameCommands.OutputfileAchievements} marks clears from before EQBuddy."));
            _panel.Children.Add(CopyAchievementsCmd());
            return;
        }

        foreach (var zone in catalog.Zones)
        {
            var records = zone.Bosses.Select(b => (Boss: b, Rec: raids.For(b))).ToList();
            var done = records.Count(x => x.Rec is { } r && (r.Kills > 0 || r.AchievementComplete));
            var heading = DesignSystem.Text(Tok.TypeRole.Caption,
                $"{zone.Zone} — {done}/{zone.Bosses.Length}");
            heading.FontWeight = FontWeights.SemiBold;
            heading.Margin = new Thickness(0, Tok.SpaceXs, 0, 1);
            heading.Ink(done == zone.Bosses.Length ? "GoodBrush" : "AccentBrush");
            _panel.Children.Add(heading);

            foreach (var (boss, rec) in records)
            {
                var cleared = rec is { } rr && (rr.Kills > 0 || rr.AchievementComplete);
                // The badge is the highest difficulty PROVEN by a witnessed kill —
                // instance tiers come off the zone-enter line (#109 data). Kills from
                // before tiers existed carry no tier and earn no badge; honesty over
                // flattery.
                var badge = rec?.HighestDifficulty() is { } hd ? $"D{hd} · " : "";
                var detail = rec switch
                {
                    { Kills: > 0 } k =>
                        $"{badge}{(k.Kills > 1 ? $"×{k.Kills} · " : "")}last {k.LastKill:MMM d}",
                    { AchievementComplete: true } => "cleared (from achievements)",
                    _ => "",
                };
                // Two columns rather than a tick typed into the string: the mark is a
                // vector, and the boss name keeps the star column so a long one trims
                // with an ellipsis instead of being clipped beside it (trap 14). The
                // uncleared state keeps its "·" — that is a typographic mark holding the
                // column open, not an icon, and the ratchet allows it as one.
                var row = new Grid { Margin = new Thickness(Tok.SpaceS, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Tok.IconInlineHit),
                });
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                });
                if (cleared)
                {
                    var tick = DesignSystem.Icon("Check", "GoodBrush", size: Tok.IconInline);
                    tick.HorizontalAlignment = HorizontalAlignment.Left;
                    row.Children.Add(tick);
                }
                else
                {
                    row.Children.Add(DesignSystem.Text(Tok.TypeRole.BodySecondary, "·"));
                }
                var bossText = DesignSystem.Text(Tok.TypeRole.BodySecondary,
                    $"{boss}{(detail.Length > 0 ? $" — {detail}" : "")}");
                bossText.TextTrimming = TextTrimming.CharacterEllipsis;
                bossText.Ink(cleared ? "TextBrush" : "DimBrush");
                Grid.SetColumn(bossText, 1);
                row.Children.Add(bossText);
                if (rec is { TierKills.Count: > 0 } tk)
                    row.ToolTip = "Kills by difficulty: " + string.Join(" · ",
                        new[] { "d4", "d3", "d2", "d1", "d0", "open", "instance", "unknown" }
                            .Where(k => tk.TierKills.ContainsKey(k))
                            .Select(k => $"{(k.StartsWith('d') ? k.ToUpperInvariant() : k)} ×{tk.TierKills[k]}"))
                        + (tk.Kills > tk.TierKills.Values.Sum()
                            ? $" · {tk.Kills - tk.TierKills.Values.Sum()} earlier kill(s) predate tier tracking"
                            : "");
                _panel.Children.Add(row);
            }
        }

        var note = DesignSystem.Text(Tok.TypeRole.Metadata,
            $"Kills count when your log sees the boss die; import {GameCommands.OutputfileAchievements} " +
            "to mark older clears.");
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        note.Ink("DimBrush");
        _panel.Children.Add(note);
        _panel.Children.Add(CopyAchievementsCmd());
    }

    /// <summary>Wherever the surface names the command, the command is one click (David,
    /// 2026-08-14) — copy, paste in game chat, then Import achievements… reads the file
    /// the game wrote.</summary>
    private static Button CopyAchievementsCmd()
    {
        var b = Theming.WireCopyCommand(Theming.Button(""), GameCommands.OutputfileAchievements);
        b.FontSize = Tok.Spec(Tok.TypeRole.Caption).Size;
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        b.ToolTip = "Copies the command — paste it into the game's chat and the game " +
            "writes its achievements dump beside its own folders; right-click → " +
            "Data & imports → Import achievements… reads it.";
        return b;
    }
}
