using Avalonia.Controls;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class AIChatView : UserControl
{
    public AIChatView()
    {
        InitializeComponent();
    }
    
    public AIChatView(AIChatViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}