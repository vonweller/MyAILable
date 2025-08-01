using System.Collections.Generic;
using System.Linq;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using AIlable.Models;

namespace AIlable.ViewModels;

public enum YoloExportFormat
{
    Detection,
    Segmentation,
    OBB,
    Pose
}

public class YoloFormatOption
{
    public YoloExportFormat Format { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Icon { get; set; } = string.Empty;
}

public partial class YoloExportDialogViewModel : ObservableObject
{
    [ObservableProperty] private YoloExportFormat _selectedFormat = YoloExportFormat.Detection;
    [ObservableProperty] private bool _includeImages = true;
    [ObservableProperty] private bool _splitTrainVal = true;
    [ObservableProperty] private double _trainRatio = 0.8;
    [ObservableProperty] private string _outputPath = string.Empty;
    [ObservableProperty] private string _formatDescription = string.Empty;
    [ObservableProperty] private List<AnnotationType> _annotationTypes = new();
    [ObservableProperty] private Dictionary<AnnotationType, int> _typeStatistics = new();
    [ObservableProperty] private string _recommendation = string.Empty;
    [ObservableProperty] private List<YoloFormatOption> _availableFormats = new();

    // 为了向后兼容，保留这个属性
    public bool UseSegmentationFormat
    {
        get => SelectedFormat == YoloExportFormat.Segmentation;
        set
        {
            if (value)
                SelectedFormat = YoloExportFormat.Segmentation;
            else
                SelectedFormat = YoloExportFormat.Detection;
        }
    }

    public YoloExportDialogViewModel()
    {
        InitializeAvailableFormats();
        UpdateFormatDescription();
    }

    public YoloExportDialogViewModel(List<AnnotationType> annotationTypes, Dictionary<AnnotationType, int> typeStatistics) : this()
    {
        _annotationTypes = annotationTypes;
        _typeStatistics = typeStatistics;
        UpdateAvailableFormats();
        UpdateFormatDescription();
        UpdateRecommendation();
    }

    private void InitializeAvailableFormats()
    {
        AvailableFormats = new List<YoloFormatOption>
        {
            new YoloFormatOption
            {
                Format = YoloExportFormat.Detection,
                Name = "目标检测 (Detection)",
                Description = "适用于目标检测任务\n格式: class_id center_x center_y width height",
                Icon = "📦",
                IsEnabled = true
            },
            new YoloFormatOption
            {
                Format = YoloExportFormat.Segmentation,
                Name = "实例分割 (Segmentation)",
                Description = "适用于实例分割任务\n格式: class_id x1 y1 x2 y2 ... xn yn",
                Icon = "🎯",
                IsEnabled = true
            },
            new YoloFormatOption
            {
                Format = YoloExportFormat.OBB,
                Name = "有向边界框 (OBB)",
                Description = "适用于旋转目标检测任务\n格式: class_id center_x center_y width height angle",
                Icon = "🔄",
                IsEnabled = true
            },
            new YoloFormatOption
            {
                Format = YoloExportFormat.Pose,
                Name = "关键点姿态 (Pose)",
                Description = "适用于人体姿态估计任务\n格式: class_id x1 y1 v1 x2 y2 v2 ... x17 y17 v17",
                Icon = "🤸",
                IsEnabled = true
            }
        };
    }

    private void UpdateAvailableFormats()
    {
        var hasRectangle = AnnotationTypes.Contains(AnnotationType.Rectangle);
        var hasCircle = AnnotationTypes.Contains(AnnotationType.Circle);
        var hasPolygon = AnnotationTypes.Contains(AnnotationType.Polygon);
        var hasObb = AnnotationTypes.Contains(AnnotationType.OrientedBoundingBox);
        var hasKeypoint = AnnotationTypes.Contains(AnnotationType.Keypoint);
        var hasLine = AnnotationTypes.Contains(AnnotationType.Line);
        var hasPoint = AnnotationTypes.Contains(AnnotationType.Point);

        foreach (var format in AvailableFormats)
        {
            switch (format.Format)
            {
                case YoloExportFormat.Detection:
                    // 检测格式支持所有类型（转换为边界框）
                    format.IsEnabled = true;
                    break;

                case YoloExportFormat.Segmentation:
                    // 分割格式最适合多边形，但也支持其他类型
                    format.IsEnabled = hasPolygon || hasRectangle || hasCircle;
                    if (!format.IsEnabled)
                        format.Description += "\n⚠️ 项目中无可分割的标注类型";
                    break;

                case YoloExportFormat.OBB:
                    // OBB格式最适合有向边界框，但也可以处理其他矩形类型
                    format.IsEnabled = hasObb || hasRectangle || hasCircle;
                    if (!format.IsEnabled)
                        format.Description += "\n⚠️ 项目中无可转换为OBB的标注类型";
                    break;

                case YoloExportFormat.Pose:
                    // 姿态格式只支持关键点标注
                    format.IsEnabled = hasKeypoint;
                    if (!format.IsEnabled)
                        format.Description += "\n⚠️ 项目中无关键点姿态标注";
                    break;
            }
        }

        // 如果当前选择的格式不可用，自动切换到第一个可用的格式
        var currentFormat = AvailableFormats.FirstOrDefault(f => f.Format == SelectedFormat);
        if (currentFormat != null && !currentFormat.IsEnabled)
        {
            var firstAvailable = AvailableFormats.FirstOrDefault(f => f.IsEnabled);
            if (firstAvailable != null)
            {
                SelectedFormat = firstAvailable.Format;
            }
        }
    }

    partial void OnSelectedFormatChanged(YoloExportFormat value)
    {
        UpdateFormatDescription();
        UpdateRecommendation();
        OnPropertyChanged(nameof(UseSegmentationFormat));
    }

    private void UpdateFormatDescription()
    {
        var selectedOption = AvailableFormats.FirstOrDefault(f => f.Format == SelectedFormat);
        if (selectedOption != null)
        {
            FormatDescription = selectedOption.Description;
        }
        else
        {
            switch (SelectedFormat)
            {
                case YoloExportFormat.Detection:
                    FormatDescription = "检测格式 (YOLOv5/v8 Detection):\n" +
                                      "• 所有标注类型 → 边界框格式\n" +
                                      "• 适用于目标检测模型训练\n" +
                                      "• 文件格式: class_id center_x center_y width height\n" +
                                      "• 所有坐标归一化到 [0,1] 范围";
                    break;

                case YoloExportFormat.Segmentation:
                    FormatDescription = "分割格式 (YOLOv5/v8 Segmentation):\n" +
                                      "• 多边形标注 → 精确分割掩码坐标\n" +
                                      "• 矩形/圆形标注 → 边界框格式\n" +
                                      "• 适用于实例分割模型训练\n" +
                                      "• 文件格式: class_id x1 y1 x2 y2 ... xn yn";
                    break;

                case YoloExportFormat.OBB:
                    FormatDescription = "OBB格式 (YOLOv8 OBB):\n" +
                                      "• 有向边界框 → 保持旋转角度\n" +
                                      "• 矩形/圆形标注 → 转换为OBB格式\n" +
                                      "• 适用于旋转目标检测模型训练\n" +
                                      "• 文件格式: class_id center_x center_y width height angle";
                    break;

                case YoloExportFormat.Pose:
                    FormatDescription = "姿态格式 (YOLOv8 Pose):\n" +
                                      "• 关键点标注 → 17个COCO关键点\n" +
                                      "• 包含关键点可见性信息\n" +
                                      "• 适用于人体姿态估计模型训练\n" +
                                      "• 文件格式: class_id x1 y1 v1 x2 y2 v2 ... x17 y17 v17";
                    break;
            }
        }
    }

    public string GetRecommendation()
    {
        var hasPolygon = AnnotationTypes.Contains(AnnotationType.Polygon);
        var hasRectangle = AnnotationTypes.Contains(AnnotationType.Rectangle);
        var hasCircle = AnnotationTypes.Contains(AnnotationType.Circle);
        var hasObb = AnnotationTypes.Contains(AnnotationType.OrientedBoundingBox);
        var hasKeypoint = AnnotationTypes.Contains(AnnotationType.Keypoint);

        if (hasKeypoint)
        {
            return "检测到关键点姿态标注。建议：\n• 使用姿态格式进行人体姿态估计训练\n• 或使用检测格式进行人体检测训练";
        }
        else if (hasObb)
        {
            return "检测到有向边界框标注。建议：\n• 使用OBB格式保持旋转角度信息\n• 或使用检测格式转换为普通边界框";
        }
        else if (hasPolygon && (hasRectangle || hasCircle))
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
                   "• 所有格式都支持，推荐使用检测格式\n" +
                   "• 如需支持旋转，可选择OBB格式";
        }
    }

    private void UpdateRecommendation()
    {
        Recommendation = GetRecommendation();
    }
}