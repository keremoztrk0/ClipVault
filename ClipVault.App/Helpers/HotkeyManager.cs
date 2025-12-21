using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Native;

namespace ClipVault.App.Helpers;

/// <summary>
/// Represents a hotkey combination with modifiers and a key.
/// Inspired by PowerToys' Hotkey structure.
/// </summary>
public readonly record struct Hotkey
{
    public bool Win { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public KeyCode Key { get; init; }
    
    /// <summary>
    /// Gets a unique handle for this hotkey combination.
    /// Uses bitmask encoding: Key (bits 0-7), Win (bit 8), Ctrl (bit 9), Shift (bit 10), Alt (bit 11).
    /// </summary>
    public ushort Handle
    {
        get
        {
            ushort handle = (ushort)((int)Key & 0xFF);
            if (Win) handle |= 1 << 8;
            if (Ctrl) handle |= 1 << 9;
            if (Shift) handle |= 1 << 10;
            if (Alt) handle |= 1 << 11;
            return handle;
        }
    }
    
    /// <summary>
    /// Returns true if this hotkey has any modifiers set.
    /// </summary>
    private bool HasModifiers => Win || Ctrl || Alt || Shift;
    
    /// <summary>
    /// Returns true if this is a valid hotkey (has a key and at least one modifier).
    /// </summary>
    public bool IsValid => Key != KeyCode.VcUndefined && HasModifiers;
    
    public override string ToString()
    {
        List<string> parts = [];
        if (Win) parts.Add("Win");
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(KeyCodeToString(Key));
        return string.Join("+", parts);
    }
    
    private static string KeyCodeToString(KeyCode keyCode)
    {
        string name = keyCode.ToString();
        // Remove "Vc" prefix if present
        if (name.StartsWith("Vc", StringComparison.Ordinal))
            name = name[2..];
        return name;
    }
}

/// <summary>
/// Manages global hotkey registration across platforms.
/// Inspired by PowerToys' HotkeyManager implementation with improvements:
/// - Uses bitmask-based hotkey handles for efficient lookup
/// - Supports multiple hotkey registrations
/// - Uses real-time modifier state detection
/// - Prevents Start Menu activation after Win key hotkeys
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly ILogger<HotkeyManager> _logger;
    private readonly SimpleGlobalHook _hook;
    private readonly Dictionary<ushort, Action> _hotkeys = new();
    private readonly Lock _lock = new();
    private bool _disposed;
    private bool _isRunning;
    private volatile bool _isSuspended;
    
    // Track pressed keys state (like PowerToys' pressedKeys)
    private Hotkey _pressedKeys;
    
    /// <summary>
    /// Event raised when a registered hotkey is pressed.
    /// This is a legacy event for backward compatibility.
    /// </summary>
    public event EventHandler? HotkeyPressed;
    
    /// <summary>
    /// Gets whether hotkey detection is currently suspended.
    /// </summary>
    public bool IsSuspended => _isSuspended;
    
    public HotkeyManager(ILogger<HotkeyManager> logger)
    {
        _logger = logger;
        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }
    
    /// <summary>
    /// Starts listening for global hotkeys.
    /// </summary>
    public Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("HotkeyManager is already running");
            return Task.CompletedTask;
        }
        
        _isRunning = true;
        _logger.LogInformation("HotkeyManager starting with {Count} registered hotkeys", _hotkeys.Count);
        return _hook.RunAsync();
    }
    
    /// <summary>
    /// Stops listening for hotkeys.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _hook.Dispose();
        _logger.LogInformation("HotkeyManager stopped");
    }
    
    /// <summary>
    /// Temporarily suspends hotkey detection.
    /// Use this when recording new hotkeys to prevent the current hotkey from triggering.
    /// </summary>
    public void Suspend()
    {
        _isSuspended = true;
        _logger.LogDebug("HotkeyManager suspended");
    }
    
    /// <summary>
    /// Resumes hotkey detection after suspension.
    /// </summary>
    public void Resume()
    {
        _isSuspended = false;
        // Reset pressed keys state to avoid stale state
        _pressedKeys = default;
        _logger.LogDebug("HotkeyManager resumed");
    }
    
    /// <summary>
    /// Registers a hotkey with a callback action.
    /// Returns a handle that can be used to unregister the hotkey.
    /// </summary>
    public ushort RegisterHotkey(Hotkey hotkey, Action callback)
    {
        if (!hotkey.IsValid)
        {
            _logger.LogWarning("Attempted to register invalid hotkey: {Hotkey}", hotkey);
            return 0;
        }
        
        using (_lock.EnterScope())
        {
            ushort handle = hotkey.Handle;
            _hotkeys[handle] = callback;
            _logger.LogInformation("Registered hotkey: {Hotkey} (handle: {Handle})", hotkey, handle);
            return handle;
        }
    }
    
    /// <summary>
    /// Unregisters a hotkey by its handle.
    /// </summary>
    public void UnregisterHotkey(ushort handle)
    {
        using (_lock.EnterScope())
        {
            if (_hotkeys.Remove(handle))
            {
                _logger.LogInformation("Unregistered hotkey with handle: {Handle}", handle);
            }
        }
    }
    
    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    public void UnregisterAllHotkeys()
    {
        using (_lock.EnterScope())
        {
            _hotkeys.Clear();
            _logger.LogInformation("Unregistered all hotkeys");
        }
    }
    
    /// <summary>
    /// Sets a single hotkey (legacy method for backward compatibility).
    /// Clears existing hotkeys and registers the new one.
    /// </summary>
    /// <param name="hotkeyString">Hotkey string like "Win+Ctrl+Shift+V"</param>
    public void SetHotkey(string hotkeyString)
    {
        Hotkey hotkey = ParseHotkeyString(hotkeyString);
        
        using (_lock.EnterScope())
        {
            _hotkeys.Clear();
            if (hotkey.IsValid)
            {
                _hotkeys[hotkey.Handle] = () => HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }
        
        _logger.LogInformation("Hotkey set to: {Hotkey}", hotkey);
    }
    
    /// <summary>
    /// Parses a hotkey string into a Hotkey struct.
    /// </summary>
    private static Hotkey ParseHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return default;
        
        string[] parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        bool win = false, ctrl = false, shift = false, alt = false;
        KeyCode keyCode = KeyCode.VcUndefined;
        
        foreach (string part in parts)
        {
            string upperPart = part.ToUpperInvariant();
            
            switch (upperPart)
            {
                case "WIN" or "WINDOWS" or "SUPER" or "META" or "CMD" or "COMMAND":
                    win = true;
                    break;
                case "CTRL" or "CONTROL":
                    ctrl = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                default:
                    keyCode = ParseKeyCode(upperPart);
                    break;
            }
        }
        
        return new Hotkey
        {
            Win = win,
            Ctrl = ctrl,
            Shift = shift,
            Alt = alt,
            Key = keyCode
        };
    }
    
    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        // Skip processing if suspended (e.g., during hotkey recording)
        if (_isSuspended) return;
        
        KeyCode keyCode = e.Data.KeyCode;
        
        // Update modifier state based on which key was pressed
        if (IsModifierKey(keyCode))
        {
            UpdateModifierState(keyCode, pressed: true);
            return;
        }
        
        // Update the current pressed key
        _pressedKeys = _pressedKeys with { Key = keyCode };
        
        // Also update modifiers from the event mask for reliability
        // (handles cases where modifier was pressed before we started listening)
        SyncModifiersFromMask(e.RawEvent.Mask);
        
        // Check if any registered hotkey matches
        ushort pressedHandle = _pressedKeys.Handle;
        
        Action? callback;
        bool hasMatch;
        using (_lock.EnterScope())
        {
            hasMatch = _hotkeys.TryGetValue(pressedHandle, out callback);
        }

        if (!hasMatch || callback == null) return;
        _logger.LogDebug("Hotkey matched: {Hotkey}", _pressedKeys);
            
        try
        {
            callback.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking hotkey callback");
        }
            
        // Suppress the event to prevent it from reaching other applications
        e.SuppressEvent = true;
            
        // If Win key was part of the hotkey, send a dummy key to prevent Start Menu
        // This is a technique used by PowerToys
        if (_pressedKeys.Win)
        {
            SuppressStartMenu();
        }
    }
    
    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        // Skip processing if suspended (e.g., during hotkey recording)
        if (_isSuspended) return;
        
        KeyCode keyCode = e.Data.KeyCode;
        
        // Update modifier state when released
        if (IsModifierKey(keyCode))
        {
            UpdateModifierState(keyCode, pressed: false);
            return;
        }
        
        // Clear the key if it was released
        if (_pressedKeys.Key == keyCode)
        {
            _pressedKeys = _pressedKeys with { Key = KeyCode.VcUndefined };
        }
    }
    
    private void UpdateModifierState(KeyCode keyCode, bool pressed)
    {
        _pressedKeys = keyCode switch
        {
            KeyCode.VcLeftControl or KeyCode.VcRightControl => 
                _pressedKeys with { Ctrl = pressed || (_pressedKeys.Ctrl && !IsControlKey(keyCode)) },
            KeyCode.VcLeftShift or KeyCode.VcRightShift => 
                _pressedKeys with { Shift = pressed || (_pressedKeys.Shift && !IsShiftKey(keyCode)) },
            KeyCode.VcLeftAlt or KeyCode.VcRightAlt => 
                _pressedKeys with { Alt = pressed || (_pressedKeys.Alt && !IsAltKey(keyCode)) },
            KeyCode.VcLeftMeta or KeyCode.VcRightMeta => 
                _pressedKeys with { Win = pressed || (_pressedKeys.Win && !IsMetaKey(keyCode)) },
            _ => _pressedKeys
        };
    }
    
    private void SyncModifiersFromMask(ModifierMask mask)
    {
        // Sync our state with the actual modifier state from the event
        // This ensures we're accurate even if we missed some events
        _pressedKeys = _pressedKeys with
        {
            Win = (mask & ModifierMask.Meta) != 0,
            Ctrl = (mask & ModifierMask.Ctrl) != 0,
            Shift = (mask & ModifierMask.Shift) != 0,
            Alt = (mask & ModifierMask.Alt) != 0
        };
    }
    
    /// <summary>
    /// Sends a dummy key event to prevent the Start Menu from activating
    /// after a Win key hotkey. This is a technique used by PowerToys.
    /// </summary>
    private void SuppressStartMenu()
    {
        try
        {
            // On Windows, we need to send a dummy key to cancel the Start Menu
            if (!OperatingSystem.IsWindows()) return;
            // Use a key that has no effect (VK_NONAME = 0xFC)
            // This cancels the pending Start Menu activation
            EventSimulator simulator = new();
            simulator.SimulateKeyPress(KeyCode.VcUndefined);
            simulator.SimulateKeyRelease(KeyCode.VcUndefined);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to suppress Start Menu activation");
        }
    }
    
    private static bool IsModifierKey(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.VcLeftControl or KeyCode.VcRightControl => true,
            KeyCode.VcLeftShift or KeyCode.VcRightShift => true,
            KeyCode.VcLeftAlt or KeyCode.VcRightAlt => true,
            KeyCode.VcLeftMeta or KeyCode.VcRightMeta => true,
            _ => false
        };
    }
    
    private static bool IsControlKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl;
    
    private static bool IsShiftKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftShift or KeyCode.VcRightShift;
    
    private static bool IsAltKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftAlt or KeyCode.VcRightAlt;
    
    private static bool IsMetaKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftMeta or KeyCode.VcRightMeta;
    
    private static KeyCode ParseKeyCode(string key)
    {
        return key switch
        {
            // Navigation & editing keys
            "ENTER" or "RETURN" => KeyCode.VcEnter,
            "SPACE" => KeyCode.VcSpace,
            "TAB" => KeyCode.VcTab,
            "ESCAPE" or "ESC" => KeyCode.VcEscape,
            "BACKSPACE" => KeyCode.VcBackspace,
            "DELETE" or "DEL" => KeyCode.VcDelete,
            "INSERT" or "INS" => KeyCode.VcInsert,
            "HOME" => KeyCode.VcHome,
            "END" => KeyCode.VcEnd,
            "PAGEUP" or "PGUP" => KeyCode.VcPageUp,
            "PAGEDOWN" or "PGDN" => KeyCode.VcPageDown,
            
            // Arrow keys
            "UP" => KeyCode.VcUp,
            "DOWN" => KeyCode.VcDown,
            "LEFT" => KeyCode.VcLeft,
            "RIGHT" => KeyCode.VcRight,
            
            // Special keys
            "PRINTSCREEN" or "PRTSC" or "PRINT" => KeyCode.VcPrintScreen,
            "SCROLLLOCK" or "SCRLK" => KeyCode.VcScrollLock,
            "PAUSE" or "BREAK" => KeyCode.VcPause,
            "NUMLOCK" => KeyCode.VcNumLock,
            "CAPSLOCK" or "CAPS" => KeyCode.VcCapsLock,
            
            // Function keys
            "F1" => KeyCode.VcF1,
            "F2" => KeyCode.VcF2,
            "F3" => KeyCode.VcF3,
            "F4" => KeyCode.VcF4,
            "F5" => KeyCode.VcF5,
            "F6" => KeyCode.VcF6,
            "F7" => KeyCode.VcF7,
            "F8" => KeyCode.VcF8,
            "F9" => KeyCode.VcF9,
            "F10" => KeyCode.VcF10,
            "F11" => KeyCode.VcF11,
            "F12" => KeyCode.VcF12,
            "F13" => KeyCode.VcF13,
            "F14" => KeyCode.VcF14,
            "F15" => KeyCode.VcF15,
            "F16" => KeyCode.VcF16,
            "F17" => KeyCode.VcF17,
            "F18" => KeyCode.VcF18,
            "F19" => KeyCode.VcF19,
            "F20" => KeyCode.VcF20,
            "F21" => KeyCode.VcF21,
            "F22" => KeyCode.VcF22,
            "F23" => KeyCode.VcF23,
            "F24" => KeyCode.VcF24,
            
            // Letters
            "A" => KeyCode.VcA,
            "B" => KeyCode.VcB,
            "C" => KeyCode.VcC,
            "D" => KeyCode.VcD,
            "E" => KeyCode.VcE,
            "F" => KeyCode.VcF,
            "G" => KeyCode.VcG,
            "H" => KeyCode.VcH,
            "I" => KeyCode.VcI,
            "J" => KeyCode.VcJ,
            "K" => KeyCode.VcK,
            "L" => KeyCode.VcL,
            "M" => KeyCode.VcM,
            "N" => KeyCode.VcN,
            "O" => KeyCode.VcO,
            "P" => KeyCode.VcP,
            "Q" => KeyCode.VcQ,
            "R" => KeyCode.VcR,
            "S" => KeyCode.VcS,
            "T" => KeyCode.VcT,
            "U" => KeyCode.VcU,
            "V" => KeyCode.VcV,
            "W" => KeyCode.VcW,
            "X" => KeyCode.VcX,
            "Y" => KeyCode.VcY,
            "Z" => KeyCode.VcZ,
            
            // Numbers
            "0" => KeyCode.Vc0,
            "1" => KeyCode.Vc1,
            "2" => KeyCode.Vc2,
            "3" => KeyCode.Vc3,
            "4" => KeyCode.Vc4,
            "5" => KeyCode.Vc5,
            "6" => KeyCode.Vc6,
            "7" => KeyCode.Vc7,
            "8" => KeyCode.Vc8,
            "9" => KeyCode.Vc9,
            
            // NumPad keys
            "NUM0" or "NUMPAD0" => KeyCode.VcNumPad0,
            "NUM1" or "NUMPAD1" => KeyCode.VcNumPad1,
            "NUM2" or "NUMPAD2" => KeyCode.VcNumPad2,
            "NUM3" or "NUMPAD3" => KeyCode.VcNumPad3,
            "NUM4" or "NUMPAD4" => KeyCode.VcNumPad4,
            "NUM5" or "NUMPAD5" => KeyCode.VcNumPad5,
            "NUM6" or "NUMPAD6" => KeyCode.VcNumPad6,
            "NUM7" or "NUMPAD7" => KeyCode.VcNumPad7,
            "NUM8" or "NUMPAD8" => KeyCode.VcNumPad8,
            "NUM9" or "NUMPAD9" => KeyCode.VcNumPad9,
            
            // OEM/Symbol keys
            "`" or "~" or "TILDE" or "BACKQUOTE" => KeyCode.VcBackQuote,
            "-" or "_" or "MINUS" => KeyCode.VcMinus,
            "=" or "+" or "PLUS" or "EQUALS" => KeyCode.VcEquals,
            "[" or "{" or "OPENBRACKET" => KeyCode.VcOpenBracket,
            "]" or "}" or "CLOSEBRACKET" => KeyCode.VcCloseBracket,
            "\\" or "|" or "PIPE" or "BACKSLASH" => KeyCode.VcBackslash,
            ";" or ":" or "SEMICOLON" => KeyCode.VcSemicolon,
            "'" or "\"" or "QUOTE" or "APOSTROPHE" => KeyCode.VcQuote,
            "," or "<" or "COMMA" => KeyCode.VcComma,
            "." or ">" or "PERIOD" => KeyCode.VcPeriod,
            "/" or "?" or "SLASH" => KeyCode.VcSlash,
            
            _ => KeyCode.VcUndefined
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _isRunning = false;
        
        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;
        _hook.Dispose();
        
        using (_lock.EnterScope())
        {
            _hotkeys.Clear();
        }
        
        GC.SuppressFinalize(this);
        _logger.LogDebug("HotkeyManager disposed");
    }
}
