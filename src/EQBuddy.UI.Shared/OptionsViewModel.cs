using System.ComponentModel;
using System.Runtime.CompilerServices;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>The alert sounds both UIs offer. File names are Windows Media files —
/// the WPF app plays them directly; other platforms map or substitute.</summary>
public static class AlertSoundCatalog
{
    public static readonly (string Name, string WindowsMediaFile)[] Sounds =
    [
        ("Ding", "Windows Ding.wav"),
        ("Notify", "Windows Notify.wav"),
        ("Chimes", "chimes.wav"),
        ("Chord", "chord.wav"),
        ("Tada", "tada.wav"),
        ("Exclamation", "Windows Exclamation.wav"),
        ("Alarm", "Alarm01.wav"),
    ];

    public static readonly string[] Names = [.. Sounds.Select(s => s.Name)];

    /// <summary>Maps legacy SystemSounds values from early builds onto the palette;
    /// anything else (a named entry or a custom file path) passes through.</summary>
    public static string Normalize(string choice) => choice switch
    {
        "Asterisk" or "" => "Ding",
        "Beep" => "Chord",
        "Hand" => "Chimes",
        "Question" => "Notify",
        _ => choice,
    };

    public static bool IsCustom(string choice) => Array.IndexOf(Names, Normalize(choice)) < 0;
}

/// <summary>The overlay cards, in default order — shared by both UIs' layout and
/// Options card editors.</summary>
public static class OverlaySections
{
    public static readonly (string Key, string Title)[] Catalog =
    [
        ("combat", "Combat"), ("healing", "Healing"), ("kills", "Kills"), ("loot", "Loot"),
        ("tracked", "Tracked"), ("money", "Money"), ("progress", "Progress"),
        ("faction", "Faction"), ("misc", "Travels & Deaths"),
    ];
}

public sealed record OptionsCardRow(string Key, string Title, bool Hidden);

/// <summary>
/// Framework-neutral Options logic: every mapping, mutation, and derived label the
/// Options window needs. Views only build controls, forward input here, and apply
/// visual side effects (scale/opacity/layout) to their own windows.
/// </summary>
public sealed class OptionsViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly Action _persist;

    public OptionsViewModel(AppSettings settings, Action persist)
    {
        _settings = settings;
        _persist = persist;
        NormalizeSectionOrder();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AppSettings Settings => _settings;
    public void Persist() => _persist();

    // ---- sliders ----
    public double UiScale
    {
        get => _settings.UiScale;
        set { _settings.UiScale = Math.Clamp(value, 0.5, 2.0); Changed(); Changed(nameof(ScaleLabel)); }
    }
    public string ScaleLabel => $"{_settings.UiScale * 100:0}%";

    public double Opacity
    {
        get => _settings.Opacity;
        set { _settings.Opacity = Math.Clamp(value, 0.3, 1.0); Changed(); Changed(nameof(OpacityLabel)); }
    }
    public string OpacityLabel => $"{_settings.Opacity * 100:0}%";

    public double BackgroundOpacity
    {
        get => _settings.BackgroundOpacity;
        set { _settings.BackgroundOpacity = Math.Clamp(value, 0.15, 1.0); Changed(); Changed(nameof(BackgroundOpacityLabel)); }
    }
    public string BackgroundOpacityLabel => $"{_settings.BackgroundOpacity * 100:0}%";

    // ---- toggles ----
    public bool TruncateLogs
    {
        get => _settings.TruncateLogs;
        set { _settings.TruncateLogs = value; PersistAnd(); }
    }
    public bool PinWatchChips
    {
        get => _settings.PinWatchChips;
        set { _settings.PinWatchChips = value; PersistAnd(); }
    }
    public bool ShowTutorial
    {
        get => _settings.ShowTutorial;
        set { _settings.ShowTutorial = value; PersistAnd(); }
    }

    // ---- recent-rate window ----
    public static readonly string[] WindowChoices = ["5 min", "15 min", "30 min"];
    public int RecentWindowIndex
    {
        get => _settings.RecentWindowMinutes switch { 5 => 0, 30 => 2, _ => 1 };
        set { _settings.RecentWindowMinutes = value switch { 0 => 5, 2 => 30, _ => 15 }; PersistAnd(); }
    }

    // ---- alert sound ----
    public const string CustomSoundChoice = "Custom file…";
    public static readonly string[] SoundChoices =
        [.. AlertSoundCatalog.Names.Select(n => n == "Ding" ? $"{n} (default)" : n), CustomSoundChoice];

    /// <summary>Index into SoundChoices for the current setting (the custom slot for paths).</summary>
    public int SoundIndex
    {
        get
        {
            var i = Array.IndexOf(AlertSoundCatalog.Names, AlertSoundCatalog.Normalize(_settings.AlertSound));
            return i >= 0 ? i : AlertSoundCatalog.Names.Length;
        }
    }

    public bool IsCustomSoundIndex(int index) => index >= AlertSoundCatalog.Names.Length;

    public void SelectNamedSound(int index)
    {
        if (IsCustomSoundIndex(index)) return;
        _settings.AlertSound = AlertSoundCatalog.Names[index];
        PersistAnd(nameof(SoundFileNote));
    }

    public void SetCustomSound(string path)
    {
        _settings.AlertSound = path;
        PersistAnd(nameof(SoundFileNote));
    }

    public string SoundFileNote =>
        AlertSoundCatalog.IsCustom(_settings.AlertSound) ? $"Custom: {_settings.AlertSound}" : "";

    // ---- watch rules ----
    public static readonly string[] KindNames = Enum.GetNames<WatchKind>();
    public IReadOnlyList<TrackedRule> Rules => _settings.TrackedRules;

    public TrackedRule AddRule()
    {
        var rule = new TrackedRule { Name = "", Pattern = "" };
        _settings.TrackedRules.Add(rule);
        PersistAnd(nameof(Rules));
        return rule;
    }

    public void RemoveRule(TrackedRule rule)
    {
        _settings.TrackedRules.Remove(rule);
        PersistAnd(nameof(Rules));
    }

    // ---- overlay cards ----
    public IReadOnlyList<OptionsCardRow> Cards =>
        [.. _settings.SectionOrder.Select(key => new OptionsCardRow(
            key,
            OverlaySections.Catalog.First(c => c.Key == key).Title,
            _settings.HiddenSections.Contains(key)))];

    public void MoveCard(string key, int delta)
    {
        var order = _settings.SectionOrder;
        var index = order.IndexOf(key);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= order.Count) return;
        (order[index], order[target]) = (order[target], order[index]);
        PersistAnd(nameof(Cards));
    }

    public void ToggleCard(string key)
    {
        if (!_settings.HiddenSections.Remove(key))
            _settings.HiddenSections.Add(key);
        PersistAnd(nameof(Cards));
    }

    private void NormalizeSectionOrder()
    {
        var order = _settings.SectionOrder.Where(k => OverlaySections.Catalog.Any(c => c.Key == k)).ToList();
        foreach (var (key, _) in OverlaySections.Catalog)
            if (!order.Contains(key)) order.Add(key);
        _settings.SectionOrder = order;
    }

    // ---- hotkeys ----
    public string HotkeyNote =>
        $"{_settings.HotkeyToggleOverlay} show/hide · {_settings.HotkeyClickThrough} click-through · " +
        $"{_settings.HotkeyMiniMode} mini · {_settings.HotkeyCampMarker} camp marker";

    private void PersistAnd(string? alsoNotify = null, [CallerMemberName] string? propertyName = null)
    {
        _persist();
        Changed(propertyName);
        if (alsoNotify is not null) Changed(alsoNotify);
    }

    private void Changed([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
