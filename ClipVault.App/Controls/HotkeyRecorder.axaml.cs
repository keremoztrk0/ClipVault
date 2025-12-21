using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Native;
using System.Text;

namespace ClipVault.App.Controls;

/// <summary>
/// A custom control for recording keyboard hotkey combinations.
/// Uses SharpHook global hook to capture keys before other applications.
/// PowerToys-style visual design with individual key boxes.
/// </summary>
public partial class HotkeyRecorder : UserControl, IDisposable
{
    private bool _isRecording;
    private bool _winPressed;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _altPressed;
    private KeyCode _mainKey = KeyCode.VcUndefined;

    // SharpHook global hook for recording
    // IMPORTANT: Keep explicit delegate references to prevent GC collection
    private SimpleGlobalHook? _recordingHook;
    private EventHandler<KeyboardHookEventArgs>? _keyPressedHandler;
    private EventHandler<KeyboardHookEventArgs>? _keyReleasedHandler;
    private Task? _hookTask;
    private readonly Lock _hookLock = new();
    private volatile bool _hookStopping;
    private bool _disposed;

    private Border? _recorderBorder;
    private StackPanel? _keysPanel;
    private TextBlock? _placeholderText;
    private TextBlock? _recordingText;
    private KeyVisual? _winKeyVisual;
    private KeyVisual? _ctrlKeyVisual;
    private KeyVisual? _altKeyVisual;
    private KeyVisual? _shiftKeyVisual;
    private TextBlock? _plusSeparator;
    private KeyVisual? _mainKeyVisual;
    private Button? _clearButton;
    private bool _controlsInitialized;

    /// <summary>
    /// Defines the Hotkey styled property.
    /// </summary>
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyRecorder, string>(nameof(Hotkey), defaultValue: string.Empty);

    /// <summary>
    /// Gets or sets the hotkey string (e.g., "Win+Ctrl+Shift+V").
    /// </summary>
    public string Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>
    /// Event raised when recording starts. 
    /// Use this to suspend global hotkey detection.
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Event raised when recording stops.
    /// Use this to resume global hotkey detection.
    /// </summary>
    public event EventHandler? RecordingStopped;

    public HotkeyRecorder()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SetupControls();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Dispose();
    }

    private void SetupControls()
    {
        if (_controlsInitialized) return;

        _recorderBorder = this.FindControl<Border>("RecorderBorder");
        _keysPanel = this.FindControl<StackPanel>("KeysPanel");
        _placeholderText = this.FindControl<TextBlock>("PlaceholderText");
        _recordingText = this.FindControl<TextBlock>("RecordingText");
        _winKeyVisual = this.FindControl<KeyVisual>("WinKeyVisual");
        _ctrlKeyVisual = this.FindControl<KeyVisual>("CtrlKeyVisual");
        _altKeyVisual = this.FindControl<KeyVisual>("AltKeyVisual");
        _shiftKeyVisual = this.FindControl<KeyVisual>("ShiftKeyVisual");
        _plusSeparator = this.FindControl<TextBlock>("PlusSeparator");
        _mainKeyVisual = this.FindControl<KeyVisual>("MainKeyVisual");
        _clearButton = this.FindControl<Button>("ClearButton");

        if (_recorderBorder != null)
        {
            _recorderBorder.PointerPressed += OnBorderPressed;
        }

        if (_clearButton != null)
        {
            _clearButton.Click += OnClearClick;
        }

        _controlsInitialized = true;
        UpdateDisplay();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != HotkeyProperty) return;
        
        var newValue = change.GetNewValue<string>();
        ParseHotkey(newValue);
        UpdateDisplay();
    }

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isRecording)
        {
            StartRecording();
        }

        e.Handled = true;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _winPressed = false;
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = KeyCode.VcUndefined;
        Hotkey = string.Empty;
        UpdateDisplay();
    }

    private void StartRecording()
    {
        if (_isRecording) return;

        _isRecording = true;
        _winPressed = false;
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = KeyCode.VcUndefined;

        RecordingStarted?.Invoke(this, EventArgs.Empty);

        StartGlobalHook();

        Focus();
        
        UpdateDisplay();
    }

    private void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;

        StopGlobalHook();

        UpdateDisplay();

        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    private void StartGlobalHook()
    {
        using (_hookLock.EnterScope())
        {
            if (_recordingHook != null || _hookStopping) return;

            _recordingHook = new SimpleGlobalHook();

            _keyPressedHandler = OnGlobalKeyPressed;
            _keyReleasedHandler = OnGlobalKeyReleased;

            _recordingHook.KeyPressed += _keyPressedHandler;
            _recordingHook.KeyReleased += _keyReleasedHandler;

            var hook = _recordingHook;

            _hookTask = Task.Run(async () =>
            {
                try
                {
                    await hook.RunAsync();
                }
                catch (ObjectDisposedException)
                {
                    // Expected when hook is disposed
                }
                catch
                {
                    // Ignore other exceptions
                }
            });
        }
    }

    private void StopGlobalHook()
    {
        SimpleGlobalHook? hookToDispose;
        Task? taskToWait;

        using (_hookLock.EnterScope())
        {
            if (_recordingHook == null || _hookStopping) return;

            _hookStopping = true;
            hookToDispose = _recordingHook;
            taskToWait = _hookTask;

            if (_keyPressedHandler != null)
            {
                _recordingHook.KeyPressed -= _keyPressedHandler;
            }

            if (_keyReleasedHandler != null)
            {
                _recordingHook.KeyReleased -= _keyReleasedHandler;
            }

            _recordingHook = null;
            _hookTask = null;
        }

        if (hookToDispose != null)
        {
            try
            {
                hookToDispose.Dispose();
            }
            catch
            {
                // Ignore dispose exceptions
            }
        }

        if (taskToWait != null)
        {
            try
            {
                taskToWait.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore wait exceptions
            }
        }

        using (_hookLock.EnterScope())
        {
            _keyPressedHandler = null;
            _keyReleasedHandler = null;
            _hookStopping = false;
        }
    }

    private void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (!_isRecording || _hookStopping) return;

        e.SuppressEvent = true;

        KeyCode keyCode = e.Data.KeyCode;
        ModifierMask mask = e.RawEvent.Mask;

        Dispatcher.UIThread.Post(() =>
        {
            if (!_isRecording || _hookStopping) return;

            if (keyCode == KeyCode.VcEscape)
            {
                ParseHotkey(Hotkey);
                StopRecording();
                return;
            }

            if (IsModifierKey(keyCode))
            {
                UpdateModifierState(keyCode, pressed: true);
                UpdateDisplay();
                return;
            }

            if (IsValidMainKey(keyCode))
            {
                _mainKey = keyCode;
                SyncModifiersFromMask(mask);

                if (_winPressed || _ctrlPressed || _shiftPressed || _altPressed)
                {
                    Hotkey = BuildHotkeyString();
                    StopRecording();
                }
                else
                {
                    UpdateDisplay();
                }
            }
        });
    }

    private void OnGlobalKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (!_isRecording || _hookStopping) return;

        e.SuppressEvent = true;

        KeyCode keyCode = e.Data.KeyCode;

        Dispatcher.UIThread.Post(() =>
        {
            if (!_isRecording || _hookStopping) return;

            if (IsModifierKey(keyCode))
            {
                UpdateModifierState(keyCode, pressed: false);
                UpdateDisplay();
            }
        });
    }

    private void UpdateModifierState(KeyCode keyCode, bool pressed)
    {
        switch (keyCode)
        {
            case KeyCode.VcLeftControl:
            case KeyCode.VcRightControl:
                _ctrlPressed = pressed;
                break;
            case KeyCode.VcLeftShift:
            case KeyCode.VcRightShift:
                _shiftPressed = pressed;
                break;
            case KeyCode.VcLeftAlt:
            case KeyCode.VcRightAlt:
                _altPressed = pressed;
                break;
            case KeyCode.VcLeftMeta:
            case KeyCode.VcRightMeta:
                _winPressed = pressed;
                break;
        }
    }

    private void SyncModifiersFromMask(ModifierMask mask)
    {
        _winPressed = (mask & ModifierMask.Meta) != 0;
        _ctrlPressed = (mask & ModifierMask.Ctrl) != 0;
        _shiftPressed = (mask & ModifierMask.Shift) != 0;
        _altPressed = (mask & ModifierMask.Alt) != 0;
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

    private static bool IsValidMainKey(KeyCode keyCode)
    {
        return keyCode switch
        {
            // Letters
            >= KeyCode.VcA and <= KeyCode.VcZ => true,
            // Numbers
            >= KeyCode.Vc0 and <= KeyCode.Vc9 => true,
            // NumPad
            >= KeyCode.VcNumPad0 and <= KeyCode.VcNumPad9 => true,
            // Function keys
            >= KeyCode.VcF1 and <= KeyCode.VcF24 => true,
            // Navigation & editing
            KeyCode.VcSpace or KeyCode.VcTab or KeyCode.VcEnter => true,
            KeyCode.VcBackspace or KeyCode.VcDelete or KeyCode.VcInsert => true,
            KeyCode.VcHome or KeyCode.VcEnd or KeyCode.VcPageUp or KeyCode.VcPageDown => true,
            // Arrow keys
            KeyCode.VcUp or KeyCode.VcDown or KeyCode.VcLeft or KeyCode.VcRight => true,
            // Symbol keys
            KeyCode.VcBackQuote or KeyCode.VcMinus or KeyCode.VcEquals => true,
            KeyCode.VcOpenBracket or KeyCode.VcCloseBracket => true,
            KeyCode.VcBackslash or KeyCode.VcSemicolon or KeyCode.VcQuote => true,
            KeyCode.VcComma or KeyCode.VcPeriod or KeyCode.VcSlash => true,
            // Special keys
            KeyCode.VcPrintScreen or KeyCode.VcScrollLock or KeyCode.VcPause => true,
            KeyCode.VcNumLock or KeyCode.VcCapsLock => true,
            _ => false
        };
    }

    // Handle Escape key via Avalonia as fallback (in case global hook doesn't catch it)
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_isRecording && e.Key == Key.Escape)
        {
            e.Handled = true;
            ParseHotkey(Hotkey);
            StopRecording();
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (!_isRecording) return;
        ParseHotkey(Hotkey);
        StopRecording();
    }

    private string BuildHotkeyString()
    {
        StringBuilder sb = new();

        if (_winPressed)
        {
            sb.Append("Win+");
        }

        if (_ctrlPressed)
        {
            sb.Append("Ctrl+");
        }

        if (_altPressed)
        {
            sb.Append("Alt+");
        }

        if (_shiftPressed)
        {
            sb.Append("Shift+");
        }

        sb.Append(KeyCodeToDisplayString(_mainKey));

        return sb.ToString();
    }

    private static string KeyCodeToDisplayString(KeyCode keyCode)
    {
        return keyCode switch
        {
            // Letters - remove "Vc" prefix
            >= KeyCode.VcA and <= KeyCode.VcZ => keyCode.ToString()[2..],
            // Numbers
            KeyCode.Vc0 => "0",
            KeyCode.Vc1 => "1",
            KeyCode.Vc2 => "2",
            KeyCode.Vc3 => "3",
            KeyCode.Vc4 => "4",
            KeyCode.Vc5 => "5",
            KeyCode.Vc6 => "6",
            KeyCode.Vc7 => "7",
            KeyCode.Vc8 => "8",
            KeyCode.Vc9 => "9",
            // NumPad
            KeyCode.VcNumPad0 => "Num0",
            KeyCode.VcNumPad1 => "Num1",
            KeyCode.VcNumPad2 => "Num2",
            KeyCode.VcNumPad3 => "Num3",
            KeyCode.VcNumPad4 => "Num4",
            KeyCode.VcNumPad5 => "Num5",
            KeyCode.VcNumPad6 => "Num6",
            KeyCode.VcNumPad7 => "Num7",
            KeyCode.VcNumPad8 => "Num8",
            KeyCode.VcNumPad9 => "Num9",
            // Navigation & editing
            KeyCode.VcSpace => "Space",
            KeyCode.VcTab => "Tab",
            KeyCode.VcEnter => "Enter",
            KeyCode.VcBackspace => "Backspace",
            KeyCode.VcDelete => "Delete",
            KeyCode.VcInsert => "Insert",
            KeyCode.VcHome => "Home",
            KeyCode.VcEnd => "End",
            KeyCode.VcPageUp => "PageUp",
            KeyCode.VcPageDown => "PageDown",
            // Arrow keys
            KeyCode.VcUp => "Up",
            KeyCode.VcDown => "Down",
            KeyCode.VcLeft => "Left",
            KeyCode.VcRight => "Right",
            // Symbol keys
            KeyCode.VcBackQuote => "`",
            KeyCode.VcMinus => "-",
            KeyCode.VcEquals => "=",
            KeyCode.VcOpenBracket => "[",
            KeyCode.VcCloseBracket => "]",
            KeyCode.VcBackslash => "\\",
            KeyCode.VcSemicolon => ";",
            KeyCode.VcQuote => "'",
            KeyCode.VcComma => ",",
            KeyCode.VcPeriod => ".",
            KeyCode.VcSlash => "/",
            // Special keys
            KeyCode.VcPrintScreen => "PrtSc",
            KeyCode.VcScrollLock => "ScrLk",
            KeyCode.VcPause => "Pause",
            KeyCode.VcNumLock => "NumLock",
            KeyCode.VcCapsLock => "CapsLock",
            // Function keys
            >= KeyCode.VcF1 and <= KeyCode.VcF24 => keyCode.ToString()[2..],
            _ => keyCode.ToString()
        };
    }

    private void ParseHotkey(string? hotkey)
    {
        _winPressed = false;
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = KeyCode.VcUndefined;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return;
        }

        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            string upperPart = part.ToUpperInvariant();

            switch (upperPart)
            {
                case "WIN":
                case "WINDOWS":
                case "SUPER":
                case "META":
                case "CMD":
                case "COMMAND":
                    _winPressed = true;
                    break;
                case "CTRL":
                case "CONTROL":
                    _ctrlPressed = true;
                    break;
                case "SHIFT":
                    _shiftPressed = true;
                    break;
                case "ALT":
                    _altPressed = true;
                    break;
                default:
                    _mainKey = StringToKeyCode(upperPart);
                    break;
            }
        }
    }

    private static KeyCode StringToKeyCode(string keyStr)
    {
        return keyStr switch
        {
            "SPACE" => KeyCode.VcSpace,
            "TAB" => KeyCode.VcTab,
            "ENTER" or "RETURN" => KeyCode.VcEnter,
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
            // NumPad
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
            // Symbol keys
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
            // Single letter
            _ when keyStr.Length == 1 && char.IsLetter(keyStr[0]) =>
                Enum.TryParse<KeyCode>("Vc" + keyStr, true, out var key) ? key : KeyCode.VcUndefined,
            _ => KeyCode.VcUndefined
        };
    }

    private void UpdateDisplay()
    {
        if (_placeholderText == null || _recordingText == null ||
            _winKeyVisual == null || _ctrlKeyVisual == null ||
            _altKeyVisual == null || _shiftKeyVisual == null ||
            _plusSeparator == null || _mainKeyVisual == null ||
            _recorderBorder == null || _clearButton == null)
        {
            return;
        }

        bool hasModifier = _winPressed || _ctrlPressed || _altPressed || _shiftPressed;
        bool hasMainKey = _mainKey != KeyCode.VcUndefined;
        bool hasValidHotkey = hasModifier && hasMainKey;

        if (_isRecording)
        {
            _placeholderText.IsVisible = false;
            _recordingText.IsVisible = !hasModifier;

            _winKeyVisual.IsVisible = _winPressed;
            _ctrlKeyVisual.IsVisible = _ctrlPressed;
            _altKeyVisual.IsVisible = _altPressed;
            _shiftKeyVisual.IsVisible = _shiftPressed;
            _plusSeparator.IsVisible = false;
            _mainKeyVisual.IsVisible = false;

            _recorderBorder.BorderBrush = GetResourceBrush("SystemAccentColor");
            _recorderBorder.BorderThickness = new Thickness(2);
            _clearButton.IsVisible = false;
        }
        else
        {
            _recordingText.IsVisible = false;

            if (hasValidHotkey)
            {
                _placeholderText.IsVisible = false;
                _winKeyVisual.IsVisible = _winPressed;
                _ctrlKeyVisual.IsVisible = _ctrlPressed;
                _altKeyVisual.IsVisible = _altPressed;
                _shiftKeyVisual.IsVisible = _shiftPressed;
                _plusSeparator.IsVisible = true;
                _mainKeyVisual.IsVisible = true;
                _mainKeyVisual.KeyText = KeyCodeToDisplayString(_mainKey);
                _clearButton.IsVisible = true;
            }
            else
            {
                _placeholderText.IsVisible = true;
                _winKeyVisual.IsVisible = false;
                _ctrlKeyVisual.IsVisible = false;
                _altKeyVisual.IsVisible = false;
                _shiftKeyVisual.IsVisible = false;
                _plusSeparator.IsVisible = false;
                _mainKeyVisual.IsVisible = false;
                _clearButton.IsVisible = false;
            }

            _recorderBorder.BorderBrush = GetResourceBrush("TextControlBorderBrush");
            _recorderBorder.BorderThickness = new Thickness(1);
        }
    }

    /// <summary>
    /// Gets a brush resource from the application resources, with fallback.
    /// </summary>
    private IBrush GetResourceBrush(string resourceKey)
    {
        var themeVariant = ActualThemeVariant;

        if (themeVariant != null &&
            Application.Current?.TryGetResource(resourceKey, themeVariant, out object? resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        bool isDark = themeVariant == ThemeVariant.Dark || themeVariant == null;

        return resourceKey switch
        {
            "TextControlForeground" => isDark ? Brushes.White : Brushes.Black,
            "TextControlPlaceholderForeground" => isDark ? Brushes.Gray : Brushes.DarkGray,
            "TextControlBorderBrush" => isDark ? Brushes.DimGray : Brushes.LightGray,
            "SystemAccentColor" => Brushes.DodgerBlue,
            _ => Brushes.Gray
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopGlobalHook();

        if (_recorderBorder != null)
        {
            _recorderBorder.PointerPressed -= OnBorderPressed;
        }

        if (_clearButton != null)
        {
            _clearButton.Click -= OnClearClick;
        }
    }
}
