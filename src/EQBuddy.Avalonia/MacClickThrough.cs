using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace EQBuddy.Avalonia;

/// <summary>
/// macOS counterpart to <see cref="X11ClickThrough"/>. Cocoa has no input-shape region;
/// the equivalent is NSWindow's <c>ignoresMouseEvents</c>, which drops the window out of
/// hit-testing entirely so clicks land on whatever is behind it — the game.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacClickThrough
{
    private const string Objc = "/usr/lib/libobjc.A.dylib";

    public static bool Set(Window window, bool enabled)
    {
        if (window.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero)
        {
            App.LogError("Click-through unavailable: Avalonia did not expose a native window handle.");
            return false;
        }

        // Avalonia reports "NSWindow" for its Cocoa windows and hands back the NSWindow*
        // itself (not the content NSView), which is what owns ignoresMouseEvents.
        if (handle.HandleDescriptor is not { Length: > 0 } descriptor ||
            !descriptor.Contains("NSWindow", StringComparison.OrdinalIgnoreCase))
        {
            App.LogError($"Click-through unavailable: native handle '{handle.HandleDescriptor}' is not an NSWindow.");
            return false;
        }

        try
        {
            var selector = sel_registerName("setIgnoresMouseEvents:");
            // A wrong selector on the wrong object is an unrecognized-selector abort, which
            // kills the process rather than throwing — so ask before sending.
            if (!class_respondsToSelector(object_getClass(handle.Handle), selector))
            {
                App.LogError("Click-through unavailable: NSWindow does not respond to setIgnoresMouseEvents:.");
                return false;
            }

            objc_msgSend_SetBool(handle.Handle, selector, (byte)(enabled ? 1 : 0));
            return true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return false;
        }
    }

    [DllImport(Objc)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(Objc)]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport(Objc)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_respondsToSelector(IntPtr cls, IntPtr selector);

    /// <summary>Objective-C BOOL is a signed char, so the argument is marshalled as a byte
    /// rather than a .NET bool, whose width in a native call is not what Cocoa reads.</summary>
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_SetBool(IntPtr receiver, IntPtr selector, byte value);
}
