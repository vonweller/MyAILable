using System.Collections.Generic;
using System.Linq;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using AIlable.Models;

namespace AIlable.ViewModels;

public partial class YoloExportDialogViewModel : ObservableObject
{
    [ObservableProperty] private bool _useSegmentationFormat = true;
    [ObservableProperty] private bool _includeImages = true;
    [ObservableProperty] private bool _splitTrainVal = true;
    [ObservableProperty] private double _trainRatio = 0.8;
    [ObservableProperty] private string _outputPath = string.Empty;
    [ObservableProperty] private string _formatDescription = string.Empty;
    [ObservableProperty] private List<AnnotationType> _annotationTypes = new();
    [ObservableProperty] private Dictionary<AnnotationType, int> _typeStatistics = new();
    [ObservableProperty] private string _recommendation = string.Empty;

    public YoloExportDialogViewModel()
    {
        UpdateFormatDescription();
    }

    public YoloExportDialogViewModel(List<AnnotationType> annotationTypes, Dictionary<AnnotationType, int> typeStatistics) : this()
    {
        _annotationTypes = annotationTypes;
        _typeStatistics = typeStatistics;
        UpdateFormatDescription();
        UpdateRecommendation();
    }

    partial void OnUseSegmentationFormatChanged(bool value)
    {
        UpdateFormatDescription();
        UpdateRecommendation();
    }

    private void UpdateFormatDescription()
    {
        if (UseSegmentationFormat)
        {
            FormatDescription = "分割格式 (YOLOv5/v8 Segmentation):\n" +
                              "• 多边形标注 → 精确分割掩码坐标\n" +
                              "• 矩形/圆形标注 → 边界框格式\n" +
                              "• 适用于实例分割模型训练\n" +
                              "• 文件格式: class_id x1 y1 x2 y2 ... xn yn";
        }
        else
        {
            FormatDescription = "检测格式 (YOLOv5/v8 Detection):\n" +
                              "• 所有标注类型 → 边界框格式\n" +
                              "• 适用于目标检测模型训练\n" +
                              "• 文件格式: class_id center_x center_y width height\n" +
                              "• 所有坐标归一化到 [0,1] 范围";
        }
    }

    public string GetRecommendation()
    {
        var hasPolygon = AnnotationTypes.Contains(AnnotationType.Polygon);
        var hasRectangle = AnnotationTypes.Contains(AnnotationType.Rectangle);
        var hasCircle = AnnotationTypes.Contains(AnnotationType.Circle);

        if (hasPolygon && (hasRectangle || hasCircle))
        {
            return "检测到混合标注类型。建议：\n" +
                   "• 如需精确分割效果，选择分割格式\n" +
                   "• 如只需检测边界框，选择检测格式";
        }
        else if (hasPolygon)
        {
            return "检测到多边形标注。建议：\n" +
                   "• 选择分割格式以保持精确形状\n" +
                   "• 或选择检测格式转换为边界框";
        }
        else
        {
            return "检测到矩形/圆形标注。建议：\n" +
                   "• 两种格式效果相同，都输出边界框\n" +
                   "• 推荐使用检测格式";
        }
    }

    private void UpdateRecommendation()
    {
        Recommendation = GetRecommendation();
    }
}
