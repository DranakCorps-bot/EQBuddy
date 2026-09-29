using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **"How long does this buff last?"** — the player's own length for one buff (#954), opened
/// by double-clicking its chip on the HUD row or on the Buffs card. Every word and every
/// decision is <see cref="BuffLengthEditor"/>'s; this is only the window.
///
/// Its own activatable Window rather than a popup on the chip, because the HUD row is
/// <c>ShowActivated = false</c> and a text box in a window that never takes focus cannot be
/// typed into. Topmost, so it is not opened behind the game it is about.
/// </summary>
internal sealed class BuffLengthWindow : Window
{
    /// <summary>Open the editor for the buff currently showing under <paramref name="label"/>.
    /// A buff that faded between the click and here opens nothing — there is no landing left
    /// to say anything true about.</summary>
    public static void Open(BuffTracker tracker, string label, Action? changed = null)
    {
        var buff = tracker.Snapshot(DateTime.Now).FirstOrDefault(
            b => b.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
        if (buff is null) return;
        var window = new BuffLengthWindow(tracker, buff, changed);
        window.Show();
        window.Activate();
    }

    private BuffLengthWindow(BuffTracker tracker, BuffState buff, Action? changed)
    {
        Title = BuffLengthEditor.Title(buff);
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var typed = tracker.PlayerLengthFor(buff.DurationKey);
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        Content = root;

        var head = DesignSystem.Text(Role.TitleWindow, BuffLengthEditor.Title(buff));
        head.Ink("AccentBrush");
        root.Children.Add(head);
        root.Children.Add(Line(BuffLengthEditor.DerivedLine(buff), "TextBrush", Tok.SpaceS));
        if (BuffLengthEditor.TypedLine(typed) is { Length: > 0 } typedLine)
            root.Children.Add(Line(typedLine, "TextBrush", Tok.SpaceXs));

        var box = new TextBox
        {
            Text = BuffLengthEditor.Prefill(buff, typed),
            FontSize = Tok.Spec(Role.Body).Size,
            Margin = new Thickness(0, Tok.SpaceM, 0, 0),
            Padding = new Thickness(Tok.SpaceS, Tok.SpaceXxs, Tok.SpaceS, Tok.SpaceXxs),
            ToolTip = BuffLengthEditor.Grammar,
        };
        box.SetResourceReference(Control.BackgroundProperty, "ComboBoxBrush");
        box.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        root.Children.Add(box);
        root.Children.Add(Line(BuffLengthEditor.Grammar, "DimBrush", Tok.SpaceXs));
        var error = Line("", "WarnBrush", Tok.SpaceXs);
        error.Visibility = Visibility.Collapsed;
        root.Children.Add(error);
        root.Children.Add(Line(BuffLengthEditor.ScopeLine(buff), "DimBrush", Tok.SpaceS));

        var buttons = new WrapPanel { Margin = new Thickness(0, Tok.SpaceL, 0, 0) };
        var save = Theming.Button(BuffLengthEditor.Save, isDefault: true);
        save.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        save.Click += (_, _) =>
        {
            if (BuffLengthEditor.Read(box.Text, out var why) is not { } seconds)
            {
                error.Text = why;
                error.Visibility = Visibility.Visible;
                box.Focus();
                box.SelectAll();
                return;
            }
            tracker.SetPlayerLength(buff.DurationKey, seconds);
            changed?.Invoke();
            Close();
        };
        var reset = Theming.Button(BuffLengthEditor.UseDerived);
        reset.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        if (typed is null)
        {
            // Trap 17: disabled has to LOOK disabled and say why.
            reset.IsEnabled = false;
            reset.Opacity = 0.45;
            ToolTipService.SetShowOnDisabled(reset, true);
            reset.ToolTip = BuffLengthEditor.UseDerivedIdle;
        }
        reset.Click += (_, _) =>
        {
            tracker.SetPlayerLength(buff.DurationKey, null);
            changed?.Invoke();
            Close();
        };
        var cancel = Theming.Button(BuffLengthEditor.Cancel, isCancel: true);
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(save);
        buttons.Children.Add(reset);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        };
    }

    private static TextBlock Line(string text, string ink, double top)
    {
        var block = DesignSystem.Text(Role.Caption, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(ink);
        block.Margin = new Thickness(0, top, 0, 0);
        return block;
    }
}
