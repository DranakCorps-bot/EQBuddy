using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Draws every icon in <see cref="IconPaths"/> to one PNG, at the two sizes the app
/// actually uses, with its name under it.
///
/// **This exists because nothing could SEE the icon set.** `IconGeometryTests` proves a
/// path parses and fills its grid, which catches a typo and nothing else — an icon can
/// pass both and still be unrecognisable at 12px, or be a perfectly good drawing of the
/// wrong thing. When Gate 5c needed a "slow" icon the honest answer was "I cannot tell
/// whether a snail reads at this size", so it shipped an hourglass instead — and David's
/// first look found the hourglass collides with the respawn countdown's meaning.
/// A sheet would have answered that before it shipped rather than after.
///
/// Opt-in, like the mobile snapshot fixtures — it writes a file, so it does not run in
/// the ordinary suite:
///
///   dotnet test tests/EQBuddy.Avalonia.Tests/EQBuddy.Avalonia.Tests.csproj -c Release \
///     --filter FullyQualifiedName~IconSheet -e EQBUDDY_SHOOT=1 -e EQBUDDY_SHOOT_OUT=&lt;dir&gt;
///
/// 12px is <see cref="DesignTokens.IconInline"/> — an icon inside a line of text, which
/// is the size a chip mark, a quest badge and a row tick are drawn at, and the size that
/// decides whether a drawing survives. 24px is the grid they are authored on.
/// </summary>
[Collection("avalonia")]
public class IconSheetTests
{
    [AvaloniaFact]
    public void WriteIconSheet()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        var names = IconPaths.Names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        var sheet = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Width = 900,
            Background = Brushes.Black,
        };

        foreach (var name in names)
        {
            var cell = new StackPanel
            {
                Width = 100,
                Margin = new global::Avalonia.Thickness(0, 10, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            // Both sizes side by side: a shape that survives the 24px grid and dies at
            // 12px is the failure this sheet is for, and one size alone hides it.
            row.Children.Add(new PathIcon
            {
                Data = StreamGeometry.Parse(IconPaths.Path(name)),
                Width = DesignTokens.IconInline,
                Height = DesignTokens.IconInline,
                Foreground = Brushes.Gainsboro,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new global::Avalonia.Thickness(0, 0, DesignTokens.SpaceM, 0),
            });
            row.Children.Add(new PathIcon
            {
                Data = StreamGeometry.Parse(IconPaths.Path(name)),
                Width = IconPaths.ViewBox,
                Height = IconPaths.ViewBox,
                Foreground = Brushes.Gainsboro,
                VerticalAlignment = VerticalAlignment.Center,
            });
            cell.Children.Add(row);
            cell.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 10,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new global::Avalonia.Thickness(0, DesignTokens.SpaceXs, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            sheet.Children.Add(cell);
        }

        var window = new Window
        {
            Width = 900,
            Height = 40 + 62 * (int)Math.Ceiling(names.Count / 9.0),
            Background = Brushes.Black,
            Content = new ScrollViewer { Content = sheet },
        };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var dir = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
                  ?? Path.Combine(Path.GetTempPath(), "eqbuddy-icons");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "icon-sheet.png");
        frame!.Save(path);
        Assert.True(File.Exists(path), $"No sheet written to {path}");
    }
}
