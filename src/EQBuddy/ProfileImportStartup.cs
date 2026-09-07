using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Where the one-time EQBuddy 1.x profile import is ASKED and RUN — Fable's transition plan
/// §2 (TR-1), Helm-signed.
///
/// **The ordering is the design, and it is why this is a startup step rather than a button
/// on a room.** <c>MainWindow._settings</c> is a <c>readonly</c> field handed by reference
/// into every view the widget builds, so a settings.json that landed after
/// <c>AppSettings.Load</c> would be reverted by the app's own next save — trap 13, with our
/// hand on the trigger, presenting to the player as "the import did nothing" and losing
/// their theme, their watch rules and their hidden cards in one write (#385's exact
/// player-facing shape). The plan says the import "runs BEFORE the first save"; the first
/// save is at the bottom of <c>Load</c>, so consent has to be asked before it too. That
/// makes this a modal at the top of <c>App.OnStartup</c>, and the plan's other half — the
/// first-run Setup room showing what came over — is the REPORT, read off the marker by
/// <see cref="SetupView"/>.
///
/// **Nothing here runs on a profile that is not the product's own.** A capture batch and
/// the E2E suite both redirect <c>EQBUDDY_APPDATA</c> to a temp directory, and an import
/// that ran there would copy a real player's v1 profile into a throwaway one on every
/// screenshot run — the capture-surface lesson (a sheet that photographed David's live
/// profile) with a whole-directory copy behind it instead of a picture.
/// <see cref="AppPaths.IsProductOwnedProfile"/> is that gate, and it deliberately treats an
/// override that POINTS AT the product directory as no redirect at all, because that is
/// what <c>install-local.ps1 -Evolved</c> sets and it is how David runs Evolved.
/// </summary>
internal static class ProfileImportStartup
{
    /// <summary>
    /// The HARNESS source override: where a fake v1 profile lives for
    /// <c>scripts/shoot.ps1</c> and <c>tests/EQBuddy.E2E</c>.
    ///
    /// It is also the only thing that unlocks <see cref="ConsentHook"/>, and that pairing is
    /// deliberate: a hook that could answer the consent question on a real player's machine
    /// would be a code path the consent question has not reached, which is trap 47 exactly.
    /// No player has this set; nothing sets it but the two harnesses.
    /// </summary>
    private const string SourceHook = "EQBUDDY_V1_APPDATA";

    /// <summary>"accept" or "decline" — what the harness would have clicked. Inert unless
    /// <see cref="SourceHook"/> is set. With the source overridden and this unset, the real
    /// dialog opens, which is how the screenshot is taken.</summary>
    private const string ConsentHook = "EQBUDDY_IMPORT_CONSENT";

    /// <summary>Was the player TOLD anything at all — the question, or the one refusal
    /// worth speaking ("close EQBuddy 1.x first")? It is deliberately not "did we copy":
    /// the three facts below go into the <c>EQBUDDY_EXPAND</c> dump precisely because the
    /// states they separate look identical from outside, and "we said something and it was
    /// a refusal" is one of them. The WPF layer has no unit tests, so this dump is the only
    /// place an import that ran can be told from one that did not.</summary>
    internal static bool Offered;

    /// <summary>Did a copy complete, marker and all?</summary>
    internal static bool Imported;

    /// <summary>Why not — one word, because the dump is space-separated <c>key=value</c>.
    /// "none" until something has decided otherwise, never "" (an empty value in a
    /// space-separated dump is a key that reads as missing).</summary>
    internal static string RefusedReason = "none";

    /// <summary>
    /// Ask, and copy if the answer is yes. Called once, from <c>App.OnStartup</c>, BEFORE
    /// <c>AppSettings.Load</c>.
    ///
    /// It never throws: a transition surface that could stop EQBuddy from starting is worse
    /// than no transition surface, and the whole point of a marker written last is that a
    /// launch which fails here leaves a profile the next launch can offer again.
    /// </summary>
    /// <param name="prepareForDialog">Run once, immediately before the question is put on
    /// screen and only when it is — the palette the dialog draws in. A callback rather than
    /// a step in <c>OnStartup</c> so a launch with nothing to ask pays nothing for it. It is
    /// handed the theme the player's EQBuddy 1.x is wearing, or null: this product has no
    /// settings to read one out of yet, which is the ordering the whole import turns on.
    /// </param>
    public static void AskAndImport(Application app, Action<string?> prepareForDialog)
    {
        try
        {
            var sourceOverride = Environment.GetEnvironmentVariable(SourceHook);
            var harness = sourceOverride is { Length: > 0 };
            var source = harness ? sourceOverride! : AppPaths.LegacyDir;

            // A 1.x build has no import to offer — its own profile IS the source. Asked
            // through the named constant rather than by testing the number here.
            var productOwned = AppPaths.IsEvolvedLine
                && (harness || AppPaths.IsProductOwnedProfile);

            var offer = ProfileImport.Inspect(source, AppPaths.Dir, productOwned,
                SingleInstance.IsHeldByAnotherCopy);
            if (!offer.WorthSaying)
            {
                RefusedReason = Word(offer.Block);
                return;
            }

            Offered = true;
            var accepted = Ask(app, offer, harness, prepareForDialog);
            if (offer.Block == ProfileImportBlock.LegacyIsRunning)
            {
                // Said, not answered. No marker — the offer has to come back, which is the
                // whole reason this block is the one that gets spoken out loud.
                RefusedReason = Word(offer.Block);
                return;
            }

            if (!accepted)
            {
                ProfileImport.Decline(offer);
                RefusedReason = "declined";
                return;
            }

            var result = ProfileImport.Run(offer, productOwned, SingleInstance.IsHeldByAnotherCopy);
            Imported = result.Imported;
            if (result.Imported) return;
            RefusedReason = "failed";
            CoreLog.Error($"The EQBuddy 1.x profile import did not run: {result.Error}");
        }
        catch (Exception ex)
        {
            RefusedReason = "failed";
            App.LogError(ex);
        }
    }

    /// <summary>The dump's word for a block. One token, no spaces, and named after the enum
    /// so a new block cannot arrive without one.</summary>
    private static string Word(ProfileImportBlock block) => block switch
    {
        ProfileImportBlock.None => "none",
        ProfileImportBlock.NotTheProductProfile => "notTheProductProfile",
        ProfileImportBlock.NoLegacyProfile => "noLegacyProfile",
        ProfileImportBlock.AlreadyAnswered => "alreadyAnswered",
        ProfileImportBlock.TargetNotEmpty => "targetNotEmpty",
        ProfileImportBlock.LegacyIsRunning => "legacyIsRunning",
        _ => "unknown",
    };

    /// <summary>
    /// Put the question on screen and return the player's answer.
    ///
    /// **The harness answers without a window, and only ever with the source overridden**
    /// (see <see cref="SourceHook"/>). There is nothing to click from out there — the E2E
    /// suite asserts on a state dump — and a modal at startup with no answer would hang
    /// every run in the suite rather than fail one test.
    /// </summary>
    private static bool Ask(Application app, ProfileImportOffer offer, bool harness,
        Action<string?> prepareForDialog)
    {
        if (harness &&
            Environment.GetEnvironmentVariable(ConsentHook) is { Length: > 0 } scripted)
            return scripted.Equals("accept", StringComparison.OrdinalIgnoreCase);

        prepareForDialog(ProfileImport.LegacyTheme(offer.Source));
        // No window exists yet, so the DEFAULT shutdown mode would end the application the
        // moment this dialog closes — "last window closed" is true of a process whose real
        // window has not been built. Restored immediately, before MainWindow is created.
        var previous = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var window = new ProfileImportWindow(offer);
            window.ShowDialog();
            return window.Accepted;
        }
        finally
        {
            app.ShutdownMode = previous;
        }
    }

    /// <summary>The three facts, for the <c>EQBUDDY_EXPAND</c> dump. Formatted here rather
    /// than in <see cref="WidgetDump"/> so the words and the keys stay together with the
    /// code that decides them.</summary>
    public static string DebugFacts() =>
        $"migrationOffered={(Offered ? 1 : 0)} " +
        $"migrationImported={(Imported ? 1 : 0)} " +
        $"migrationRefusedReason={RefusedReason}";
}
