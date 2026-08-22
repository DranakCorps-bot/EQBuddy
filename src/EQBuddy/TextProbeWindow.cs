using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EQBuddy;

/// <summary>
/// A throwaway diagnostic for text that renders at the wrong distances under Wine —
/// opened only by <c>EQBUDDY_TEXTPROBE=1</c> or <c>--textprobe</c>, and inert otherwise.
///
/// It exists because the obvious answer was wrong twice. The 2026-08-21 CrossOver report
/// ("the main font is still having kerning issues") survived a font rebuild that added
/// the two missing weights, and measuring the reporter's screenshot said why: the line
/// was 360px wide where the font's own advances predict 361.9px, and all eleven word
/// spaces landed within a pixel of prediction. **The metrics are right and the font is
/// the one we shipped.** What is wrong is five ~1-2px gaps INSIDE words ("an d th is",
/// "bun dles", "Win e"), at positions no per-glyph rounding model predicts. That is a
/// rasterisation-side defect, and no .ttf we build can reach it.
///
/// So: stop theorising and photograph the answer (trap 33 — "ship the instrument before
/// the third theory"). One screenshot of this window says which font WPF actually
/// resolved, which faces it found for each weight, and whether any combination of
/// TextFormattingMode/TextRenderingMode renders the sample cleanly. If Display looks
/// right and Ideal does not, the fix is a formatting mode and not a font; if every cell
/// is equally bad, it is below WPF and the next suspect is the process-wide
/// SoftwareOnly render mode.
///
/// Delete this file once the answer is in the trap list.
/// </summary>
internal sealed class TextProbeWindow : Window
{
    // Words whose letters pulled apart in the report, so a clean cell is obvious.
    private const string Sample = "and this was left behind: EQBuddy bundles its own font for Wine";

    public static bool Requested(IEnumerable<string> args) =>
        Environment.GetEnvironmentVariable("EQBUDDY_TEXTPROBE") == "1" ||
        args.Any(a => string.Equals(a, "--textprobe", StringComparison.OrdinalIgnoreCase));

    public TextProbeWindow()
    {
        Title = "EQBuddy text probe";
        Width = 900;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1C, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xD0, 0xC6));

        var body = new StackPanel { Margin = new Thickness(16) };
        foreach (var line in ResolvedFacts())
            body.Children.Add(Fact(line));

        body.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        body.Children.Add(Fact(
            "Same sentence under every text mode. Look for the gaps inside words — " +
            "\"an d\", \"th is\", \"bun dles\", \"Win e\"."));

        foreach (var formatting in new[] { TextFormattingMode.Ideal, TextFormattingMode.Display })
        foreach (var rendering in new[]
                 {
                     TextRenderingMode.Auto, TextRenderingMode.ClearType,
                     TextRenderingMode.Grayscale, TextRenderingMode.Aliased,
                 })
            body.Children.Add(Cell(formatting, rendering));

        Content = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 900,
        };
    }

    /// <summary>What WPF actually resolved — the half of the question a picture of the
    /// text cannot answer. A FontUri pointing anywhere but our own pack:// resource
    /// means the bundled font never loaded and every theory above is moot.</summary>
    private static IEnumerable<string> ResolvedFacts()
    {
        yield return $"EQBuddy {typeof(TextProbeWindow).Assembly.GetName().Version}   " +
                     $"Wine: {(WineFonts.IsRunningUnderWine() ? "yes" : "no")}";

        var family = Application.Current.Resources["AppFontFamily"] as FontFamily;
        yield return $"AppFontFamily = {family?.Source ?? "<unset>"}";

        foreach (var (label, weight) in new[]
                 {
                     ("Normal  ", FontWeights.Normal),
                     ("SemiBold", FontWeights.SemiBold),
                     ("Bold    ", FontWeights.Bold),
                 })
        {
            var typeface = new Typeface(family ?? new FontFamily("Segoe UI"),
                FontStyles.Normal, weight, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphs))
            {
                yield return $"  {label} -> NO GLYPH TYPEFACE (WPF found no face at all)";
                continue;
            }
            var name = glyphs.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-us"), out var n)
                ? n
                : glyphs.FamilyNames.Values.FirstOrDefault() ?? "?";
            // A synthesised weight shows here as a face whose own Weight is 400 while
            // the request was 600/700 — which is the whole question this line answers.
            yield return $"  {label} -> {name}, face weight {glyphs.Weight.ToOpenTypeWeight()}, " +
                         $"uri {glyphs.FontUri}";
        }
    }

    private static TextBlock Fact(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Consolas, monospace"),
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 1, 0, 1),
    };

    private static UIElement Cell(TextFormattingMode formatting, TextRenderingMode rendering)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = $"TextFormattingMode.{formatting} + TextRenderingMode.{rendering}",
            FontFamily = new FontFamily("Consolas, monospace"),
            FontSize = 10,
            Opacity = 0.65,
        });

        // The modes are attached properties and INHERIT, so setting them on the sample's
        // own TextBlock is what makes each cell a genuinely independent measurement.
        var sample = new TextBlock { Text = Sample, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        TextOptions.SetTextFormattingMode(sample, formatting);
        TextOptions.SetTextRenderingMode(sample, rendering);
        panel.Children.Add(sample);
        return panel;
    }
}
