using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **"Delete my telemetry data?"** — the confirm step before the one request that erases a
/// player's heartbeats on the backend (§8.3 §C, copy from <see cref="TelemetryCopy"/>).
///
/// The mirror of the first-open prompt: two equal buttons, neither the default, and every
/// path but one is the safe answer — here that is Cancel, so Esc, the ✕ and clicking away all
/// cancel. <see cref="Confirmed"/> says only that the player asked; whether anything was
/// deleted is the server's answer, which <see cref="TelemetryRuntime.DeleteAsync"/> waits for.
/// </summary>
internal sealed class TelemetryDeleteWindow : Window
{
    public bool Confirmed { get; private set; }

    public TelemetryDeleteWindow()
    {
        Title = TelemetryCopy.DeleteTitle;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        Content = root;

        var head = DesignSystem.Text(Role.TitleWindow, TelemetryCopy.DeleteTitle);
        head.Ink("AccentBrush");
        root.Children.Add(head);

        root.Children.Add(Line(TelemetryCopy.DeleteLead, Tok.SpaceS, 0));
        foreach (var item in TelemetryCopy.DeleteItems)
            root.Children.Add(Line("• " + item, Tok.SpaceXs, Tok.SpaceM));
        root.Children.Add(Line(TelemetryCopy.DeleteKeeps, Tok.SpaceS, 0));
        root.Children.Add(Line(TelemetryCopy.DeleteAfter, Tok.SpaceS, 0));

        var buttons = new UniformGrid
        {
            Columns = 2, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Tok.SpaceL, 0, 0),
        };
        var cancel = Theming.Button(TelemetryCopy.DeleteCancel);
        cancel.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        cancel.Click += (_, _) => Close();
        var confirm = Theming.Button(TelemetryCopy.DeleteConfirm);
        confirm.Click += (_, _) => { Confirmed = true; Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        root.Children.Add(buttons);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            Close();
        };
        Deactivated += (_, _) => { if (IsVisible && !Confirmed) Close(); };
    }

    private static TextBlock Line(string text, double top, double left)
    {
        var block = DesignSystem.Text(Role.Body, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink("TextBrush");
        block.Margin = new Thickness(left, top, 0, 0);
        return block;
    }
}
