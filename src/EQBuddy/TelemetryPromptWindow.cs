using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The first-open telemetry notice** — shown once per install (TEL-001 as amended by
/// DRA-1980; copy is §8.3 §A, drawn from <see cref="TelemetryCopy"/>). Telemetry is on by
/// default, so this is a disclosure, not a question: a title, three short lines and two
/// buttons, the detail behind "Learn more", and opening that answers nothing.
///
/// <para><b>The close rule is <see cref="TelemetryHeartbeat.AnswerFor"/>'s, not this
/// window's.</b> OK, Esc, the ✕ and Alt+F4 keep it on — the notice says so in its own words —
/// and "Turn off" is the only off (DRA-1980, Challenger condition DRA-1981). Clicking away is
/// NOT a close: the window stays up, and a close the player did not make (the session ending,
/// the app going down) reports <see cref="TelemetryHeartbeat.PromptAnswer.Unanswered"/>, which
/// writes nothing, so the notice shows again next launch (DRA-385). The two buttons are the
/// same control at the same size — a two-column <see cref="UniformGrid"/> — with no default,
/// no accent fill and no pre-focus, so Turn off is never the smaller or the harder one.</para>
///
/// <para><b>It opens in front.</b> Topmost and activated on show, because a startup prompt
/// that opens behind the game is the one nobody answers. Topmost drops when the player opens
/// the requirement-page link, so the page can come in front of it.</para>
///
/// <para>Plain system chrome, like <see cref="ProfileImportWindow"/>, for the same reason: a
/// question about what leaves the player's machine gets the most ordinary, trustworthy frame
/// there is.</para>
/// </summary>
internal sealed class TelemetryPromptWindow : Window
{
    /// <summary>How the window closed. Stays <see cref="TelemetryHeartbeat.PromptAnswer.Unanswered"/>
    /// unless the player pressed a button, Esc, the ✕ or Alt+F4.</summary>
    public TelemetryHeartbeat.PromptAnswer Answer { get; private set; } =
        TelemetryHeartbeat.PromptAnswer.Unanswered;

    private const int WmSysCommand = 0x0112;
    private const int ScClose = 0xF060;

    public TelemetryPromptWindow()
    {
        Title = TelemetryCopy.PromptTitle;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ContentRendered += (_, _) => Activate();
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        Content = root;

        var head = DesignSystem.Text(Role.TitleWindow, TelemetryCopy.PromptTitle);
        head.Ink("AccentBrush");
        root.Children.Add(head);

        root.Children.Add(Line(TelemetryCopy.PromptBody, Role.Body, Tok.SpaceS));
        root.Children.Add(Line(TelemetryCopy.PromptCloseKeepsOn, Role.Body, Tok.SpaceS));
        root.Children.Add(Line(TelemetryCopy.PromptChangeLater, Role.Metadata, Tok.SpaceS));
        root.Children.Add(LinkLine());

        var buttons = new UniformGrid
        {
            Columns = 2, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Tok.SpaceL, 0, 0),
        };
        var turnOff = Theming.Button(TelemetryCopy.PromptTurnOff);
        turnOff.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        turnOff.MinWidth = 96;
        turnOff.Click += (_, _) => CloseAs(TelemetryHeartbeat.NoticeClose.TurnOffButton);
        var ok = Theming.Button(TelemetryCopy.PromptOk);
        ok.MinWidth = 96;
        ok.Click += (_, _) => CloseAs(TelemetryHeartbeat.NoticeClose.OkButton);
        buttons.Children.Add(turnOff);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        // Esc closes without making OK the Cancel button, which would give it a role the
        // other button does not have.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            CloseAs(TelemetryHeartbeat.NoticeClose.Escape);
        };
    }

    private void CloseAs(TelemetryHeartbeat.NoticeClose how)
    {
        Answer = TelemetryHeartbeat.AnswerFor(how);
        Close();
    }

    /// <summary>The ✕ is recognised by the SC_CLOSE it sends (as do Alt+F4 and the system
    /// menu's Close — all things the player did to this window). A close that arrives any other
    /// way — session end, shutdown — never passes here and stays Unanswered.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = System.Windows.Interop.HwndSource.FromHwnd(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        source?.AddHook((IntPtr _, int msg, IntPtr wParam, IntPtr _, ref bool _) =>
        {
            if (msg == WmSysCommand && ((int)wParam.ToInt64() & 0xFFF0) == ScClose)
                Answer = TelemetryHeartbeat.AnswerFor(TelemetryHeartbeat.NoticeClose.SystemClose);
            return IntPtr.Zero;
        });
    }

    private UIElement LinkLine()
    {
        var block = DesignSystem.Text(Role.Metadata);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink("DimBrush");
        block.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        var link = new Hyperlink(new Run(TelemetryCopy.PromptLearnMore))
        {
            NavigateUri = new Uri(TelemetryCopy.PlayerPageUrl),
        };
        link.ToolTip = TelemetryCopy.PlayerPageUrl;
        link.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
        link.RequestNavigate += (_, e) =>
        {
            Topmost = false;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) { App.LogError(ex); }
            e.Handled = true;
        };
        block.Inlines.Add(link);
        return block;
    }

    private static TextBlock Line(string text, Role role, double top)
    {
        var block = DesignSystem.Text(role, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(role == Role.Body ? "TextBrush" : "DimBrush");
        block.Margin = new Thickness(0, top, 0, 0);
        return block;
    }
}
