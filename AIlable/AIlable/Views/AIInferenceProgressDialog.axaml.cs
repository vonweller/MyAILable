using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class AIInferenceProgressDialog : Window
{
    public AIInferenceProgressDialog()
    {
        InitializeComponent();
    }

    public AIInferenceProgressDialog(AIInferenceProgressDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        // 移除事件订阅，对话框只显示进度信息
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理资源
        if (DataContext is AIInferenceProgressDialogViewModel viewModel)
        {
            // 停止计时器
            viewModel.SetCancelled();
        }

        base.OnClosed(e);
    }
}
