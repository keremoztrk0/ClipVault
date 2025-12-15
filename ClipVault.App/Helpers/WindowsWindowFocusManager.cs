using System.Runtime.InteropServices;
using Avalonia.Controls;
using Serilog;

namespace ClipVault.App.Helpers;

/// <summary>
/// Windows-specific implementation of window focus management.
/// Uses native Win32 APIs with SendInput workaround for reliable window activation.
/// </summary>
/// <remarks>
/// Windows restricts which processes can call SetForegroundWindow successfully.
/// The workaround (used by PowerToys) is to send a dummy mouse input event first,
/// which tricks Windows into allowing the focus change.
/// See: https://github.com/AvaloniaUI/Avalonia/discussions/19324
/// </remarks>
public partial class WindowsWindowFocusManager : IWindowFocusManager
{
    private static readonly ILogger Logger = Log.ForContext<WindowsWindowFocusManager>();

    private const int INPUT_MOUSE = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public MOUSEINPUT mi;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int SW_RESTORE = 9;
    

    /// <inheritdoc />
    public bool FocusWindow(Window window)
    {
        try
        {
            IntPtr hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
            {
                Logger.Warning("Could not get window handle");
                return false;
            }

            // Restore if minimized
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            // Send dummy mouse input to trick Windows into allowing SetForegroundWindow
            // This workaround is used by PowerToys and other apps that need reliable focus stealing
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi = new MOUSEINPUT();
            SendInput(1, inputs, Marshal.SizeOf<INPUT>());

            bool result = SetForegroundWindow(hwnd);
            Logger.Debug("Window focused using native SetForegroundWindow with SendInput workaround: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to focus window");
            return false;
        }
    }
}