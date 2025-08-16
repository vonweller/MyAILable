using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 多线程AI模型管理器，支持并发推理和进度监控
/// </summary>
public class MultiThreadAIModelManager : IDisposable
{
    private readonly Dictionary<AIModelType, IAIModelService> _modelServices;
    private IAIModelService? _activeModel;
    private List<string> _projectLabels = new();
    private readonly SemaphoreSlim _modelSwitchSemaphore = new(1, 1);
    
    public MultiThreadAIModelManager()
    {
        _modelServices = new Dictionary<AIModelType, IAIModelService>();
    }
    
    /// <summary>
    /// 当前激活的模型
    /// </summary>
    public IAIModelService? ActiveModel => _activeModel;
    
    /// <summary>
    /// 当前是否有加载的模型
    /// </summary>
    public bool HasActiveModel => _activeModel?.IsModelLoaded == true;
    
    /// <summary>
    /// 设置项目标签
    /// </summary>
    public void SetProjectLabels(List<string> projectLabels)
    {
        _projectLabels = projectLabels ?? new List<string>();
        
        // 更新所有YOLO模型服务的标签
        foreach (var service in _modelServices.Values)
        {
            if (service is YoloModelService yoloService)
            {
                yoloService.UpdateProjectLabels(_projectLabels);
            }
        }
        
        Console.WriteLine($"已更新项目标签: {string.Join(", ", _projectLabels)}");
    }
    
    /// <summary>
    /// 加载指定类型的AI模型
    /// </summary>
    public async Task<bool> LoadModelAsync(AIModelType modelType, string modelPath, int maxConcurrency = 0)
    {
        await _modelSwitchSemaphore.WaitAsync();
        
        try
        {
            // 卸载当前激活的模型
            if (_activeModel != null)
            {
                _activeModel.UnloadModel();
                if (_activeModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            // 创建新的模型服务实例
            IAIModelService? modelService = modelType switch
            {
                AIModelType.YOLO => new YoloModelService(_projectLabels),
                AIModelType.SegmentAnything => new SegmentationModelService(),
                AIModelType.Custom => new OBBModelService(),
                _ => null
            };
            
            if (modelService == null)
            {
                Console.WriteLine($"Unsupported model type: {modelType}");
                return false;
            }
            
            // 加载新模型
            var success = await modelService.LoadModelAsync(modelPath);
            if (success)
            {
                _activeModel = modelService;
                _modelServices[modelType] = modelService;
                Console.WriteLine($"Successfully loaded {modelType} model: {modelService.ModelName}");
            }
            else
            {
                if (modelService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading model: {ex.Message}");
            return false;
        }
        finally
        {
            _modelSwitchSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 卸载当前模型
    /// </summary>
    public async Task UnloadCurrentModelAsync()
    {
        await _modelSwitchSemaphore.WaitAsync();
        
        try
        {
            if (_activeModel != null)
            {
                _activeModel.UnloadModel();
                if (_activeModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _activeModel = null;
                Console.WriteLine("Model unloaded");
            }
        }
        finally
        {
            _modelSwitchSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 对单张图像进行AI推理
    /// </summary>
    public async Task<IEnumerable<Annotation>> InferImageAsync(string imagePath, float confidenceThreshold = 0.5f)
    {
        if (!HasActiveModel)
        {
            Console.WriteLine("No active model loaded");
            return Array.Empty<Annotation>();
        }
        
        try
        {
            return await _activeModel!.InferAsync(imagePath, confidenceThreshold);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during inference: {ex.Message}");
            return Array.Empty<Annotation>();
        }
    }
    
    /// <summary>
    /// 批量推理多张图像（基础版本）
    /// </summary>
    public async Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAsync(
        IEnumerable<string> imagePaths, 
        float confidenceThreshold = 0.5f)
    {
        if (!HasActiveModel)
        {
            Console.WriteLine("No active model loaded");
            return new Dictionary<string, IEnumerable<Annotation>>();
        }
        
        try
        {
            return await _activeModel!.InferBatchAsync(imagePaths, confidenceThreshold);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during batch inference: {ex.Message}");
            return new Dictionary<string, IEnumerable<Annotation>>();
        }
    }
    
    /// <summary>
    /// 高级批量推理，支持进度报告和取消
    /// </summary>
    public async Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAdvancedAsync(
        IEnumerable<string> imagePaths,
        float confidenceThreshold = 0.5f,
        IProgress<InferenceProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!HasActiveModel)
        {
            Console.WriteLine("No active model loaded");
            return new Dictionary<string, IEnumerable<Annotation>>();
        }
        
        try
        {
            // 如果当前模型支持高级批量推理，使用它
            // 对于YOLO模型，使用基础批量推理
            if (_activeModel is YoloModelService yoloService)
            {
                return await yoloService.InferBatchAsync(imagePaths, confidenceThreshold);
            }
            
            // 否则回退到基础批量推理
            return await _activeModel.InferBatchAsync(imagePaths, confidenceThreshold);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during advanced batch inference: {ex.Message}");
            return new Dictionary<string, IEnumerable<Annotation>>();
        }
    }
    
    /// <summary>
    /// 获取支持的模型类型列表
    /// </summary>
    public IEnumerable<AIModelType> GetSupportedModelTypes()
    {
        return new[] { AIModelType.YOLO, AIModelType.SegmentAnything, AIModelType.Custom };
    }
    
    /// <summary>
    /// 获取模型信息
    /// </summary>
    public string GetModelInfo()
    {
        if (!HasActiveModel)
            return "No model loaded";
        
        var info = $"Model: {_activeModel!.ModelName} (Type: {_activeModel.ModelType})";
        
        if (_activeModel is YoloModelService yoloService)
        {
            info += $" [YOLO Model]";
        }
        
        return info;
    }
    
    /// <summary>
    /// 检查当前模型是否支持多线程推理
    /// </summary>
    public bool IsMultiThreadSupported()
    {
        return _activeModel is YoloModelService;
    }
    
    /// <summary>
    /// 获取当前模型的推理统计信息
    /// </summary>
    public string GetInferenceStats()
    {
        if (!HasActiveModel)
            return "No active model";
        
        // 这里可以扩展以获取更详细的统计信息
        return $"Model: {_activeModel.ModelName}, Type: {_activeModel.ModelType}";
    }
    
    public void Dispose()
    {
        foreach (var service in _modelServices.Values)
        {
            service?.UnloadModel();
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        _modelServices.Clear();
        _activeModel = null;
        _modelSwitchSemaphore?.Dispose();
    }
}