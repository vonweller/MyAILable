using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// AI模型管理器，统一管理各种AI模型服务
/// </summary>
public class AIModelManager
{
    private readonly Dictionary<AIModelType, IAIModelService> _modelServices;
    private IAIModelService? _activeModel;
    
    public AIModelManager()
    {
        _modelServices = new Dictionary<AIModelType, IAIModelService>
        {
            { AIModelType.YOLO, new YoloModelService() }
            // 可以添加更多模型类型
        };
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
    /// 加载指定类型的AI模型
    /// </summary>
    public async Task<bool> LoadModelAsync(AIModelType modelType, string modelPath)
    {
        try
        {
            if (!_modelServices.TryGetValue(modelType, out var modelService))
            {
                Console.WriteLine($"Unsupported model type: {modelType}");
                return false;
            }
            
            // 卸载当前激活的模型
            if (_activeModel != null)
            {
                _activeModel.UnloadModel();
            }
            
            // 加载新模型
            var success = await modelService.LoadModelAsync(modelPath);
            if (success)
            {
                _activeModel = modelService;
                Console.WriteLine($"Successfully loaded {modelType} model: {modelService.ModelName}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading model: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 卸载当前模型
    /// </summary>
    public void UnloadCurrentModel()
    {
        if (_activeModel != null)
        {
            _activeModel.UnloadModel();
            _activeModel = null;
            Console.WriteLine("Model unloaded");
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
    /// 批量推理多张图像
    /// </summary>
    public async Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAsync(IEnumerable<string> imagePaths, float confidenceThreshold = 0.5f)
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
    /// 获取支持的模型类型列表
    /// </summary>
    public IEnumerable<AIModelType> GetSupportedModelTypes()
    {
        return _modelServices.Keys;
    }
    
    /// <summary>
    /// 获取模型信息
    /// </summary>
    public string GetModelInfo()
    {
        if (!HasActiveModel)
            return "No model loaded";
        
        return $"Model: {_activeModel!.ModelName} (Type: {_activeModel.ModelType})";
    }
}