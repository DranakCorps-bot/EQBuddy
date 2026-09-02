namespace EQBuddy.Core;

/// <summary>
/// Glue between live SessionStats and the SQLite history: periodic checkpoints of the
/// active session, finalization on rollover/character switch/app exit, and crash
/// recovery on startup. All writes run off the caller's thread.
/// </summary>
public sealed class SessionArchiver : IDisposable
{
    private readonly SessionRepository _repo;
    private readonly object _lock = new();
    private long _activeId;
    /// <summary>Monotonic session generation, bumped by every finalize (audit
    /// finding 6). The bare "_activeId == id" install test let a queued FIRST
    /// checkpoint (captured id 0) that completed after a finalize reset _activeId
    /// to 0 hand the OLD session's row id to the NEW session — every later
    /// checkpoint then overwrote finalized history. A stale generation's work is
    /// dead on arrival instead.</summary>
    private long _sessionGen;
    private string _server = "";
    private string _character = "";

    public SessionArchiver(SessionRepository repo)
    {
        _repo = repo;
        _repo.MarkInterruptedAsRecovered();
    }

    /// <summary>The live session's checkpointed row, or 0. The wiki pack's history pool
    /// excludes this row and takes the live session from the live snapshot instead —
    /// counting both would pool the same kills twice (#217 ask 2).</summary>
    public long ActiveRowId { get { lock (_lock) return _activeId; } }

    public void SetIdentity(string? server, string? character)
    {
        lock (_lock)
        {
            _server = server ?? "";
            _character = character ?? "";
        }
    }

    /// <summary>The (server, character) every row this archiver writes is KEYED by, empty
    /// while no character is being followed.
    ///
    /// Exposed because a reader that queries history for "this character" has to use the
    /// same two strings the writer used, and the two lanes source them differently — WPF
    /// from the log FILENAME (<c>LogWatcher.MostRecentlyActive</c>), Avalonia from the
    /// parsed log. `SessionRepository`'s lookups compare with SQL `=`, which is
    /// case-sensitive, so "close enough" is a query that silently returns nothing.
    /// Asking the writer is the only way to be sure (trap 4: one fact, two derivations).</summary>
    public (string Server, string Character) Identity
    {
        get { lock (_lock) return (_server, _character); }
    }

    /// <summary>Checkpoint the active session (no-op for noise-only sessions).</summary>
    public void Checkpoint(StatsSnapshot s)
    {
        if (!SessionRepository.IsMeaningful(s)) return;
        long id, gen; string server, character;
        lock (_lock) { id = _activeId; gen = _sessionGen; server = _server; character = _character; }
        if (server.Length == 0 || character.Length == 0) return;
        Task.Run(() =>
        {
            try { RunCheckpoint(gen, id, s, server, character); }
            catch (Exception ex) { CoreLog.Error(ex); }
        });
    }

    /// <summary>The queued half of <see cref="Checkpoint"/> — internal so tests can
    /// replay the delayed-completion interleaving Task.Run won't order on demand
    /// (finding 6: a checkpoint born in an earlier session must neither write nor
    /// install its row id once a finalize has moved the generation on).</summary>
    internal void RunCheckpoint(long gen, long id, StatsSnapshot s, string server, string character)
    {
        lock (_lock) { if (_sessionGen != gen) return; }   // session already finalized — stale work
        var newId = _repo.Checkpoint(id, s, server, character, "Active");
        lock (_lock) { if (_sessionGen == gen && _activeId == id) _activeId = newId; }
    }

    /// <summary>Finalize the active session with an end reason and start a fresh one.</summary>
    public void FinalizeActive(StatsSnapshot s, string endReason)
    {
        long id; string server, character;
        lock (_lock) { id = _activeId; server = _server; character = _character; _activeId = 0; _sessionGen++; }
        if (!SessionRepository.IsMeaningful(s) || server.Length == 0 || character.Length == 0)
            return;
        Task.Run(() =>
        {
            try { _repo.Checkpoint(id, s, server, character, endReason); }
            catch (Exception ex) { CoreLog.Error(ex); }
        });
    }

    /// <summary>Synchronous checkpoint — used right before opening the history view.</summary>
    public void CheckpointSync(StatsSnapshot s)
    {
        if (!SessionRepository.IsMeaningful(s)) return;
        long id, gen; string server, character;
        lock (_lock) { id = _activeId; gen = _sessionGen; server = _server; character = _character; }
        if (server.Length == 0 || character.Length == 0) return;
        try { RunCheckpoint(gen, id, s, server, character); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    /// <summary>Synchronous finalize for application shutdown (SESSION-007).</summary>
    public void FinalizeActiveSync(StatsSnapshot s, string endReason)
    {
        long id; string server, character;
        lock (_lock) { id = _activeId; server = _server; character = _character; _activeId = 0; _sessionGen++; }
        if (!SessionRepository.IsMeaningful(s) || server.Length == 0 || character.Length == 0)
            return;
        try { _repo.Checkpoint(id, s, server, character, endReason); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    public void Dispose() { }
}
