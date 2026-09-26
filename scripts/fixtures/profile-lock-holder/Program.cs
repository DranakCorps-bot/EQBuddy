// Stand-in for the DRA-169 prove-fail. It is named EQBuddy so the pre-fix
// Get-Process filter can see it, it takes instance.lock with the same share
// mode as SingleInstance.TryClaim, and its window has no owner so
// CloseMainWindow can find it. A WinForms form is owned by the parking
// window, which Process.MainWindowHandle skips, so this is raw Win32.
using System.IO;
using System.Runtime.InteropServices;

static class Program
{
    const int WM_CLOSE = 0x0010;
    const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    const int SW_SHOWNA = 8;

    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateWindowExW(int ex, string cls, string title, int style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage(ref MSG msg);

    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int code);

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string? name);

    static WndProc? _proc;

    static IntPtr Proc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLOSE)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    static int Main(string[] args)
    {
        var lockPath = args[0];
        var readyPath = args[1];
        var hold = args.Length < 3 || args[2] != "free";
        FileStream? fs = null;
        if (hold)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        _proc = Proc;
        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            lpszClassName = "EqLockHolder169",
            hInstance = GetModuleHandleW(null),
        };
        RegisterClassW(ref wc);
        var hwnd = CreateWindowExW(0, "EqLockHolder169", "eqbuddy-lock-holder", WS_OVERLAPPEDWINDOW,
            -20000, -20000, 80, 40, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        ShowWindow(hwnd, SW_SHOWNA);
        File.WriteAllText(readyPath, "up");

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        fs?.Dispose();
        return 0;
    }
}
