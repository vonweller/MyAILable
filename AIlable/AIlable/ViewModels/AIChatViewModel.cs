using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIlable.Models;
using AIlable.Services;

namespace AIlable.ViewModels;

public partial class AIChatViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConfigured;
    [ObservableProperty] private string _statusText = "请先配置AI提供商";
    
    // AI提供商配置
    [ObservableProperty] private ObservableCollection<AIProviderConfig> _availableProviders = new();
    [ObservableProperty] private AIProviderConfig? _selectedProvider;
    [ObservableProperty] private bool _isConfigPanelVisible = true;
    
    // 录音相关属性
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _recordingTime = "00:00";
    [ObservableProperty] private bool _isRecordingSupported;
    
    private readonly IAIChatService _chatService;
    private readonly IFileDialogService? _fileDialogService;
    private readonly IVoiceRecordingService? _voiceRecordingService;
    private readonly IAudioPlayerService? _audioPlayerService;
    private readonly IConfigurationService _configurationService;
    private CancellationTokenSource? _currentRequestCancellation;
    
    public AIChatViewModel() : this(new AIChatService(), null, null, null, new Services.ConfigurationService()) { }
    
    public AIChatViewModel(IAIChatService chatService, IFileDialogService? fileDialogService, IVoiceRecordingService? voiceRecordingService = null, IAudioPlayerService? audioPlayerService = null, IConfigurationService? configurationService = null)
    {
        _chatService = chatService;
        _fileDialogService = fileDialogService;
        _voiceRecordingService = voiceRecordingService ?? new VoiceRecordingService();
        _audioPlayerService = audioPlayerService ?? new Services.AudioPlayerService();
        _configurationService = configurationService ?? new Services.ConfigurationService();
        
        InitializeAsync();
    }
    
    private async void InitializeAsync()
    {
        InitializeCommands();
        InitializeVoiceRecording();
        
        // 异步加载保存的配置
        await LoadSavedProvidersAsync();
    }
    
    private async Task LoadSavedProvidersAsync()
    {
        try
        {
            Console.WriteLine("[DEBUG VM] Loading saved providers...");
            
            // 首先检查配置文件是否被严重污染
            var savedProviders = await _configurationService.LoadProvidersAsync();
            bool configurationCorrupted = false;
            
            if (savedProviders.Count > 0)
            {
                foreach (var provider in savedProviders)
                {
                    if (!string.IsNullOrEmpty(provider.ApiKey) && provider.ApiKey.Length > 50)
                    {
                        Console.WriteLine($"[WARNING VM] Detected corrupted API Key in {provider.DisplayName}: length {provider.ApiKey.Length}");
                        configurationCorrupted = true;
                        break;
                    }
                }
            }
            
            if (configurationCorrupted)
            {
                Console.WriteLine("[WARNING VM] Configuration files are corrupted, resetting...");
                await _configurationService.ResetConfigurationFilesAsync();
                
                // 重新初始化默认配置
                InitializeProviders();
                if (AvailableProviders.Count > 0)
                {
                    SelectedProvider = AvailableProviders[0];
                }
                return;
            }
            
            // 如果配置没有被污染，正常清理
            await _configurationService.CleanConfigurationFilesAsync();
            
            // 重新加载保存的配置
            savedProviders = await _configurationService.LoadProvidersAsync();
            
            // 初始化默认配置
            InitializeProviders();
            
            // 合并保存的配置（更新API Key等信息）
            if (savedProviders.Count > 0)
            {
                foreach (var savedProvider in savedProviders)
                {
                    var existingProvider = AvailableProviders.FirstOrDefault(p => 
                        p.ProviderType == savedProvider.ProviderType && 
                        p.DisplayName == savedProvider.DisplayName);
                    
                    if (existingProvider != null)
                    {
                        // 更新保存的API Key和其他配置，并清理非ASCII字符
                        existingProvider.ApiKey = CleanApiKey(savedProvider.ApiKey);
                        existingProvider.ApiUrl = savedProvider.ApiUrl;
                        existingProvider.Model = savedProvider.Model;
                        existingProvider.Temperature = savedProvider.Temperature;
                        existingProvider.MaxTokens = savedProvider.MaxTokens;
                        
                        Console.WriteLine($"[DEBUG VM] Updated provider {existingProvider.DisplayName} with saved config");
                        Console.WriteLine($"[DEBUG VM] Cleaned API Key length: {existingProvider.ApiKey?.Length ?? 0}");
                    }
                }
            }
            
            // 尝试恢复上次使用的配置
            var lastUsedProvider = await _configurationService.GetLastUsedProviderAsync();
            if (lastUsedProvider != null)
            {
                // 清理API Key
                lastUsedProvider.ApiKey = CleanApiKey(lastUsedProvider.ApiKey);
                
                var matchingProvider = AvailableProviders.FirstOrDefault(p => 
                    p.ProviderType == lastUsedProvider.ProviderType && 
                    p.DisplayName == lastUsedProvider.DisplayName);
                
                if (matchingProvider != null)
                {
                    // 更新配置信息
                    matchingProvider.ApiKey = lastUsedProvider.ApiKey;
                    matchingProvider.ApiUrl = lastUsedProvider.ApiUrl;
                    matchingProvider.Model = lastUsedProvider.Model;
                    matchingProvider.Temperature = lastUsedProvider.Temperature;
                    matchingProvider.MaxTokens = lastUsedProvider.MaxTokens;
                    
                    SelectedProvider = matchingProvider;
                    Console.WriteLine($"[DEBUG VM] Restored last used provider: {matchingProvider.DisplayName}");
                }
            }
            
            // 如果没有选择任何配置，选择第一个
            if (SelectedProvider == null && AvailableProviders.Count > 0)
            {
                SelectedProvider = AvailableProviders[0];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR VM] Failed to load saved providers: {ex.Message}");
            
            // 出错时使用默认配置
            InitializeProviders();
            if (AvailableProviders.Count > 0)
            {
                SelectedProvider = AvailableProviders[0];
            }
        }
    }
    
    /// <summary>
    /// 清理API Key中的非ASCII字符和多余空格
    /// </summary>
    private static string CleanApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return string.Empty;
            
        // 移除前后空格和控制字符
        var cleaned = apiKey.Trim();
        
        // 移除非ASCII字符（只保留ASCII字符）
        var result = new StringBuilder();
        foreach (char c in cleaned)
        {
            if (c <= 127 && c >= 32) // 保留可打印的ASCII字符
            {
                result.Append(c);
            }
            else
            {
                Console.WriteLine($"[DEBUG VM] Removed non-ASCII character: '{c}' (code: {(int)c})");
            }
        }
        
        var finalResult = result.ToString();
        if (finalResult != cleaned)
        {
            Console.WriteLine($"[DEBUG VM] API Key cleaned: '{cleaned}' -> '{finalResult}'");
        }
        
        return finalResult;
    }
    
    public ICommand SendMessageCommand { get; private set; } = null!;
    public ICommand AttachImageCommand { get; private set; } = null!;
    public ICommand AttachFileCommand { get; private set; } = null!;
    public ICommand AttachAudioCommand { get; private set; } = null!;    // 新增音频附件
    public ICommand AttachVideoCommand { get; private set; } = null!;    // 新增视频附件
    public ICommand StartVoiceRecordingCommand { get; private set; } = null!;  // 开始录音
    public ICommand StopVoiceRecordingCommand { get; private set; } = null!;   // 停止录音
    public ICommand ClearChatCommand { get; private set; } = null!;
    public ICommand CancelRequestCommand { get; private set; } = null!;
    public ICommand SaveChatCommand { get; private set; } = null!;
    public ICommand ConfigureProviderCommand { get; private set; } = null!;
    public ICommand ToggleConfigPanelCommand { get; private set; } = null!;
    public ICommand PlayAudioCommand { get; private set; } = null!;  // 播放音频命令
    
    private void InitializeCommands()
    {
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(InputText));
        AttachImageCommand = new AsyncRelayCommand(AttachImageAsync);
        AttachFileCommand = new AsyncRelayCommand(AttachFileAsync);
        AttachAudioCommand = new AsyncRelayCommand(AttachAudioAsync);    // 新增音频命令
        AttachVideoCommand = new AsyncRelayCommand(AttachVideoAsync);    // 新增视频命令
        StartVoiceRecordingCommand = new AsyncRelayCommand(StartVoiceRecordingAsync, () => !IsRecording && IsRecordingSupported);
        StopVoiceRecordingCommand = new AsyncRelayCommand(StopVoiceRecordingAsync, () => IsRecording);
        ClearChatCommand = new RelayCommand(ClearChat);
        CancelRequestCommand = new RelayCommand(CancelCurrentRequest, () => IsLoading);
        SaveChatCommand = new AsyncRelayCommand(SaveChatAsync);
        ConfigureProviderCommand = new AsyncRelayCommand(ConfigureProviderAsync);
        ToggleConfigPanelCommand = new RelayCommand(() => IsConfigPanelVisible = !IsConfigPanelVisible);
        PlayAudioCommand = new AsyncRelayCommand<ChatMessage>(PlayAudioAsync);
    }
    
    private void InitializeProviders()
    {
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.Ollama, "Ollama (本地)")
        {
            ApiUrl = "http://localhost:11434",
            Model = "llama2"
        });
        
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.OpenAI, "OpenAI")
        {
            ApiUrl = "https://api.openai.com/v1",
            Model = "gpt-3.5-turbo"
        });
        
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.DeepSeek, "DeepSeek")
        {
            ApiUrl = "https://api.deepseek.com/v1",
            Model = "deepseek-chat"
        });
        
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.AliCloud, "阿里云通义千问")
        {
            ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Model = "qwen-turbo"  // 更换为更通用的模型
        });
        
        // 添加全模态模型配置
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.AliCloud, "阿里云全模态模型")
        {
            ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Model = "qwen-omni-turbo",  // 全模态模型
            MaxTokens = 2048  // qwen-omni-turbo 的最大限制
        });
        
        // 添加视觉理解模型
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.AliCloud, "阿里云视觉理解模型")
        {
            ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Model = "qwen-vl-max-latest",
            MaxTokens = 2048
        });
        
        // 添加推理模型
        AvailableProviders.Add(new AIProviderConfig(AIProviderType.AliCloud, "阿里云推理模型")
        {
            ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", 
            Model = "qvq-max",
            MaxTokens = 2048
        });
    }
    
    partial void OnSelectedProviderChanged(AIProviderConfig? value)
    {
        if (value != null)
        {
            Console.WriteLine($"[DEBUG VM] Provider changed to: {value.DisplayName}");
            Console.WriteLine($"[DEBUG VM] Provider type: {value.ProviderType}");
            Console.WriteLine($"[DEBUG VM] API URL: {value.ApiUrl}");
            Console.WriteLine($"[DEBUG VM] Model: {value.Model}");
            Console.WriteLine($"[DEBUG VM] Has API Key: {!string.IsNullOrEmpty(value.ApiKey)}");
            
            _chatService.Configure(value);
            IsConfigured = value.IsValid();
            
            if (IsConfigured)
            {
                StatusText = $"✅ 已连接到 {value.DisplayName} ({value.Model})";
                Console.WriteLine($"[DEBUG VM] Configuration valid, status: {StatusText}");
                
                // 保存上次使用的配置
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _configurationService.SaveLastUsedProviderAsync(value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR VM] Failed to save last used provider: {ex.Message}");
                    }
                });
            }
            else
            {
                StatusText = $"❌ 配置无效，请检查设置 (需要API Key: {value.ProviderType != AIProviderType.Ollama})";
                Console.WriteLine($"[DEBUG VM] Configuration invalid, status: {StatusText}");
            }
        }
        else
        {
            IsConfigured = false;
            StatusText = "请选择AI提供商";
            Console.WriteLine($"[DEBUG VM] No provider selected");
        }
        
        ((AsyncRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
    }
    
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || !IsConfigured || SelectedProvider == null)
        {
            Console.WriteLine($"[DEBUG VM] Cannot send message - InputText empty: {string.IsNullOrWhiteSpace(InputText)}, IsConfigured: {IsConfigured}, HasProvider: {SelectedProvider != null}");
            return;
        }
        
        Console.WriteLine($"[DEBUG VM] Starting to send message: '{InputText.Trim()}'");
        StatusText = "🚀 正在发送消息...";
            
        var userMessage = ChatMessage.CreateUserTextMessage(InputText.Trim());
        Messages.Add(userMessage);
        
        var assistantMessage = ChatMessage.CreateAssistantMessage("");
        assistantMessage.IsStreaming = true;
        Messages.Add(assistantMessage);
        
        var messageToSend = InputText.Trim();
        InputText = string.Empty;
        
        IsLoading = true;
        _currentRequestCancellation = new CancellationTokenSource();
        
        Console.WriteLine($"[DEBUG VM] Starting API call to {SelectedProvider.DisplayName}");
        
        try
        {
            StatusText = "📡 等待AI响应...";
            
            var history = Messages.Where(m => !m.IsStreaming && m != userMessage).ToList();
            Console.WriteLine($"[DEBUG VM] History count: {history.Count}");
            
            var streamResponse = await _chatService.SendMessageStreamAsync(messageToSend, history, assistantMessage, _currentRequestCancellation.Token);
            
            Console.WriteLine($"[DEBUG VM] Got stream response, starting to process chunks");
            StatusText = "💬 接收AI回复中...";
            
            var chunkCount = 0;
            await foreach (var chunk in streamResponse)
            {
                if (_currentRequestCancellation.Token.IsCancellationRequested)
                {
                    Console.WriteLine($"[DEBUG VM] Request was cancelled after {chunkCount} chunks");
                    break;
                }
                    
                chunkCount++;
                assistantMessage.Content += chunk;
                
                if (chunkCount % 10 == 0) // 每10个chunk记录一次
                {
                    Console.WriteLine($"[DEBUG VM] Processed {chunkCount} chunks, current length: {assistantMessage.Content.Length}");
                }
            }
            
            Console.WriteLine($"[DEBUG VM] Stream completed with {chunkCount} total chunks");
            StatusText = $"✅ 已连接到 {SelectedProvider.DisplayName} ({SelectedProvider.Model})";
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[DEBUG VM] Request was cancelled by user");
            assistantMessage.Content = "❌ [请求已取消]";
            StatusText = "⏹️ 请求已取消";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR VM] Send message failed: {ex.Message}");
            Console.WriteLine($"[ERROR VM] Exception type: {ex.GetType().Name}");
            assistantMessage.Content = $"❌ 错误: {ex.Message}";
            StatusText = $"❌ 请求失败: {ex.Message}";
        }
        finally
        {
            assistantMessage.IsStreaming = false;
            IsLoading = false;
            _currentRequestCancellation?.Dispose();
            _currentRequestCancellation = null;
            
            ((RelayCommand)CancelRequestCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
            
            Console.WriteLine($"[DEBUG VM] Send message completed, final message length: {assistantMessage.Content.Length}");
        }
    }
    
    private async Task AttachImageAsync()
    {
        if (_fileDialogService == null) return;
        
        try
        {
            var imagePath = await _fileDialogService.ShowOpenFileDialogAsync(
                "选择图片", 
                new[] { 
                    new FilePickerFileType("图片文件") 
                    { 
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp" } 
                    }
                });
                
            if (!string.IsNullOrEmpty(imagePath))
            {
                var message = ChatMessage.CreateUserImageMessage($"[图片: {Path.GetFileName(imagePath)}]", imagePath);
                Messages.Add(message);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加图片失败: {ex.Message}";
        }
    }
    
    private async Task AttachAudioAsync()
    {
        if (_fileDialogService == null) return;
        
        try
        {
            var audioPath = await _fileDialogService.ShowOpenFileDialogAsync(
                "选择音频文件", 
                new[] { 
                    new FilePickerFileType("音频文件") 
                    { 
                        Patterns = new[] { "*.wav", "*.mp3", "*.flac", "*.ogg", "*.m4a", "*.aac" } 
                    }
                });
                
            if (!string.IsNullOrEmpty(audioPath))
            {
                var message = ChatMessage.CreateUserAudioMessage($"[音频: {Path.GetFileName(audioPath)}]", audioPath);
                Messages.Add(message);
                Console.WriteLine($"[DEBUG] Added audio message: {audioPath}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加音频失败: {ex.Message}";
        }
    }
    
    private async Task AttachVideoAsync()
    {
        if (_fileDialogService == null) return;
        
        try
        {
            var framePaths = await _fileDialogService.ShowOpenMultipleFilesDialogAsync(
                "选择视频帧图片（按顺序选择多张图片组成视频）", 
                new[] { 
                    new FilePickerFileType("图片文件") 
                    { 
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp" } 
                    }
                });
                
            if (framePaths.Any())
            {
                var frameList = framePaths.ToList();
                var message = ChatMessage.CreateUserVideoMessage($"[视频: {frameList.Count}帧]", frameList);
                Messages.Add(message);
                Console.WriteLine($"[DEBUG] Added video message with {frameList.Count} frames");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加视频失败: {ex.Message}";
        }
    }
    
    private async Task AttachFileAsync()
    {
        if (_fileDialogService == null) return;
        
        try
        {
            var filePath = await _fileDialogService.ShowOpenFileDialogAsync(
                "选择文件", 
                new[] { 
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
                });
                
            if (!string.IsNullOrEmpty(filePath))
            {
                var fileData = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                
                var message = ChatMessage.CreateUserFileMessage($"[文件: {fileName}]", fileName, fileData);
                Messages.Add(message);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加文件失败: {ex.Message}";
        }
    }
    
    private void ClearChat()
    {
        Messages.Clear();
        StatusText = IsConfigured ? $"已连接到 {SelectedProvider?.DisplayName}" : "请配置AI提供商";
    }
    
    private void CancelCurrentRequest()
    {
        _currentRequestCancellation?.Cancel();
    }
    
    private async Task SaveChatAsync()
    {
        if (_fileDialogService == null || Messages.Count == 0) return;
        
        try
        {
            var filePath = await _fileDialogService.ShowSaveFileDialogAsync(
                "保存聊天记录",
                "chat_history.txt",
                new[] { 
                    new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } }
                });
                
            if (!string.IsNullOrEmpty(filePath))
            {
                var chatContent = string.Join("\n\n", Messages.Select(m => 
                    $"[{m.Timestamp:HH:mm:ss}] {(m.IsUser ? "用户" : "AI")}: {m.Content}"));
                    
                await File.WriteAllTextAsync(filePath, chatContent);
                StatusText = "聊天记录已保存";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败: {ex.Message}";
        }
    }
    
    private async Task ConfigureProviderAsync()
    {
        if (SelectedProvider == null)
        {
            StatusText = "请先选择AI提供商";
            return;
        }
        
        Console.WriteLine($"[DEBUG VM] Manual configure triggered for {SelectedProvider.DisplayName}");
        
        // 重新触发配置
        _chatService.Configure(SelectedProvider);
        IsConfigured = SelectedProvider.IsValid();
        
        if (IsConfigured)
        {
            StatusText = $"✅ 已连接到 {SelectedProvider.DisplayName} ({SelectedProvider.Model})";
            Console.WriteLine($"[DEBUG VM] Manual configuration successful: {StatusText}");
            
            // 保存配置
            try
            {
                await _configurationService.SaveProvidersAsync(AvailableProviders.ToList());
                await _configurationService.SaveLastUsedProviderAsync(SelectedProvider);
                Console.WriteLine($"[DEBUG VM] Configuration saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR VM] Failed to save configuration: {ex.Message}");
            }
        }
        else
        {
            StatusText = $"❌ 配置无效，请检查设置 (需要API Key: {SelectedProvider.ProviderType != AIProviderType.Ollama})";
            Console.WriteLine($"[DEBUG VM] Manual configuration failed: {StatusText}");
        }
        
        ((AsyncRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
    }
    
    partial void OnInputTextChanged(string value)
    {
        ((AsyncRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
    }
    
    private void InitializeVoiceRecording()
    {
        if (_voiceRecordingService != null)
        {
            IsRecordingSupported = _voiceRecordingService.IsSupported;
            _voiceRecordingService.StateChanged += OnRecordingStateChanged;
            _voiceRecordingService.RecordingProgress += OnRecordingProgress;
            
            Console.WriteLine($"[DEBUG] Voice recording initialized, supported: {IsRecordingSupported}");
        }
        else
        {
            IsRecordingSupported = false;
            Console.WriteLine("[DEBUG] Voice recording service not available");
        }
    }
    
    private void OnRecordingStateChanged(object? sender, RecordingState state)
    {
        IsRecording = state == RecordingState.Recording;
        
        // 更新命令状态
        ((AsyncRelayCommand)StartVoiceRecordingCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)StopVoiceRecordingCommand).NotifyCanExecuteChanged();
        
        Console.WriteLine($"[DEBUG] Recording state changed: {state}, IsRecording: {IsRecording}");
    }
    
    private void OnRecordingProgress(object? sender, TimeSpan elapsed)
    {
        RecordingTime = elapsed.ToString(@"mm\:ss");
    }
    
    private async Task StartVoiceRecordingAsync()
    {
        if (_voiceRecordingService == null || IsRecording) return;
        
        try
        {
            Console.WriteLine("[DEBUG] Starting voice recording...");
            StatusText = "🎤 正在录音，再次点击停止...";
            
            var started = await _voiceRecordingService.StartRecordingAsync();
            if (!started)
            {
                StatusText = "❌ 录音启动失败";
                Console.WriteLine("[ERROR] Failed to start voice recording");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Start recording error: {ex.Message}");
            StatusText = $"录音失败: {ex.Message}";
        }
    }
    
    private async Task StopVoiceRecordingAsync()
    {
        if (_voiceRecordingService == null || !IsRecording) return;
        
        try
        {
            Console.WriteLine("[DEBUG] Stopping voice recording...");
            StatusText = "🎤 正在处理录音...";
            
            var audioData = await _voiceRecordingService.StopRecordingAsync();
            
            if (audioData != null && audioData.Length > 0)
            {
                // 自动创建音频消息并发送
                var audioMessage = ChatMessage.CreateUserAudioMessage("🎤 语音消息", audioData, "wav");
                Messages.Add(audioMessage);
                
                Console.WriteLine($"[DEBUG] Created voice message, audio size: {audioData.Length} bytes");
                StatusText = "✅ 语音消息已发送";
                
                // 自动发送给AI（如果配置了AI）
                if (IsConfigured && SelectedProvider != null)
                {
                    await SendVoiceMessageToAI(audioMessage);
                }
            }
            else
            {
                StatusText = "❌ 录音失败或录音时间太短";
                Console.WriteLine("[WARNING] No audio data received from recording");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Stop recording error: {ex.Message}");
            StatusText = $"录音处理失败: {ex.Message}";
        }
    }
    
    private async Task SendVoiceMessageToAI(ChatMessage voiceMessage)
    {
        try
        {
            Console.WriteLine("[DEBUG] Sending voice message to AI...");
            StatusText = "🚀 AI正在处理语音消息...";
            
            // 创建AI回复消息
            var assistantMessage = ChatMessage.CreateAssistantMessage("");
            assistantMessage.IsStreaming = true;
            Messages.Add(assistantMessage);
            
            IsLoading = true;
            _currentRequestCancellation = new CancellationTokenSource();
            
            // 获取聊天历史（排除当前语音消息和正在创建的AI消息）
            var history = Messages.Where(m => !m.IsStreaming && m != voiceMessage && m != assistantMessage).ToList();
            
            // 发送语音消息内容给AI
            var streamResponse = await _chatService.SendMessageStreamAsync("请解析这个语音消息的内容", history.Concat(new[] { voiceMessage }).ToList(), assistantMessage, _currentRequestCancellation.Token);
            
            StatusText = "💬 AI正在回复...";
            
            await foreach (var chunk in streamResponse)
            {
                if (_currentRequestCancellation.Token.IsCancellationRequested) break;
                assistantMessage.Content += chunk;
            }
            
            StatusText = IsConfigured ? $"✅ 已连接到 {SelectedProvider?.DisplayName}" : "请配置AI提供商";
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[DEBUG] Voice message AI processing was cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Voice message AI processing failed: {ex.Message}");
            StatusText = $"❌ AI处理失败: {ex.Message}";
        }
        finally
        {
            if (Messages.LastOrDefault()?.IsStreaming == true)
            {
                Messages.Last().IsStreaming = false;
            }
            IsLoading = false;
            _currentRequestCancellation?.Dispose();
            _currentRequestCancellation = null;
        }
    }
    
    private async Task PlayAudioAsync(ChatMessage? message)
    {
        if (message?.AiAudioData == null || _audioPlayerService == null)
        {
            StatusText = "❌ 没有可播放的音频数据";
            return;
        }

        try
        {
            StatusText = "🔊 正在播放AI语音...";
            Console.WriteLine($"[DEBUG] Playing AI audio, size: {message.AiAudioData.Length} bytes");
            
            var success = await _audioPlayerService.PlayAudioAsync(message.AiAudioData, "wav");
            
            if (success)
            {
                StatusText = "✅ 音频播放完成";
            }
            else
            {
                StatusText = "❌ 音频播放失败";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Audio playback failed: {ex.Message}");
            StatusText = $"❌ 播放失败: {ex.Message}";
        }
    }
}