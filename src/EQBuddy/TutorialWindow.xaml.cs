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
            "If you keep your logs — because you also run GINA or GamParse, or upload them " +
            "to another parser — turn that off below. (Cleanup never runs while the game, " +
            "GINA, or GamParse is open.) You can change this any time in Options.",
            null, TruncationChoice: true),

        new("The widget",
            "An always-on-top stack of cards that follows whoever is playing. Most cards open " +
            "downward when you click them; a few have an ↗ instead, and those open a full " +
            "window — there is more to say than a card can hold. Drag anywhere to move it. " +
            "The dot is green while the log is live. The round arrow restarts the session " +
            "count, the dash minimizes to the mini pill, and the gear opens Options.",
            "t-widget.png"),

        // Added 2026-08-19 with the Progress theme. The tour predated the windows
        // entirely and still told people every card "drills into details" — which stopped
        // being true the moment a card became a launcher. #219 is what a player does when
        // a surface moves and nothing tells them: they go looking in Options, find
        // nothing, and reasonably conclude the feature was removed.
        new("Cards that open windows",
            "Two cards are doors. QUESTS opens the Quest Tracker — every quest searchable by " +
            "the reward you want, plus your Epic 1.0 and Plane of Sky checklists. PROGRESS " +
            "opens four tabs: Experience, Wealth (coin AND motes, with the per-hour rate), " +
            "Faction, and Raids.\n\n" +
            "Both replaced several smaller cards, so if you are looking for one that used to " +
            "be on the widget — Money, Motes, Faction, Raids, Sky Quest, Epics — it is a tab " +
            "inside one of these now. Options → Cards & windows names which, under the " +
            "card that absorbed it.\n\n" +
            "The card's own line still carries the numbers worth glancing at while you play, " +
            "so you only open the window when you actually want to read something.",
            null),

        new("Combat, Details!-style",
            "Damage by attack with share bars: total · hits · average · per-ability DPS · crit " +
            "rate. Click the sort labels to re-rank (the bars follow the sorted column). Below: " +
            "damage taken per mob, your recent fights with per-fight DPS, and a stance " +
            "breakdown. Healing gets the same treatment.\n\n" +
            "Star ★ the card and minimize, and that same board pops out as its own small " +
            "window you can park anywhere — which is what the picture shows.",
            "t-combat.png"),

        new("Watch rules & alerts",
            "EQBuddy starts with one rule already on: when any of your crowd-control spells " +
            "breaks — charm, mez, root, lull or stun — you get a banner and a sound. Turn " +
            "either off, or delete the rule, in Options → Watch rules. Add your own to " +
            "watch Loot ('mote'), Kills, Skill-ups, Deaths, Milestones, spells wearing off, " +
            "or any text in the log at all. Match text is a substring, so 'mote' catches " +
            "every tier. Set a Delay and the alert lands that many seconds later, which " +
            "turns a rule into a cue — 'recast that DoT now'. Matches count on the Watch " +
            "card with per-hour rates. The banner tile is click-through and movable — drag " +
            "it while Options is open. Not sure what to type? Options → Watch rules → " +
            "Show examples has a worked one for every kind.",
            "t-watch.png"),

        new("Spawn timers",
            "Kill a named mob — or the placeholder that spawns in its spot — and a small " +
            "countdown chicklet appears: a stopwatch, the name, and the time left — " +
            "\"Asaka L`Rei 3:12\". Chicklets stack, drag " +
            "anywhere as one, and keep counting every timer you have running in any " +
            "zone. At zero a chicklet turns DUE (with a sound, if its bell is on) for " +
            "a minute, then clears itself — camps you walked away from tidy up on " +
            "their own. Double-click any chicklet — or " +
            "right-click → Spawn timers… — for the full zone list, which follows you " +
            "zone to zone. We captured the respawn times we could from community " +
            "sources, but they're player lore, not gospel: if you notice a discrepancy " +
            "in game, type over the duration right in the row — your number wins and " +
            "survives updates. ▶ starts a timer by hand for camps you arrived at late, " +
            "and + adds named the list doesn't know. Track spawns in Options turns " +
            "the whole thing off.",
            null),

        new("Mini mode & hotkeys",
            "Star ★ the stats you care about, then minimize: a tiny pill shows just those, " +
            "plus watch-rule chips. Right-click for click-through mode (game clicks pass " +
            "straight through the widget; the padlock chip beside it turns it back off).",
            "t-mini.png"),

        new("Session history",
            "Every session saves itself to a local database — nothing uploads, ever. " +
            "Right-click → Session history: search anything, add notes and tags, Ctrl-click " +
            "two sessions to compare rates, import old log files, export JSON.\n\n" +
            "That's the tour — happy hunting! Finishing turns off this launch tour; get it " +
            "back any time via right-click → Quick tutorial…, or re-enable it in Options.",
            "t-history.png"),
    ];

    /// <summary><paramref name="startPage"/> is 1-based and exists so the tour can be
    /// PHOTOGRAPHED. Every page but the first carries an illustration, and every one of
    /// those illustrations went stale for a month without anyone noticing — because the
    /// only way to see page 4 was to install the app, launch it, and click Next three
    /// times. A surface nobody can capture reads as reviewed (trap 22). Used by the
    /// EQBUDDY_TOUR hook; out of range clamps.</summary>
    public TutorialWindow(MainWindow main, int startPage = 1)
    {
        InitializeComponent();
        _main = main;
        Owner = main;
        KeepLogsCheck.IsChecked = !main.Settings.TruncateLogs;
        _page = Math.Clamp(startPage - 1, 0, Pages.Length - 1);
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
