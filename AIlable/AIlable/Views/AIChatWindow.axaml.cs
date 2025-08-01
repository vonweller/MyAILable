using Avalonia.Controls;
using AIlable.ViewModels;
using AIlable.Services;

namespace AIlable.Views;

public partial class AIChatWindow : Window
{
    public AIChatWindow()
    {
        InitializeComponent();
        
        // 创建默认的ViewModel和服务
        var chatService = new AIChatService();
        var viewModel = new AIChatViewModel(chatService, null);
        DataContext = viewModel;
    }
    
    public AIChatWindow(AIChatViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
    
    public AIChatWindow(IFileDialogService fileDialogService) : this()
    {
        var chatService = new AIChatService();
        var viewModel = new AIChatViewModel(chatService, fileDialogService);
        DataContext = viewModel;
    }
}