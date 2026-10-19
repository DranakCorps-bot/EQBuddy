using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The v2.0.7 telemetry note on the widget** (DRA-2010 §2; copy is §8.3 §A, drawn from
/// <see cref="TelemetryCopy"/>). Telemetry is on for every install, and this is how the player
/// is told: a NON-BLOCKING line under the header, where the eye lands (trap 44), beside the
/// logging and update banners. Nothing waits on it and nothing is gated behind it.
///
/// <para><b>When it shows is <see cref="TelemetryHeartbeat.NoteFor"/>'s, decided once at
/// startup</b> (<see cref="TelemetrySwitchOnStartup.Note"/>): at the switch-on, and on every
/// launch after it while the first beat is held. "Got it" hides it for this launch only and
/// writes nothing — the next launch asks the policy again.</para>
///
/// <para><b>"Open Options" is a door, not a gate</b>: it opens Options on the Behavior tab at
/// the toggle. The toggle is the only off (DRA-2010), so the note offers no switch of its
/// own.</para>
///
/// <para>Its own file because the widget's code-behind is the ratchet hotspot: MainWindow's
/// XAML declares a <see cref="TelemetryNoteHost"/> and its code-behind holds no line of this.
/// The element decides its own visibility and spacing (trap 15) — the host is bare.</para>
/// </summary>
internal static class TelemetryNoteBanner
{
    private static Border? _shown;

    /// <summary>The banner for this launch, or null when the policy says no note.</summary>
    public static UIElement? Build(MainWindow main, TelemetryHeartbeat.NoteKind kind)
    {
        if (kind == TelemetryHeartbeat.NoteKind.None) return null;

        var root = new StackPanel();
        var heading = DesignSystem.Text(Role.Body, TelemetryCopy.NoteHeading);
        heading.FontWeight = FontWeights.SemiBold;
        heading.TextWrapping = TextWrapping.Wrap;
        heading.Ink("AccentBrush");
        root.Children.Add(heading);

        root.Children.Add(Line(TelemetryCopy.NoteBody, "TextBrush"));
        root.Children.Add(Line(TelemetryCopy.NotePurpose, "TextBrush"));
        if (kind == TelemetryHeartbeat.NoteKind.Returning)
            root.Children.Add(Line(TelemetryCopy.NoteReturning, "TextBrush"));
        var where = Line(TelemetryCopy.NoteWhereOff + " ", "DimBrush");
        where.Inlines.Add(LearnMore());
        root.Children.Add(where);

        var border = new Border { Child = root, BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BackgroundProperty, "RaisedBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        border.SetResourceReference(Border.CornerRadiusProperty, "CornerCard");
        border.SetResourceReference(Border.PaddingProperty, "PadRow");
        border.SetResourceReference(FrameworkElement.MarginProperty, "LeadM");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, Tok.SpaceS, 0, 0),
        };
        var open = Theming.Button(TelemetryCopy.NoteOpenOptions);
        open.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        open.Click += (_, _) => OpenOptions(main);
        var dismiss = Theming.Button(TelemetryCopy.NoteDismiss);
        dismiss.Click += (_, _) => border.Visibility = Visibility.Collapsed;
        buttons.Children.Add(open);
        buttons.Children.Add(dismiss);
        root.Children.Add(buttons);

        _shown = border;
        _kind = kind;
        return border;
    }

    private static TelemetryHeartbeat.NoteKind _kind;

    /// <summary>Opens Options on the Behavior tab with the telemetry row in view. An Options
    /// window already open is closed and reopened, so the door lands on the row rather than
    /// activating whatever tab was showing (a door that does nothing is a silent no-op).</summary>
    private static void OpenOptions(MainWindow main)
    {
        foreach (var open in Application.Current.Windows.OfType<OptionsWindow>().ToList())
            open.Close();
        main.Settings.OptionsTab = "behavior";
        SettingsTelemetryView.ScrollIntoViewOnNextLoad = true;
        main.ShowOptions();
    }

    private static TextBlock Line(string text, string ink)
    {
        var block = DesignSystem.Text(Role.Body, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(ink);
        block.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        return block;
    }

    private static Hyperlink LearnMore()
    {
        var link = new Hyperlink(new Run(TelemetryCopy.NoteLearnMore))
        {
            NavigateUri = new Uri(TelemetryCopy.PlayerPageUrl), ToolTip = TelemetryCopy.PlayerPageUrl,
        };
        link.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
        link.RequestNavigate += (_, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) { App.LogError(ex); }
            e.Handled = true;
        };
        return link;
    }

    /// <summary>The <c>EQBUDDY_EXPAND</c> fact, off the BUILT element (trap 42):
    /// <c>telemetryNote=none|generic|returning</c> is the form that was built, and
    /// <c>telemetryNoteShown=</c> whether it is on screen now (a dismissal reads 0).</summary>
    public static string DebugFacts() =>
        $"telemetryNote={(_shown is null ? "none" : TelemetryHeartbeat.Word(_kind))} "
        + $"telemetryNoteShown={(_shown is { Visibility: Visibility.Visible } ? 1 : 0)}";
}

/// <summary>The bare host MainWindow's XAML declares under its banners. It builds the note the
/// first time it is loaded into the widget, so the widget's code-behind carries nothing.</summary>
internal sealed class TelemetryNoteHost : ContentControl
{
    private bool _built;

    public TelemetryNoteHost()
    {
        Focusable = false;
        Loaded += (_, _) =>
        {
            if (_built || Window.GetWindow(this) is not MainWindow main) return;
            _built = true;
            Content = TelemetryNoteBanner.Build(main, TelemetrySwitchOnStartup.Note);
        };
    }
}
