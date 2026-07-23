using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly SessionRepository _repository;
    private readonly HistoryImportService _importService;
    private readonly List<long> _selectionOrder = [];
    private HistoryFilterOption _selectedFilter = HistoryFilterOption.All;
    private string _searchText = "";
    private string _countText = HistoryPresentation.BuildCount(0);
    private string _detailText = HistoryPresentation.SelectSessionText;
    private string _note = "";
    private string _tags = "";
    private SessionRow? _selectedRow;
    private StatsSnapshot? _selectedSnapshot;

    public HistoryViewModel(SessionRepository repository, HistoryImportService? importService = null)
    {
        _repository = repository;
        _importService = importService ?? new HistoryImportService(repository);
        RefreshFilters();
        RefreshSessions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<HistoryFilterOption> Filters { get; } = [];
    public ObservableCollection<HistorySessionItem> Sessions { get; } = [];
    public IReadOnlySet<long> SelectedSessionIds => _selectionOrder.ToHashSet();

    public HistoryFilterOption SelectedFilter
    {
        get => _selectedFilter;
        set => SetField(ref _selectedFilter, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value ?? "");
    }

    public string CountText { get => _countText; private set => SetField(ref _countText, value); }
    public string DetailText { get => _detailText; private set => SetField(ref _detailText, value); }

    public string Note
    {
        get => _note;
        set => SetField(ref _note, value ?? "");
    }

    public string Tags
    {
        get => _tags;
        set => SetField(ref _tags, value ?? "");
    }

    public bool HasSelectedSession => _selectedRow is not null && _selectedSnapshot is not null;
    public string? SelectedSummary => HasSelectedSession
        ? HistoryPresentation.BuildOverview(_selectedRow!, _selectedSnapshot!)
        : null;
    public string? ExportFileName => HasSelectedSession ? HistoryPresentation.BuildExportFileName(_selectedRow!) : null;
    public string? ExportJson => HasSelectedSession ? HistoryPresentation.BuildExportJson(_selectedSnapshot!) : null;

    public void RefreshFilters()
    {
        var selectedServer = SelectedFilter.Server;
        var selectedCharacter = SelectedFilter.Character;
        Filters.Clear();
        Filters.Add(HistoryFilterOption.All);
        foreach (var (server, character) in _repository.Characters())
            Filters.Add(HistoryPresentation.BuildFilter(server, character));
        SelectedFilter = Filters.FirstOrDefault(filter =>
            filter.Server == selectedServer && filter.Character == selectedCharacter) ?? Filters[0];
    }

    public void RefreshSessions()
    {
        var rows = _repository.Query(SelectedFilter.Server, SelectedFilter.Character, SearchText);
        Sessions.Clear();
        foreach (var row in rows)
            Sessions.Add(new HistorySessionItem(row, HistoryPresentation.BuildSessionRow(row)));
        CountText = HistoryPresentation.BuildCount(Sessions.Count);
        ClearSelection();
    }

    public void SelectSession(long id, bool additive)
    {
        if (Sessions.All(item => item.Row.Id != id)) return;

        if (!additive)
            _selectionOrder.Clear();

        var existing = _selectionOrder.IndexOf(id);
        if (additive && existing >= 0)
            _selectionOrder.RemoveAt(existing);
        else
        {
            if (existing >= 0) _selectionOrder.RemoveAt(existing);
            _selectionOrder.Add(id);
        }

        UpdateSelectionDetail();
        OnPropertyChanged(nameof(SelectedSessionIds));
    }

    public bool IsSelected(long id) => _selectionOrder.Contains(id);

    public void SaveMetadata()
    {
        if (_selectedRow is null) return;
        _repository.SetNoteTags(_selectedRow.Id, Note.Trim(), Tags.Trim());
        RefreshSessions();
    }

    public void DeleteSelected()
    {
        if (_selectedRow is null) return;
        _repository.Delete(_selectedRow.Id);
        RefreshFilters();
        RefreshSessions();
    }

    public async Task ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        DetailText = HistoryPresentation.BuildImporting(path);
        var result = await _importService.ImportAsync(path, cancellationToken);
        RefreshFilters();
        RefreshSessions();
        DetailText = result.Message;
    }

    private void UpdateSelectionDetail()
    {
        _selectedRow = null;
        _selectedSnapshot = null;

        if (_selectionOrder.Count == 2)
        {
            var selected = Sessions.Where(item => _selectionOrder.Contains(item.Row.Id)).ToList();
            if (selected.Count == 2)
                DetailText = HistoryPresentation.BuildComparison(
                    selected[0].Row, _repository.LoadSnapshot(selected[0].Row.Id),
                    selected[1].Row, _repository.LoadSnapshot(selected[1].Row.Id));
            Note = "";
            Tags = "";
            NotifyCommandState();
            return;
        }

        var latestId = _selectionOrder.LastOrDefault();
        var latest = Sessions.FirstOrDefault(item => item.Row.Id == latestId);
        if (latest is null)
        {
            DetailText = HistoryPresentation.SelectSessionText;
            Note = "";
            Tags = "";
            NotifyCommandState();
            return;
        }

        _selectedRow = latest.Row;
        _selectedSnapshot = _repository.LoadSnapshot(latest.Row.Id);
        Note = latest.Row.Note;
        Tags = latest.Row.Tags;
        DetailText = _selectedSnapshot is null
            ? HistoryPresentation.MissingSessionText
            : HistoryPresentation.BuildOverview(latest.Row, _selectedSnapshot);
        NotifyCommandState();
    }

    private void ClearSelection()
    {
        _selectionOrder.Clear();
        _selectedRow = null;
        _selectedSnapshot = null;
        Note = "";
        Tags = "";
        DetailText = HistoryPresentation.SelectSessionText;
        OnPropertyChanged(nameof(SelectedSessionIds));
        NotifyCommandState();
    }

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(HasSelectedSession));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(ExportFileName));
        OnPropertyChanged(nameof(ExportJson));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
