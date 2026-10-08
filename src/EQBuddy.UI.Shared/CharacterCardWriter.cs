using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **WHEN THE CHARACTER CARD IS WRITTEN, WHERE, AND WHAT MAY BE DELETED** (DRA-1288 D2,
/// <c>docs/plans/DRA-1288.md</c> decisions 1–4). The host (<c>EQBuddy/CharacterCardRuntime</c>)
/// hands it the followed character's <see cref="CharacterCardSources"/> once a tick; every
/// decision is made here, in UI.Shared, so <c>CharacterCardWriterTests</c> can drive it with a
/// stated clock rather than a window (docs/TestPlan.md §5).
///
/// <list type="bullet">
/// <item><b>Off writes nothing.</b> <see cref="AppSettings.CharacterCardEnabled"/> is read on
/// every call, so the tick after the player turns it off is already silent. Only
/// <see cref="WriteNow"/> — the player's own one-shot press — writes while it is off.</item>
/// <item><b>Changed = the rendered text differs, ignoring the written-at line</b> (decision 3,
/// through <see cref="CharacterCardPresentation.WithoutWrittenAt"/>). No fingerprint to keep in
/// step with the projection (trap 4).</item>
/// <item><b>At most once per <see cref="MinInterval"/> per file</b> (decision 2). A change is
/// written on the first tick the interval allows — within five seconds of it. That interval
/// suppresses a re-render only for the five seconds after a write. When nothing has changed,
/// <see cref="Tick"/> still renders every second on the UI thread: a
/// <c>SessionRepository.Query</c>, a dump-folder enumeration, and an inventory and faction
/// parse.</item>
/// <item><b>Atomic</b>: <see cref="WholeFilePublish.Write"/>, so an app reading the file never
/// sees half of it (trap 84; not <c>ProfileJson</c>, whose <c>File.Replace</c> measured worst
/// under a concurrent reader).</item>
/// <item><b>A switch renders the OUTGOING character first</b> (<see cref="Switching"/>,
/// DRA-1472 binding condition 2): guide progress counts the BOUND character's Sky/Epic working
/// copy (<see cref="QuestTickBinding"/>), so the outgoing card must be rendered before the
/// binding moves or it would carry the incoming character's counts.</item>
/// <item><b>Delete removes only names this writer recorded</b> (decision 4, Reviewer note (d)),
/// in <see cref="AppSettings.CharacterCardFiles"/>, so a player's own <c>.md</c> beside the
/// cards survives — including one that happens to look like a card's name.</item>
/// </list>
/// </summary>
public sealed class CharacterCardWriter
{
    /// <summary>The folder under the profile root (decision 1).</summary>
    public const string FolderName = "Character cards";

    /// <summary>The fewest seconds between two writes of one file (decision 2), matching
    /// <c>HelperSources.Read</c>'s 5 s cache.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);

    /// <summary>Where the cards go for this profile. Under the profile root, so tests and
    /// captures land in their redirected <c>EQBUDDY_APPDATA</c> (traps 68/69).</summary>
    public static string DefaultFolder => AppPaths.File(FolderName);

    private readonly AppSettings _settings;
    private readonly Action _persist;

    private string? _boundPath;
    private string? _lastText;
    private DateTimeOffset? _lastWriteAt;
    private string? _lastError;

    /// <param name="folder">The cards folder — <see cref="DefaultFolder"/> in the app, a temp
    /// directory in a test.</param>
    /// <param name="persist">Saves <paramref name="settings"/> after a new file name is
    /// recorded or a delete forgets some. The app's own <c>AppSettings.Save</c>.</param>
    public CharacterCardWriter(string folder, AppSettings settings, Action persist)
    {
        Folder = folder;
        _settings = settings;
        _persist = persist;
    }

    public string Folder { get; }

    /// <summary>Files actually published this process — the dump's <c>characterCardWrites</c>.
    /// Counted after <see cref="WholeFilePublish.Write"/> answered, so a dropped version is not
    /// a write.</summary>
    public int Writes { get; private set; }

    /// <summary>The file the writer is following now, or null before any character is known.</summary>
    public string? CurrentPath => _boundPath;

    /// <summary>
    /// The file a character's card lives in: <c>&lt;Name&gt; (&lt;server&gt;).md</c>
    /// (decision 1), or null when the character is not known. Both halves come from the log
    /// FILE's name, so they are already file-safe; any character a file name cannot hold is
    /// replaced anyway rather than trusted.
    /// </summary>
    public static string? FileNameFor(string? name, string? server)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(server)) return null;
        var bad = Path.GetInvalidFileNameChars();
        string Safe(string s) => new([.. s.Trim().Select(c => bad.Contains(c) ? '_' : c)]);
        return $"{Safe(name)} ({Safe(server)}).md";
    }

    public string? PathFor(CharacterCardSources? sources) =>
        sources is null || FileNameFor(sources.Name, sources.Server) is not { } file
            ? null
            : Path.Combine(Folder, file);

    /// <summary>
    /// The host's once-a-tick call. Writes the followed character's card when the switch is on,
    /// the host is <paramref name="ready"/> (live log, initial replay finished — never while an
    /// archived log is being reviewed), the interval allows it and the text changed.
    /// </summary>
    /// <returns>True when a file was published.</returns>
    public bool Tick(CharacterCardSources? sources, bool ready, DateTimeOffset now)
    {
        if (!_settings.CharacterCardEnabled || !ready || PathFor(sources) is not { } path) return false;
        Bind(path);
        if (_lastWriteAt is { } last && now - last < MinInterval) return false;
        return Publish(path, Render(sources!, now), now, force: false);
    }

    /// <summary>
    /// The character is about to change. Call it BEFORE the host moves anything — the session
    /// identity, the log, the Sky/Epic binding — so <paramref name="outgoing"/> still describes
    /// the character being left. Writes that character's card now if it changed, interval or no
    /// interval: this is the last moment its stores are the bound ones.
    /// </summary>
    public bool Switching(CharacterCardSources? outgoing, bool ready, DateTimeOffset now)
    {
        if (!_settings.CharacterCardEnabled || !ready || PathFor(outgoing) is not { } path) return false;
        Bind(path);
        return Publish(path, Render(outgoing!, now), now, force: false);
    }

    /// <summary>What <see cref="WriteNow"/> managed, for the line under the button.</summary>
    public enum NowOutcome { Written, NoCharacter, Reviewing, Failed }

    /// <summary>
    /// The player's "Write it now": writes the followed character's card immediately, also
    /// while the switch is off and also when nothing changed (the press is the reason). Never
    /// while an archived log is being reviewed — the followed character then is not the player.
    /// </summary>
    public NowOutcome WriteNow(CharacterCardSources? sources, bool reviewing, DateTimeOffset now)
    {
        if (reviewing) return NowOutcome.Reviewing;
        if (PathFor(sources) is not { } path) return NowOutcome.NoCharacter;
        Bind(path);
        return Publish(path, Render(sources!, now), now, force: true) ? NowOutcome.Written : NowOutcome.Failed;
    }

    /// <summary>
    /// "Delete card files": removes every file this writer recorded and nothing else, and
    /// forgets them. A recorded name that is not a bare <c>.md</c> file name — a hand-edited
    /// settings.json carrying <c>..\x.md</c> — is forgotten and NEVER deleted: the rule is a
    /// file in THIS folder, not a path somebody wrote down.
    /// </summary>
    /// <returns>How many files were deleted.</returns>
    public int Delete()
    {
        var deleted = 0;
        foreach (var name in _settings.CharacterCardFiles.ToList())
        {
            if (!IsCardName(name)) continue;
            var path = Path.Combine(Folder, name);
            try
            {
                if (File.Exists(path)) { File.Delete(path); deleted++; }
                var pending = path + WholeFilePublish.PendingSuffix;
                if (File.Exists(pending)) File.Delete(pending);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(ex);
                continue;   // kept on the list, so the next press tries again
            }
        }
        _settings.CharacterCardFiles.RemoveAll(n => !IsCardName(n) || !File.Exists(Path.Combine(Folder, n)));
        _persist();
        // The text that was on disk is gone, so the next allowed tick writes the card afresh.
        _boundPath = null;
        _lastText = null;
        _lastWriteAt = null;
        return deleted;
    }

    private static bool IsCardName(string? name) =>
        !string.IsNullOrEmpty(name)
        && name == Path.GetFileName(name)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static string Render(CharacterCardSources sources, DateTimeOffset now) =>
        CharacterCardPresentation.Render(CharacterCard.From(sources, now));

    /// <summary>Follow <paramref name="path"/>. On a new file, what is already on disk is the
    /// baseline, so a relaunch over an unchanged character writes nothing.</summary>
    private void Bind(string path)
    {
        if (string.Equals(path, _boundPath, StringComparison.OrdinalIgnoreCase)) return;
        _boundPath = path;
        _lastWriteAt = null;
        _lastText = File.Exists(path)
            ? CharacterCardPresentation.WithoutWrittenAt(WholeFilePublish.Read(path))
            : null;
    }

    private bool Publish(string path, string text, DateTimeOffset now, bool force)
    {
        var compared = CharacterCardPresentation.WithoutWrittenAt(text);
        if (!force && compared == _lastText) return false;
        try
        {
            Directory.CreateDirectory(Folder);
            if (WholeFilePublish.Write(path, text) == WholeFilePublish.Outcome.SkippedToKeepThePreviousFileWhole)
                return false;   // the previous card stayed whole; the next allowed tick tries again
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(ex);
            return false;
        }
        _lastText = compared;
        _lastWriteAt = now;
        _lastError = null;
        Writes++;
        Record(Path.GetFileName(path));
        return true;
    }

    private void Record(string name)
    {
        if (_settings.CharacterCardFiles.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
        _settings.CharacterCardFiles.Add(name);
        _persist();
    }

    /// <summary>A failure is logged once, not once a second: an unwritable folder would
    /// otherwise fill error.log at the tick rate.</summary>
    private void Log(Exception ex)
    {
        if (ex.Message == _lastError) return;
        _lastError = ex.Message;
        CoreLog.Error($"Character card: {ex.Message}");
    }
}
