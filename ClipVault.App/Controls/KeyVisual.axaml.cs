using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace ClipVault.App.Controls;

/// <summary>
/// Visual style for key display boxes.
/// </summary>
public enum KeyVisualStyle
{
    /// <summary>Default gray style for modifier keys.</summary>
    Default,
    /// <summary>Accent color style for the main key.</summary>
    Accent,
    /// <summary>Subtle style for inactive/placeholder keys.</summary>
    Subtle
}

/// <summary>
/// A visual control representing a single key in a keyboard shortcut display.
/// Similar to PowerToys' key visual style.
/// </summary>
public partial class KeyVisual : UserControl
{
    private Border? _keyBorder;
    private TextBlock? _keyTextBlock;
    private bool _controlsInitialized;
    
    /// <summary>
    /// Defines the KeyText styled property.
    /// </summary>
    public static readonly StyledProperty<string> KeyTextProperty =
        AvaloniaProperty.Register<KeyVisual, string>(nameof(KeyText), defaultValue: string.Empty);
    
    /// <summary>
    /// Defines the VisualStyle styled property.
    /// </summary>
    public static readonly StyledProperty<KeyVisualStyle> VisualStyleProperty =
        AvaloniaProperty.Register<KeyVisual, KeyVisualStyle>(nameof(VisualStyle), defaultValue: KeyVisualStyle.Default);
    
    /// <summary>
    /// Gets or sets the text displayed on the key.
    /// </summary>
    public string KeyText
    {
        get => GetValue(KeyTextProperty);
        set => SetValue(KeyTextProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the visual style of the key.
    /// </summary>
    public KeyVisualStyle VisualStyle
    {
        get => GetValue(VisualStyleProperty);
        set => SetValue(VisualStyleProperty, value);
    }
    
    public KeyVisual()
    {
        InitializeComponent();
    }
    
    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SetupControls();
    }
    
    private void SetupControls()
    {
        if (_controlsInitialized) return;
        
        _keyBorder = this.FindControl<Border>("KeyBorder");
        _keyTextBlock = this.FindControl<TextBlock>("KeyTextBlock");
        
        _controlsInitialized = true;
        UpdateVisuals();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == KeyTextProperty || change.Property == VisualStyleProperty)
        {
            UpdateVisuals();
        }
    }
    
    private void UpdateVisuals()
    {
        // Skip if controls aren't ready or control isn't fully loaded
        if (_keyBorder == null || _keyTextBlock == null || !IsLoaded)
        {
            return;
        }
        
        try
        {
            _keyTextBlock.Text = KeyText;
            
            // ActualThemeVariant can be null during layout before control is fully attached
            var themeVariant = ActualThemeVariant;
            bool isDark = themeVariant == ThemeVariant.Dark || themeVariant == null;
            
            switch (VisualStyle)
            {
                case KeyVisualStyle.Default:
                    _keyBorder.Background = GetBrush(isDark ? "#3D3D3D" : "#E5E5E5");
                    _keyBorder.BorderBrush = GetBrush(isDark ? "#5C5C5C" : "#CCCCCC");
                    _keyTextBlock.Foreground = GetBrush(isDark ? "#FFFFFF" : "#1A1A1A");
                    break;
                    
                case KeyVisualStyle.Accent:
                    // Use fallback colors - don't try to access theme resources during layout
                    _keyBorder.Background = GetBrush("#0078D4");
                    _keyBorder.BorderBrush = GetBrush("#106EBE");
                    _keyTextBlock.Foreground = Brushes.White;
                    break;
                    
                case KeyVisualStyle.Subtle:
                    _keyBorder.Background = GetBrush(isDark ? "#2D2D2D" : "#F0F0F0");
                    _keyBorder.BorderBrush = GetBrush(isDark ? "#4D4D4D" : "#DDDDDD");
                    _keyTextBlock.Foreground = GetBrush(isDark ? "#888888" : "#888888");
                    break;
            }
        }
        catch
        {
            // Ignore exceptions during layout/resize - visuals will update on next valid call
        }
    }
    
    private static SolidColorBrush GetBrush(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }
}
