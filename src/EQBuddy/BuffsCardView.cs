using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>What the Buffs card cannot answer from its own snapshot: the buff SET (which is
/// assembled from the character's classes and stored per character) and the level-up
/// suggestions that ride the same card. Handed in as one value, the way
/// <c>ProgressSurfaceSet</c> is, so the card's whole reach back into the widget is one
/// name — and so <see cref="ICardContext"/> does not grow five methods it would then owe
/// every other card.</summary>
internal sealed record BuffCardServices(
    Func<StatsSnapshot, List<string>> AssembledSet,
    Func<List<string>, List<BuffState>, DateTime, List<BuffSetEntryState>> Evaluate,
    Func<StatsSnapshot, List<string>, List<BuffSuggestion>> Suggestions,
    Action<BuffSuggestion> Accept,
    Action<BuffSuggestion> Dismiss);

/// <summary>
/// THE BUFFS CARD (#120, Frankthetankk; re-shaped for density in OE-4) — every buff believed
/// active on you as a wrapped grid of chips, the buff set's honesty line, and the level-up
/// suggestions.
///
/// **Lifted out of <c>MainWindow</c> in the same change that re-shaped it**, because that is
/// the only way this change could land: the <c>MainWindow*</c> ratchet had ONE line of
/// headroom (4,221 of 4,222) after OE-1's own lift, so a density rewrite in place was not
/// available at any size. It is the <c>WatchCardView</c> move for the same reason — a
/// surface concentrated in one render, whose behaviour is pinned in <c>tests/EQBuddy.E2E</c>
/// (<c>buffChips</c>, <c>buffWrapped</c>, <c>buffRows</c>) before the move, since the WPF
/// layer has no unit tests (docs/TestPlan.md §5) and that assertion is the only thing
/// standing between a lift and a silent regression.
///
/// **The chips are a <see cref="WrapPanel"/>, never a horizontal <see cref="StackPanel"/>**
/// (trap 25): a stack measures with infinite width in the stacking direction, so the chips
/// past the card's edge would be silently CLIPPED — no ellipsis, no overflow, simply not on
/// screen, which is exactly how a quarter of the Progress tab strip went missing.
///
/// What the card draws, and what stays with the window: the roster, the set line and the
/// suggestion rows are here; the card's VISIBILITY, its expander and its header count are
/// the window's, because a lifted view brings its content and leaves the chrome behind
/// (trap 15).
/// </summary>
internal sealed class BuffsCardView : IWidgetCard
{
    private readonly AppSettings _settings;
    private readonly BuffTracker _tracker;
    private readonly BuffCardServices _services;
    private readonly StackPanel _panel = new();
    private readonly WrapPanel _chips = new();

    public string Key => "buffs";

    public UIElement Body => _panel;

    /// <summary>Chips drawn this tick — the roster's own count, which is the number the
    /// density change is ABOUT. Not the same as <c>buffsActive</c>: expiring-only mode shows
    /// fewer, and the dump carries both so "no chip because nothing is running" and "no chip
    /// because you asked to be told late" stay two readings.</summary>
    public int ChipCount => _chips.Children.Count;

    /// <summary>Everything under the chips — the set line and the suggestion rows — plus the
    /// chip container itself. The empty state replaces the chips with one line, so a
    /// zero-chip card is not a zero-row card.</summary>
    public int RowCount => _panel.Children.Count;

    /// <summary>The roster is a wrapping grid rather than a column or — the trap-25 failure —
    /// a horizontal stack that clips silently. The TYPE is read off the live tree rather than
    /// taken from this file's own field declaration, because "the chips are in a WrapPanel"
    /// is precisely the claim a later refactor drops in silence, and a fact that cannot come
    /// back false is not a fact (trap 39).</summary>
    public bool Wrapped =>
        _panel.Children.Count > 0 && _panel.Children[0] is WrapPanel { Children.Count: > 0 };

    public BuffsCardView(AppSettings settings, BuffTracker tracker, BuffCardServices services)
    {
        _settings = settings;
        _tracker = tracker;
        _services = services;
    }

    /// <summary>Per-tick chip parts, parallel to the roster's own order: while the SET of
    /// buffs has not changed, a tick writes countdown text and gauge width in place instead
    /// of rebuilding the panel (the idiom this card invented and the Watch card borrowed).
    /// </summary>
    private readonly List<HudChip.Live> _live = [];
    private string _signature = "";

    /// <summary>The card's chips are all one family in a 320-unit widget — see
    /// <see cref="HudChip.Look"/> for why that is a parameter and not a second renderer.
    /// </summary>
    private static readonly HudChip.Look CardLook =
        new(BuffRosterPresentation.NameMaxWidth, ShowIcon: false);

    /// <summary>Force a rebuild on the next render — an edit to the set has to show at once,
    /// and a change that waits for the next tick reads as a silent no-op.</summary>
    public void Invalidate() => _signature = "";

    public void Render(StatsSnapshot snap)
    {
        var now = DateTime.Now;
        var active = _tracker.ActiveCount > 0 ? _tracker.Snapshot(now) : [];

        // The buff set's honesty line (#120) is evaluated against the FULL active list,
        // before the expiring-only filter — the set cares what's up, not what's shown.
        var set = _services.AssembledSet(snap);
        List<BuffSetEntryState> setStates = set.Count > 0 ? _services.Evaluate(set, active, now) : [];
        var missing = setStates.Where(s => s.Status == BuffSetStatus.Missing).Select(s => s.Spell).ToList();
        var notSeen = setStates.Where(s => s.Status == BuffSetStatus.NotSeen).Select(s => s.Spell).ToList();
        var expiring = setStates.Where(s => s.Status == BuffSetStatus.Expiring).Select(s => s.Spell).ToList();
        // Stage 3 (#120): new-buff-unlock suggestions ride the same card — rows only while
        // suggestions exist, never a popup (David's UX rules).
        var suggestions = _services.Suggestions(snap, set);

        var shown = BuffRosterPresentation.Shown(
            active, now, _settings.BuffTimersExpiringOnly, _settings.BuffWarnSeconds, out var quiet);
        var signature = BuffRosterPresentation.Signature(shown, quiet, missing, notSeen, expiring,
            suggestions.Select(x => x.Spell + "@" + x.Class));
        if (signature == _signature)
        {
            // Same chips, newer clocks: text and gauge move, the element tree does not.
            var entries = BuffRosterPresentation.Chips(shown, now, _settings.BuffWarnSeconds);
            for (var i = 0; i < _live.Count && i < entries.Count; i++)
                HudChip.Tick(_live[i], entries[i]);
            return;
        }
        _signature = signature;
        _live.Clear();
        _chips.Children.Clear();
        _panel.Children.Clear();

        if (shown.Count == 0)
        {
            _panel.Children.Add(CardParts.EmptyLine(BuffRosterPresentation.EmptyLine(
                _settings.BuffTimersExpiringOnly, quiet, _settings.BuffWarnSeconds)));
            AddSetLine(missing, notSeen, expiring);
            AddSuggestionRows(suggestions);
            return;
        }
        foreach (var entry in BuffRosterPresentation.Chips(shown, now, _settings.BuffWarnSeconds))
        {
            _chips.Children.Add(HudChip.Build(entry, out var live, look: CardLook));
            _live.Add(live);
        }
        _panel.Children.Add(_chips);
        AddSetLine(missing, notSeen, expiring);
        AddSuggestionRows(suggestions);
    }

    /// <summary>The "missing:" line (#120): appears ONLY when a set buff isn't cleanly up,
    /// and disappears entirely when everything is. Three visibly different claims: missing
    /// (seen fading, or timer ran out), expiring (inside the warn window), and not seen (no
    /// landing line this session — it may be up from before the log was watched; we can't
    /// know, and never pretend to).</summary>
    private void AddSetLine(List<string> missing, List<string> notSeen, List<string> expiring)
    {
        if (missing.Count == 0 && notSeen.Count == 0 && expiring.Count == 0) return;
        var line = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, Tok.SpaceXs, 0, 0),
            ToolTip = "Your buff set. missing = EQBuddy saw it fade this session (or its timer ran out). "
                + "expiring = still up, inside the warn window. "
                + "not seen = no landing line this session — it may still be up from before "
                + "EQBuddy was watching; the log can't tell, so this stays a separate state. "
                + "The set is assembled from your active classes' picks plus (any class). "
                + "Edit it in Options → Alerts & chips, or in the Buff set breakout.",
        };
        void Add(string label, List<string> names, string brush, bool italic = false)
        {
            if (names.Count == 0) return;
            if (line.Inlines.Count > 0)
            {
                var sep = new Run(" · ");
                sep.SetResourceReference(TextElement.ForegroundProperty, "DimBrush");
                line.Inlines.Add(sep);
            }
            var run = new Run(label + string.Join(", ", names));
            if (italic) run.FontStyle = FontStyles.Italic;
            run.SetResourceReference(TextElement.ForegroundProperty, brush);
            line.Inlines.Add(run);
        }
        // No warning sign in front of "missing". The three labels are parallel states of one
        // line and only this one wore a glyph, so it read as a fourth channel that said
        // nothing the word and the warn ink did not already say — and it is a box on a Wine
        // prefix. An InlineUIContainer could carry a vector here, but not one that stays
        // aligned through a wrap.
        Add("missing: ", missing, "WarnBrush");
        Add("expiring: ", expiring, "AccentBrush");
        Add("not seen: ", notSeen, "DimBrush", italic: true);
        _panel.Children.Add(line);
    }

    /// <summary>New-buff-unlock suggestion rows (#120 stage 3, Frankthetankk): one dim row
    /// per genuinely new buff line the ding made available — ✓ adds it to the gaining class's
    /// bucket, ✕ dismisses for good (per character). Present only while suggestions exist;
    /// never auto-added — the player decides everything.</summary>
    private void AddSuggestionRows(List<BuffSuggestion> suggestions)
    {
        foreach (var sug in suggestions)
        {
            var row = new Grid { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new TextBlock
            {
                Text = $"new buff at your level — add {sug.Spell} to {sug.Class}?",
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Your level-up made this buff available (the Progress card's "
                    + "\"New at level\" list). The tick adds it to that class's set bucket; "
                    + "the cross never asks again for this character. A new RANK of a buff "
                    + "already in your set folds into the same slot and is never "
                    + "suggested — only genuinely new lines appear here.",
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            row.Children.Add(text);
            row.Children.Add(Tick("Check", "GoodBrush",
                $"Add {sug.Spell} to your {sug.Class} set", 1, () => _services.Accept(sug)));
            row.Children.Add(Tick("Close", "DimBrush",
                "Dismiss — never suggest this buff for this character again", 2,
                () => _services.Dismiss(sug)));
            _panel.Children.Add(row);
        }
    }

    /// <summary>Accept / dismiss on a buff-suggestion row.
    ///
    /// A real <see cref="DesignSystem.InlineIconButton"/> rather than a click-handled glyph:
    /// the tick and the cross were TextBlocks, which hit-test across their whole layout rect,
    /// and the drawn strokes of a vector do not — that is #211 exactly, on a pair of controls
    /// where a missed click either adds a buff you didn't want or fails to silence a
    /// suggestion you're tired of. The button also makes them keyboard-reachable, which the
    /// TextBlocks never were.</summary>
    private static Button Tick(string icon, string brush, string tip, int column, Action act)
    {
        var button = DesignSystem.InlineIconButton(icon, tip, (_, _) => act(), brush);
        Grid.SetColumn(button, column);
        return button;
    }
}
