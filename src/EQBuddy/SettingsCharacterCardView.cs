using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **"Character card" — Options → Behavior, its own block below the heartbeat's** (DRA-1288 D2;
/// every word is <see cref="CharacterCardCopy"/>'s). The other choice in this tab about data
/// leaving the app, so it sits beside that one. Composed by <see cref="SettingsBehaviorView"/>,
/// so each host (Options and the shell's Settings room) builds its own instance (trap 45).
///
/// <para><b>The lead is printed, not hidden behind an ⓘ</b> — it is the disclosure, the same
/// reason <see cref="SettingsTelemetryView"/>'s copy is printed.</para>
///
/// <para><b>It holds no clock and writes no file.</b> The toggle writes the setting through
/// <see cref="OptionsViewModel.SetCharacterCardEnabled"/> (the setting's one writer); the three
/// buttons call <see cref="CharacterCardRuntime"/>, the process's one writer.</para>
/// </summary>
internal sealed class SettingsCharacterCardView
{
    private readonly OptionsViewModel _vm;
    private readonly Func<object, object> _resource;
    private readonly Func<bool> _hostReady;

    private CheckBox _toggle = null!;
    private TextBlock _result = null!;
    private TextBox _snippet = null!;
    private TextBlock _copied = null!;
    private UIElement? _block;

    public SettingsCharacterCardView(OptionsViewModel vm, Func<object, object> resource, Func<bool> ready)
    {
        _vm = vm;
        _resource = resource;
        _hostReady = ready;
    }

    private static CharacterCardRuntime? Runtime => CharacterCardRuntime.Current;

    public UIElement Block => _block ??= Build();

    /// <summary>Facts off the BUILT controls, for the host's dump (trap 42).</summary>
    /// <remarks><c>characterCardMcpSnippet</c> is the text the snippet box DRAWS (DRA-1446 D2),
    /// <see cref="Uri.EscapeDataString"/>-encoded because the dump is space-separated and the
    /// snippet is indented JSON — the E2E asserts it names the exe the harness launched.</remarks>
    public string DebugFacts() => _block is null
        ? ""
        : $"characterCardToggle={(_toggle.IsChecked == true ? 1 : 0)} "
          + $"characterCardMcpSnippet={Uri.EscapeDataString(_snippet.Text)}";

    private UIElement Build()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        var label = new TextBlock { Text = CharacterCardCopy.ToggleLabel, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _toggle = new CheckBox { Content = label, IsChecked = _vm.CharacterCardEnabled };
        _toggle.Checked += (_, _) => Toggled();
        _toggle.Unchecked += (_, _) => Toggled();
        panel.Children.Add(_toggle);

        var copy = new StackPanel { Margin = new Thickness(20, 6, 0, 0) };
        copy.Children.Add(Para(CharacterCardCopy.Lead));
        copy.Children.Add(Para(CharacterCardCopy.Contents));
        // The full path, selectable, so "it is somewhere in AppData" is never a hunt (decision 1).
        var folder = new TextBox
        {
            Text = $"{CharacterCardCopy.FolderLabel} {Runtime?.Folder ?? CharacterCardWriter.DefaultFolder}",
            IsReadOnly = true, BorderThickness = new Thickness(0), Background = null,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), FontSize = 12,
        };
        folder.SetResourceReference(Control.ForegroundProperty, "DimBrush");
        copy.Children.Add(folder);
        panel.Children.Add(copy);

        var buttons = new WrapPanel { Margin = new Thickness(20, 8, 0, 0) };
        buttons.Children.Add(Button(CharacterCardCopy.OpenFolderButton, null, () => Runtime?.OpenFolder()));
        buttons.Children.Add(Button(CharacterCardCopy.WriteNowButton, CharacterCardCopy.WriteNowTip,
            () => Show(Runtime?.WriteNow())));
        buttons.Children.Add(Button(CharacterCardCopy.DeleteButton, CharacterCardCopy.DeleteTip,
            () => Show(Runtime?.Delete())));
        panel.Children.Add(buttons);

        _result = new TextBlock
        {
            Style = (Style)_resource("Dim"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 6, 0, 0), Visibility = Visibility.Collapsed,
        };
        panel.Children.Add(_result);
        panel.Children.Add(BuildConnect());

        panel.Loaded += (_, _) =>
        {
            // Another host may have flipped the switch since this one was built.
            if (_toggle.IsChecked != _vm.CharacterCardEnabled) _toggle.IsChecked = _vm.CharacterCardEnabled;
            // Screenshot hook (shoot.ps1 'options-character-card'): the block is last in the
            // longest Settings tab.
            if (Environment.GetEnvironmentVariable("EQBUDDY_SCROLL_CHARACTER_CARD") == "1")
                panel.Dispatcher.BeginInvoke(() => panel.BringIntoView(), DispatcherPriority.ApplicationIdle);
        };
        return panel;
    }

    /// <summary>
    /// **"Connect an AI app"** (DRA-1446 D2; every word is <see cref="CharacterCardMcpCopy"/>'s,
    /// except the disclosure, which is <see cref="CharacterCardCopy.Lead"/> drawn again — trap 4).
    /// Shown whether the toggle is on or off: <c>--mcp</c> serves whatever cards exist and says
    /// so when there are none. The snippet is read-only and comes from the one producer,
    /// <see cref="CharacterCardMcpCopy.ConfigSnippet"/>, over the exe actually running.
    /// </summary>
    private UIElement BuildConnect()
    {
        var connect = new StackPanel { Margin = new Thickness(20, 14, 0, 0) };

        var heading = new TextBlock { Text = CharacterCardMcpCopy.ConnectHeading, FontSize = 12, FontWeight = FontWeights.SemiBold };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        connect.Children.Add(heading);
        connect.Children.Add(Para(CharacterCardMcpCopy.ConnectLead));
        connect.Children.Add(Para(CharacterCardCopy.Lead));
        connect.Children.Add(Para(CharacterCardMcpCopy.SnippetLabel));

        _snippet = new TextBox
        {
            Text = CharacterCardMcpCopy.ConfigSnippet(Environment.ProcessPath ?? "EQBuddy.exe"),
            // InputBox, so the box takes the theme's input colours (a bare TextBox is white
            // under light text); read-only, so it can be selected but never edited.
            Style = (Style)_resource("InputBox"),
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Margin = new Thickness(0, 4, 0, 0),
        };
        connect.Children.Add(_snippet);

        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        _copied = new TextBlock
        {
            Text = CharacterCardMcpCopy.Copied, Style = (Style)_resource("Dim"),
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
        };
        row.Children.Add(Button(CharacterCardMcpCopy.CopyButton, null, CopySnippet));
        row.Children.Add(_copied);
        connect.Children.Add(row);
        return connect;
    }

    private void CopySnippet()
    {
        try
        {
            Clipboard.SetText(_snippet.Text);
            _copied.Visibility = Visibility.Visible;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another app holds the clipboard; the text stays selectable in the box.
            _snippet.Focus();
            _snippet.SelectAll();
        }
    }

    private void Toggled()
    {
        if (!_hostReady()) return;
        var on = _toggle.IsChecked == true;
        if (_vm.CharacterCardEnabled != on) _vm.SetCharacterCardEnabled(on);
    }

    private void Show(string? line)
    {
        _result.Text = line ?? "";
        _result.Visibility = string.IsNullOrEmpty(line) ? Visibility.Collapsed : Visibility.Visible;
    }

    private Button Button(string text, string? tip, Action click)
    {
        var button = new Button
        {
            Content = text, Style = (Style)_resource("ActionButton"),
            Margin = new Thickness(0, 0, 8, 4), ToolTip = tip,
        };
        button.Click += (_, _) => click();
        return button;
    }

    private TextBlock Para(string text) => new()
    {
        Text = text, Style = (Style)_resource("Dim"), TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
    };
}
