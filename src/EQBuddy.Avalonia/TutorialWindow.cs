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
            "If you keep your logs because you run GINA or GamParse, or upload them to another " +
            "parser, turn that off below. Cleanup never runs while the game, GINA, or GamParse " +
            "is open. You can change this any time in ⚙ Options.",
            null, TruncationChoice: true),

        new("The widget",
            "An always-on-top stack of cards that follows whoever is playing. Most cards open " +
            "downward when you click them; a few have an ↗ instead, and those open a full " +
            "window — there is more to say than a card can hold. Drag anywhere to move it. " +
            "The dot is green while the log is live. ↻ restarts the session count; " +
            "– minimizes to the mini pill; ⚙ opens Options.",
            "t-widget.png"),

        // Added 2026-08-19 with the Progress theme, in the same change as the WPF twin.
        // The tour predated the windows and still told people every card "drills into
        // details", which stopped being true the moment a card became a launcher.
        new("Cards that open windows",
            "Two cards are doors. QUESTS opens the Quest Tracker — every quest searchable by " +
            "the reward you want, plus your Epic 1.0 and Plane of Sky checklists. PROGRESS " +
            "opens four tabs: Experience, Wealth (coin AND motes, with the per-hour rate), " +
            "Faction, and Raids.\n\n" +
            "Both replaced several smaller cards, so if you are looking for one that used to " +
            "be on the widget — Money, Motes, Faction, Raids, Sky Quest, Epics — it is a tab " +
            "inside one of these now. ⚙ Options → Cards & windows names which, under the " +
            "card that absorbed it.\n\n" +
            "The card's own line still carries the numbers worth glancing at while you play, " +
            "so you only open the window when you actually want to read something.",
            null),

        new("Combat, Details!-style",
            "Damage by attack with share bars: total · hits · average · per-ability DPS · crit " +
            "rate. Click the sort labels to re-rank (the bars follow the sorted column). Below: " +
            "damage taken per mob, your recent fights with per-fight DPS, and a stance " +
            "breakdown. Healing gets the same treatment.",
            "t-combat.png"),

        new("Watch rules & alerts",
            "EQBuddy starts with one rule already on: when any of your crowd-control spells " +
            "breaks — charm, mez, root, lull or stun — you get a banner and a sound. Turn " +
            "either off, or delete the rule, in ⚙ Options → Watch rules. Add your own to " +
            "watch Loot ('mote'), Kills, Skill-ups, Deaths, Milestones, spells wearing off, " +
            "or any text in the log. Set a Delay and the alert lands that many seconds " +
            "later, turning a rule into a cue. " +
            "Matches count on the 🎯 Watch card with per-hour rates. The banner tile is " +
            "click-through and movable — drag it while Options is open.",
            "t-watch.png"),

        new("Spawn timers",
            "Kill a named mob - or its placeholder - and a compact countdown chip appears: " +
            "⏳ Asaka L`Rei 3:12. Chips stack, drag anywhere as one, and keep every timer " +
            "running across zones. At zero a chip turns DUE for one minute, with a sound if " +
            "that named's bell is enabled, then clears itself. Double-click a chip - or use " +
            "right-click -> Spawn timers - for the full " +
            "zone list. Community times are editable there and your correction survives updates; " +
            "▶ starts a timer manually and + adds a missing named.",
            null),

        new("Mini mode & hotkeys",
            "Star ★ the stats you care about, then minimize: a tiny pill shows just those, " +
            "plus watch-rule chips.",
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
