using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

public sealed class HistoryViewModelTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("eqbuddy-history-vm-").FullName;
    private readonly SessionRepository _repository;

    public HistoryViewModelTests() =>
        _repository = new SessionRepository(Path.Combine(_directory, "history.db"));

    public void Dispose()
    {
        _repository.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void RefreshBuildsFiltersAndPreservesSelectedFilter()
    {
        AddSession("freeport", "Kaybek");
        AddSession("qeynos", "Douglas");
        var viewModel = new HistoryViewModel(_repository);
        var kaybek = Assert.Single(viewModel.Filters, filter => filter.Character == "Kaybek");

        viewModel.SelectedFilter = kaybek;
        viewModel.RefreshSessions();
        viewModel.RefreshFilters();

        Assert.Equal("Kaybek", viewModel.SelectedFilter.Character);
        Assert.Single(viewModel.Sessions);
        Assert.Equal("1 session", viewModel.CountText);
    }

    [Fact]
    public void SearchSelectionComparisonAndDeselectUpdateState()
    {
        AddSession("freeport", "Kaybek");
        AddSession("qeynos", "Douglas");
        var viewModel = new HistoryViewModel(_repository);
        var first = viewModel.Sessions[0].Row.Id;
        var second = viewModel.Sessions[1].Row.Id;

        viewModel.SelectSession(first, additive: false);
        Assert.True(viewModel.HasSelectedSession);
        Assert.Contains("Kills", viewModel.DetailText);

        viewModel.SelectSession(second, additive: true);
        Assert.False(viewModel.HasSelectedSession);
        Assert.Contains("SESSION COMPARISON", viewModel.DetailText);

        viewModel.SelectSession(second, additive: true);
        Assert.True(viewModel.HasSelectedSession);
        Assert.Single(viewModel.SelectedSessionIds);

        viewModel.SearchText = "does-not-exist";
        viewModel.RefreshSessions();
        Assert.Empty(viewModel.Sessions);
        Assert.Equal("0 sessions", viewModel.CountText);
        Assert.Equal(HistoryPresentation.SelectSessionText, viewModel.DetailText);
    }

    [Fact]
    public void SaveMetadataTrimsValuesAndDeleteRefreshesRows()
    {
        AddSession("freeport", "Kaybek");
        var viewModel = new HistoryViewModel(_repository);
        viewModel.SelectSession(viewModel.Sessions[0].Row.Id, additive: false);
        viewModel.Note = "  great camp  ";
        viewModel.Tags = "  solo  ";

        viewModel.SaveMetadata();

        var stored = Assert.Single(_repository.Query());
        Assert.Equal("great camp", stored.Note);
        Assert.Equal("solo", stored.Tags);

        viewModel.SelectSession(viewModel.Sessions[0].Row.Id, additive: false);
        viewModel.DeleteSelected();
        Assert.Empty(viewModel.Sessions);
        Assert.Equal(HistoryPresentation.SelectSessionText, viewModel.DetailText);
    }

    [Fact]
    public async Task ImportStreamsMeaningfulSessionsAndRefreshesViewModel()
    {
        var path = Path.Combine(_directory, "eqlog_Kaybek_freeport.txt");
        await File.WriteAllLinesAsync(path,
        [
            "[Sat Jul 18 15:00:00 2026] You have entered Clan Crushbone.",
            "[Sat Jul 18 15:00:05 2026] You slash orc pawn for 10 points of damage.",
            "[Sat Jul 18 15:00:10 2026] You have slain orc pawn!",
        ]);
        var viewModel = new HistoryViewModel(_repository);

        await viewModel.ImportAsync(path);

        Assert.Single(viewModel.Sessions);
        Assert.Contains("Imported 1 session", viewModel.DetailText);
        Assert.Equal("Kaybek", Assert.Single(viewModel.Filters, filter => filter.Character == "Kaybek").Character);
    }

    [Fact]
    public async Task ImportSplitsGapSeparatedSessionsAndReimportDoesNotDuplicate()
    {
        var path = Path.Combine(_directory, "eqlog_Kaybek_freeport.txt");
        await File.WriteAllLinesAsync(path,
        [
            "[Sat Jul 18 15:00:00 2026] You slash orc pawn for 10 points of damage.",
            "[Sat Jul 18 15:00:05 2026] You have slain orc pawn!",
            "[Sat Jul 18 16:01:00 2026] You slash orc centurion for 20 points of damage.",
            "[Sat Jul 18 16:01:05 2026] You have slain orc centurion!",
        ]);
        var service = new HistoryImportService(_repository);

        var first = await service.ImportAsync(path);
        var second = await service.ImportAsync(path);

        Assert.Equal(2, first.ImportedSessions);
        Assert.Equal(2, second.ImportedSessions);
        Assert.Equal(2, _repository.Query().Count);
    }

    [Fact]
    public async Task ImportSkipsEmptyLog()
    {
        var path = Path.Combine(_directory, "eqlog_Kaybek_freeport.txt");
        await File.WriteAllTextAsync(path, "");

        var result = await new HistoryImportService(_repository).ImportAsync(path);

        Assert.Equal(0, result.ImportedSessions);
        Assert.Empty(_repository.Query());
    }

    [Fact]
    public async Task ImportHonorsCancellationAndInvalidPaths()
    {
        var service = new HistoryImportService(_repository);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ImportAsync(Path.Combine(_directory, "missing.txt"), cancelled.Token));
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.ImportAsync(Path.Combine(_directory, "missing.txt")));
    }

    private long AddSession(string server, string character) =>
        _repository.Checkpoint(0, HistoryPresentationTests.Snapshot(), server, character, "Manual");
}
