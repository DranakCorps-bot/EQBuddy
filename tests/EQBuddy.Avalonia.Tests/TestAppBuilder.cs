using Avalonia;
using Avalonia.Headless;
using EQBuddy.Avalonia.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// ONE headless Avalonia session per assembly, ONE thread — so this assembly may not run
// two tests at once, and xUnit's default is that separate COLLECTIONS do exactly that.
//
// Nineteen of the twenty-one test classes carry `[Collection("avalonia")]`, which made
// them serial with respect to each other and left the rest in collections of their own,
// running alongside. `Avalonia.Headless.HeadlessUnitTestSession` tears the Application
// down and stands a fresh one up around each dispatched test (EnsureIsolatedApplication);
// two threads doing that to one session interleave, and the rebuild then runs where the
// dispatcher is owned by the other thread:
//
//     [Test Case Cleanup Failure (…)] System.InvalidOperationException :
//       The calling thread cannot access this object because a different thread owns it.
//         at Avalonia.Rendering.DefaultRenderLoop.Add(IRenderLoopTask)
//         at Avalonia.Headless.AvaloniaHeadlessPlatform.Initialize(…)
//         at Avalonia.Headless.HeadlessUnitTestSession.EnsureIsolatedApplication()
//
// It lands on a DIFFERENT test every time (MezTargets…, ClosingAndReopening…,
// MapCircleMenu…), which is what a race looks like from the outside and is why it read as
// three unrelated flakes rather than one. It was live on `main` — 2026-09-04 runs
// 33920002880 and 33918054739, both green on a re-run — and PR #294 only surfaced it by
// asking CI the same question nine times.
//
// Assembly-wide rather than one more `[Collection]` attribute on the stragglers, because
// the constraint is a fact about the SESSION, not about which classes someone remembered
// to label. A class added without the attribute would re-open it silently (trap 30: a
// hand-maintained list stops covering the set the day the set grows).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Boots the real <see cref="App"/> on Avalonia's headless platform, so these tests exercise
/// the same application the Linux build ships rather than a stand-in.
///
/// <c>UseHeadlessDrawing = false</c> is the important part: with the default no-op drawing
/// backend a window "renders" without producing pixels, which would make these tests pass on
/// a UI that draws nothing. Skia does the real work and frames can be captured.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
