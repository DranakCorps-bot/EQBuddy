using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// The notification-area icon (#114 follow-up, Frankthetankk): when the widget hides —
/// unfocused game, game not running — its taskbar entry vanishes with it, and a
/// running program with no visible presence anywhere reads as "closed". The tray icon
/// is the always-there answer: visible the whole time EQBuddy runs, hidden or not.
/// Left-click surfaces the widget (which then stays up while EQBuddy has focus — the
/// same carve-out the hide logic already honors); the menu adds Options and Exit.
///
/// Deliberately WinForms NotifyIcon: WPF has no native tray support, and one shell
/// icon does not justify a dependency package.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public TrayIcon(MainWindow main)
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = $"{UpdateChecker.DisplayName} — click to show",
            Visible = true,
        };
        try
        {
            if (Environment.ProcessPath is { } exe)
                _icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
        }
        catch { /* icon-less tray entry still beats no tray entry */ }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show EQBuddy", null, (_, _) => main.RestoreFromAnotherInstance());
        menu.Items.Add("Options…", null, (_, _) =>
        {
            main.RestoreFromAnotherInstance();
            main.ShowOptions();
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit EQBuddy", null, (_, _) => main.Close());
        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                main.RestoreFromAnotherInstance();
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
