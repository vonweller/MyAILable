using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIlable.Services;

/// <summary>
/// Ollama连接测试服务
/// </summary>
public class OllamaTestService
{
    private readonly HttpClient _httpClient;
    
    public OllamaTestService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // 增加到5分钟，适应大模型加载时间
    }
    
    /// <summary>
    /// 测试Ollama服务是否可用
    /// </summary>
    public async Task<(bool isAvailable, string message)> TestOllamaConnectionAsync(string baseUrl = "http://localhost:11434")
    {
        try
        {
            Console.WriteLine($"[OLLAMA TEST] Testing connection to: {baseUrl}");
            
            // 1. 测试基础连接
            var response = await _httpClient.GetAsync($"{baseUrl}/api/tags");
            
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Ollama服务不可用，HTTP状态码: {response.StatusCode}");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[OLLAMA TEST] Tags response: {content}");
            
            // 2. 解析可用模型
            var modelsInfo = ParseAvailableModels(content);
            
            if (string.IsNullOrEmpty(modelsInfo))
            {
                return (false, "Ollama服务可用，但没有安装任何模型。请先下载模型，例如：ollama pull llama3.2");
            }
            
            return (true, $"Ollama服务正常运行\n{modelsInfo}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[OLLAMA TEST] HTTP error: {ex.Message}");
            return (false, $"无法连接到Ollama服务: {ex.Message}\n请确保Ollama已启动并运行在 {baseUrl}");
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时，请检查Ollama服务是否正在运行");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OLLAMA TEST] Unexpected error: {ex.Message}");
            return (false, $"测试连接时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 测试特定模型是否可用
    /// </summary>
    public async Task<(bool isAvailable, string message)> TestModelAsync(string baseUrl, string modelName)
    {
        try
        {
            Console.WriteLine($"[OLLAMA TEST] Testing model: {modelName}");
            
            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                stream = false
            };
            
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{baseUrl}/api/chat", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OLLAMA TEST] Model test failed: {errorContent}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (false, $"模型 '{modelName}' 未找到。请先下载：ollama pull {modelName}");
                }
                
                return (false, $"模型测试失败: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[OLLAMA TEST] Model test success: {responseContent}");
            
            return (true, $"模型 '{modelName}' 工作正常");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OLLAMA TEST] Model test error: {ex.Message}");
            return (false, $"测试模型时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取Ollama版本信息
    /// </summary>
    public async Task<string> GetOllamaVersionAsync(string baseUrl = "http://localhost:11434")
    {
        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/api/version");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OLLAMA TEST] Version check error: {ex.Message}");
        }
        
        return "无法获取版本信息";
    }
    
    private string ParseAvailableModels(string tagsResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(tagsResponse);
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                var modelList = new StringBuilder();
                modelList.AppendLine("可用模型:");
                
                foreach (var model in models.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name))
                    {
                        var modelName = name.GetString();
                        var size = "";
                        
                        if (model.TryGetProperty("size", out var sizeProperty))
                        {
                            var sizeBytes = sizeProperty.GetInt64();
                            size = $" ({FormatBytes(sizeBytes)})";
                        }
                        
                        modelList.AppendLine($"  • {modelName}{size}");
                    }
                }
                
                return modelList.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OLLAMA TEST] Parse models error: {ex.Message}");
        }
        
        return "";
    }
    
    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1}{suffixes[counter]}";
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}