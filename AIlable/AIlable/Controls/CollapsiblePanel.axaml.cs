using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIlable.Controls;

public partial class CollapsiblePanel : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CollapsiblePanel, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<CollapsiblePanel, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsiblePanel, bool>(nameof(IsExpanded), true);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public CollapsiblePanel()
    {
        InitializeComponent();
        UpdateExpanderArrow();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == IsExpandedProperty)
        {
            UpdateExpanderArrow();
        }
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void UpdateExpanderArrow()
    {
        var arrow = this.FindControl<TextBlock>("ExpanderArrow");
        if (arrow != null)
        {
            if (IsExpanded)
            {
                arrow.Classes.Add("expanded");
            }
            else
            {
                arrow.Classes.Remove("expanded");
            }
        }
    }
}
