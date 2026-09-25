using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **"Help improve EQBuddy" — the opt-in heartbeat's Settings row** (DRA-362 TEL-PR3; copy is
/// §8.3 §B–§D, drawn from <see cref="TelemetryCopy"/>). Composed by
/// <see cref="SettingsBehaviorView"/>, so the one view reaches both hosts (Options and the
/// shell's Settings room); each host's Behavior block builds its own instance (trap 45).
///
/// <para><b>Its own file, and deliberately outside the prose-to-hover pass.</b> The copy under
/// the toggle is the consent disclosure — TEL-001: the toggle "carries the entire payload in
/// its own copy" — and §8.3 §B says it is <i>shown always</i>. Moving it behind an ⓘ, which is
/// what <see cref="SettingsProsePolicy"/> would do to paragraphs this long, would hide the one
/// thing the row exists to show. So it does not live in a block that pass measures.</para>
///
/// <para><b>It holds no clock of its own for sending.</b> Every action is a call into
/// <see cref="TelemetryRuntime"/>, the process's one scheduler. Its only timer repaints the
/// status line's relative time while the row is on screen.</para>
///
/// <para><b>The status line cannot resize its row</b> (trap 12): it sits in a one-cell
/// <see cref="Grid"/> with every string it can show laid out HIDDEN beside it, so the cell is
/// always the width of the widest, whatever the clock says.</para>
/// </summary>
internal sealed class SettingsTelemetryView
{
    private readonly Func<object, object> _resource;
    private readonly Func<bool> _hostReady;

    private CheckBox _toggle = null!;
    private StackPanel _copy = null!;
    private TextBlock _status = null!;
    private Grid _statusCell = null!;
    private Button _delete = null!;
    private DispatcherTimer? _repaint;
    private bool _syncing;

    public SettingsTelemetryView(Func<object, object> resource, Func<bool> ready)
    {
        _resource = resource;
        _hostReady = ready;
    }

    private static TelemetryRuntime? Runtime => TelemetryRuntime.Current;

    private UIElement? _block;
    public UIElement Block => _block ??= Build();

    /// <summary>Facts off the BUILT controls, for the host's dump (trap 42).</summary>
    public string DebugFacts() => _block is null
        ? ""
        : $"telemetryToggle={(_toggle.IsChecked == true ? 1 : 0)} " +
          $"telemetryDeleteEnabled={(_delete.IsEnabled ? 1 : 0)} " +
          $"telemetryStatusShown={(_statusCell.Visibility == Visibility.Visible ? 1 : 0)}";

    private UIElement Build()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        var label = new TextBlock { Text = TelemetryCopy.ToggleLabel, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _toggle = new CheckBox { Content = label, IsChecked = Runtime?.Enabled == true };
        _toggle.Checked += (_, _) => Toggled();
        _toggle.Unchecked += (_, _) => Toggled();
        panel.Children.Add(_toggle);

        _statusCell = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(20, 4, 0, 0) };
        foreach (var sample in TelemetryCopy.StatusWidthSamples)
            _statusCell.Children.Add(StatusText(sample, Visibility.Hidden));
        _status = StatusText("", Visibility.Visible);
        _statusCell.Children.Add(_status);
        panel.Children.Add(_statusCell);

        _copy = new StackPanel { Margin = new Thickness(20, 6, 0, 0) };
        panel.Children.Add(_copy);

        _delete = new Button
        {
            Content = TelemetryCopy.DeleteButton, Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(20, 8, 0, 0),
        };
        ToolTipService.SetShowOnDisabled(_delete, true);
        _delete.Click += async (_, _) => await DeleteClicked();
        panel.Children.Add(_delete);

        Paint();
        panel.Loaded += (_, _) =>
        {
            _repaint ??= new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Background,
                (_, _) => PaintStatus(), panel.Dispatcher);
            _repaint.Start();
            Paint();
            // Screenshot hook (shoot.ps1 'options-telemetry-*'): the row is last in the
            // longest Settings tab, so a shot of the tab is a picture of everything above it.
            if (Environment.GetEnvironmentVariable("EQBUDDY_SCROLL_TELEMETRY") == "1")
                panel.Dispatcher.BeginInvoke(() => panel.BringIntoView(),
                    DispatcherPriority.ApplicationIdle);
        };
        panel.Unloaded += (_, _) => _repaint?.Stop();
        return panel;
    }

    private void Toggled()
    {
        if (_syncing || !_hostReady() || Runtime is not { } runtime) return;
        runtime.SetEnabled(_toggle.IsChecked == true);
        Paint();
    }

    private async Task DeleteClicked()
    {
        if (Runtime is not { Enabled: true } runtime) return;
        var confirm = new TelemetryDeleteWindow { Owner = Window.GetWindow(_delete) };
        confirm.ShowDialog();
        if (!confirm.Confirmed) return;
        _delete.IsEnabled = false;
        await runtime.DeleteAsync();
        Paint();
    }

    /// <summary>Everything from the runtime's state: the toggle (another host may have
    /// flipped it), the copy for ON or OFF, the status line and the delete button.</summary>
    private void Paint()
    {
        var runtime = Runtime;
        var on = runtime?.Enabled == true;
        if (_toggle.IsChecked != on)
        {
            _syncing = true;
            _toggle.IsChecked = on;
            _syncing = false;
        }
        BuildCopy(on, runtime?.InstallId);

        _delete.IsEnabled = on && runtime is { Deleting: false };
        // Trap 17: a disabled button with no disabled visual reads as a live one.
        _delete.Opacity = _delete.IsEnabled ? 1 : 0.45;
        _delete.ToolTip = on ? null : TelemetryCopy.DeleteDisabledTip;
        PaintStatus();
    }

    private void PaintStatus()
    {
        var line = Runtime?.Status(DateTime.UtcNow);
        _status.Text = line ?? "";
        _statusCell.Visibility = line is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool? _copyFor;

    private void BuildCopy(bool on, string? installId)
    {
        if (_copyFor == on) return;
        _copyFor = on;
        _copy.Children.Clear();
        if (on)
        {
            _copy.Children.Add(Para(Bold(TelemetryCopy.OnHeading), Plain(" " + TelemetryCopy.OnLead)));
            AddFields(TelemetryCopy.OnFields(TelemetryCopy.IdPrefix(installId)));
            _copy.Children.Add(Para(Plain(TelemetryCopy.OnRetention)));
        }
        else
        {
            _copy.Children.Add(Para(Bold(TelemetryCopy.OffHeading)));
            _copy.Children.Add(Para(Plain(TelemetryCopy.OffLead)));
            AddFields(TelemetryCopy.OffFields);
            _copy.Children.Add(Para(Plain(TelemetryCopy.OffRetention)));
        }
        _copy.Children.Add(Para(Bold(TelemetryCopy.OffDoesLabel), Plain(" " + TelemetryCopy.OffDoes)));
        _copy.Children.Add(Para(Bold(TelemetryCopy.OffDoesNotLabel), Plain(" " + TelemetryCopy.OffDoesNot)));
    }

    private void AddFields(IReadOnlyList<(string Name, string Text)> fields)
    {
        var n = 1;
        foreach (var (name, text) in fields)
        {
            var row = Para(Plain($"{n++}. "), Bold(name), Plain(" " + text));
            row.Margin = new Thickness(12, 2, 0, 0);
            _copy.Children.Add(row);
        }
    }

    private TextBlock Para(params Inline[] inlines)
    {
        var block = new TextBlock
        {
            Style = (Style)_resource("Dim"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        block.Inlines.AddRange(inlines);
        return block;
    }

    private static Inline Plain(string text) => new Run(text);
    private static Inline Bold(string text) => new System.Windows.Documents.Bold(new Run(text));

    private TextBlock StatusText(string text, Visibility visibility)
    {
        // ONE line: no wrap, no ellipsis, no tooltip (§8.3 §D). Same size as its sibling rows.
        var block = new TextBlock
        {
            Text = text, FontSize = 12, TextWrapping = TextWrapping.NoWrap, Visibility = visibility,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }
}
