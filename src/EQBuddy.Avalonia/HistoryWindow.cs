using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

public sealed class HistoryWindow : Window
{
    private readonly HistoryViewModel _viewModel;
    private readonly ComboBox _charFilter = new() { Width = 180 };
    private readonly TextBox _searchBox = TextBox("");
    private readonly TextBlock _countText = AppTheme.DimText("");
    private readonly StackPanel _sessionRows = new();
    private readonly TextBlock _detailText = new()
    {
        Text = HistoryPresentation.SelectSessionText,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.TextBrush,
        FontFamily = FontFamily.Parse("Consolas, monospace"),
    };
    private readonly TextBox _noteBox = TextBox("");
    private readonly TextBox _tagsBox = TextBox("");
    private bool _rendering;

    public HistoryWindow(SessionRepository repository)
    {
        _viewModel = new HistoryViewModel(repository);
        Title = "EQBuddy - Session History";
        Width = 860;
        Height = 560;
        MinWidth = 640;
        MinHeight = 400;
        Background = AppTheme.BgBrush;
        StyleDarkCombo(_charFilter);
        Content = BuildContent();
        RenderAll();
    }

    private Control BuildContent()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(330)));
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var filter = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        filter.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _charFilter.SelectionChanged += (_, _) =>
        {
            if (_rendering || _charFilter.SelectedItem is not HistoryFilterOption selected) return;
            _viewModel.SelectedFilter = selected;
            _viewModel.RefreshSessions();
            RenderSessionsAndDetail();
        };
        filter.Children.Add(_charFilter);
        _searchBox.Margin = new Thickness(8, 0, 0, 0);
        _searchBox.TextChanged += (_, _) =>
        {
            if (_rendering) return;
            _viewModel.SearchText = _searchBox.Text ?? "";
            _viewModel.RefreshSessions();
            RenderSessionsAndDetail();
        };
        Grid.SetColumn(_searchBox, 1);
        filter.Children.Add(_searchBox);
        _countText.VerticalAlignment = VerticalAlignment.Center;
        _countText.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_countText, 2);
        filter.Children.Add(_countText);
        var import = AppTheme.IconButton("Import log...", "Parse an existing eqlog file into session history");
        import.FontSize = 12;
        import.Margin = new Thickness(8, 0, 0, 0);
        import.Click += OnImportLog;
        Grid.SetColumn(import, 3);
        filter.Children.Add(import);
        Grid.SetColumnSpan(filter, 2);
        root.Children.Add(filter);

        var sessionScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(Color.FromArgb(0x44, 0x1C, 0x19, 0x17)),
            Content = _sessionRows,
        };
        Grid.SetRow(sessionScroll, 1);
        root.Children.Add(sessionScroll);

        var detail = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        detail.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        detail.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        detail.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _detailText,
        });
        var meta = BuildMetaPanel();
        Grid.SetRow(meta, 1);
        detail.Children.Add(meta);
        Grid.SetRow(detail, 1);
        Grid.SetColumn(detail, 1);
        root.Children.Add(detail);
        return root;
    }

    private Control BuildMetaPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(LabeledBox("Notes:", _noteBox));
        panel.Children.Add(LabeledBox("Tags:", _tagsBox, new Thickness(0, 4, 0, 0)));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(ActionButton("Save notes", OnSaveMeta));
        buttons.Children.Add(ActionButton("Copy summary", OnCopySummary, new Thickness(8, 0, 0, 0)));
        buttons.Children.Add(ActionButton("Export JSON", OnExportJson, new Thickness(8, 0, 0, 0)));
        var delete = ActionButton("Delete session", OnDelete, new Thickness(24, 0, 0, 0));
        delete.Foreground = AppTheme.BadBrush;
        buttons.Children.Add(delete);
        panel.Children.Add(buttons);
        return panel;
    }

    private void RenderAll()
    {
        _rendering = true;
        try
        {
            _charFilter.Items.Clear();
            foreach (var filter in _viewModel.Filters) _charFilter.Items.Add(filter);
            _charFilter.SelectedItem = _viewModel.SelectedFilter;
            _searchBox.Text = _viewModel.SearchText;
        }
        finally
        {
            _rendering = false;
        }
        RenderSessionsAndDetail();
    }

    private void RenderSessionsAndDetail()
    {
        _sessionRows.Children.Clear();
        foreach (var item in _viewModel.Sessions)
            _sessionRows.Children.Add(SessionRow(item));
        RenderDetail();
    }

    private void RenderDetail()
    {
        _rendering = true;
        try
        {
            _countText.Text = _viewModel.CountText;
            _detailText.Text = _viewModel.DetailText;
            _noteBox.Text = _viewModel.Note;
            _tagsBox.Text = _viewModel.Tags;
            RefreshRowSelectionVisuals();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void OnSessionSelected(long id, KeyModifiers modifiers)
    {
        _viewModel.SelectSession(id, modifiers.HasFlag(KeyModifiers.Control));
        RenderDetail();
    }

    private void RefreshRowSelectionVisuals()
    {
        for (var i = 0; i < _sessionRows.Children.Count && i < _viewModel.Sessions.Count; i++)
        {
            if (_sessionRows.Children[i] is not Border row) continue;
            var selected = _viewModel.IsSelected(_viewModel.Sessions[i].Row.Id);
            row.Background = selected
                ? new SolidColorBrush(Color.FromArgb(0x88, 0x5A, 0x45, 0x13))
                : new SolidColorBrush(Color.FromArgb(0x55, 0x2A, 0x25, 0x1F));
            row.BorderBrush = selected ? AppTheme.AccentBrush : AppTheme.BorderBrush;
        }
    }

    private async void OnImportLog(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
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

        _detailText.Text = HistoryPresentation.BuildImporting(path);
        try
        {
            await _viewModel.ImportAsync(path);
            RenderAll();
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            _detailText.Text = $"Could not import {Path.GetFileName(path)}.";
        }
    }

    private void OnSaveMeta(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.Note = _noteBox.Text ?? "";
        _viewModel.Tags = _tagsBox.Text ?? "";
        _viewModel.SaveMetadata();
        RenderSessionsAndDetail();
    }

    private async void OnCopySummary(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.SelectedSummary is not { } summary) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(summary);
    }

    private async void OnExportJson(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
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

    private void OnDelete(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.DeleteSelected();
        RenderAll();
    }

    private static Control LabeledBox(string label, TextBox box, Thickness? margin = null)
    {
        var row = new Grid { Margin = margin ?? default };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.Children.Add(AppTheme.DimText(label, new Thickness(0, 0, 6, 0)));
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        return row;
    }

    private static Button ActionButton(string text,
        EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> action, Thickness? margin = null)
    {
        var button = AppTheme.IconButton(text, text);
        button.FontSize = 12;
        button.Margin = margin ?? default;
        button.Click += action;
        return button;
    }

    private static TextBox TextBox(string text)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(6, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F)),
            Foreground = AppTheme.TextBrush,
            BorderBrush = AppTheme.BorderBrush,
            CaretBrush = AppTheme.AccentBrush,
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0x77, 0xE3, 0xB3, 0x41)),
        };
        box.GotFocus += (_, _) => ApplyDarkInput(box);
        box.LostFocus += (_, _) => ApplyDarkInput(box);
        return box;
    }

    private static void ApplyDarkInput(TextBox box)
    {
        box.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
        box.Foreground = AppTheme.TextBrush;
        box.BorderBrush = AppTheme.BorderBrush;
    }

    private Border SessionRow(HistorySessionItem item)
    {
        var row = new Border
        {
            Margin = new Thickness(0, 0, 0, 2),
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0x2A, 0x25, 0x1F)),
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = item.DisplayText,
                FontSize = 12,
                Foreground = AppTheme.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        row.PointerPressed += (_, args) =>
        {
            OnSessionSelected(item.Row.Id, args.KeyModifiers);
            args.Handled = true;
        };
        row.PointerEntered += (_, _) =>
        {
            if (!_viewModel.IsSelected(item.Row.Id))
                row.Background = new SolidColorBrush(Color.FromArgb(0x77, 0x2F, 0x2A, 0x22));
        };
        row.PointerExited += (_, _) => RefreshRowSelectionVisuals();
        return row;
    }

    private static void StyleDarkCombo(ComboBox combo)
    {
        combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
        combo.Foreground = AppTheme.TextBrush;
        combo.BorderBrush = AppTheme.BorderBrush;
        combo.FontSize = 12;
        combo.GotFocus += (_, _) =>
        {
            combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
            combo.Foreground = AppTheme.TextBrush;
        };
        combo.LostFocus += (_, _) =>
        {
            combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
            combo.Foreground = AppTheme.TextBrush;
        };
    }
}
