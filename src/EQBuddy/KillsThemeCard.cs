using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Composes the KILLS &amp; DROPS theme's inline card — Inline themes PR 2, the same
/// shape as <see cref="ProgressThemeCard"/> (which carries the fuller commentary).
///
/// Kills is the Full room: the rate and counts that move while you fight, which is the
/// #228 job — read them on the widget without opening anything. **Drops is the GLANCE
/// room** (Bevel, Helm-signed 2026-08-22): thirteen creature headings is the tallest body
/// in the set, and it READS THE WIKI, which an expanded card over a running game must not
/// do. Its one line is <see cref="CreatureTheme.DropsGlance"/> and the ⧉ is the door.
/// </summary>
internal static class KillsThemeCard
{
    public static ThemeCardView<CreatureTab> Build(
        Expander section,
        ContentControl bodyHost,
        ContentControl popOutHost,
        ThemeHost<CreatureTab> host,
        Func<KillsCardView> newKills,
        Action popOut,
        Action bringWindowForward,
        double bodyMaxHeight)
    {
        KillsCardView? kills = null;
        KillsCardView Kills() => kills ??= newKills();

        var card = new ThemeCardView<CreatureTab>(
            section, bodyHost, host,
            tabs: s => CreatureTheme.Tabs(s)
                .Select(t => new ThemeCardTab<CreatureTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: CreatureSurface.InlineModeFor,
            bodyFor: _ => Kills().Body,
            glanceFor: (_, s) => CreatureTheme.DropsGlance(s),
            render: (tab, s) =>
            {
                if (tab == CreatureTab.Kills) Kills().Render(s);
                // Drops never reaches here — a Glance tab has no body to paint.
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Kills & Drops window — the full lists, on your second monitor",
            bodyMaxHeight: bodyMaxHeight);

        popOutHost.Content = card.PopOutButton;
        return card;
    }
}
