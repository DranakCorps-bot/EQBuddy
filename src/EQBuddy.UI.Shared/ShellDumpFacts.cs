namespace EQBuddy.UI.Shared;

/// <summary>
/// One tiny rule about the <c>EQBUDDY_EXPAND</c> dump, and it earned its own file the
/// moment the Evolved shell became a SECOND host for surfaces the widget still opens.
///
/// **The dump is one flat namespace, and two hosts of one room write the same keys into
/// it.** <c>MapView.DebugFacts()</c> reports <c>mapShown</c>, <c>mapZones</c>,
/// <c>mapNamedRows</c> — and it reports them identically whether it is hanging in
/// <c>WorldWindow</c> or in the shell's World room. Open both at once (which is exactly
/// what the two-host E2E assertions do, on purpose) and the later writer silently wins:
/// the shell's numbers would arrive under the window's names, every existing assertion on
/// <c>map*</c> would quietly start reading a different window, and nothing about that
/// shows in a diff, a build or a screenshot. It is trap 4 — one entry, two sources for one
/// fact — with the two sources being two hosts instead of two lines.
///
/// **The fix is not to re-implement the facts under new names.** A hand-written
/// <c>shellWorldMapZones = _map.SomethingPublic</c> would be a second producer of the
/// number the window already reports, which is the failure it is trying to avoid, one
/// level up; and the day <c>MapView</c> gains a seventh fact the shell would silently stop
/// reporting it. So the room asks the SAME view for the SAME string and renames the keys
/// mechanically. The two hosts cannot report different facts, because there is only one
/// place the facts are written.
/// </summary>
public static class ShellDumpFacts
{
    /// <summary>
    /// Re-key a space-separated <c>key=value</c> dump fragment under a prefix:
    /// <c>Prefixed("shellWorld", "mapZones=4")</c> is <c>"shellWorldMapZones=4"</c>.
    ///
    /// A token with no <c>=</c> is passed through untouched rather than mangled. The dump
    /// format has no such token today, and a reader that silently renamed something it did
    /// not understand would be the more expensive of the two mistakes.
    /// </summary>
    public static string Prefixed(string prefix, string facts) =>
        string.Join(' ', facts
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Contains('=') && token.Length > 0
                ? prefix + char.ToUpperInvariant(token[0]) + token[1..]
                : token));
}
