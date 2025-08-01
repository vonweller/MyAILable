using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    [ObservableProperty] private ObservableCollection<string> _availableModels = new();
    [ObservableProperty] private string _aiVoice = "Cherry";
    [ObservableProperty] private ObservableCollection<string> _availableVoices = new();

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
                AvailableModels.Clear();
                AvailableModels.Add("gpt-3.5-turbo");
                AvailableModels.Add("gpt-4");
                AvailableModels.Add("gpt-4-turbo");
                AvailableModels.Add("gpt-4o");
                AvailableModels.Add("gpt-4o-mini");
                break;
            case AIProviderType.Ollama:
                ApiUrl = "http://localhost:11434";
                Model = "llama2";
                AvailableModels.Clear();
                AvailableModels.Add("llama2");
                AvailableModels.Add("llama3");
                AvailableModels.Add("llama3.1");
                AvailableModels.Add("qwen2");
                AvailableModels.Add("gemma");
                AvailableModels.Add("mistral");
                AvailableModels.Add("codellama");
                break;
            case AIProviderType.AliCloud:
                ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                Model = "qwen-turbo";
                AvailableModels.Clear();
                AvailableModels.Add("qwen-turbo");
                AvailableModels.Add("qwen-plus");
                AvailableModels.Add("qwen-max");
                AvailableModels.Add("qwen-omni-turbo");
                AvailableModels.Add("qwen-vl-max-latest");
                AvailableModels.Add("qvq-max");
                AvailableModels.Add("qwen2-7b-instruct");
                AvailableModels.Add("qwen2-72b-instruct");
                // 设置语音选项
                AvailableVoices.Clear();
                AvailableVoices.Add("Cherry");
                AvailableVoices.Add("Serena");
                AvailableVoices.Add("Ethan");
                AvailableVoices.Add("Chelsie");
                AiVoice = "Cherry";
                break;
            case AIProviderType.DeepSeek:
                ApiUrl = "https://api.deepseek.com/v1";
                Model = "deepseek-chat";
                AvailableModels.Clear();
                AvailableModels.Add("deepseek-chat");
                AvailableModels.Add("deepseek-coder");
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