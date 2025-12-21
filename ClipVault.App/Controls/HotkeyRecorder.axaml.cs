using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Styling;
using System.Text;

namespace ClipVault.App.Controls;

/// <summary>
/// A custom control for recording keyboard hotkey combinations.
/// Uses Avalonia's keyboard events to capture keys when the control has focus.
/// PowerToys-style visual design with individual key boxes.
/// </summary>
public partial class HotkeyRecorder : UserControl
{
    private bool _isRecording;
    private bool _winPressed;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _altPressed;
    private Key _mainKey = Key.None;

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
        _mainKey = Key.None;
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
        _mainKey = Key.None;

        RecordingStarted?.Invoke(this, EventArgs.Empty);

        Focus();
        
        UpdateDisplay();
    }

    private void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;

        UpdateDisplay();

        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;

        // Handle Escape to cancel
        if (e.Key == Key.Escape)
        {
            ParseHotkey(Hotkey);
            StopRecording();
            return;
        }

        // Update modifier state
        _ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _altPressed = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _winPressed = e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Check if this is a modifier key only
        if (IsModifierKey(e.Key))
        {
            UpdateDisplay();
            return;
        }

        // This is a main key
        if (IsValidMainKey(e.Key))
        {
            _mainKey = e.Key;

            // Only accept if at least one modifier is pressed
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
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyUp(e);
            return;
        }

        e.Handled = true;

        // Update modifier state on key up
        _ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _altPressed = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _winPressed = e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        UpdateDisplay();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (!_isRecording) return;
        ParseHotkey(Hotkey);
        StopRecording();
    }

    private static bool IsModifierKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => true,
            Key.LeftShift or Key.RightShift => true,
            Key.LeftAlt or Key.RightAlt => true,
            Key.LWin or Key.RWin => true,
            _ => false
        };
    }

    private static bool IsValidMainKey(Key key)
    {
        return key switch
        {
            // Letters
            >= Key.A and <= Key.Z => true,
            // Numbers
            >= Key.D0 and <= Key.D9 => true,
            // NumPad
            >= Key.NumPad0 and <= Key.NumPad9 => true,
            // Function keys
            >= Key.F1 and <= Key.F24 => true,
            // Navigation & editing
            Key.Space or Key.Tab or Key.Enter => true,
            Key.Back or Key.Delete or Key.Insert => true,
            Key.Home or Key.End or Key.PageUp or Key.PageDown => true,
            // Arrow keys
            Key.Up or Key.Down or Key.Left or Key.Right => true,
            // Symbol keys
            Key.OemTilde or Key.OemMinus or Key.OemPlus => true,
            Key.OemOpenBrackets or Key.OemCloseBrackets => true,
            Key.OemPipe or Key.OemSemicolon or Key.OemQuotes => true,
            Key.OemComma or Key.OemPeriod or Key.OemQuestion => true,
            // Special keys
            Key.PrintScreen or Key.Scroll or Key.Pause => true,
            Key.NumLock or Key.CapsLock => true,
            _ => false
        };
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

        sb.Append(KeyToDisplayString(_mainKey));

        return sb.ToString();
    }

    private static string KeyToDisplayString(Key key)
    {
        return key switch
        {
            // Letters
            >= Key.A and <= Key.Z => key.ToString(),
            // Numbers (D0-D9 -> 0-9)
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            // NumPad
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)key - (int)Key.NumPad0),
            // Navigation & editing
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            // Arrow keys
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            // Symbol keys
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            // Special keys
            Key.PrintScreen => "PrtSc",
            Key.Scroll => "ScrLk",
            Key.Pause => "Pause",
            Key.NumLock => "NumLock",
            Key.CapsLock => "CapsLock",
            // Function keys
            >= Key.F1 and <= Key.F24 => key.ToString(),
            _ => key.ToString()
        };
    }

    private void ParseHotkey(string? hotkey)
    {
        _winPressed = false;
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = Key.None;

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
                    _mainKey = StringToKey(upperPart);
                    break;
            }
        }
    }

    private static Key StringToKey(string keyStr)
    {
        return keyStr switch
        {
            "SPACE" => Key.Space,
            "TAB" => Key.Tab,
            "ENTER" or "RETURN" => Key.Enter,
            "BACKSPACE" => Key.Back,
            "DELETE" or "DEL" => Key.Delete,
            "INSERT" or "INS" => Key.Insert,
            "HOME" => Key.Home,
            "END" => Key.End,
            "PAGEUP" or "PGUP" => Key.PageUp,
            "PAGEDOWN" or "PGDN" => Key.PageDown,
            "UP" => Key.Up,
            "DOWN" => Key.Down,
            "LEFT" => Key.Left,
            "RIGHT" => Key.Right,
            "PRINTSCREEN" or "PRTSC" or "PRINT" => Key.PrintScreen,
            "SCROLLLOCK" or "SCRLK" => Key.Scroll,
            "PAUSE" or "BREAK" => Key.Pause,
            "NUMLOCK" => Key.NumLock,
            "CAPSLOCK" or "CAPS" => Key.CapsLock,
            // Function keys
            "F1" => Key.F1, "F2" => Key.F2, "F3" => Key.F3, "F4" => Key.F4,
            "F5" => Key.F5, "F6" => Key.F6, "F7" => Key.F7, "F8" => Key.F8,
            "F9" => Key.F9, "F10" => Key.F10, "F11" => Key.F11, "F12" => Key.F12,
            "F13" => Key.F13, "F14" => Key.F14, "F15" => Key.F15, "F16" => Key.F16,
            "F17" => Key.F17, "F18" => Key.F18, "F19" => Key.F19, "F20" => Key.F20,
            "F21" => Key.F21, "F22" => Key.F22, "F23" => Key.F23, "F24" => Key.F24,
            // Numbers
            "0" => Key.D0, "1" => Key.D1, "2" => Key.D2, "3" => Key.D3, "4" => Key.D4,
            "5" => Key.D5, "6" => Key.D6, "7" => Key.D7, "8" => Key.D8, "9" => Key.D9,
            // NumPad
            "NUM0" or "NUMPAD0" => Key.NumPad0,
            "NUM1" or "NUMPAD1" => Key.NumPad1,
            "NUM2" or "NUMPAD2" => Key.NumPad2,
            "NUM3" or "NUMPAD3" => Key.NumPad3,
            "NUM4" or "NUMPAD4" => Key.NumPad4,
            "NUM5" or "NUMPAD5" => Key.NumPad5,
            "NUM6" or "NUMPAD6" => Key.NumPad6,
            "NUM7" or "NUMPAD7" => Key.NumPad7,
            "NUM8" or "NUMPAD8" => Key.NumPad8,
            "NUM9" or "NUMPAD9" => Key.NumPad9,
            // Symbol keys
            "`" or "~" or "TILDE" or "BACKQUOTE" => Key.OemTilde,
            "-" or "_" or "MINUS" => Key.OemMinus,
            "=" or "+" or "PLUS" or "EQUALS" => Key.OemPlus,
            "[" or "{" or "OPENBRACKET" => Key.OemOpenBrackets,
            "]" or "}" or "CLOSEBRACKET" => Key.OemCloseBrackets,
            "\\" or "|" or "PIPE" or "BACKSLASH" => Key.OemPipe,
            ";" or ":" or "SEMICOLON" => Key.OemSemicolon,
            "'" or "\"" or "QUOTE" or "APOSTROPHE" => Key.OemQuotes,
            "," or "<" or "COMMA" => Key.OemComma,
            "." or ">" or "PERIOD" => Key.OemPeriod,
            "/" or "?" or "SLASH" => Key.OemQuestion,
            // Single letter
            _ when keyStr.Length == 1 && char.IsLetter(keyStr[0]) =>
                Enum.TryParse<Key>(keyStr, true, out var key) ? key : Key.None,
            _ => Key.None
        };
    }

    private void UpdateDisplay()
    {
        if (!_controlsInitialized || !IsLoaded ||
            _placeholderText == null || _recordingText == null ||
            _winKeyVisual == null || _ctrlKeyVisual == null ||
            _altKeyVisual == null || _shiftKeyVisual == null ||
            _plusSeparator == null || _mainKeyVisual == null ||
            _recorderBorder == null || _clearButton == null)
        {
            return;
        }

        try
        {
            bool hasModifier = _winPressed || _ctrlPressed || _altPressed || _shiftPressed;
            bool hasMainKey = _mainKey != Key.None;
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
                    _mainKeyVisual.KeyText = KeyToDisplayString(_mainKey);
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
        catch
        {
            // Ignore exceptions during layout/resize
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
}
