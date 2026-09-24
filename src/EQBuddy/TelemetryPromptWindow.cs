using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The first-open telemetry prompt** — shown once per install, decline is final
/// (TEL-001 as amended by DRA-336 §1; copy is §8.3 §A, drawn from <see cref="TelemetryCopy"/>).
///
/// <para><b>Declining is every path but one.</b> "Not now", Esc, the ✕ and clicking away from
/// the window all decline; only "Send these heartbeats" accepts. The two buttons are the same
/// control at the same size — a two-column <see cref="UniformGrid"/> — with no default, no
/// accent fill and no pre-focus, so neither one is the one the eye or the Enter key lands on.
/// </para>
///
/// <para>One carve-out on "clicking away": the requirement-page link opens a browser, which
/// takes focus from this window. Reading what you are being asked about is not a decline, so
/// the deactivation the link itself causes is not counted.</para>
///
/// <para>Plain system chrome, like <see cref="ProfileImportWindow"/>, for the same reason: a
/// question about what leaves the player's machine gets the most ordinary, trustworthy frame
/// there is.</para>
/// </summary>
internal sealed class TelemetryPromptWindow : Window
{
    /// <summary>True only when the player pressed "Send these heartbeats".</summary>
    public bool Accepted { get; private set; }

    private bool _openingLink;

    public TelemetryPromptWindow()
    {
        Title = TelemetryCopy.PromptTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        Content = root;

        var head = DesignSystem.Text(Role.TitleWindow, TelemetryCopy.PromptTitle);
        head.Ink("AccentBrush");
        root.Children.Add(head);

        root.Children.Add(Line(TelemetryCopy.PromptLead, Role.Body, Tok.SpaceS));
        root.Children.Add(Line(TelemetryCopy.PromptListLead, Role.Body, Tok.SpaceS));
        var n = 1;
        foreach (var (name, text) in TelemetryCopy.PromptFields)
            root.Children.Add(Field($"{n++}. ", name, " — " + text));
        root.Children.Add(Line(TelemetryCopy.PromptRetention, Role.Body, Tok.SpaceS));
        root.Children.Add(Line(TelemetryCopy.PromptOffLater, Role.Body, Tok.SpaceS));
        root.Children.Add(Line(TelemetryCopy.PromptOptional, Role.Body, Tok.SpaceS));

        root.Children.Add(Line(TelemetryCopy.PromptFootnote, Role.Metadata, Tok.SpaceM));
        root.Children.Add(LinkLine());

        var buttons = new UniformGrid
        {
            Columns = 2, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Tok.SpaceL, 0, 0),
        };
        var decline = Theming.Button(TelemetryCopy.PromptDecline);
        decline.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        decline.Click += (_, _) => Close();
        var accept = Theming.Button(TelemetryCopy.PromptAccept);
        accept.Click += (_, _) => { Accepted = true; Close(); };
        buttons.Children.Add(decline);
        buttons.Children.Add(accept);
        root.Children.Add(buttons);

        // Esc declines without making "Not now" the Cancel button, which would give it a
        // role the other button does not have.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            Close();
        };
        Deactivated += (_, _) =>
        {
            if (_openingLink) { _openingLink = false; return; }
            if (IsVisible && !Accepted) Close();
        };
    }

    private UIElement LinkLine()
    {
        var block = DesignSystem.Text(Role.Metadata);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink("DimBrush");
        block.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        block.Inlines.Add(new Run(TelemetryCopy.PromptLinkLead + " "));
        var link = new Hyperlink(new Run(TelemetryCopy.RequirementPageUrl))
        {
            NavigateUri = new Uri(TelemetryCopy.RequirementPageUrl),
        };
        link.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
        link.RequestNavigate += (_, e) =>
        {
            _openingLink = true;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) { _openingLink = false; App.LogError(ex); }
            e.Handled = true;
        };
        block.Inlines.Add(link);
        return block;
    }

    private static TextBlock Field(string number, string name, string text)
    {
        var block = DesignSystem.Text(Role.Body);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink("TextBrush");
        block.Margin = new Thickness(Tok.SpaceM, Tok.SpaceXs, 0, 0);
        block.Inlines.Add(new Run(number));
        block.Inlines.Add(new Bold(new Run(name)));
        block.Inlines.Add(new Run(text));
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
