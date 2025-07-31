using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(ExportDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportDialogViewModel viewModel) return;

        var folderPicker = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出文件夹",
            AllowMultiple = false
        });

        if (folderPicker.Count > 0)
        {
            viewModel.OutputPath = folderPicker[0].Path.LocalPath;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExportDialogViewModel viewModel)
        {
            if (string.IsNullOrEmpty(viewModel.OutputPath))
            {
                // 可以添加错误提示
                return;
            }
        }
        
        Close(true);
    }
}