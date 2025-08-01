using System;
using System.Collections.Generic;
using System.Linq;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 智能工具切换服务 - 根据AI推理结果自动切换合适的标注工具
/// </summary>
public class SmartToolSwitchingService
{
    private readonly ToolManager _toolManager;
    private AIModelType? _currentModelType;
    private AnnotationType? _lastPredominantType;
    
    public SmartToolSwitchingService(ToolManager toolManager)
    {
        _toolManager = toolManager;
    }
    
    /// <summary>
    /// 设置当前使用的AI模型类型
    /// </summary>
    public void SetCurrentModelType(AIModelType modelType)
    {
        _currentModelType = modelType;
        
        // 根据模型类型预先切换到合适的工具
        var suggestedTool = GetSuggestedToolForModelType(modelType);
        if (suggestedTool.HasValue)
        {
            _toolManager.ActiveTool = suggestedTool.Value;
            Console.WriteLine($"Auto-switched to {suggestedTool.Value} tool for {modelType} model");
        }
    }
    
    /// <summary>
    /// 根据AI推理结果智能切换工具
    /// </summary>
    public void SwitchToolBasedOnInferenceResult(IEnumerable<Annotation> annotations)
    {
        if (!annotations.Any()) return;
        
        // 分析推理结果中的标注类型分布
        var typeDistribution = AnalyzeAnnotationTypes(annotations);
        var predominantType = GetPredominantType(typeDistribution);
        
        // 如果主要类型发生变化，切换工具
        if (predominantType != _lastPredominantType && predominantType.HasValue)
        {
            var suggestedTool = GetToolForAnnotationType(predominantType.Value);
            if (suggestedTool.HasValue)
            {
                _toolManager.ActiveTool = suggestedTool.Value;
                _lastPredominantType = predominantType;
                
                Console.WriteLine($"Smart tool switch: Detected {predominantType.Value} annotations, switched to {suggestedTool.Value} tool");
            }
        }
    }
    
    /// <summary>
    /// 分析标注类型分布
    /// </summary>
    private Dictionary<AnnotationType, int> AnalyzeAnnotationTypes(IEnumerable<Annotation> annotations)
    {
        var distribution = new Dictionary<AnnotationType, int>();
        
        foreach (var annotation in annotations)
        {
            if (distribution.ContainsKey(annotation.Type))
                distribution[annotation.Type]++;
            else
                distribution[annotation.Type] = 1;
        }
        
        return distribution;
    }
    
    /// <summary>
    /// 获取占主导地位的标注类型
    /// </summary>
    private AnnotationType? GetPredominantType(Dictionary<AnnotationType, int> typeDistribution)
    {
        if (!typeDistribution.Any()) return null;
        
        return typeDistribution.OrderByDescending(kvp => kvp.Value).First().Key;
    }
    
    /// <summary>
    /// 根据AI模型类型获取建议的工具
    /// </summary>
    private Models.AnnotationTool? GetSuggestedToolForModelType(AIModelType modelType)
    {
        return modelType switch
        {
            AIModelType.YOLO => Models.AnnotationTool.Rectangle,
            AIModelType.SegmentAnything => Models.AnnotationTool.Polygon,
            AIModelType.Custom => Models.AnnotationTool.OrientedBoundingBox, // 假设Custom用于OBB
            _ => null
        };
    }
    
    /// <summary>
    /// 根据标注类型获取对应的工具
    /// </summary>
    private Models.AnnotationTool? GetToolForAnnotationType(AnnotationType annotationType)
    {
        return annotationType switch
        {
            AnnotationType.Rectangle => Models.AnnotationTool.Rectangle,
            AnnotationType.Polygon => Models.AnnotationTool.Polygon,
            AnnotationType.Circle => Models.AnnotationTool.Circle,
            AnnotationType.Line => Models.AnnotationTool.Line,
            AnnotationType.Point => Models.AnnotationTool.Point,
            AnnotationType.OrientedBoundingBox => Models.AnnotationTool.OrientedBoundingBox,
            _ => null
        };
    }
    
    /// <summary>
    /// 获取工具切换建议信息
    /// </summary>
    public string GetToolSwitchSuggestion(IEnumerable<Annotation> annotations)
    {
        if (!annotations.Any()) 
            return "无AI推理结果";
            
        var typeDistribution = AnalyzeAnnotationTypes(annotations);
        var predominantType = GetPredominantType(typeDistribution);
        
        if (!predominantType.HasValue)
            return "无法确定标注类型";
            
        var suggestedTool = GetToolForAnnotationType(predominantType.Value);
        var currentTool = _toolManager.ActiveTool;
        
        if (suggestedTool.HasValue && suggestedTool.Value != currentTool)
        {
            return $"检测到{predominantType.Value}标注，建议切换到{GetToolDisplayName(suggestedTool.Value)}工具";
        }
        
        return $"当前工具({GetToolDisplayName(currentTool)})适合检测到的{predominantType.Value}标注";
    }
    
    /// <summary>
    /// 获取工具显示名称
    /// </summary>
    private string GetToolDisplayName(Models.AnnotationTool tool)
    {
        return tool switch
        {
            Models.AnnotationTool.Rectangle => "矩形",
            Models.AnnotationTool.Polygon => "多边形",
            Models.AnnotationTool.Circle => "圆形",
            Models.AnnotationTool.Line => "线条",
            Models.AnnotationTool.Point => "点",
            Models.AnnotationTool.OrientedBoundingBox => "有向边界框",
            Models.AnnotationTool.Select => "选择",
            _ => "未知"
        };
    }
    
    /// <summary>
    /// 重置工具切换状态
    /// </summary>
    public void Reset()
    {
        _currentModelType = null;
        _lastPredominantType = null;
    }
}