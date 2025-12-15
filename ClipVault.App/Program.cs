using System.Runtime.InteropServices;
using Avalonia;
using ClipVault.App.Data;
using Serilog;

namespace ClipVault.App;

internal class Program
{
    private const string MutexName = "ClipVault_SingleInstance_Mutex";
    private static Mutex? _mutex;
    
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Serilog first so we can log the single instance check
        string logPath = Path.Combine(AppDbContext.GetAppDataPath(), "Logs", "clipvault-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            // Check for single instance
            if (!EnsureSingleInstance())
            {
                Log.Information("Another instance of ClipVault is already running. Exiting.");
                BringExistingInstanceToFront();
                return;
            }
            
            Log.Information("Starting ClipVault...");

            // Initialize Dapper type handlers for SQLite
            DapperConfig.Initialize();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            ReleaseMutex();
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// Ensures only a single instance of the application is running.
    /// </summary>
    /// <returns>True if this is the first instance, false if another instance is already running.</returns>
    private static bool EnsureSingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            
            if (!createdNew)
            {
                // Another instance already has the mutex
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking for single instance");
            // If we can't create the mutex, allow the app to run anyway
            return true;
        }
    }
    
    /// <summary>
    /// Releases the mutex when the application exits.
    /// </summary>
    private static void ReleaseMutex()
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error releasing mutex");
        }
    }
    
    /// <summary>
    /// Attempts to bring the existing instance window to the front.
    /// </summary>
    private static void BringExistingInstanceToFront()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }
        
        try
        {
            // Find the existing ClipVault window and bring it to front
            nint hwnd = FindWindow(null, "ClipVault");
            if (hwnd != IntPtr.Zero)
            {
                // Restore if minimized
                ShowWindow(hwnd, SW_RESTORE);
                // Bring to foreground
                SetForegroundWindow(hwnd);
                Log.Debug("Brought existing ClipVault window to front");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not bring existing instance to front");
        }
    }
    
    // Windows API imports for bringing existing window to front
    private const int SW_RESTORE = 9;
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? lpClassName, string lpWindowName);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}