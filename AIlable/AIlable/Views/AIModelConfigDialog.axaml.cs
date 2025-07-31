using Avalonia.Controls;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class AIModelConfigDialog : Window
{
    public AIModelConfigDialog()
    {
        InitializeComponent();
    }
    
    public AIModelConfigDialog(AIModelConfigDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}