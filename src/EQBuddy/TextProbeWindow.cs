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
/// the third theory").
///
/// **It answered, in one screenshot, on the first run.** Under CrossOver:
///   - the font was never in doubt after all — Normal/SemiBold/Bold each resolved to
///     their own pack:// face at 400/600/700, so the three-weight family groups
///     correctly under Wine and the earlier fix landed exactly as intended;
///   - every TextFormattingMode.Ideal cell split the sample's 12 words into 17 pieces;
///   - every TextFormattingMode.Display cell rendered exactly 12, words intact;
///   - TextRenderingMode changed NOTHING, in either mode — all four values identical.
/// The fix is <see cref="WineText"/>; the reasoning is in TextRenderingPolicy.
///
/// **Kept rather than deleted.** It is opt-in and inert, it is the only thing in the app
/// that can say which face WPF actually resolved, and the next report of this shape will
/// want exactly this picture. Note it deliberately sets the two modes on its own samples,
/// so it still shows the BEFORE alongside the after even now that Wine defaults to
/// Display — a probe that inherited the fix would have nothing to compare against.
/// </summary>
internal sealed class TextProbeWindow : Window
{
    // Words whose letters pulled apart in the report, so a clean cell is obvious.
    private const string Sample = "and this was left behind: EQBuddy bundles its own font for Wine";

    private readonly TextBlock _inherited;

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

        // The question the first version of this probe could not answer, and the one
        // that matters most: does the app-wide policy actually REACH an ordinary
        // element? `_inherited` is a plain TextBlock with nothing set on it, read once
        // it is in the tree — if it says Ideal while the policy says Display, the fix is
        // present in the build and failing to apply, which looks exactly like a stale
        // binary from the outside.
        _inherited = Fact("effective mode on a plain TextBlock: (measured on load)");
        body.Children.Add(_inherited);
        Loaded += (_, _) => _inherited.Text =
            $"POLICY SAYS {WineText.Resolve(Core.AppSettings.Load())}  ->  a plain TextBlock in this window " +
            $"actually resolves {TextOptions.GetTextFormattingMode(_inherited)}" +
            (TextOptions.GetTextFormattingMode(_inherited) == TextFormattingMode.Display
                ? "   [applied]"
                : "   [NOT APPLIED - the policy is not reaching ordinary text]");

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
