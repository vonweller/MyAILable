using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// AI模型服务接口，支持各种目标检测和分割模型
/// </summary>
public interface IAIModelService
{
    /// <summary>
    /// 模型是否已加载
    /// </summary>
    bool IsModelLoaded { get; }
    
    /// <summary>
    /// 模型名称
    /// </summary>
    string ModelName { get; }
    
    /// <summary>
    /// 支持的模型类型
    /// </summary>
    AIModelType ModelType { get; }
    
    /// <summary>
    /// 加载AI模型
    /// </summary>
    Task<bool> LoadModelAsync(string modelPath);
    
    /// <summary>
    /// 卸载模型
    /// </summary>
    void UnloadModel();
    
    /// <summary>
    /// 对图像进行推理，返回检测到的标注
    /// </summary>
    Task<IEnumerable<Annotation>> InferAsync(string imagePath, float confidenceThreshold = 0.5f);
    
    /// <summary>
    /// 批量推理多张图像
    /// </summary>
    Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAsync(IEnumerable<string> imagePaths, float confidenceThreshold = 0.5f);
}

/// <summary>
/// AI模型类型枚举
/// </summary>
public enum AIModelType
{
    /// <summary>
    /// YOLO目标检测模型
    /// </summary>
    YOLO,
    
    /// <summary>
    /// Segment Anything分割模型
    /// </summary>
    SegmentAnything,
    
    /// <summary>
    /// 其他自定义模型
    /// </summary>
    Custom
}

/// <summary>
/// AI推理结果
/// </summary>
public class AIInferenceResult
{
    public string ImagePath { get; set; } = string.Empty;
    public IEnumerable<Annotation> Annotations { get; set; } = new List<Annotation>();
    public float InferenceTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}