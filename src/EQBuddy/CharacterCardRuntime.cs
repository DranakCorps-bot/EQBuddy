using System.IO;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The character card's one instance per process** (DRA-1288 D2, <c>docs/plans/DRA-1288.md</c>).
/// It is only the WIRING: it reads the followed character's stores off the widget, hands them
/// to <see cref="CharacterCardWriter"/> — where every decision about when, where and what is
/// made and unit-tested — and reports what happened to the Options block and the
/// <c>EQBUDDY_EXPAND</c> dump.
///
/// <para><b>One per process, not one per window</b> — <see cref="TelemetryRuntime"/>'s reason:
/// the Behavior block is built by two hosts (Options and the shell's Settings room, trap 45),
/// and two writers of one file would be two clocks racing over it.</para>
///
/// <para><b>MainWindow calls it twice and nowhere else</b>: <see cref="Tick"/> from the
/// once-a-second refresh, and <see cref="Switching"/> at the top of the character-switch
/// branch, before the session, the log or the Sky/Epic binding moves (DRA-1472 condition 2).
/// It wraps MainWindow's <see cref="AppSettings"/>, never a second snapshot (trap 13).</para>
/// </summary>
internal sealed class CharacterCardRuntime
{
    /// <summary>The process's instance, or null before MainWindow exists.</summary>
    public static CharacterCardRuntime? Current { get; private set; }

    private readonly MainWindow _main;
    private readonly CharacterCardWriter _writer;
    private readonly string _version;

    private CharacterCardRuntime(MainWindow main)
    {
        _main = main;
        _writer = new CharacterCardWriter(CharacterCardWriter.DefaultFolder, main._settings, main._settings.Save);
        _version = typeof(AppSettings).Assembly.GetName().Version?.ToString(3) ?? "";
    }

    public static void Start(MainWindow main) => Current ??= new CharacterCardRuntime(main);

    public string Folder => _writer.Folder;

    /// <summary>The live log has been read and no archive is under review. Before that the
    /// stores describe a half-replayed session, and during review they describe somebody
    /// else's evening.</summary>
    private bool Ready => _main._watcher.InitialIngestDone && !_main.IsReviewingArchive;

    /// <summary>The once-a-second call. Off reads nothing — not even the item catalog.</summary>
    public void Tick()
    {
        if (!_main._settings.CharacterCardEnabled) return;
        try { _writer.Tick(Sources(), Ready, DateTimeOffset.Now); }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>The character is about to change: write the one being left while its stores
    /// are still the bound ones.</summary>
    public void Switching()
    {
        if (!_main._settings.CharacterCardEnabled) return;
        try { _writer.Switching(Sources(), Ready, DateTimeOffset.Now); }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>"Write it now" — the sentence for the line under the button.</summary>
    public string WriteNow()
    {
        try
        {
            return _writer.WriteNow(Sources(), _main.IsReviewingArchive, DateTimeOffset.Now) switch
            {
                CharacterCardWriter.NowOutcome.Written => CharacterCardCopy.WrittenTo(_writer.CurrentPath ?? _writer.Folder),
                CharacterCardWriter.NowOutcome.NoCharacter => CharacterCardCopy.NothingToWrite,
                CharacterCardWriter.NowOutcome.Reviewing => CharacterCardCopy.NotWhileReviewing,
                _ => CharacterCardCopy.WriteFailed,
            };
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return CharacterCardCopy.WriteFailed;
        }
    }

    /// <summary>"Delete card files" — the sentence for the line under the button.</summary>
    public string Delete() =>
        CharacterCardCopy.Deleted(_writer.Delete(), _main._settings.CharacterCardEnabled);

    /// <summary>"Open folder". Creates it first, so the button is never a silent no-op on a
    /// profile that has not written a card yet.</summary>
    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_writer.Folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_writer.Folder)
            { UseShellExecute = true });
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>
    /// The followed character's stores, or null before a log is followed. The identity is the
    /// log FILE's (<see cref="CharacterLog.FromPath"/>), the same two halves the session
    /// archiver is given, and the session rows are filtered to exactly that pair, so a moment
    /// in which the two disagree answers no sessions rather than another character's.
    /// </summary>
    private CharacterCardSources? Sources()
    {
        if (_main._watcher.CurrentPath is not { } path || CharacterLog.FromPath(path) is not { } log)
            return null;
        var settings = _main._settings;
        return new CharacterCardSources(
            log.Character, log.Server, _main.QuestLedger, settings,
            (server, character) => [.. _main.StoredSessions().Where(r =>
                string.Equals(r.Server, server, StringComparison.Ordinal)
                && string.Equals(r.Character, character, StringComparison.Ordinal))],
            settings.LogFolder, _main.CurrentSnapshot().InferredClasses,
            ItemCatalog.Default, GuideCatalog.Default, _version, _main.ActiveSessionRowId);
    }

    /// <summary>
    /// The <c>EQBUDDY_EXPAND</c> facts. <c>characterCardEnabled</c> reads the SETTING, so a
    /// default flipped to on reddens the E2E default fact; <c>characterCardWrites</c> counts
    /// files actually published; <c>characterCardPath</c> is the followed file,
    /// <see cref="Uri.EscapeDataString"/>-encoded because the dump is space-separated and the
    /// folder name has a space in it (<c>none</c> before any character is known).
    /// </summary>
    public static string DebugFacts() =>
        Current is { } c
            ? $"characterCardEnabled={(c._main._settings.CharacterCardEnabled ? 1 : 0)} "
              + $"characterCardWrites={c._writer.Writes} "
              + $"characterCardPath={(c._writer.CurrentPath is { } p ? Uri.EscapeDataString(p) : "none")}"
            : "characterCardEnabled=0 characterCardWrites=0 characterCardPath=none";
}
