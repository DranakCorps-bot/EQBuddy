using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Feedback goes where the community already lives: GitHub Discussions (David,
/// 2026-08-06). No backend, no anonymous ids, nothing uploaded from here — the dialog
/// composes a prefilled draft (ideas for features, q-a for bugs) and opens it in the
/// browser, where the player reviews every word and posts under their own account.
/// The only thing appended is the app version and Windows build; log lines are the
/// player's to paste, never auto-attached.
/// </summary>
public partial class FeedbackWindow : Window
{
    private const string Repo = "https://github.com/DranakCorps-bot/EQBuddy";

    public FeedbackWindow()
    {
        InitializeComponent();
        AppendNote.Text = $"Posting opens on GitHub for your review — nothing is sent from the app. " +
            $"Appended for context: {VersionLine()}";
        FeatureRadio.Checked += (_, _) => UpdateHint();
        BugRadio.Checked += (_, _) => UpdateHint();
        Loaded += (_, _) => BodyBox.Focus();
    }

    internal static string VersionLine()
    {
        var v = typeof(FeedbackWindow).Assembly.GetName().Version;
        // A dev build names its commit here too (DRA-707 D3), so a report filed from the
        // Founder's auto-rolled copy says which merge it was.
        return $"{DevBuildStamp.Append($"EQBuddy {v?.ToString(3) ?? "?"}")} · Windows {Environment.OSVersion.Version.Build}";
    }

    private void UpdateHint() =>
        BugHint.Visibility = BugRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void OnBodyChanged(object sender, TextChangedEventArgs e) =>
        SendBtn.IsEnabled = BodyBox.Text.Trim().Length > 0;

    private void OnSend(object sender, RoutedEventArgs e)
    {
        var text = BodyBox.Text.Trim();
        if (text.Length == 0) return;

        var category = BugRadio.IsChecked == true ? "q-a" : "ideas";
        // GitHub wants a title; the first line serves, trimmed to something list-friendly.
        var firstLine = text.Split('\n')[0].Trim();
        var title = firstLine.Length <= 70 ? firstLine : firstLine[..69].TrimEnd() + "…";
        var body = $"{text}\n\n---\n_{VersionLine()}_";

        var url = $"{Repo}/discussions/new?category={category}" +
            $"&title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not (Button or TextBox or RadioButton) &&
            e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
