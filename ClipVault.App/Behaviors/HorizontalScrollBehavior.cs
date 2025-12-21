using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace ClipVault.App.Behaviors;

/// <summary>
/// Behavior that converts vertical mouse wheel scrolling to horizontal scrolling.
/// Useful for horizontal ScrollViewers where users expect mouse wheel to scroll horizontally.
/// </summary>
public class HorizontalScrollBehavior : Behavior<ScrollViewer>
{
    /// <summary>
    /// The scroll speed multiplier for mouse wheel events.
    /// </summary>
    public static readonly StyledProperty<double> ScrollSpeedProperty =
        AvaloniaProperty.Register<HorizontalScrollBehavior, double>(nameof(ScrollSpeed), 50.0);
    
    /// <summary>
    /// Gets or sets the scroll speed multiplier.
    /// </summary>
    public double ScrollSpeed
    {
        get => GetValue(ScrollSpeedProperty);
        set => SetValue(ScrollSpeedProperty, value);
    }
    
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerWheelChanged += OnPointerWheelChanged;
        }
    }
    
    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerWheelChanged -= OnPointerWheelChanged;
        }
        base.OnDetaching();
    }
    
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (AssociatedObject == null) return;
        
        // Convert vertical scroll to horizontal scroll
        double delta = e.Delta.Y * ScrollSpeed;
        AssociatedObject.Offset = new Vector(
            AssociatedObject.Offset.X - delta, 
            AssociatedObject.Offset.Y);
        
        e.Handled = true;
    }
}
