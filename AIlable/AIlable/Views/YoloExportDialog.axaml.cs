using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AIlable.ViewModels;

namespace AIlable.Views;

public partial class YoloExportDialog : Window
{
    public YoloExportDialog()
    {
        InitializeComponent();
    }

    public YoloExportDialog(YoloExportDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not YoloExportDialogViewModel viewModel) return;

        var storageProvider = StorageProvider;
        if (storageProvider == null) return;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出目录",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            viewModel.OutputPath = result[0].Path.LocalPath;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
