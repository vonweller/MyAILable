using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public enum AIProviderType
{
    OpenAI,
    Ollama,
    AliCloud,
    DeepSeek,
    Custom
}

public partial class AIProviderConfig : ObservableObject
{
    [ObservableProperty] private AIProviderType _providerType;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _apiUrl = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private float _temperature = 0.7f;
    [ObservableProperty] private int _maxTokens = 4000;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private Dictionary<string, object> _customParameters = new();

    public AIProviderConfig() { }
    
    public AIProviderConfig(AIProviderType providerType, string displayName)
    {
        ProviderType = providerType;
        DisplayName = displayName;
        SetDefaultValues();
    }
    
    private void SetDefaultValues()
    {
        switch (ProviderType)
        {
            case AIProviderType.OpenAI:
                ApiUrl = "https://api.openai.com/v1";
                Model = "gpt-3.5-turbo";
                break;
            case AIProviderType.Ollama:
                ApiUrl = "http://localhost:11434";
                Model = "llama2";
                break;
            case AIProviderType.AliCloud:
                ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                Model = "qwen-turbo";
                break;
            case AIProviderType.DeepSeek:
                ApiUrl = "https://api.deepseek.com/v1";
                Model = "deepseek-chat";
                break;
        }
    }
    
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(ApiUrl) && 
               !string.IsNullOrEmpty(Model) &&
               (ProviderType == AIProviderType.Ollama || !string.IsNullOrEmpty(ApiKey));
    }
}