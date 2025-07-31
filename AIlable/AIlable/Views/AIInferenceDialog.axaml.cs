using Avalonia.Controls;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class AIInferenceDialog : Window
{
    public AIInferenceDialog()
    {
        InitializeComponent();
    }
    
    public AIInferenceDialog(AIInferenceDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}