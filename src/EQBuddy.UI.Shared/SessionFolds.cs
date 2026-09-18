namespace EQBuddy.UI.Shared;

/// <summary>
/// Which folds are open, for as long as EQBuddy runs - and not a moment longer.
///
/// **Why it outlives the window** (Hateborne, 2026-09-18): *"if I minimize something like
/// 'No longer needed' in the Quest Tracker pop out, then close the pop out, I don't want
/// to have to collapse it every single time the pop out opens."* The Sky bands kept their
/// fold state in a field on the view, and closing the tracker threw the view away. The
/// main window owns one of these for the whole run instead, and every host of the surface
/// (the pop-out and the shell's Guide room) reads and writes the same one.
///
/// **Why it never touches settings.** The folds are session-only by ruling (Bevel,
/// Helm-signed 2026-09-03: "session-only, default OPEN") - folding a box away is a decision
/// about this sitting, not about how the app opens tomorrow - and a setting would outlive
/// the surfaces that write it, which is what <c>DeadSettingTests</c> exists to catch.
/// "Session" here means the run, which is what a player means by it; a window's lifetime
/// was an accident of where the field happened to live.
///
/// Framework-free so the rule is unit-tested: the WPF layer has no test project.
/// </summary>
public sealed class SessionFolds
{
    private readonly Dictionary<string, bool> _open = new(StringComparer.Ordinal);

    /// <summary>Bumped on every change, so a host that did not make the change can see it
    /// in its repaint signature and redraw (the <c>gx:</c> lesson: a click force-refreshes
    /// only the view it landed on).</summary>
    public int Version { get; private set; }

    /// <summary>Whether <paramref name="key"/> is open; <paramref name="defaultOpen"/> until
    /// someone says otherwise.</summary>
    public bool IsOpen(string key, bool defaultOpen = true) => _open.GetValueOrDefault(key, defaultOpen);

    public void Set(string key, bool open)
    {
        if (_open.TryGetValue(key, out var was) && was == open) return;
        _open[key] = open;
        Version++;
    }

    /// <summary>Flip <paramref name="key"/> from whatever it reads as now.</summary>
    public void Toggle(string key, bool defaultOpen = true) => Set(key, !IsOpen(key, defaultOpen));
}
