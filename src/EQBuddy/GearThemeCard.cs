using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Composes the GEAR &amp; LOOT theme's inline card — Inline themes PR 2, the same shape
/// as <see cref="ProgressThemeCard"/> (which carries the fuller commentary).
///
/// Loot, Items and Wishlist are Full rooms — Loot capped by the shared body height,
/// Items and Loot sharing one view exactly as they share <c>_loot.Body</c> in the window.
/// **Inventory is the GLANCE room** (Bevel's host rule, Helm-signed 2026-08-22): a long
/// list with its own filter bar must not be shrink-wrapped onto a SizeToContent
/// always-on-top panel. Its one line is <see cref="LootTheme.InventoryGlance"/> and the
/// ⧉ is the door.
/// </summary>
internal static class GearThemeCard
{
    public static ThemeCardView<LootTab> Build(
        Expander section,
        ContentControl bodyHost,
        ContentControl popOutHost,
        ThemeHost<LootTab> host,
        Func<LootCardView> newLoot,
        Func<GearCardView> newGear,
        Func<StatsSnapshot, IReadOnlyList<LootTabHeader>> tabs,
        Func<int?> inventoryCount,
        Action popOut,
        Action bringWindowForward,
        double bodyMaxHeight)
    {
        LootCardView? loot = null;
        LootCardView Loot() => loot ??= newLoot();
        GearCardView? gear = null;
        GearCardView Gear() => gear ??= newGear();

        var card = new ThemeCardView<LootTab>(
            section, bodyHost, host,
            tabs: s => tabs(s)
                .Select(t => new ThemeCardTab<LootTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: LootSurface.InlineModeFor,
            bodyFor: tab => tab switch
            {
                LootTab.Gear => Gear().Body,
                _ => Loot().Body,   // Loot AND Items — one view, as in the window
            },
            glanceFor: (_, _) => LootTheme.InventoryGlance(inventoryCount()),
            render: (tab, s) =>
            {
                switch (tab)
                {
                    case LootTab.Gear: Gear().Render(); break;
                    case LootTab.Inventory: break;   // Glance never reaches here
                    default: Loot().Render(s); break;
                }
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Gear & Loot window — the full tabs, on your second monitor",
            bodyMaxHeight: bodyMaxHeight);

        popOutHost.Content = card.PopOutButton;
        return card;
    }
}
