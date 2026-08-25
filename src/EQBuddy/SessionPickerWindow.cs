using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// "Which session?" for archive review (#74 round two): a pre-splitter log holds
/// days of sessions, and always replaying the last one made a 10 MB file look like
/// a 10-minute evening. Newest first — the recent session is usually the target.
/// </summary>
internal sealed class SessionPickerWindow : Window
{
    private LogSessionInfo? _choice;
    private readonly ListBox _list = new() { FontSize = 12, MaxHeight = 320, BorderThickness = new Thickness(0) };

    public static LogSessionInfo? Choose(Window owner, string fileName, List<LogSessionInfo> sessions)
    {
        var w = new SessionPickerWindow(fileName, sessions) { Owner = owner };
        w.ShowDialog();
        return w._choice;
    }

    private SessionPickerWindow(string fileName, List<LogSessionInfo> sessions)
    {
        Title = "Review which session?";
        WindowStyle = WindowStyle.ToolWindow;
        // Resizable (David, 2026-08-25). Size is NOT remembered: this is a modal picker
        // shown once per import (ShowDialog), with no AppSettings in scope.
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var heading = new TextBlock
        {
            Text = $"{fileName} holds {sessions.Count} sessions (60+ min of quiet ends one):",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        _list.SetResourceReference(Control.BackgroundProperty, "PanelBrush");
        _list.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        foreach (var s in Enumerable.Reverse(sessions))
            _list.Items.Add(new ListBoxItem { Content = Describe(s), Tag = s, Padding = new Thickness(6, 3, 6, 3) });
        _list.SelectedIndex = 0;
        _list.MouseDoubleClick += (_, _) => Accept();

        var ok = Theming.Button("Review", isDefault: true);
        ok.Click += (_, _) => Accept();
        var cancel = Theming.Button("Cancel", isCancel: true);
        cancel.Margin = new Thickness(8, 0, 0, 0);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(heading);
        root.Children.Add(_list);
        root.Children.Add(buttons);
        Content = root;
    }

    private void Accept()
    {
        _choice = (_list.SelectedItem as ListBoxItem)?.Tag as LogSessionInfo;
        Close();
    }

    private static string Describe(LogSessionInfo s)
    {
        var d = s.Duration;
        var len = d.TotalHours >= 1 ? $"{(int)d.TotalHours}h {d.Minutes:00}m" : $"{Math.Max(1, (int)d.TotalMinutes)}m";
        return $"{s.Start:ddd MMM d}  {s.Start:HH:mm} – {s.End:HH:mm}   ({len})";
    }
}
