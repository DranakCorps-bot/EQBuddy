using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace EQBuddy.Avalonia;

/// <summary>
/// Six-page quick tour. Its first page is the log-truncation consent question, so
/// startup cleanup remains deferred while the launch tutorial setting is enabled.
/// </summary>
public sealed class TutorialWindow : Window
{
    private readonly MainWindow _main;
    private readonly TextBlock _pageTitle = new();
    private readonly TextBlock _pageBody = new();
    private readonly TextBlock _pageDots = new();
    private readonly CheckBox _keepLogs = new();
    private readonly Border _imageFrame = new();
    private readonly Image _pageImage = new();
    private readonly Button _previous;
    private readonly Button _next;
    private bool _ready;
    private int _page;

    private sealed record Page(string Title, string Body, string? Image, bool TruncationChoice = false);

    private static readonly Page[] Pages =
    [
        new("Your log files — one decision first",
            "EQBuddy reads the log file EverQuest Legends writes (the /log command — EQBuddy " +
            "turns it on for you). It never touches the game itself.\n\n" +
            "Because logging stays on permanently, EQBuddy automatically EMPTIES a character's " +
            "log after it has been quiet for 60+ minutes (a finished play session), so the files " +
            "never grow forever.\n\n" +
            "If you keep your logs — for example to upload them to another parser — turn that " +
            "off below. You can change this any time in ⚙ Options.",
            null, TruncationChoice: true),

        new("The widget",
            "An always-on-top card that follows whoever is playing. Click any section to drill " +
            "into details, drag anywhere to move it. The dot is green while the log is live. " +
            "↻ restarts the session count; – minimizes to the mini pill; ⚙ opens Options.",
            "t-widget.png"),

        new("Combat, Details!-style",
            "Damage by attack with share bars: total · hits · average · per-ability DPS · crit " +
            "rate. Click the sort labels to re-rank (the bars follow the sorted column). Below: " +
            "damage taken per mob, your recent fights with per-fight DPS, and a stance " +
            "breakdown. Healing gets the same treatment.",
            "t-combat.png"),

        new("Watch rules & alerts",
            "⚙ Options → Watch rules: watch Loot ('mote'), Kills, Skill-ups, Deaths, " +
            "Milestones, or SpellFade — your mez or charm breaking. Matches count on the 🎯 " +
            "Tracked card with per-hour rates, and can 🔔 pop a floating banner and 🔊 play a " +
            "sound. The banner tile is click-through and movable — drag it while Options is open.",
            "t-watch.png"),

        new("Mini mode & hotkeys",
            "Star ★ the stats you care about, then minimize: a tiny pill shows just those, " +
            "plus watch-rule chips. Global hotkeys: Ctrl+Shift+H hide/show · Ctrl+Shift+T " +
            "click-through (play right through the widget) · Ctrl+Shift+M mini mode · " +
            "Ctrl+Shift+K camp marker.",
            "t-mini.png"),

        new("Session history",
            "Every session saves itself to a local database — nothing uploads, ever. " +
            "Right-click → Session history: search anything, add notes and tags, Ctrl-click " +
            "two sessions to compare rates, import old log files, export JSON.\n\n" +
            "That's the tour — happy hunting! Finishing turns off this launch tour; get it " +
            "back any time via right-click → Quick tutorial…, or re-enable it in ⚙ Options.",
            "t-history.png"),
    ];

    public TutorialWindow(MainWindow main)
    {
        _main = main;
        Title = "Welcome to EQBuddy";
        Width = 580;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;

        _pageTitle.FontSize = 16;
        _pageTitle.FontWeight = FontWeight.Bold;
        _pageTitle.Foreground = AppTheme.AccentBrush;
        _pageDots.FontSize = 12;
        _pageDots.Foreground = AppTheme.DimBrush;
        _pageDots.HorizontalAlignment = HorizontalAlignment.Right;
        _pageDots.VerticalAlignment = VerticalAlignment.Center;
        _pageBody.FontSize = 13;
        _pageBody.Foreground = AppTheme.TextBrush;
        _pageBody.TextWrapping = TextWrapping.Wrap;
        _pageBody.LineHeight = 19;
        _pageBody.Margin = new Thickness(0, 10, 0, 0);

        _keepLogs.Content = new TextBlock
        {
            Text = "Keep my log files — disable auto-empty",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = AppTheme.TextBrush,
        };
        _keepLogs.Margin = new Thickness(0, 14, 0, 2);
        _keepLogs.IsChecked = !main.Settings.TruncateLogs;
        _keepLogs.IsCheckedChanged += (_, _) =>
        {
            if (_ready) _main.SetTruncateLogs(_keepLogs.IsChecked != true);
        };

        _pageImage.MaxHeight = 320;
        _pageImage.MaxWidth = 528;
        _pageImage.Stretch = Stretch.Uniform;
        _imageFrame.Margin = new Thickness(0, 14, 0, 0);
        _imageFrame.HorizontalAlignment = HorizontalAlignment.Center;
        _imageFrame.BorderBrush = AppTheme.BorderBrush;
        _imageFrame.BorderThickness = new Thickness(1);
        _imageFrame.CornerRadius = new CornerRadius(6);
        _imageFrame.Child = _pageImage;

        _previous = AppTheme.IconButton("◀ Previous", "Previous page");
        _previous.FontSize = 12;
        _previous.Click += (_, _) => { if (_page > 0) { _page--; Render(); } };
        _next = AppTheme.IconButton("Next ▶", "Next page");
        _next.FontSize = 12;
        _next.FontWeight = FontWeight.SemiBold;
        _next.Foreground = AppTheme.AccentBrush;
        _next.Margin = new Thickness(14, 0, 0, 0);
        _next.Click += (_, _) =>
        {
            if (_page < Pages.Length - 1) { _page++; Render(); }
            else Finish();
        };

        Content = BuildContent();
        PointerPressed += OnDrag;
        _ready = true;
        Render();
    }

    private Control BuildContent()
    {
        var header = new Grid();
        header.Children.Add(_pageTitle);
        header.Children.Add(_pageDots);

        var never = AppTheme.IconButton("Never show again", "Don't show this tour at launch");
        never.FontSize = 12;
        never.Foreground = AppTheme.DimBrush;
        never.Click += (_, _) => Finish();
        var skip = AppTheme.IconButton("Skip for now", "Show this tour again next launch");
        skip.FontSize = 12;
        skip.Foreground = AppTheme.DimBrush;
        skip.Margin = new Thickness(10, 0, 0, 0);
        skip.Click += (_, _) => Close();
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(never);
        left.Children.Add(skip);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        right.Children.Add(_previous);
        right.Children.Add(_next);
        var buttons = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(left);
        buttons.Children.Add(right);

        var panel = new StackPanel();
        panel.Children.Add(header);
        panel.Children.Add(_pageBody);
        panel.Children.Add(_keepLogs);
        panel.Children.Add(_imageFrame);
        panel.Children.Add(buttons);
        return new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16),
            Child = panel,
        };
    }

    private void Render()
    {
        var page = Pages[_page];
        _pageTitle.Text = page.Title;
        _pageBody.Text = page.Body;
        _pageDots.Text = $"{_page + 1} of {Pages.Length}";
        _keepLogs.IsVisible = page.TruncationChoice;
        _imageFrame.IsVisible = page.Image is not null;
        if (page.Image is { } image)
        {
            using var stream = AssetLoader.Open(
                new Uri($"avares://EQBuddy.Avalonia/Assets/tutorial/{image}"));
            _pageImage.Source = new Bitmap(stream);
        }
        _previous.IsEnabled = _page > 0;
        _next.Content = _page == Pages.Length - 1 ? "Finish ✔" : "Next ▶";
    }

    private void Finish()
    {
        _main.Settings.ShowTutorial = false;
        _main.PersistSettings();
        Close();
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(IsInteractiveControl))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private static bool IsInteractiveControl(Visual visual) => visual is Button or CheckBox;
}
