namespace EQBuddy.E2E;

/// <summary>
/// **The Font pick reaches a drawn TextBlock** (discussion #1046, Miss Outlaw; DRA-1048).
/// The unit suite (<c>AppFontChoiceTests</c>) proves the rule; this proves the EFFECT — the
/// face the widget's own header resolved — because "in the settings" and "in effect" are
/// different claims (trap 42). The default run is the half that stops a broken swap from
/// passing by reporting the pick whatever was drawn.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget.
/// </summary>
[Collection("e2e")]
public sealed class AppFontTests
{
    [Fact]
    public void WithNoPickTheWidgetDrawsInSegoeUi()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("appFont", "default", "no pick to resolve to the default face");
        app.WaitForDump("widgetFontFace", "SegoeUIVariableText", "the widget header to draw in Segoe UI");
    }

    [Fact]
    public void ASavedPickIsTheFaceTheWidgetDrawsIn()
    {
        using var app = new AppHarness(settings => settings.AppFont = "atkinson-hyperlegible-next");
        app.Launch();

        app.WaitForDump("appFont", "atkinson-hyperlegible-next", "the saved pick to resolve");
        app.WaitForDump("appFontFellBack", 0, "a bundled face never to fall back");
        app.WaitForDump("widgetFontFace", "AtkinsonHyperlegibleNext",
            "the widget header to draw in the bundled Atkinson Hyperlegible Next");
    }

    /// <summary>A LIVE pick in Options → Look (through <c>EQBUDDY_FONT_PICK</c>, the pointer's
    /// stand-in — trap 22) restyles the already-open widget without a restart.</summary>
    [Fact]
    public void APickInOptionsRestylesTheOpenWidget()
    {
        using var app = new AppHarness(null, new Dictionary<string, string>
        {
            ["EQBUDDY_OPTIONS"] = "1",
            ["EQBUDDY_FONT_PICK"] = "open-sans",
        });
        app.Launch();

        app.WaitForDump("optionsLookFontItems", 5, "one picker row per curated face");
        app.WaitForDump("optionsLookFontEnabled", 1, "the picker to be usable on Windows");
        app.WaitForDump("optionsLookFontIndex", 2, "the hook to have picked Open Sans");
        app.WaitForDump("appFont", "open-sans", "the live pick to resolve");
        app.WaitForDump("widgetFontFace", "OpenSans", "the open widget to redraw in Open Sans");
    }
}
