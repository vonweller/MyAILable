using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIlable.Controls;

public partial class SimpleCollapsiblePanel : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SimpleCollapsiblePanel, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SimpleCollapsiblePanel, bool>(nameof(IsExpanded), false);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public SimpleCollapsiblePanel()
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
            arrow.Text = IsExpanded ? "▲" : "▼";
        }
    }
}
