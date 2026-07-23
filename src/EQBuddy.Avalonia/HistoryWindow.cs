using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

public sealed partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _viewModel;
    private bool _refreshing;

    public HistoryWindow(SessionRepository repository)
    {
        _viewModel = new HistoryViewModel(repository);
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || CharFilter.SelectedItem is not HistoryFilterOption selected) return;
        _viewModel.SelectedFilter = selected;
        RefreshSessions();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_refreshing) return;
        _viewModel.SearchText = SearchBox.Text ?? "";
        RefreshSessions();
    }

    private void OnSessionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;

        foreach (var removed in e.RemovedItems.OfType<HistorySessionItem>())
            _viewModel.SelectSession(removed.Row.Id, additive: true);
        foreach (var added in e.AddedItems.OfType<HistorySessionItem>())
            _viewModel.SelectSession(added.Row.Id, additive: true);
    }

    private void RefreshSessions()
    {
        _refreshing = true;
        try
        {
            _viewModel.RefreshSessions();
            SessionList.SelectedItems?.Clear();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async void OnImportLog(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import an existing log into session history",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("EverQuest logs") { Patterns = ["eqlog_*.txt", "*.txt"] },
                FilePickerFileTypes.All,
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;

        try
        {
            await _viewModel.ImportAsync(path);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
        }
    }

    private void OnSaveMeta(object? sender, RoutedEventArgs e)
    {
        _viewModel.Note = NoteBox.Text ?? "";
        _viewModel.Tags = TagsBox.Text ?? "";
        _viewModel.SaveMetadata();
    }

    private async void OnCopySummary(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSummary is not { } summary) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(summary);
    }

    private async void OnExportJson(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.ExportFileName is not { } fileName || _viewModel.ExportJson is not { } json) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export session JSON",
            SuggestedFileName = fileName,
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });
        var path = file?.TryGetLocalPath();
        if (path is not null) await File.WriteAllTextAsync(path, json);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelected();
    }
}
