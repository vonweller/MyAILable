using Avalonia.Controls;
using Avalonia.Interactivity;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class MixedAnnotationWarningDialog : Window
{
    public MixedAnnotationWarningDialog()
    {
        InitializeComponent();
    }

    public MixedAnnotationWarningDialog(MixedAnnotationWarningDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
