using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

public interface IAIChatService
{
    Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default);
    Task<IAsyncEnumerable<string>> SendMessageStreamAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
    void Configure(AIProviderConfig config);
}

public class AIChatService : IAIChatService, IDisposable
{
    private readonly HttpClient _httpClient;
    private AIProviderConfig? _config;
    
    public bool IsConfigured => _config?.IsValid() == true;
    
    public AIChatService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }
    
    public void Configure(AIProviderConfig config)
    {
        _config = config;
        
        // 清除之前的headers
        _httpClient.DefaultRequestHeaders.Clear();
        
        Console.WriteLine($"[DEBUG] Configuring provider: {config.ProviderType}");
        Console.WriteLine($"[DEBUG] API URL: {config.ApiUrl}");
        Console.WriteLine($"[DEBUG] Model: {config.Model}");
        Console.WriteLine($"[DEBUG] Has API Key: {!string.IsNullOrEmpty(config.ApiKey)}");
        
        // 基础认证headers
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            Console.WriteLine($"[DEBUG] Added auth header for {config.ProviderType}");
        }
        
        // 添加User-Agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AIlable-Chat/1.0");
    }
    
    public async Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("AI Chat service is not configured");
            
        try
        {
            var requestBody = BuildRequestBody(message, history, false);
            var jsonContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var endpoint = GetApiEndpoint();
            Console.WriteLine($"[DEBUG] Sending request to: {endpoint}");
            Console.WriteLine($"[DEBUG] Provider: {_config!.ProviderType}");
            Console.WriteLine($"[DEBUG] Request body: {requestBody}");
            
            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
            
            Console.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Error response: {errorContent}");
                throw new HttpRequestException($"API request failed: {response.StatusCode}, {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Response content: {responseContent}");
            return ParseResponse(responseContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] AI Chat error: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
    
    public async Task<IAsyncEnumerable<string>> SendMessageStreamAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("AI Chat service is not configured");
            
        await Task.CompletedTask; // 修复warning
        return SendMessageStreamAsyncCore(message, history, cancellationToken);
    }
    
    private async IAsyncEnumerable<string> SendMessageStreamAsyncCore(string message, List<ChatMessage> history, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestBody = BuildRequestBody(message, history, true);
        var jsonContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
        
        var endpoint = GetApiEndpoint();
        Console.WriteLine($"[DEBUG STREAM] Sending stream request to: {endpoint}");
        Console.WriteLine($"[DEBUG STREAM] Provider: {_config!.ProviderType}");
        Console.WriteLine($"[DEBUG STREAM] Request body: {requestBody}");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = jsonContent
        };
        
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Console.WriteLine($"[DEBUG STREAM] Response status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG STREAM] Error response: {errorContent}");
                throw new HttpRequestException($"API stream request failed: {response.StatusCode}, {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR STREAM] Stream error: {ex.Message}");
            Console.WriteLine($"[ERROR STREAM] Stack trace: {ex.StackTrace}");
            throw;
        }
        
        // 将流处理移到try-catch外部
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        
        string? line;
        var lineCount = 0;
        while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
        {
            lineCount++;
            Console.WriteLine($"[DEBUG STREAM] Line {lineCount}: {line}");
            
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (data == "[DONE]") 
                {
                    Console.WriteLine($"[DEBUG STREAM] Stream ended with [DONE]");
                    break;
                }
                
                var deltaContent = ParseStreamingResponse(data);
                if (!string.IsNullOrEmpty(deltaContent))
                {
                    Console.WriteLine($"[DEBUG STREAM] Yielding: '{deltaContent}'");
                    yield return deltaContent;
                }
            }
        }
    }
    
    private string BuildRequestBody(string message, List<ChatMessage> history, bool stream)
    {
        var messages = new List<object>();
        
        // 添加历史消息
        foreach (var historyMessage in history)
        {
            messages.Add(new
            {
                role = historyMessage.Role.ToString().ToLower(),
                content = historyMessage.Content
            });
        }
        
        // 添加当前消息
        messages.Add(new
        {
            role = "user",
            content = message
        });

        // 所有提供商都使用OpenAI兼容格式（阿里云现在支持兼容模式）
        var requestBody = new
        {
            model = _config!.Model,
            messages = messages,
            temperature = _config.Temperature,
            max_tokens = _config.MaxTokens,
            stream = stream
        };
        
        return JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
    
    private string GetApiEndpoint()
    {
        return _config!.ProviderType switch
        {
            AIProviderType.OpenAI => $"{_config.ApiUrl}/chat/completions",
            AIProviderType.DeepSeek => $"{_config.ApiUrl}/chat/completions",
            AIProviderType.AliCloud => $"{_config.ApiUrl}/chat/completions", // 使用OpenAI兼容模式
            AIProviderType.Ollama => $"{_config.ApiUrl}/api/chat",
            _ => $"{_config.ApiUrl}/chat/completions"
        };
    }
    
    private string ParseResponse(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            
            return _config!.ProviderType switch
            {
                AIProviderType.Ollama => 
                    doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "",
                _ => // OpenAI兼容格式 (包括阿里云兼容模式)
                    doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? ""
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse AI response: {ex.Message}");
            return "解析响应失败";
        }
    }
    
    private string ParseStreamingResponse(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            
            return _config!.ProviderType switch
            {
                AIProviderType.Ollama => 
                    doc.RootElement.TryGetProperty("message", out var message) 
                        ? message.GetProperty("content").GetString() ?? "" : "",
                _ => // OpenAI兼容格式 (包括阿里云兼容模式)
                    doc.RootElement.GetProperty("choices")[0].GetProperty("delta").TryGetProperty("content", out var content) 
                        ? content.GetString() ?? "" : ""
            };
        }
        catch
        {
            return "";
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}