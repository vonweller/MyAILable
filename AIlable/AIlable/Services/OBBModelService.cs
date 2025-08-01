using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// OBB模型服务 - 支持有向边界框检测
/// </summary>
public class OBBModelService : IAIModelService
{
    private bool _isModelLoaded;
    private string _modelName = string.Empty;
    
    public bool IsModelLoaded => _isModelLoaded;
    public string ModelName => _modelName;
    public AIModelType ModelType => AIModelType.Custom; // 使用Custom类型表示OBB模型
    
    public async Task<bool> LoadModelAsync(string modelPath)
    {
        try
        {
            // 实现OBB模型加载逻辑
            // 可以集成YOLOv5-OBB、YOLOv8-OBB等模型
            await Task.Delay(100);
            
            _modelName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
            _isModelLoaded = true;
            
            Console.WriteLine($"OBB model loaded: {_modelName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load OBB model: {ex.Message}");
            return false;
        }
    }
    
    public void UnloadModel()
    {
        _isModelLoaded = false;
        _modelName = string.Empty;
        Console.WriteLine("OBB model unloaded");
    }
    
    public async Task<IEnumerable<Annotation>> InferAsync(string imagePath, float confidenceThreshold = 0.5f)
    {
        if (!IsModelLoaded)
            return Array.Empty<Annotation>();
            
        try
        {
            // 模拟OBB推理 - 生成有向边界框标注
            await Task.Delay(300);
            
            var annotations = new List<Annotation>();
            
            // 生成示例OBB标注
            var obbAnnotation = new OrientedBoundingBoxAnnotation(200, 200, 100, 60, 30) // 中心点(200,200), 宽100, 高60, 角度30度
            {
                Id = Guid.NewGuid().ToString(),
                Label = "rotated_object",
                Color = "#FF00FF",
                StrokeWidth = 2,
                IsVisible = true
            };
            
            annotations.Add(obbAnnotation);
            
            Console.WriteLine($"OBB inference completed: {annotations.Count} oriented boxes detected");
            return annotations;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OBB inference error: {ex.Message}");
            return Array.Empty<Annotation>();
        }
    }
    
    public async Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAsync(IEnumerable<string> imagePaths, float confidenceThreshold = 0.5f)
    {
        var results = new Dictionary<string, IEnumerable<Annotation>>();
        
        foreach (var imagePath in imagePaths)
        {
            var annotations = await InferAsync(imagePath, confidenceThreshold);
            results[imagePath] = annotations;
        }
        
        return results;
    }
}