using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace EQBuddy;

/// <summary>
/// Quick tour, shown at every launch while Settings.ShowTutorial is on. Page 1
/// doubles as the log-truncation consent question — the startup janitor holds off
/// truncating while the tour is still enabled. Finishing or "Never show again"
/// disables the launch tour (the last page says so); "Skip for now" shows it again
/// next launch. Reopen via right-click → Quick tutorial… or the Options checkbox.
/// </summary>
public partial class TutorialWindow : Window
{
    private readonly MainWindow _main;
    private readonly bool _ready;
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
        InitializeComponent();
        _main = main;
        Owner = main;
        KeepLogsCheck.IsChecked = !main.Settings.TruncateLogs;
        _ready = true;
        Render();
    }

    private void Render()
    {
        var p = Pages[_page];
        PageTitle.Text = p.Title;
        PageBody.Text = p.Body;
        PageDots.Text = $"{_page + 1} of {Pages.Length}";
        KeepLogsCheck.Visibility = p.TruncationChoice ? Visibility.Visible : Visibility.Collapsed;
        if (p.Image is { } img)
        {
            PageImage.Source = new BitmapImage(
                new Uri($"pack://application:,,,/Assets/tutorial/{img}"));
            ImageFrame.Visibility = Visibility.Visible;
        }
        else
        {
            ImageFrame.Visibility = Visibility.Collapsed;
        }
        PrevBtn.IsEnabled = _page > 0;
        NextBtn.Content = _page == Pages.Length - 1 ? "Finish ✔" : "Next ▶";
    }

    private void OnKeepLogsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _main.SetTruncateLogs(KeepLogsCheck.IsChecked != true);
    }

    private void OnPrev(object sender, RoutedEventArgs e) { if (_page > 0) { _page--; Render(); } }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_page < Pages.Length - 1) { _page++; Render(); return; }
        Finish();
    }

    private void OnSkip(object sender, RoutedEventArgs e) => Close();   // shows again next launch

    private void OnNever(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        // Finishing counts as "seen it" — stop auto-showing (the last page says how
        // to get it back: right-click → Quick tutorial…, or the Options checkbox).
        _main.Settings.ShowTutorial = false;
        _main.PersistSettings();
        Close();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    { if (e.ChangedButton == MouseButton.Left) DragMove(); }
}
