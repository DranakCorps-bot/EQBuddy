using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace EQBuddy.Avalonia;

internal sealed class X11HotkeyService : IDisposable
{
    private const int KeyPress = 2;
    private const uint ShiftMask = 1;
    private const uint LockMask = 2;
    private const uint ControlMask = 4;
    private const uint Mod1Mask = 8;
    private const uint Mod2Mask = 16;
    private const uint Mod4Mask = 64;
    private const int GrabModeAsync = 1;

    private readonly Dictionary<(uint KeyCode, uint Mods), Action> _actions = [];
    private readonly Thread _thread;
    private readonly IntPtr _display;
    private readonly IntPtr _root;
    private volatile bool _stopping;

    public X11HotkeyService(IEnumerable<(string Spec, Action Action)> hotkeys)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("X11 hotkeys are only available on Linux/Unix desktops.");
        if (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 } &&
            Environment.GetEnvironmentVariable("DISPLAY") is not { Length: > 0 })
            throw new PlatformNotSupportedException("Global hotkeys require X11; Wayland did not expose an X11 display.");

        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("XOpenDisplay failed; global hotkeys are disabled.");
        _root = XDefaultRootWindow(_display);

        foreach (var (spec, action) in hotkeys)
            Register(spec, action);

        XFlush(_display);
        _thread = new Thread(EventLoop)
        {
            IsBackground = true,
            Name = "EQBuddy X11 hotkeys",
        };
        _thread.Start();
    }

    private void Register(string spec, Action action)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;
        if (!TryParse(spec, out var mods, out var keyName))
        {
            App.LogError($"Hotkey '{spec}' could not be registered (invalid format).");
            return;
        }

        var keysym = XStringToKeysym(keyName);
        if (keysym == IntPtr.Zero && keyName.Length == 1)
            keysym = XStringToKeysym(keyName.ToUpperInvariant());
        if (keysym == IntPtr.Zero)
        {
            App.LogError($"Hotkey '{spec}' could not be registered (unknown key '{keyName}').");
            return;
        }

        var keycode = XKeysymToKeycode(_display, keysym);
        if (keycode == 0)
        {
            App.LogError($"Hotkey '{spec}' could not be registered (no keycode for '{keyName}').");
            return;
        }

        _actions[(keycode, mods)] = action;
        foreach (var variant in LockVariants(mods))
            XGrabKey(_display, (int)keycode, variant, _root, true, GrabModeAsync, GrabModeAsync);
    }

    private void EventLoop()
    {
        try
        {
            while (!_stopping)
            {
                while (!_stopping && XPending(_display) > 0)
                {
                    XNextEvent(_display, out var evt);
                    if (evt.Type != KeyPress) continue;
                    var mods = evt.State & ~(LockMask | Mod2Mask);
                    if (_actions.TryGetValue((evt.KeyCode, mods), out var action))
                        Dispatcher.UIThread.Post(action);
                }
                Thread.Sleep(25);
            }
        }
        catch (Exception ex)
        {
            if (!_stopping) App.LogError(ex);
        }
    }

    private static IEnumerable<uint> LockVariants(uint mods)
    {
        yield return mods;
        yield return mods | LockMask;
        yield return mods | Mod2Mask;
        yield return mods | LockMask | Mod2Mask;
    }

    private static bool TryParse(string spec, out uint mods, out string keyName)
    {
        mods = 0;
        keyName = "";
        foreach (var part in spec.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": mods |= ControlMask; break;
                case "SHIFT": mods |= ShiftMask; break;
                case "ALT": mods |= Mod1Mask; break;
                case "WIN" or "SUPER" or "META": mods |= Mod4Mask; break;
                default: keyName = NormalizeKeyName(part); break;
            }
        }
        return keyName.Length > 0;
    }

    private static string NormalizeKeyName(string key) => key.ToUpperInvariant() switch
    {
        "ESC" => "Escape",
        "RETURN" => "Return",
        "ENTER" => "Return",
        "SPACE" => "space",
        "PLUS" => "plus",
        "MINUS" => "minus",
        _ => key.Length == 1 ? key.ToUpperInvariant() : key,
    };

    public void Dispose()
    {
        _stopping = true;
        if (_display == IntPtr.Zero) return;
        foreach (var (keyCode, mods) in _actions.Keys)
            foreach (var variant in LockVariants(mods))
                XUngrabKey(_display, (int)keyCode, variant, _root);
        XFlush(_display);
        if (!_thread.Join(TimeSpan.FromMilliseconds(250)))
            App.LogError("X11 hotkey thread did not stop within 250ms.");
        XCloseDisplay(_display);
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XKeyEvent
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(80)] public uint State;
        [FieldOffset(84)] public uint KeyCode;
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XStringToKeysym(string key);

    [DllImport("libX11.so.6")]
    private static extern uint XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport("libX11.so.6")]
    private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport("libX11.so.6")]
    private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);

    [DllImport("libX11.so.6")]
    private static extern int XPending(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(IntPtr display, out XKeyEvent evt);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);
}
