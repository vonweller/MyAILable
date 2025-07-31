using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using AIlable.Models;

namespace AIlable.ViewModels;

public partial class MixedAnnotationWarningDialogViewModel : ObservableObject
{
    [ObservableProperty] private List<AnnotationType> _annotationTypes = new();
    [ObservableProperty] private string _exportFormat = string.Empty;
    [ObservableProperty] private bool _useSegmentationFormat;
    [ObservableProperty] private string _warningMessage = string.Empty;
    [ObservableProperty] private string _recommendationMessage = string.Empty;

    public MixedAnnotationWarningDialogViewModel(List<AnnotationType> annotationTypes, string exportFormat, bool useSegmentationFormat = true)
    {
        _annotationTypes = annotationTypes;
        _exportFormat = exportFormat;
        _useSegmentationFormat = useSegmentationFormat;
        
        GenerateWarningMessage();
        GenerateRecommendationMessage();
    }

    private void GenerateWarningMessage()
    {
        var typeNames = AnnotationTypes.Select(GetAnnotationTypeName).ToList();
        var typeList = string.Join("、", typeNames);
        
        WarningMessage = $"检测到项目中包含多种标注类型：{typeList}";
    }

    private void GenerateRecommendationMessage()
    {
        var hasRectangle = AnnotationTypes.Contains(AnnotationType.Rectangle);
        var hasPolygon = AnnotationTypes.Contains(AnnotationType.Polygon);
        var hasCircle = AnnotationTypes.Contains(AnnotationType.Circle);

        switch (ExportFormat.ToUpper())
        {
            case "YOLO":
                if (UseSegmentationFormat)
                {
                    if (hasPolygon && (hasRectangle || hasCircle))
                    {
                        RecommendationMessage = "分割格式导出：\n" +
                                              "• 多边形标注 → 精确分割掩码\n" +
                                              "• 矩形/圆形标注 → 边界框格式\n\n" +
                                              "建议：统一使用多边形标注以获得最佳分割效果";
                    }
                    else if (hasPolygon)
                    {
                        RecommendationMessage = "分割格式导出：多边形标注将输出为精确的分割掩码";
                    }
                    else
                    {
                        RecommendationMessage = "分割格式导出：所有标注将转换为边界框格式";
                    }
                }
                else
                {
                    RecommendationMessage = "检测格式导出：所有标注类型都将转换为边界框格式，适用于目标检测模型训练";
                }
                break;

            case "COCO":
                if (hasPolygon && (hasRectangle || hasCircle))
                {
                    RecommendationMessage = "COCO格式导出：\n" +
                                          "• 多边形标注 → 分割掩码\n" +
                                          "• 矩形/圆形标注 → 边界框\n\n" +
                                          "支持同时用于检测和分割任务";
                }
                else
                {
                    RecommendationMessage = "COCO格式导出：支持多种标注类型，可用于检测和分割任务";
                }
                break;

            case "VOC":
                RecommendationMessage = "VOC格式导出：所有标注类型都将转换为边界框格式，适用于目标检测模型训练";
                break;

            default:
                RecommendationMessage = "不同标注类型在导出时可能会有不同的处理方式，请确认这符合您的训练需求";
                break;
        }
    }

    private string GetAnnotationTypeName(AnnotationType type)
    {
        return type switch
        {
            AnnotationType.Rectangle => "矩形",
            AnnotationType.Circle => "圆形",
            AnnotationType.Line => "线条",
            AnnotationType.Point => "点",
            AnnotationType.Polygon => "多边形",
            _ => type.ToString()
        };
    }

    public static readonly IValueConverter AnnotationTypeConverter = new FuncValueConverter<AnnotationType, string>(type =>
    {
        return type switch
        {
            AnnotationType.Rectangle => "矩形",
            AnnotationType.Circle => "圆形",
            AnnotationType.Line => "线条",
            AnnotationType.Point => "点",
            AnnotationType.Polygon => "多边形",
            _ => type.ToString()
        };
    });
}
