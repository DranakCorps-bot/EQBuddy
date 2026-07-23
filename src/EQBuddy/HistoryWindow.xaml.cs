using System.IO;
using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Thin WPF view over the shared HistoryViewModel (EQBuddy.UI.Shared) — the same
/// ViewModel drives the Avalonia HistoryWindow, so behavior stays identical across
/// platforms. This class only maps WPF controls/events onto the ViewModel and
/// renders the structured detail (native bar rows via BreakdownRows).
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _vm;
    private bool _syncing;

    public HistoryWindow(SessionRepository repo)
    {
        InitializeComponent();
        _vm = new HistoryViewModel(repo);

        CharFilter.ItemsSource = _vm.Filters;
        CharFilter.SelectedItem = _vm.SelectedFilter;
        SessionList.ItemsSource = _vm.Sessions;
        SessionList.DisplayMemberPath = nameof(HistorySessionItem.DisplayText);

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.CountText): CountText.Text = _vm.CountText; break;
                case nameof(_vm.Note): NoteBox.Text = _vm.Note; break;
                case nameof(_vm.Tags): TagsBox.Text = _vm.Tags; break;
                case nameof(_vm.DetailText):
                case nameof(_vm.SelectedDetail): RenderDetail(); break;
            }
        };
        CountText.Text = _vm.CountText;
        RenderDetail();
    }

    /// <summary>Structured detail gets native bar rows; comparison/import/empty states
    /// render the ViewModel's plain text.</summary>
    private void RenderDetail()
    {
        if (_vm.SelectedDetail is { } d)
        {
            DetailText.Text = d.HeaderText;
            BreakdownRows.FillRows(this, DamageVisualList, d.DamageRows);
            DamageVisualLabel.Visibility = d.DamageRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            BreakdownRows.FillRows(this, HealVisualList, d.HealRows);
            HealVisualLabel.Visibility = d.HealRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DetailRest.Text = d.RestText;
            DetailRest.Visibility = d.RestText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            DetailText.Text = _vm.DetailText;
            DamageVisualList.Items.Clear();
            HealVisualList.Items.Clear();
            DamageVisualLabel.Visibility = HealVisualLabel.Visibility = Visibility.Collapsed;
            DetailRest.Visibility = Visibility.Collapsed;
        }
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || CharFilter.SelectedItem is not HistoryFilterOption filter) return;
        _syncing = true;
        _vm.SelectedFilter = filter;
        _vm.RefreshSessions();
        _syncing = false;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        _vm.SearchText = SearchBox.Text;
        _vm.RefreshSessions();
        _syncing = false;
    }

    private void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        var selected = SessionList.SelectedItems.OfType<HistorySessionItem>().ToList();
        if (selected.Count == 0) return;   // list refresh cleared it — ViewModel already knows
        _syncing = true;
        // Two selected → comparison (COMPARE-*); otherwise the last item is the session.
        _vm.SelectSession(selected[0].Row.Id, additive: false);
        if (selected.Count >= 2)
            _vm.SelectSession(selected[^1].Row.Id, additive: true);
        _syncing = false;
    }

    private void OnSaveMeta(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelectedSession) return;
        _syncing = true;
        _vm.Note = NoteBox.Text;
        _vm.Tags = TagsBox.Text;
        _vm.SaveMetadata();
        _syncing = false;
    }

    private void OnCopySummary(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedSummary is { } summary) Clipboard.SetText(summary);
    }

    private void OnExportJson(object sender, RoutedEventArgs e)
    {
        if (_vm.ExportFileName is not { } name || _vm.ExportJson is not { } json) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = name, Filter = "JSON|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, json);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedRow is not { } row) return;
        if (MessageBox.Show(this,
                $"Delete the {row.StartLocal:MMM d h:mm tt} session for {row.Character}? This cannot be undone.",
                "Delete session", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        _syncing = true;
        _vm.DeleteSelected();
        CharFilter.SelectedItem = _vm.SelectedFilter;
        _syncing = false;
    }

    private async void OnImportLog(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import an existing log into session history",
            Filter = "EQ log files (eqlog_*.txt)|eqlog_*.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        _syncing = true;
        try
        {
            await _vm.ImportAsync(dlg.FileName);
            CharFilter.SelectedItem = _vm.SelectedFilter;
        }
        catch (Exception ex) { CoreLog.Error(ex); }
        finally { _syncing = false; }
    }
}
