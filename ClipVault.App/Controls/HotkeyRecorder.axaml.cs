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
/// </summary>
public partial class HotkeyRecorder : UserControl
{
    private bool _isRecording;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _altPressed;
    private Key _mainKey = Key.None;
    
    private Border? _recorderBorder;
    private TextBlock? _hotkeyDisplay;
    private Button? _clearButton;
    private bool _controlsInitialized;
    
    /// <summary>
    /// Defines the Hotkey styled property.
    /// </summary>
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyRecorder, string>(nameof(Hotkey), defaultValue: string.Empty);
    
    /// <summary>
    /// Gets or sets the hotkey string (e.g., "Ctrl+Shift+V").
    /// </summary>
    public string Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }
    
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
        _hotkeyDisplay = this.FindControl<TextBlock>("HotkeyDisplay");
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
        
        if (change.Property == HotkeyProperty)
        {
            ParseHotkey(change.GetNewValue<string>() ?? string.Empty);
            UpdateDisplay();
        }
    }
    
    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        StartRecording();
        e.Handled = true;
    }
    
    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = Key.None;
        Hotkey = string.Empty;
        UpdateDisplay();
    }
    
    private void StartRecording()
    {
        _isRecording = true;
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = Key.None;
        
        Focus();
        UpdateDisplay();
    }
    
    private void StopRecording()
    {
        _isRecording = false;
        UpdateDisplay();
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyDown(e);
            return;
        }
        
        e.Handled = true;
        
        switch (e.Key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                _ctrlPressed = true;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                _shiftPressed = true;
                break;
            case Key.LeftAlt:
            case Key.RightAlt:
                _altPressed = true;
                break;
            case Key.Escape:
                // Cancel recording
                ParseHotkey(Hotkey);
                StopRecording();
                break;
            case Key.System:
                // System key is pressed when Alt is down
                break;
            default:
                // This is the main key
                if (IsValidMainKey(e.Key))
                {
                    _mainKey = e.Key;
                    
                    // Check if at least one modifier is pressed
                    if (_ctrlPressed || _shiftPressed || _altPressed)
                    {
                        // Valid hotkey combination
                        Hotkey = BuildHotkeyString();
                        StopRecording();
                    }
                }
                break;
        }
        
        UpdateDisplay();
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyUp(e);
            return;
        }
        
        e.Handled = true;
        
        switch (e.Key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                _ctrlPressed = false;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                _shiftPressed = false;
                break;
            case Key.LeftAlt:
            case Key.RightAlt:
                _altPressed = false;
                break;
        }
        
        UpdateDisplay();
    }
    
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        
        if (_isRecording)
        {
            // Restore previous hotkey if recording was cancelled
            ParseHotkey(Hotkey);
            StopRecording();
        }
    }
    
    private bool IsValidMainKey(Key key)
    {
        // Allow letters, numbers, function keys, and special keys
        return key switch
        {
            >= Key.A and <= Key.Z => true,
            >= Key.D0 and <= Key.D9 => true,
            >= Key.NumPad0 and <= Key.NumPad9 => true,
            >= Key.F1 and <= Key.F12 => true,
            Key.Space or Key.Tab or Key.Enter or Key.Back or Key.Delete or Key.Insert => true,
            Key.Home or Key.End or Key.PageUp or Key.PageDown => true,
            Key.Up or Key.Down or Key.Left or Key.Right => true,
            Key.OemTilde or Key.OemMinus or Key.OemPlus => true,
            Key.OemOpenBrackets or Key.OemCloseBrackets => true,
            Key.OemPipe or Key.OemSemicolon or Key.OemQuotes => true,
            Key.OemComma or Key.OemPeriod or Key.OemQuestion => true,
            _ => false
        };
    }
    
    private string BuildHotkeyString()
    {
        var sb = new StringBuilder();
        
        if (_ctrlPressed)
        {
            sb.Append("Ctrl+");
        }
        
        if (_shiftPressed)
        {
            sb.Append("Shift+");
        }
        
        if (_altPressed)
        {
            sb.Append("Alt+");
        }
        
        sb.Append(KeyToString(_mainKey));
        
        return sb.ToString();
    }
    
    private static string KeyToString(Key key)
    {
        return key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => key.ToString()[1..], // Remove 'D' prefix
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + key.ToString()[6..],
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
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
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
            _ when key >= Key.F1 && key <= Key.F12 => key.ToString(),
            _ => key.ToString()
        };
    }
    
    private void ParseHotkey(string hotkey)
    {
        _ctrlPressed = false;
        _shiftPressed = false;
        _altPressed = false;
        _mainKey = Key.None;
        
        if (string.IsNullOrWhiteSpace(hotkey))
            return;
        
        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        foreach (string part in parts)
        {
            string upperPart = part.ToUpperInvariant();
            
            switch (upperPart)
            {
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
            "F1" => Key.F1,
            "F2" => Key.F2,
            "F3" => Key.F3,
            "F4" => Key.F4,
            "F5" => Key.F5,
            "F6" => Key.F6,
            "F7" => Key.F7,
            "F8" => Key.F8,
            "F9" => Key.F9,
            "F10" => Key.F10,
            "F11" => Key.F11,
            "F12" => Key.F12,
            "0" => Key.D0,
            "1" => Key.D1,
            "2" => Key.D2,
            "3" => Key.D3,
            "4" => Key.D4,
            "5" => Key.D5,
            "6" => Key.D6,
            "7" => Key.D7,
            "8" => Key.D8,
            "9" => Key.D9,
            _ when keyStr.Length == 1 && char.IsLetter(keyStr[0]) => 
                Enum.TryParse<Key>(keyStr, true, out var key) ? key : Key.None,
            _ => Key.None
        };
    }
    
    private void UpdateDisplay()
    {
        if (_hotkeyDisplay == null || _recorderBorder == null || _clearButton == null)
            return;
        
        if (_isRecording)
        {
            // Show current recording state
            var sb = new StringBuilder();
            
            if (_ctrlPressed) sb.Append("Ctrl + ");
            if (_shiftPressed) sb.Append("Shift + ");
            if (_altPressed) sb.Append("Alt + ");
            
            if (sb.Length > 0)
            {
                sb.Append("...");
                _hotkeyDisplay.Text = sb.ToString();
                _hotkeyDisplay.Foreground = GetResourceBrush("TextControlForeground");
            }
            else
            {
                _hotkeyDisplay.Text = "Press keys...";
                _hotkeyDisplay.Foreground = GetResourceBrush("TextControlPlaceholderForeground");
            }
            
            _recorderBorder.BorderBrush = GetResourceBrush("SystemAccentColor");
            _recorderBorder.BorderThickness = new Thickness(2);
            _clearButton.IsVisible = false;
        }
        else
        {
            // Show current hotkey or placeholder
            if (!string.IsNullOrEmpty(Hotkey) && _mainKey != Key.None)
            {
                _hotkeyDisplay.Text = FormatHotkeyForDisplay();
                _hotkeyDisplay.Foreground = GetResourceBrush("TextControlForeground");
                _clearButton.IsVisible = true;
            }
            else
            {
                _hotkeyDisplay.Text = "Click to record...";
                _hotkeyDisplay.Foreground = GetResourceBrush("TextControlPlaceholderForeground");
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
        // Try to get from Application resources first (theme-aware)
        if (Application.Current?.TryGetResource(resourceKey, ActualThemeVariant, out object? resource) == true 
            && resource is IBrush brush)
        {
            return brush;
        }
        
        // Fallback colors based on theme
        bool isDark = ActualThemeVariant == ThemeVariant.Dark;
        
        return resourceKey switch
        {
            "TextControlForeground" => isDark ? Brushes.White : Brushes.Black,
            "TextControlPlaceholderForeground" => isDark ? Brushes.Gray : Brushes.DarkGray,
            "TextControlBorderBrush" => isDark ? Brushes.DimGray : Brushes.LightGray,
            "SystemAccentColor" => Brushes.DodgerBlue,
            _ => Brushes.Gray
        };
    }
    
    private string FormatHotkeyForDisplay()
    {
        var sb = new StringBuilder();
        
        if (_ctrlPressed) sb.Append("Ctrl + ");
        if (_shiftPressed) sb.Append("Shift + ");
        if (_altPressed) sb.Append("Alt + ");
        
        sb.Append(KeyToString(_mainKey));
        
        return sb.ToString();
    }
}
