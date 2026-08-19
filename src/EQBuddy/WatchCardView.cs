using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Watch card (#105, wizen) — every tracked rule with its running count, the cue
/// countdown when one is in flight, and the per-item breakdown behind a fold.
///
/// Lifted out of <c>MainWindow</c> for ratchet room: that file had 79 lines of headroom
/// left and not one unreferenced method in it, so the space had to come from a surface
/// moving rather than from tidying. This one was chosen because it is 230 lines
/// concentrated in a single render — the best size-to-entanglement ratio left — and
/// because its behaviour was pinned in <c>tests/EQBuddy.E2E</c> first (watchRows,
/// watchStrip, watchSort). The WPF layer has no unit tests (docs/TestPlan.md §5), so
/// that assertion is the only thing standing between this move and a silent regression.
///
/// It reaches the widget through <see cref="ICardContext"/> and nothing else. The two
/// things it genuinely cannot answer from a snapshot are passed in: the alert
/// scheduler's due map, and the settings it both reads and writes. Notably it does NOT
/// need a "repaint the whole widget" call — a fold or a sort click invalidates its own
/// signature and re-renders from <see cref="ICardContext.CurrentSnapshot"/>, which is
/// the seam working as intended.
/// </summary>
internal sealed class WatchCardView : IWidgetCard
{
    private readonly ICardContext _context;
    private readonly AppSettings _settings;
    private readonly Func<DateTime, IReadOnlyDictionary<string, DateTime>> _cuesDue;
    private readonly StackPanel _panel = new();

    public string Key => "tracked";

    public UIElement Body => _panel;

    /// <summary>Rendered row count, for the <c>EQBUDDY_EXPAND</c> dump E2E asserts on.</summary>
    public int RowCount => _panel.Children.Count;

    /// <summary>True while the sort strip is up — it appears only above two or more
    /// rules, which is exactly the kind of condition a refactor drops silently.</summary>
    public bool SortStripShown => _panel.Children.Count > 0 && _panel.Children[0] is WrapPanel;

    /// <param name="cuesDue">The alert scheduler's "when does each rule's cue fire"
    /// map. A card cannot derive this from its snapshot — the cue is scheduled by the
    /// alert path, not by the session — so it is handed in rather than reached for.</param>
    public WatchCardView(ICardContext context, AppSettings settings,
        Func<DateTime, IReadOnlyDictionary<string, DateTime>> cuesDue)
    {
        _context = context;
        _settings = settings;
        _cuesDue = cuesDue;
    }

    /// <summary>Rules whose full per-item breakdown is open. Session-scoped on purpose:
    /// the collapsed "last:" view is the designed default.</summary>
    private readonly HashSet<string> _expandedRules = new(StringComparer.Ordinal);

    /// <summary>The rebuild signature + kept TextBlocks (perf audit #14, the RenderBuffs
    /// idiom): while the signature holds, ticks update countdown / rate / age text in
    /// place instead of rebuilding the panel's element tree. Row refs are parallel to
    /// the signature's rule order.</summary>
    private string _signature = "";

    /// <param name="Cue">The cue countdown, or null when this rule has none. Its
    /// PRESENCE is part of the signature, so within the text-in-place path it never
    /// appears or vanishes — only its digits move.</param>
    private sealed record RowRefs(
        string RuleId, string RuleName, TextBlock Head, TextBlock? Cue,
        TextBlock Rate, TextBlock? LastLine);

    private readonly List<RowRefs> _rowRefs = [];

    public void Render(StatsSnapshot s)
    {
        if (_settings.TrackedRules.Count == 0)
        {
            if (_signature == "empty") return;
            _signature = "empty";
            _rowRefs.Clear();
            _panel.Children.Clear();
            _panel.Children.Add(CardParts.EmptyLine(
                "No watch rules yet — add one in Options (or pick a recent log line there)."));
            return;
        }

        var dueNow = _cuesDue(DateTime.Now);
        var orderedResults = _settings.WatchSortMode switch
        {
            "alpha" => s.Tracked.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "total" => s.Tracked.OrderByDescending(t => t.TotalQuantity).ToList(),
            // Never-matched rules sink to the bottom rather than jumbling the top.
            "recent" => s.Tracked.OrderByDescending(t => t.LastMatch ?? DateTime.MinValue).ToList(),
            _ => s.Tracked,
        };

        // A signature over everything that changes the element TREE — rule identities and
        // order, counts, last-match identity, sort mode, cue presence, and the expanded
        // per-item breakdowns. While it holds, the per-tick work is text-in-place: the
        // live cue countdown, the rates (their hour denominators move with every event),
        // and the "last: … ago" age. Anything structural (a match, a sort click, a cue
        // starting or firing, an expand toggle, a rule edit) changes the signature and
        // rebuilds exactly as before.
        var signature = _settings.WatchSortMode + "§" + string.Join("¦",
            orderedResults.Select(r =>
                $"{r.Id}|{r.Name}|{r.TotalQuantity}|{r.LastItem}|{r.Items.Count}" +
                $"|{dueNow.ContainsKey(r.Id)}|{_expandedRules.Contains(r.Id)}" +
                (_expandedRules.Contains(r.Id) && r.Items.Count > 1
                    ? "|" + string.Join(",", r.Items.Select(i => $"{i.Name}:{i.Count}"))
                    : "")));
        if (signature == _signature)
        {
            for (var i = 0; i < _rowRefs.Count && i < orderedResults.Count; i++)
            {
                var row = _rowRefs[i];
                var r = orderedResults[i];
                // The name is in the signature, so it cannot have changed here — only the
                // countdown beside it moves.
                if (row.Cue is { } cue && dueNow.TryGetValue(row.RuleId, out var due))
                    cue.Text = EQBuddy.UI.Shared.Countdown.Format(due - DateTime.Now);
                row.Rate.Text = RateLine(r);
                if (row.LastLine is { } lastLine && r.LastMatch is { } lm && r.LastItem is { } li)
                    lastLine.Text = LastLineText(li, lm);
            }
            return;
        }
        _signature = signature;
        _rowRefs.Clear();
        _panel.Children.Clear();

        // Sort strip (#105, wizen): THE chip (docs/DesignSystem.md §11.2). A WrapPanel
        // rather than a StackPanel because four pills are wider than four text links and
        // the widget is 342px at its narrowest — a horizontal stack would push the last
        // one off the card. No "sort:" caption: one strip beside the list it orders does
        // not need one, and Gate 5a's first screenshot is why (SortStrip.Caption).
        if (s.Tracked.Count > 1)
        {
            var sortBar = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, Tok.SpaceXxs, Tok.SpaceXxs, 0),
            };
            var strip = new EqSegmentedStrip(sortBar);
            foreach (var option in EQBuddy.UI.Shared.SortStrip.ForWatchRules)
            {
                var picked = option.Key;
                strip.Add(option.Label, option.Key, tip: option.Tip, onClick: () =>
                {
                    _settings.WatchSortMode = picked;
                    _settings.Save();
                    Repaint();
                });
            }
            strip.Select(_settings.WatchSortMode);
            _panel.Children.Add(sortBar);
        }

        foreach (var r in orderedResults)
        {
            var head = new Grid { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // A rule with a cue counting down says so in its heading, so you can watch the
            // respawn timer you set without opening Options to remember what it was.
            //
            // The countdown is its own TextBlock beside a vector, not text inside the
            // name: the per-tick path above writes THAT rather than rebuilding the name
            // every second. The name keeps the star column and its trimming — a rule
            // called "Ancient Cyclops placeholder" beside an icon in a horizontal
            // StackPanel would be clipped with no ellipsis to say so (trap 14).
            var counting = dueNow.TryGetValue(r.Id, out var dueAt);
            var headText = new TextBlock
            {
                Text = r.Name.ToUpperInvariant(),
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            headText.Ink(counting ? "WarnBrush" : "AccentBrush");
            head.Children.Add(headText);
            TextBlock? headCountdown = null;
            if (counting)
            {
                var cue = DesignSystem.Icon("Timer", "WarnBrush", size: Tok.IconInline);
                cue.Margin = new Thickness(Tok.SpaceS, 0, Tok.SpaceXs, 0);
                Grid.SetColumn(cue, 1);
                head.Children.Add(cue);
                headCountdown = new TextBlock
                {
                    Text = EQBuddy.UI.Shared.Countdown.Format(dueAt - DateTime.Now),
                    FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, Tok.SpaceS, 0),
                };
                headCountdown.Ink("WarnBrush");
                Grid.SetColumn(headCountdown, 2);
                head.Children.Add(headCountdown);
            }
            var rate = new TextBlock
            {
                Text = RateLine(r),
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            };
            rate.Ink("DimBrush");
            Grid.SetColumn(rate, 3);
            head.Children.Add(rate);
            _panel.Children.Add(head);

            // The card leads with what just happened, not with everything that ever did
            // (asked for by an enchanter drowning in an hour of mez targets): one
            // "last:" line per rule, the full per-item breakdown behind a toggle.
            TextBlock? lastLine = null;
            if (r.LastMatch is { } lm && r.LastItem is { } li)
            {
                lastLine = new TextBlock
                {
                    Text = LastLineText(li, lm),
                    FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                    Margin = new Thickness(Tok.SpaceS, 1, 0, Tok.SpaceXxs),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                lastLine.Ink("TextBrush");
                _panel.Children.Add(lastLine);
            }
            else
            {
                var none = new TextBlock
                {
                    Text = "no matches yet",
                    FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                    Margin = new Thickness(Tok.SpaceS, 1, 0, Tok.SpaceXxs),
                };
                none.Ink("DimBrush");
                _panel.Children.Add(none);
            }
            _rowRefs.Add(new RowRefs(r.Id, r.Name, headText, headCountdown, rate, lastLine));

            if (r.Items.Count > 1) AddBreakdown(r);
        }
    }

    private void AddBreakdown(TrackedRuleResult r)
    {
        var expanded = _expandedRules.Contains(r.Id);
        if (expanded)
            foreach (var item in r.Items)
            {
                var row = new TextBlock
                {
                    Text = $"{item.Name}   ×{item.Count}",
                    FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                    Margin = new Thickness(Tok.SpaceL, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                row.Ink("TextBrush");
                _panel.Children.Add(row);
            }
        // Transparent ground, not null: a StackPanel with no background only hit-tests
        // where its children are painted, so the gaps between the chevron and the words
        // would have been click-through (trap 16).
        var toggle = new EqFoldLabel
        {
            // The look the widget's other folds wear ("Pet abilities", "All AA
            // abilities"): a dim semibold heading over the list it opens.
            Section = true,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = Brushes.Transparent,
            Margin = new Thickness(Tok.SpaceS, 0, 0, Tok.SpaceXxs),
        };
        toggle.Set(expanded, expanded ? "less" : $"all {r.Items.Count} kinds");
        var id = r.Id;
        toggle.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (!_expandedRules.Remove(id)) _expandedRules.Add(id);
            Repaint();
        };
        _panel.Children.Add(toggle);
    }

    /// <summary>Re-render from the last painted tick after the player changed one of
    /// this card's own controls. The signature is cleared first because the change is
    /// in card-local state (a fold, the sort) that the signature is computed FROM —
    /// leaving it would compare equal and paint nothing.</summary>
    private void Repaint()
    {
        _signature = "";
        Render(_context.CurrentSnapshot());
    }

    private static string RateLine(TrackedRuleResult r) =>
        $"{r.TotalQuantity} total · {r.PerHour:0.#}/hr · {r.PerActiveHour:0.#}/active hr";

    private static string LastLineText(string item, DateTime at) =>
        $"last: {item} · {FormatAge(DateTime.Now - at)} ago";

    private static string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"{Math.Max(0, (int)age.TotalSeconds)}s"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m" : $"{(int)age.TotalHours}h {age.Minutes}m";
}
