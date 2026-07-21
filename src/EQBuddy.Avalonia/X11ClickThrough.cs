using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace EQBuddy.Avalonia;

internal static class X11ClickThrough
{
    private const int ShapeInput = 2;

    public static bool Set(Window window, bool enabled)
    {
        if (window.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero)
        {
            App.LogError("Click-through unavailable: Avalonia did not expose a native window handle.");
            return false;
        }

        if (handle.HandleDescriptor is { Length: > 0 } descriptor &&
            !descriptor.Contains("X", StringComparison.OrdinalIgnoreCase))
        {
            App.LogError($"Click-through unavailable: native handle '{descriptor}' is not an X11 window.");
            return false;
        }

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            App.LogError("Click-through unavailable: XOpenDisplay failed.");
            return false;
        }

        try
        {
            if (!XFixesQueryExtension(display, out _, out _))
            {
                App.LogError("Click-through unavailable: XFixes extension is not available.");
                return false;
            }

            if (enabled)
            {
                var emptyRegion = XFixesCreateRegion(display, IntPtr.Zero, 0);
                XFixesSetWindowShapeRegion(display, handle.Handle, ShapeInput, 0, 0, emptyRegion);
                XFixesDestroyRegion(display, emptyRegion);
            }
            else
            {
                XFixesSetWindowShapeRegion(display, handle.Handle, ShapeInput, 0, 0, IntPtr.Zero);
            }
            XFlush(display);
            return true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return false;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libXfixes.so.3")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool XFixesQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    [DllImport("libXfixes.so.3")]
    private static extern IntPtr XFixesCreateRegion(IntPtr display, IntPtr rectangles, int nrectangles);

    [DllImport("libXfixes.so.3")]
    private static extern void XFixesDestroyRegion(IntPtr display, IntPtr region);

    [DllImport("libXfixes.so.3")]
    private static extern void XFixesSetWindowShapeRegion(IntPtr display, IntPtr window,
        int shapeKind, int xOff, int yOff, IntPtr region);
}
