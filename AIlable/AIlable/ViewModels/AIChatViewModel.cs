using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    
    private readonly IAIChatService _chatService;
    private readonly IFileDialogService? _fileDialogService;
    private CancellationTokenSource? _currentRequestCancellation;
    
    public AIChatViewModel() : this(new AIChatService(), null) { }
    
    public AIChatViewModel(IAIChatService chatService, IFileDialogService? fileDialogService)
    {
        _chatService = chatService;
        _fileDialogService = fileDialogService;
        
        InitializeProviders();
        InitializeCommands();
        
        // 如果有默认配置，自动选择
        if (AvailableProviders.Count > 0)
        {
            SelectedProvider = AvailableProviders[0];
        }
    }
    
    public ICommand SendMessageCommand { get; private set; } = null!;
    public ICommand AttachImageCommand { get; private set; } = null!;
    public ICommand AttachFileCommand { get; private set; } = null!;
    public ICommand ClearChatCommand { get; private set; } = null!;
    public ICommand CancelRequestCommand { get; private set; } = null!;
    public ICommand SaveChatCommand { get; private set; } = null!;
    public ICommand ConfigureProviderCommand { get; private set; } = null!;
    public ICommand ToggleConfigPanelCommand { get; private set; } = null!;
    
    private void InitializeCommands()
    {
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(InputText));
        AttachImageCommand = new AsyncRelayCommand(AttachImageAsync);
        AttachFileCommand = new AsyncRelayCommand(AttachFileAsync);
        ClearChatCommand = new RelayCommand(ClearChat);
        CancelRequestCommand = new RelayCommand(CancelCurrentRequest, () => IsLoading);
        SaveChatCommand = new AsyncRelayCommand(SaveChatAsync);
        ConfigureProviderCommand = new RelayCommand(ConfigureProvider);
        ToggleConfigPanelCommand = new RelayCommand(() => IsConfigPanelVisible = !IsConfigPanelVisible);
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
            
            var streamResponse = await _chatService.SendMessageStreamAsync(messageToSend, history, _currentRequestCancellation.Token);
            
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
    
    private void ConfigureProvider()
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
}