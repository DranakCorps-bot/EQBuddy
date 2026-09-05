using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// ONE CHICKLET on the HUD chip row — icon, name, countdown and the gauge along its bottom
/// edge.
///
/// Lifted out of <c>SpawnChipsWindow.Rebuild</c> and <c>MezChipsWindow.Rebuild</c> in
/// Surface A / SA-2, where it existed TWICE: two near-copies of one renderer, which the mez
/// window's own comment named as the mechanism behind #122 and #152 ("a near-copy here is
/// how #122 and #152 happened"). Reused, not rebuilt — the geometry, the padding, the corner
/// radius, the brush keys and the trimming are the numbers the two windows shipped.
///
/// **What differed between the two is not flattened, it is asked for**: the DUE flip and the
/// gauge direction come from <see cref="HudChipRow"/>, which owns the family traits and is
/// unit-tested with no window.
///
/// **The icon column is a Grid, not a StackPanel** (trap 14): a stack measures with infinite
/// width in the stacking direction, so a long chip name would clip against the panel edge
/// with no ellipsis rather than trim against the countdown.
/// </summary>
internal static class HudChip
{
    /// <summary>The live parts of a built chicklet — what the per-second tick writes to
    /// when the row's SET of chips has not changed and a rebuild would be wasted work (and
    /// a flicker). The gauge froze at whatever the last REBUILD saw until this was fixed on
    /// both windows; keeping the pair together is what stops that returning.</summary>
    internal sealed record Live(TextBlock Countdown, Grid? Track, Border? Fill);

    /// <summary>Longest chip name before it trims — the number both retired windows
    /// used.</summary>
    private const double NameMaxWidth = 180;

    /// <param name="onClick">Left-click. Null leaves the chicklet inert to a single click,
    /// which is what a fight chip is: the DRAG the two windows carried here died with free
    /// placement (the row is slaved to the HUD and has no position of its own).</param>
    /// <param name="onDoubleClick">Left double-click, when the family has one.</param>
    /// <param name="onDismiss">Right-click. Null means this chip is not dismissible.</param>
    public static Border Build(HudChipEntry entry, out Live live,
        Action? onClick = null, Action? onDoubleClick = null, Action? onDismiss = null)
    {
        var chip = entry.Chip;

        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // chip.Icon is an IconPaths NAME, not a glyph: this is the one surface a player
        // watches mid-pull, and on the Wine prefixes where "⏳"/"💤"/"🐌" do not render it
        // told its three kinds apart with three identical boxes (#148, #166).
        var kind = DesignSystem.Icon(chip.Icon, "TextBrush", size: Tok.IconInline);
        kind.Margin = new Thickness(0, 0, Tok.SpaceXs, 0);
        row.Children.Add(kind);

        var name = new TextBlock
        {
            Text = chip.Name, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = NameMaxWidth,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        var countdown = new TextBlock
        {
            Text = HudChipRow.FaceText(entry),
            FontSize = 11, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        countdown.SetResourceReference(TextBlock.ForegroundProperty,
            chip.IsDue ? "WarnBrush" : "AccentBrush");
        Grid.SetColumn(countdown, 2);
        row.Children.Add(countdown);

        // The countdown made visual (2026-08-11): a progress track along the chicklet's
        // bottom edge. Spawn FILLS with elapsed time, the fight family DRAINS the remaining
        // share like a buff bar — HudChipRow.GaugeShare owns which, and answers null when
        // there is no known duration so the track hides rather than lying.
        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition());
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.Children.Add(row);

        Grid? track = null;
        Border? fill = null;
        if (HudChipRow.GaugeShare(entry) is { } share)
        {
            track = new Grid { Height = 2.5, Margin = new Thickness(0, 3, 0, 0) };
            var trackBg = new Border { CornerRadius = new CornerRadius(1.25) };
            trackBg.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
            track.Children.Add(trackBg);
            fill = new Border
            {
                CornerRadius = new CornerRadius(1.25),
                HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
            };
            // A DUE spawn chip's bar goes solid in the BAD ink, exactly as it did; every
            // other state is the accent, with the warn tint reserved for the border.
            fill.SetResourceReference(Border.BackgroundProperty,
                chip.IsDue && HudChipRow.FlipsToDue(entry.Family) ? "BadBrush" : "AccentBrush");
            track.Children.Add(fill);
            track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * share);
            Grid.SetRow(track, 1);
            host.Children.Add(track);
        }

        var border = new Border
        {
            Child = host,
            ToolTip = Tip(chip, onDoubleClick is not null, onDismiss is not null),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(8, 3, 8, 4),
            // The row is HORIZONTAL now, so the margin that separated stacked chicklets
            // separates them side by side instead. Same 3 units, turned ninety degrees.
            Margin = new Thickness(0, 0, 3, 0),
            BorderThickness = new Thickness(1),
            Tag = entry,
        };
        border.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        border.SetResourceReference(Border.BorderBrushProperty,
            chip.IsDue ? "WarnBrush" : "BorderBrush");

        if (onClick is not null || onDoubleClick is not null)
            border.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2 && onDoubleClick is not null)
                {
                    onDoubleClick();
                    e.Handled = true;
                    return;
                }
                if (e.ClickCount == 1 && onClick is not null)
                {
                    onClick();
                    e.Handled = true;
                }
            };
        if (onDismiss is not null)
            border.MouseRightButtonUp += (_, e) => { e.Handled = true; onDismiss(); };

        live = new Live(countdown, track, fill);
        return border;
    }

    /// <summary>Update a built chicklet in place for a tick that changed no chip's identity.
    /// The gauge ticks with the countdown — it froze at whatever the last REBUILD saw on
    /// both windows once (audit finding 14), which is why this writes both.</summary>
    public static void Tick(Live live, HudChipEntry entry)
    {
        live.Countdown.Text = HudChipRow.FaceText(entry);
        if (live.Track is { } track && live.Fill is { } fill && HudChipRow.GaugeShare(entry) is { } share)
            fill.Width = Math.Max(0, track.ActualWidth * share);
    }

    /// <summary>The hover text, with whatever gestures this chicklet actually has appended.
    /// Naming a gesture the chip does not carry is the "tick box that lies" (the two windows
    /// hard-coded their own suffix, and the mez one carried none at all).</summary>
    private static string Tip(SpawnChip chip, bool hasDoubleClick, bool dismissible)
    {
        var tip = chip.Detail;
        if (hasDoubleClick) tip += "\nDouble-click: the zone's camp list";
        if (dismissible) tip += "\nRight-click: dismiss";
        return tip;
    }
}
