using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AIlable.Services;

public interface IFileDialogService
{
    Task<string?> ShowOpenFileDialogAsync(string title, IEnumerable<FilePickerFileType> fileTypes);
    Task<IEnumerable<string>> ShowOpenMultipleFilesDialogAsync(string title, IEnumerable<FilePickerFileType> fileTypes);
    Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, IEnumerable<FilePickerFileType> fileTypes);
    Task<string?> ShowSelectFolderDialogAsync(string title);
}

public class FileDialogService : IFileDialogService
{
    private readonly Window _parentWindow;

    public FileDialogService(Window parentWindow)
    {
        _parentWindow = parentWindow;
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, IEnumerable<FilePickerFileType> fileTypes)
    {
        var storageProvider = _parentWindow.StorageProvider;
        
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes.ToList()
        };

        var result = await storageProvider.OpenFilePickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<IEnumerable<string>> ShowOpenMultipleFilesDialogAsync(string title, IEnumerable<FilePickerFileType> fileTypes)
    {
        var storageProvider = _parentWindow.StorageProvider;
        
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = fileTypes.ToList()
        };

        var result = await storageProvider.OpenFilePickerAsync(options);
        return result.Select(f => f.Path.LocalPath);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, IEnumerable<FilePickerFileType> fileTypes)
    {
        var storageProvider = _parentWindow.StorageProvider;
        
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = fileTypes.ToList()
        };

        var result = await storageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    public async Task<string?> ShowSelectFolderDialogAsync(string title)
    {
        var storageProvider = _parentWindow.StorageProvider;
        
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }

    // File type definitions
    public static readonly FilePickerFileType ImageFiles = new("图像文件")
    {
        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff", "*.tif", "*.webp" },
        MimeTypes = new[] { "image/*" }
    };

    public static readonly FilePickerFileType ProjectFiles = new("AIlable项目文件")
    {
        Patterns = new[] { "*.ailproj" }
    };

    public static readonly FilePickerFileType JsonFiles = new("JSON文件")
    {
        Patterns = new[] { "*.json" }
    };

    public static readonly FilePickerFileType XmlFiles = new("XML文件")
    {
        Patterns = new[] { "*.xml" }
    };

    public static readonly FilePickerFileType TextFiles = new("文本文件")
    {
        Patterns = new[] { "*.txt" }
    };

    public static readonly FilePickerFileType AllFiles = new("所有文件")
    {
        Patterns = new[] { "*.*" }
    };
}