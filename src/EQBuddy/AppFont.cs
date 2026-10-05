using System.Windows;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The WPF half of <see cref="AppFontChoice"/>: puts the player's Font pick into the
/// <c>AppFontFamily</c> resource every TextBlock reads through Theme.xaml's implicit style, so
/// one assignment restyles the widget, the HUD chips, the alert banner and every open window
/// live (discussion #1046, DRA-1048).
///
/// **Under Wine it does nothing and <see cref="WineFonts"/> keeps winning** (Planner decision
/// 4): Wine's DirectWrite reads only the primary font, so any face but the bundled icon font
/// boxes every section icon. Options dims the picker there and says why.
/// </summary>
internal static class AppFont
{
    private static readonly Uri PackBase = new("pack://application:,,,/");

    /// <summary>What the last <see cref="Apply"/> decided — Options reads the fallback
    /// sentence off it and the <c>EQBUDDY_EXPAND</c> dump reports it.</summary>
    public static AppFontResolution Current { get; private set; } =
        new(AppFontChoice.Default, AppFontChoice.Default, null);

    /// <summary>True when the picker cannot apply here (Wine).</summary>
    public static bool Locked => WineFonts.IsRunningUnderWine();

    /// <summary>The face code that DRAWS text itself (FormattedText) must use, so a chart
    /// caption follows the pick instead of staying in Segoe UI. Read off the resource, so it
    /// is right under Wine too.</summary>
    public static FontFamily Family =>
        Application.Current?.TryFindResource("AppFontFamily") as FontFamily ?? new FontFamily(AppFontChoice.DefaultFamilies);

    /// <summary>Applies <see cref="AppSettings.AppFont"/>. Called once at startup, after
    /// <see cref="WineFonts.ApplyIfNeeded"/> and the settings load, and again on every pick.
    /// Font cosmetics must never stop startup.</summary>
    public static void Apply(ResourceDictionary appResources, AppSettings settings)
    {
        try
        {
            if (Locked) return;
            Current = AppFontChoice.Resolve(settings.AppFont, IsInstalled);
            appResources["AppFontFamily"] = new FontFamily(PackBase, AppFontChoice.FamilySource(Current.Drawn));
        }
        catch (Exception ex)
        {
            CoreLog.Sink?.Invoke(ex);
        }
    }

    private static bool IsInstalled(string family) =>
        Fonts.SystemFontFamilies.Any(f => string.Equals(f.Source, family, StringComparison.OrdinalIgnoreCase));
}
