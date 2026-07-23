using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public sealed class HistoryImportService(SessionRepository repository)
{
    public Task<HistoryImportResult> ImportAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Import(path, cancellationToken), cancellationToken);

    private HistoryImportResult Import(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = CharacterLog.FromPath(path);
        var character = info?.Character ?? Path.GetFileNameWithoutExtension(path);
        var server = info?.Server ?? "imported";
        var imported = 0;
        var stats = new SessionStats { CharacterName = character, ServerName = server };
        stats.SessionEnding += snapshot =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SessionRepository.IsMeaningful(snapshot)) return;
            repository.Checkpoint(0, snapshot, server, character, "ImportedBoundary");
            imported++;
        };

        foreach (var line in File.ReadLines(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gameEvent = LogParser.Parse(line);
            if (gameEvent is not null) stats.Apply(gameEvent);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var final = stats.Snapshot();
        if (SessionRepository.IsMeaningful(final))
        {
            repository.Checkpoint(0, final, server, character, "ImportedBoundary");
            imported++;
        }

        return HistoryPresentation.BuildImportResult(path, imported);
    }
}
