using Serilog;
using SharpHook;
using SharpHook.Data;

namespace ClipVault.App.Helpers;

/// <summary>
/// Manages global hotkey registration across platforms.
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly SimpleGlobalHook _hook;
    private bool _disposed;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _altPressed;
    private KeyCode _targetKeyCode = KeyCode.VcEnter;
    private bool _requireCtrl = true;
    private bool _requireShift = true;
    private bool _requireAlt = false;
    
    public event EventHandler? HotkeyPressed;
    
    public HotkeyManager()
    {
        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }
    
    /// <summary>
    /// Starts listening for the global hotkey.
    /// </summary>
    public Task StartAsync()
    {
        Log.Information("HotkeyManager starting with hotkey: Ctrl={Ctrl}, Shift={Shift}, Alt={Alt}, Key={Key}", 
            _requireCtrl, _requireShift, _requireAlt, _targetKeyCode);
        return _hook.RunAsync();
    }
    
    /// <summary>
    /// Stops listening for hotkeys.
    /// </summary>
    public void Stop()
    {
        _hook.Dispose();
    }
    
    /// <summary>
    /// Sets the hotkey combination.
    /// </summary>
    /// <param name="hotkey">Hotkey string like "Ctrl+Shift+Enter"</param>
    public void SetHotkey(string hotkey)
    {
        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        _requireCtrl = false;
        _requireShift = false;
        _requireAlt = false;
        
        foreach (string part in parts)
        {
            string upperPart = part.ToUpperInvariant();
            
            switch (upperPart)
            {
                case "CTRL":
                case "CONTROL":
                    _requireCtrl = true;
                    break;
                case "SHIFT":
                    _requireShift = true;
                    break;
                case "ALT":
                    _requireAlt = true;
                    break;
                default:
                    _targetKeyCode = ParseKeyCode(upperPart);
                    break;
            }
        }
        
        Log.Debug("Hotkey set: Ctrl={Ctrl}, Shift={Shift}, Alt={Alt}, Key={Key}", 
            _requireCtrl, _requireShift, _requireAlt, _targetKeyCode);
    }
    
    private static KeyCode ParseKeyCode(string key)
    {
        return key switch
        {
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
            "UP" => KeyCode.VcUp,
            "DOWN" => KeyCode.VcDown,
            "LEFT" => KeyCode.VcLeft,
            "RIGHT" => KeyCode.VcRight,
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
            _ => KeyCode.VcUndefined
        };
    }
    
    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        KeyCode keyCode = e.Data.KeyCode;
        
        // Track modifier keys
        switch (keyCode)
        {
            case KeyCode.VcLeftControl:
            case KeyCode.VcRightControl:
                _ctrlPressed = true;
                break;
            case KeyCode.VcLeftShift:
            case KeyCode.VcRightShift:
                _shiftPressed = true;
                break;
            case KeyCode.VcLeftAlt:
            case KeyCode.VcRightAlt:
                _altPressed = true;
                break;
        }
        
        // Check if our hotkey is pressed
        if (keyCode == _targetKeyCode)
        {
            Log.Verbose("Target key pressed. Ctrl={Ctrl}/{ReqCtrl}, Shift={Shift}/{ReqShift}, Alt={Alt}/{ReqAlt}",
                _ctrlPressed, _requireCtrl, _shiftPressed, _requireShift, _altPressed, _requireAlt);
                
            if (_requireCtrl == _ctrlPressed && 
                _requireShift == _shiftPressed && 
                _requireAlt == _altPressed)
            {
                Log.Information("Global hotkey triggered!");
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        KeyCode keyCode = e.Data.KeyCode;
        
        switch (keyCode)
        {
            case KeyCode.VcLeftControl:
            case KeyCode.VcRightControl:
                _ctrlPressed = false;
                break;
            case KeyCode.VcLeftShift:
            case KeyCode.VcRightShift:
                _shiftPressed = false;
                break;
            case KeyCode.VcLeftAlt:
            case KeyCode.VcRightAlt:
                _altPressed = false;
                break;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;
        _hook.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
