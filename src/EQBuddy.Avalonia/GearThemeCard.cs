using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Composes the GEAR &amp; LOOT theme's inline card for this lane — the Avalonia half of
/// Inline themes PR 2; <c>EQBuddy/GearThemeCard.cs</c> is the WPF twin. Loot, Items and
/// Wishlist are Full rooms (Loot and Items share one view, as in the window); Inventory
/// is the GLANCE (Bevel's host rule — a long list with its own filter bar must not be
/// shrink-wrapped onto a SizeToContent always-on-top panel).
/// </summary>
internal static class GearThemeCard
{
    public static ThemeCardPanel<LootTab> Build(
        Control header,
        ThemeHost<LootTab> host,
        Func<LootSurfaceSet> newSurfaces,
        Func<StatsSnapshot, IReadOnlyList<LootTabHeader>> tabs,
        Func<int?> inventoryCount,
        Action popOut,
        Action bringWindowForward)
    {
        LootSurfaceSet? surfaces = null;
        LootSurfaceSet Surfaces() => surfaces ??= newSurfaces();

        return new ThemeCardPanel<LootTab>(
            header, host,
            tabs: s => tabs(s)
                .Select(t => new ThemeCardTab<LootTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: LootSurface.InlineModeFor,
            bodyFor: tab => tab switch
            {
                LootTab.Gear => Surfaces().Gear.Body,
                _ => Surfaces().Loot.Body,   // Loot AND Items — one view, as in the window
            },
            glanceFor: (_, _) => LootTheme.InventoryGlance(inventoryCount()),
            render: (tab, s) =>
            {
                switch (tab)
                {
                    case LootTab.Gear: Surfaces().Gear.Render(); break;
                    case LootTab.Inventory: break;   // Glance never reaches here
                    default: Surfaces().Loot.Render(s); break;
                }
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Gear & Loot window — the full tabs, on your second monitor",
            bodyMaxHeight: WidgetMetrics.ThemeBodyMaxHeight);
    }
}
