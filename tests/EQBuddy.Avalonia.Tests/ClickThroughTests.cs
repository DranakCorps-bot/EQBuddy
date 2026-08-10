using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Click-through against a backend that cannot provide it. The headless platform stands in
/// for the real cases — Wayland, a missing XFixes, an unrecognized windowing system — and
/// the contract is the same for all of them: report false, never throw. The throw matters
/// because both backends reach a native library that may not exist on the running machine,
/// and an escaping DllNotFoundException would take the widget down mid-session rather than
/// leaving one feature switched off.
/// </summary>
[Collection("avalonia")]
public sealed class ClickThroughTests
{
    [AvaloniaFact]
    public void UnsupportedBackendDeclinesInsteadOfThrowing()
    {
        var window = new Window();
        window.Show();

        Assert.False(ClickThrough.Set(window, enabled: true));
        Assert.False(ClickThrough.Set(window, enabled: false));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
