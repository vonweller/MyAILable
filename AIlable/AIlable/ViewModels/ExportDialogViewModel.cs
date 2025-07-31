using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AIlable.Models;
using AIlable.Services;

namespace AIlable.ViewModels;

public partial class ExportDialogViewModel : ViewModelBase
{
    [ObservableProperty] private ExportFormatInfo _selectedFormat;
    [ObservableProperty] private string _outputPath = "";
    [ObservableProperty] private bool _includeImages;
    [ObservableProperty] private bool _splitTrainVal;
    [ObservableProperty] private double _trainRatio = 0.8;

    public List<ExportFormatInfo> AvailableFormats { get; }

    public ExportDialogViewModel()
    {
        _includeImages = true;
        _splitTrainVal = true;

        AvailableFormats = new List<ExportFormatInfo>
        {
            new ExportFormatInfo(ExportFormat.COCO, "COCO", "MS COCO 格式 - 用于目标检测和分割"),
            new ExportFormatInfo(ExportFormat.VOC, "PASCAL VOC", "PASCAL VOC 格式 - XML 标注文件"),
            new ExportFormatInfo(ExportFormat.YOLO, "YOLO", "YOLO 格式 - TXT 标注文件，适用于 YOLOv5/v8"),
            new ExportFormatInfo(ExportFormat.TXT, "TXT", "简单文本格式 - 每行一个标注")
        };
        
        _selectedFormat = AvailableFormats[0]; // 默认选择第一个
    }
}

public record ExportFormatInfo(ExportFormat Format, string Name, string Description);