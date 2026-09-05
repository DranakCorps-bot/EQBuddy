using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Progress ROOM as the Evolved shell hosts it — the first surface moved into the
/// shell (E-3 Phase 2 PR 1), and the one Fable's plan named because
/// <see cref="MainWindow.NewProgressSurfaces"/> already exists as the trap-45 factory.
/// So this exercises the host without also having to invent an ownership seam in the
/// same diff.
///
/// **It builds its OWN instances and hands none out.** A <c>UIElement</c> has exactly one
/// parent, so a surface shared with <see cref="ProgressWindow"/> would be torn out of
/// whichever host painted it last — on WPF silently, with no exception to point at, which
/// is harder to notice than the Avalonia crash that found this (trap 45). The shell being
/// a second host for surfaces the widget still renders is precisely the condition that
/// produced it, and <c>SurfaceOwnershipTests</c> scans for the accessor shape.
///
/// **Why this is a second composition and not a view extracted from
/// <see cref="ProgressWindow"/>.** Every RULE is already shared — which tabs exist, their
/// order, their labels, their keys and their badges all come from <see cref="ProgressSurface"/>
/// (Core) and <see cref="ProgressTheme"/> (UI.Shared), and the bodies come from one
/// factory. What is duplicated is the wiring that swaps a body when a chip is clicked,
/// and it is duplicated ON PURPOSE: Bevel's signed IA RESHAPES this room for Evolved —
/// Raids leave for Live, Faction becomes Advanced — while <see cref="ProgressWindow"/> is
/// v1 chrome scheduled for retirement. Extracting a shared view now would couple the two
/// exactly where they are about to diverge, and then be unpicked one PR later.
///
/// **Scrolling belongs to the host** (trap 36), and the host here is the room's own
/// bounded <c>*</c> cell in the shell — a real overflow, not the infinite-height measure
/// that makes a child scroller swallow the wheel and scroll nothing. The tab strip stays
/// pinned above it rather than being concatenated into the scrolling body, which is what
/// trap 37 cost the Drops tab's footer.
/// </summary>
internal sealed class ProgressRoom : Grid
{
    private readonly MainWindow _main;
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();

    private ProgressTab _tab = ProgressSurface.DefaultInlineTab;

    private readonly ProgressCardView _experience;
    private readonly MoneyCardView _money;
    private readonly MotesCardView _motes;
    private readonly FactionCardView _faction;
    private readonly RaidsCardView _raids;

    /// <summary>The Wealth tab's body: the two surfaces it merges, each under its own
    /// label. Built once and kept — a tab switch must not rebuild element trees that
    /// nothing changed about.</summary>
    private readonly StackPanel _wealthBody = new();

    public ProgressRoom(MainWindow main)
    {
        _main = main;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // WRAPS, and it has to: a horizontal StackPanel measures its children with
        // INFINITE width, so a chip never reaches a boundary to wrap at and the last one
        // is simply clipped at the panel's edge — silently, with no ellipsis (trap 25,
        // itself trap 14 with chips). These badges carry real sentences.
        var strip = new WrapPanel { Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0) };
        SetRow(strip, 0);
        Children.Add(strip);
        _tabs = new EqSegmentedStrip(strip);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, Tok.SpaceM),
            Content = _body,
        };
        SetRow(scroll, 1);
        Children.Add(scroll);

        var surfaces = main.NewProgressSurfaces();
        (_experience, _money, _motes, _faction, _raids) =
            (surfaces.Experience, surfaces.Money, surfaces.Motes, surfaces.Faction, surfaces.Raids);

        _wealthBody.Children.Add(CardParts.BlockLabel("Coin", hidden: false));
        _wealthBody.Children.Add(_money.Body);
        _wealthBody.Children.Add(CardParts.BlockLabel("Motes", hidden: false));
        _wealthBody.Children.Add(_motes.Body);
    }

    /// <summary>Land on a tab by its wire key — the second half of a <c>page:room</c>
    /// address. An unknown key is left alone rather than snapped to a default: showing
    /// the wrong room silently is worse than showing the one already open.</summary>
    public void SetTab(string key)
    {
        if (ProgressSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Render(_main.CurrentSnapshot());
    }

    public void Render(StatsSnapshot s)
    {
        BuildTabs(s);
        // Only the ACTIVE tab paints, and its body is swapped in rather than all four
        // being stacked and hidden — a hidden StackPanel still measures on every layout
        // pass, and the Raids list is 29 rows of it.
        _body.Content = _tab switch
        {
            ProgressTab.Wealth => _wealthBody,
            ProgressTab.Faction => _faction.Body,
            ProgressTab.Raids => _raids.Body,
            _ => _experience.Body,
        };
        switch (_tab)
        {
            case ProgressTab.Wealth: _money.Render(s); _motes.Render(s); break;
            case ProgressTab.Faction: _faction.Render(s); break;
            case ProgressTab.Raids: _raids.Render(s); break;
            default: _experience.Render(s); break;
        }
    }

    /// <summary>Build the strip from Core's <see cref="ProgressSurface"/> and UI.Shared's
    /// <see cref="ProgressTheme"/> — the same two sources the Progress window and EQBuddy
    /// Mobile read, so three surfaces cannot end up naming four different tabs or
    /// reporting four different numbers (#184, #210).</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        foreach (var header in ProgressTheme.Tabs(
                     s, _main.ProgressDingUnlockCount(s),
                     _raids.DefeatedCount, RaidTargetCatalog.Default.BossCount))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                Render(_main.CurrentSnapshot());
            });
        }
        // Chips first, THEN the selection — colouring before rebuilding leaves every
        // fresh chip unstyled, including the selected one, which is the whole signal.
        _tabs.Select(_tab);
    }

    /// <summary>The room's facts for the <c>EQBUDDY_EXPAND</c> dump, under
    /// <c>shellProgress*</c> so they sit beside — and can be compared with — the
    /// <c>progress*</c> keys <see cref="ProgressWindow"/> reports from the same surfaces.
    /// Two hosts of one room is exactly where a silent divergence would live, and the
    /// WPF layer has no unit tests to catch it any other way.</summary>
    public string DebugFacts() =>
        $"shellProgressTab={ProgressSurface.KeyFor(_tab)} " +
        $"shellProgressTabs={_tabs.Count} " +
        $"shellProgressRaidsRows={_raids.RowCount} " +
        $"shellProgressMotesRows={_motes.RowCount} " +
        $"shellProgressFaction={_faction.RowCount} " +
        $"shellProgressSkills={_experience.SkillRows}";
}
