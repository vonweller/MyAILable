using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

public interface IAIChatService
{
    Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default);
    Task<IAsyncEnumerable<string>> SendMessageStreamAsync(string message, List<ChatMessage> history, ChatMessage currentMessage, CancellationToken cancellationToken = default);
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
        
        // 确保HTTP客户端使用UTF-8编码
        _httpClient.DefaultRequestHeaders.AcceptCharset.Clear();
        _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));
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
        
        try
        {
            // 基础认证headers - 确保API Key只包含ASCII字符
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                // 验证API Key是否只包含ASCII字符
                var apiKey = config.ApiKey.Trim();
                if (IsValidAsciiString(apiKey))
                {
                    var authHeaderValue = $"Bearer {apiKey}";
                    if (IsValidAsciiString(authHeaderValue))
                    {
                        _httpClient.DefaultRequestHeaders.Add("Authorization", authHeaderValue);
                        Console.WriteLine($"[DEBUG] Added auth header for {config.ProviderType}");
                    }
                    else
                    {
                        throw new InvalidOperationException("Authorization header contains non-ASCII characters");
                    }
                }
                else
                {
                    throw new InvalidOperationException("API Key contains non-ASCII characters");
                }
            }
            
            // 添加User-Agent - 使用纯ASCII字符
            var userAgent = "AIlable-Chat/1.0";
            if (IsValidAsciiString(userAgent))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                Console.WriteLine($"[DEBUG] Added User-Agent header: {userAgent}");
            }
            else
            {
                throw new InvalidOperationException("User-Agent contains non-ASCII characters");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to configure HTTP headers: {ex.Message}");
            throw new InvalidOperationException($"HTTP headers configuration failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 验证字符串是否只包含ASCII字符
    /// </summary>
    /// <summary>
    /// 检查文本是否为思考过程内容
    /// </summary>
    private static bool IsThinkingContent(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        
        // 检查是否包含思考标识符
        var thinkingKeywords = new[]
        {
            "<thinking>", "</thinking>",
            "我需要思考", "让我思考", "让我分析",
            "首先我需要", "我应该先", "我应该考虑",
            "这需要仔细", "让我仔细分析", "我要仔细考虑",
            "从问题出发", "根据问题", "针对这个问题",
            "第一步", "第二步", "第三步",
            "分步骤", "按步骤", "逐步",
            "我先来", "让我先", "首先要"
        };
        
        foreach (var keyword in thinkingKeywords)
        {
            if (text.Contains(keyword))
            {
                return true;
            }
        }
        
        // 检查是否为长文本且包含通常的思考模式
        if (text.Length > 50 && 
            !text.Contains("\n\n") && 
            !text.Contains("```"))
        {
            var reasoningPatterns = new[]
            {
                "首先", "然后", "所以", "因为", "考虑", "分析",
                "因此", "由于", "可以看出", "可以知道", "显然",
                "接下来", "综上所述", "总的来说", "总之"
            };
            
            var patternCount = reasoningPatterns.Count(pattern => text.Contains(pattern));
            if (patternCount >= 2)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool IsValidAsciiString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return true;
            
        foreach (char c in input)
        {
            if (c > 127)
            {
                Console.WriteLine($"[ERROR] Non-ASCII character found: '{c}' (code: {(int)c})");
                return false;
            }
        }
        return true;
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
    
    public async Task<IAsyncEnumerable<string>> SendMessageStreamAsync(string message, List<ChatMessage> history, ChatMessage currentMessage, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("AI Chat service is not configured");
            
        await Task.CompletedTask; // 修复warning
        return SendMessageStreamAsyncCore(message, history, currentMessage, cancellationToken);
    }
    
    private async IAsyncEnumerable<string> SendMessageStreamAsyncCore(string message, List<ChatMessage> history, ChatMessage? currentMessage, [EnumeratorCancellation] CancellationToken cancellationToken)
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
        
        // 将流处理移到try-catch外部，确保使用正确的UTF-8编码
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        
        string? line;
        var lineCount = 0;
        var audioDataBuilder = new StringBuilder(); // 用于收集音频数据
        
        while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
        {
            lineCount++;
            Console.WriteLine($"[DEBUG STREAM] Line {lineCount}: {line}");
            
            // 处理不同的流格式
            string dataToProcess = "";
            
            if (_config!.ProviderType == AIProviderType.Ollama)
            {
                // Ollama直接返回JSON，不使用SSE格式
                if (!string.IsNullOrWhiteSpace(line))
                {
                    dataToProcess = line;
                }
            }
            else
            {
                // OpenAI等使用SSE格式 "data: {...}"
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    if (data == "[DONE]") 
                    {
                        Console.WriteLine($"[DEBUG STREAM] Stream ended with [DONE]");
                        
                        // 如果收集到音频数据，在最后处理音频
                        if (audioDataBuilder.Length > 0)
                        {
                            Console.WriteLine($"[DEBUG STREAM] Processing collected audio data: {audioDataBuilder.Length} chars");
                            var audioProcessingResult = ProcessAudioData(audioDataBuilder.ToString(), currentMessage);
                            if (!string.IsNullOrEmpty(audioProcessingResult))
                            {
                                yield return audioProcessingResult;
                            }
                        }
                        break;
                    }
                    dataToProcess = data;
                }
            }
            
            // 处理数据
            if (!string.IsNullOrEmpty(dataToProcess))
            {
                var (textContent, audioData) = ParseStreamingResponse(dataToProcess);
                if (!string.IsNullOrEmpty(textContent))
                {
                    Console.WriteLine($"[DEBUG STREAM] Yielding text: '{textContent}'");
                    yield return textContent;
                }
                
                // 收集音频数据
                if (!string.IsNullOrEmpty(audioData))
                {
                    audioDataBuilder.Append(audioData);
                    Console.WriteLine($"[DEBUG STREAM] Collected audio data chunk, total length: {audioDataBuilder.Length}");
                }
                
                // 检查Ollama的done标志
                if (_config!.ProviderType == AIProviderType.Ollama)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(dataToProcess);
                        if (doc.RootElement.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
                        {
                            Console.WriteLine($"[DEBUG STREAM] Ollama stream completed (done=true)");
                            break;
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略JSON解析错误，继续处理
                    }
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
            if (_config!.ProviderType == AIProviderType.Ollama)
            {
                // Ollama格式：图片单独放在images字段
                var messageObj = new
                {
                    role = historyMessage.Role.ToString().ToLower(),
                    content = historyMessage.Content,
                    images = GetImagesForOllama(historyMessage)
                };
                messages.Add(messageObj);
            }
            else
            {
                // OpenAI格式：图片嵌入在content中
                var messageContent = BuildMessageContent(historyMessage);
                messages.Add(new
                {
                    role = historyMessage.Role.ToString().ToLower(),
                    content = messageContent
                });
            }
        }
        
        // 添加当前消息（只是文本）
        messages.Add(new
        {
            role = "user",
            content = message
        });

        // 根据不同的提供商构建不同的请求体
        if (_config!.ProviderType == AIProviderType.Ollama)
        {
            // Ollama特定的请求格式
            var ollamaRequestBody = new
            {
                model = _config.Model,
                messages = messages,
                stream = stream,
                options = new
                {
                    temperature = _config.Temperature,
                    num_predict = _config.MaxTokens
                }
            };
            
            return JsonSerializer.Serialize(ollamaRequestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
        else
        {
            // OpenAI兼容格式（包括其他提供商）
            var requestBody = new
            {
                model = _config.Model,
                messages = messages,
                temperature = _config.Temperature,
                max_tokens = IsOmniModel(_config.Model) ? Math.Min(_config.MaxTokens, 2048) : _config.MaxTokens,
                stream = stream,
                // 全模态相关参数
                modalities = IsOmniModel(_config.Model) ? new[] { "text", "audio" } : null,
                audio = IsOmniModel(_config.Model) ? new { voice = _config.AiVoice, format = "wav" } : null,
                stream_options = stream ? new { include_usage = true } : null
            };
            
            return JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    private string[]? GetImagesForOllama(ChatMessage message)
    {
        if (message.Type == MessageType.Image && !string.IsNullOrEmpty(message.ImageFilePath))
        {
            try
            {
                // 读取图片并转换为base64
                var imageBytes = File.ReadAllBytes(message.ImageFilePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                
                Console.WriteLine($"[DEBUG] Ollama image converted to base64, size: {imageBytes.Length} bytes");
                
                // Ollama格式：直接返回base64字符串数组
                return new[] { base64Image };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read image for Ollama: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    private object BuildMessageContent(ChatMessage message)
    {
        Console.WriteLine($"[DEBUG] Building message content for type: {message.Type}");
        
        switch (message.Type)
        {
            case MessageType.Text:
                return message.Content;
                
            case MessageType.Image:
                if (!string.IsNullOrEmpty(message.ImageFilePath))
                {
                    try
                    {
                        // 读取图片并转换为base64
                        var imageBytes = File.ReadAllBytes(message.ImageFilePath);
                        var base64Image = Convert.ToBase64String(imageBytes);
                        var mimeType = GetImageMimeType(message.ImageFilePath);
                        
                        Console.WriteLine($"[DEBUG] Image converted to base64, size: {imageBytes.Length} bytes, mime: {mimeType}");
                        
                        // 根据提供商使用不同的格式
                        if (_config!.ProviderType == AIProviderType.Ollama)
                        {
                            // Ollama格式：content是数组，包含文本和图片
                            return new object[]
                            {
                                new { 
                                    type = "text", 
                                    text = message.Content 
                                },
                                new { 
                                    type = "image", 
                                    image = base64Image 
                                }
                            };
                        }
                        else
                        {
                            // OpenAI Vision API格式
                            return new object[]
                            {
                                new { type = "text", text = message.Content },
                                new { 
                                    type = "image_url", 
                                    image_url = new { 
                                        url = $"data:{mimeType};base64,{base64Image}" 
                                    } 
                                }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to read image: {ex.Message}");
                        return $"{message.Content} [Image read failed: {ex.Message}]";
                    }
                }
                return message.Content;
            
            case MessageType.Audio:
                // 处理音频消息
                if (!string.IsNullOrEmpty(message.AudioFilePath) || message.AudioData != null)
                {
                    try
                    {
                        byte[] audioBytes;
                        if (!string.IsNullOrEmpty(message.AudioFilePath))
                        {
                            audioBytes = File.ReadAllBytes(message.AudioFilePath);
                        }
                        else
                        {
                            audioBytes = message.AudioData!;
                        }
                        
                        var base64Audio = Convert.ToBase64String(audioBytes);
                        Console.WriteLine($"[DEBUG] Audio converted to base64, size: {audioBytes.Length} bytes, format: {message.AudioFormat}");
                        
                        // 根据是否是全模态模型使用不同的格式
                        if (IsOmniModel(_config!.Model))
                        {
                            // qwen-omni-turbo 暂时不支持音频输入，只支持音频输出
                            // 将音频转换为文本描述
                            Console.WriteLine($"[DEBUG] Omni model detected, converting audio to text description");
                            return $"{message.Content} [User sent an audio clip, length: {audioBytes.Length} bytes]";
                        }
                        else
                        {
                            // 普通模型：转换为文本描述
                            return $"{message.Content} [Audio file: {message.AudioFormat}, size: {audioBytes.Length} bytes]";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to process audio: {ex.Message}");
                        return $"{message.Content} [Audio processing failed: {ex.Message}]";
                    }
                }
                return message.Content;
            
            case MessageType.Video:
                // 处理视频消息（作为图片序列）
                if (message.VideoFramePaths?.Count > 0 || message.VideoFrameData?.Count > 0)
                {
                    try
                    {
                        List<string> frameUrls = new List<string>();
                        
                        if (message.VideoFramePaths?.Count > 0)
                        {
                            // 从文件路径读取
                            foreach (var framePath in message.VideoFramePaths)
                            {
                                var frameBytes = File.ReadAllBytes(framePath);
                                var base64Frame = Convert.ToBase64String(frameBytes);
                                var mimeType = GetImageMimeType(framePath);
                                frameUrls.Add($"data:{mimeType};base64,{base64Frame}");
                            }
                        }
                        else if (message.VideoFrameData?.Count > 0)
                        {
                            // 从字节数据读取
                            foreach (var frameData in message.VideoFrameData)
                            {
                                var base64Frame = Convert.ToBase64String(frameData);
                                frameUrls.Add($"data:image/jpeg;base64,{base64Frame}");
                            }
                        }
                        
                        Console.WriteLine($"[DEBUG] Video processed as {frameUrls.Count} frames");
                        
                        // 使用视频格式
                        return new object[]
                        {
                            new { 
                                type = "video", 
                                video = frameUrls.ToArray()
                            },
                            new { type = "text", text = message.Content }
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to process video: {ex.Message}");
                        return $"{message.Content} [Video processing failed: {ex.Message}]";
                    }
                }
                return message.Content;
                
            case MessageType.File:
                if (message.FileData != null && !string.IsNullOrEmpty(message.FileName))
                {
                    try
                    {
                        // 检查是否是文本文件
                        if (IsTextFile(message.FileName))
                        {
                            var fileContent = Encoding.UTF8.GetString(message.FileData);
                            Console.WriteLine($"[DEBUG] Text file content extracted, length: {fileContent.Length}");
                            return $"{message.Content}\n\nFile content ({message.FileName}):\n```\n{fileContent}\n```";
                        }
                        else
                        {
                            var base64File = Convert.ToBase64String(message.FileData);
                            Console.WriteLine($"[DEBUG] Binary file converted to base64, size: {message.FileData.Length} bytes");
                            return $"{message.Content}\n\n[File: {message.FileName}, size: {message.FileData.Length} bytes, Base64: {base64File.Substring(0, Math.Min(100, base64File.Length))}...]";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to process file: {ex.Message}");
                        return $"{message.Content} [File processing failed: {ex.Message}]";
                    }
                }
                return message.Content;
                
            default:
                return message.Content;
        }
    }

    private string GetImageMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg" // 默认
        };
    }

    private bool IsTextFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        var textExtensions = new[] { ".txt", ".md", ".json", ".xml", ".html", ".css", ".js", ".cs", ".py", ".java", ".cpp", ".c", ".h" };
        return textExtensions.Contains(extension);
    }
    
    private bool IsOmniModel(string model)
    {
        // 判断是否是真正的全模态模型（只有omni系列支持音频输出）
        return model.Contains("omni", StringComparison.OrdinalIgnoreCase) || 
               model.Contains("qwen-omni", StringComparison.OrdinalIgnoreCase);
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
            return "Failed to parse response";
        }
    }
    
    private (string textContent, string audioData) ParseStreamingResponse(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            string textContent = "";
            string audioData = "";
            
            // 输出完整的JSON以便调试
            Console.WriteLine($"[DEBUG STREAM] Full JSON: {data}");
            
            if (_config!.ProviderType == AIProviderType.Ollama)
            {
                // Ollama流式响应格式 - 根据官方文档修复
                // 格式: {"model":"...","created_at":"...","message":{"role":"assistant","content":"..."},"done":false}
                // 支持reasoning模型（如DeepSeek-R1）的reasoning字段
                if (doc.RootElement.TryGetProperty("message", out var message))
                {
                    Console.WriteLine($"[DEBUG STREAM] Found 'message' property - Ollama native format");
                    
                    // 首先尝试获取reasoning字段（适用于推理模型如DeepSeek-R1、Qwen2.5-Instruct等）
                    if (message.TryGetProperty("reasoning", out var reasoning))
                    {
                        var reasoningText = reasoning.GetString();
                        Console.WriteLine($"[DEBUG STREAM] Found reasoning: '{reasoningText}'");
                        if (!string.IsNullOrEmpty(reasoningText))
                        {
                            textContent = $"💭 推理: {reasoningText}";
                            Console.WriteLine($"[DEBUG STREAM] Using reasoning as thinking process");
                        }
                    }
                    
                    // 如果没有reasoning或reasoning为空，则获取content
                    if (string.IsNullOrEmpty(textContent))
                    {
                        if (message.TryGetProperty("content", out var content))
                        {
                            var contentStr = content.GetString() ?? "";
                            textContent = contentStr;
                            Console.WriteLine($"[DEBUG STREAM] Ollama content extracted: '{textContent}'");
                        }
                    }
                }
                // 检查done标志
                if (doc.RootElement.TryGetProperty("done", out var done))
                {
                    var isDone = done.GetBoolean();
                    Console.WriteLine($"[DEBUG STREAM] Ollama done: {isDone}");
                }
            }
            else
            {
                // OpenAI兼容格式 (包括阿里云兼容模式)
                Console.WriteLine($"[DEBUG STREAM] Checking for 'choices' property...");
                if (doc.RootElement.TryGetProperty("choices", out var choicesProperty))
                {
                    Console.WriteLine($"[DEBUG STREAM] Found 'choices', array length: {choicesProperty.GetArrayLength()}");
                    var choices = choicesProperty;
                    if (choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        Console.WriteLine($"[DEBUG STREAM] Processing first choice...");
                        
                        // 检查是否有delta属性
                        if (choice.TryGetProperty("delta", out var delta))
                        {
                            Console.WriteLine($"[DEBUG STREAM] Found 'delta' property");
                            
                            // 输出所有delta属性以便调试
                            Console.WriteLine("[DEBUG STREAM] Delta properties:");
                            foreach (var prop in delta.EnumerateObject())
                            {
                                Console.WriteLine($"[DEBUG STREAM]   - {prop.Name}: {prop.Value.ToString().Substring(0, Math.Min(100, prop.Value.ToString().Length))}...");
                            }
                            
                            // 处理qvq模型的reasoning_content字段
                            if (delta.TryGetProperty("reasoning_content", out var reasoningContent))
                            {
                                var reasoningText = reasoningContent.GetString();
                                Console.WriteLine($"[DEBUG STREAM] Found reasoning_content: '{reasoningText}'");
                                if (!string.IsNullOrEmpty(reasoningText))
                                {
                                    // qvq模型的推理内容
                                    textContent = $"💭 推理中: {reasoningText}";
                                    Console.WriteLine($"[DEBUG STREAM] Using reasoning_content as thinking process");
                                }
                                else
                                {
                                    // reasoning_content为null或空，说明推理过程没有暴露
                                    Console.WriteLine($"[DEBUG STREAM] reasoning_content is null - qvq model reasoning is internal only");
                                }
                            }
                            // 处理传统的reasoning字段
                            else if (delta.TryGetProperty("reasoning", out var reasoning))
                            {
                                var reasoningText = reasoning.GetString() ?? "";
                                if (!string.IsNullOrEmpty(reasoningText))
                                {
                                    textContent = $"💭 推理: {reasoningText}";
                                    Console.WriteLine($"[DEBUG STREAM] Found traditional reasoning content: '{reasoningText.Substring(0, Math.Min(100, reasoningText.Length))}...'");
                                }
                            }
                            // 处理thinking属性（某些思考模型使用这个字段）
                            else if (delta.TryGetProperty("thinking", out var thinking))
                            {
                                var thinkingText = thinking.GetString() ?? "";
                                if (!string.IsNullOrEmpty(thinkingText))
                                {
                                    textContent = $"💭 思考中: {thinkingText}";
                                    Console.WriteLine($"[DEBUG STREAM] Found thinking content: '{thinkingText.Substring(0, Math.Min(100, thinkingText.Length))}...'");
                                }
                            }
                            // 处理普通文本内容
                            else if (delta.TryGetProperty("content", out var content))
                            {
                                var contentText = content.GetString() ?? "";
                                textContent = contentText;
                                Console.WriteLine($"[DEBUG STREAM] Found regular content: '{contentText}'");
                            }

                            // 处理音频数据（全模态模型特有）
                            if (delta.TryGetProperty("audio", out var audio))
                            {
                                Console.WriteLine($"[DEBUG STREAM] Found 'audio' property");
                                if (audio.TryGetProperty("data", out var audioDataProperty))
                                {
                                    audioData = audioDataProperty.GetString() ?? "";
                                    Console.WriteLine($"[DEBUG STREAM] Found audio data chunk: {audioData.Length} chars");
                                }
                                else if (audio.TryGetProperty("transcript", out var transcript))
                                {
                                    // qwen-omni-turbo经常将文本内容放在audio.transcript中
                                    var transcriptText = transcript.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(transcriptText))
                                    {
                                        // 使用transcript中的文本，这通常包含完整的回复文本
                                        textContent = transcriptText;
                                        Console.WriteLine($"[DEBUG STREAM] Found text in audio transcript: '{textContent}'");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[DEBUG STREAM] No 'data' or 'transcript' in audio");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG STREAM] No 'audio' in delta");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG STREAM] No 'delta' property in choice");
                            // 检查是否直接有message属性（非流式格式）
                            if (choice.TryGetProperty("message", out var message))
                            {
                                Console.WriteLine($"[DEBUG STREAM] Found 'message' property instead");
                                if (message.TryGetProperty("content", out var messageContent))
                                {
                                    textContent = messageContent.GetString() ?? "";
                                    Console.WriteLine($"[DEBUG STREAM] Found message content: '{textContent}'");
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG STREAM] Choices array is empty");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG STREAM] No 'choices' property found");
                    // 输出所有顶级属性以便调试
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        Console.WriteLine($"[DEBUG STREAM] Root property: {property.Name}");
                    }
                }
            }
            
            Console.WriteLine($"[DEBUG STREAM] Final result - text: '{textContent}', audio: {audioData.Length} chars");
            return (textContent, audioData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR STREAM] JSON parsing failed: {ex.Message}");
            Console.WriteLine($"[ERROR STREAM] JSON data: {data}");
            return ("", "");
        }
    }
    
    private string ProcessAudioData(string audioBase64, ChatMessage? currentMessage)
    {
        try
        {
            Console.WriteLine($"[DEBUG STREAM] Processing audio base64, length: {audioBase64.Length}");
            Console.WriteLine($"[DEBUG STREAM] Audio base64 sample (first 100 chars): {audioBase64.Substring(0, Math.Min(100, audioBase64.Length))}");
            
            // 清理base64字符串
            var cleanedBase64 = audioBase64.Trim().Replace("\n", "").Replace("\r", "");
            
            // 验证base64格式
            if (cleanedBase64.Length % 4 != 0)
            {
                // 添加必要的填充
                var padding = 4 - (cleanedBase64.Length % 4);
                if (padding < 4)
                {
                    cleanedBase64 += new string('=', padding);
                    Console.WriteLine($"[DEBUG STREAM] Added {padding} padding chars to base64");
                }
            }
            
            var audioBytes = Convert.FromBase64String(cleanedBase64);
            Console.WriteLine($"[DEBUG STREAM] Decoded audio bytes length: {audioBytes.Length}");
            Console.WriteLine($"[DEBUG STREAM] Audio bytes sample (first 32 bytes): {BitConverter.ToString(audioBytes.Take(32).ToArray())}");
            
            // 检查音频数据是否异常（全零或重复模式）
            if (IsAudioDataCorrupted(audioBytes))
            {
                Console.WriteLine($"[WARNING STREAM] Audio data appears to be corrupted, attempting to fix...");
                audioBytes = TryFixAudioData(audioBytes);
            }
            
            if (currentMessage != null)
            {
                currentMessage.HasAudioOutput = true;
                currentMessage.AiAudioData = audioBytes;
                Console.WriteLine($"[DEBUG STREAM] Saved AI audio output to message: {audioBytes.Length} bytes");
                return "\n🔊 [AI generated voice reply, click to play]";
            }
            else
            {
                // 即使没有currentMessage，也要通知用户有音频数据
                Console.WriteLine($"[WARNING] Audio data collected but no current message to attach: {audioBytes.Length} bytes");
                return $"\n🔊 [AI generated voice reply({audioBytes.Length} bytes), but cannot save for playback]";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR STREAM] Failed to process AI audio: {ex.Message}");
            Console.WriteLine($"[ERROR STREAM] Audio base64 length: {audioBase64.Length}");
            return "\n🔊 [AI voice output processing failed]";
        }
    }
    
    private bool IsAudioDataCorrupted(byte[] audioData)
    {
        if (audioData.Length < 100) return true;
        
        // 检查是否全为零
        var allZero = audioData.Take(100).All(b => b == 0);
        if (allZero)
        {
            Console.WriteLine("[DEBUG] Audio data is all zeros");
            return true;
        }
        
        // 检查是否有重复模式 (如 FD-FF-FE-FF 重复)
        var pattern = audioData.Take(4).ToArray();
        var hasPattern = true;
        for (int i = 0; i < Math.Min(100, audioData.Length - 4); i += 4)
        {
            if (!audioData.Skip(i).Take(4).SequenceEqual(pattern))
            {
                hasPattern = false;
                break;
            }
        }
        
        if (hasPattern)
        {
            Console.WriteLine($"[DEBUG] Audio data has repeating pattern: {BitConverter.ToString(pattern)}");
            return true;
        }
        
        return false;
    }
    
    private byte[] TryFixAudioData(byte[] corruptedData)
    {
        try
        {
            // 尝试方法1：跳过可能的头部垃圾数据
            var skipBytes = 64; // 跳过前64字节
            if (corruptedData.Length > skipBytes)
            {
                var fixedData = corruptedData.Skip(skipBytes).ToArray();
                Console.WriteLine($"[DEBUG] Attempted fix by skipping {skipBytes} bytes, new length: {fixedData.Length}");
                
                if (!IsAudioDataCorrupted(fixedData))
                {
                    return fixedData;
                }
            }
            
            // 尝试方法2：寻找有效的音频数据段
            for (int offset = 0; offset < Math.Min(1000, corruptedData.Length - 1000); offset += 100)
            {
                var segment = corruptedData.Skip(offset).Take(1000).ToArray();
                if (!IsAudioDataCorrupted(segment))
                {
                    var remainingData = corruptedData.Skip(offset).ToArray();
                    Console.WriteLine($"[DEBUG] Found valid audio data at offset {offset}, length: {remainingData.Length}");
                    return remainingData;
                }
            }
            
            Console.WriteLine("[WARNING] Unable to fix corrupted audio data");
            return corruptedData; // 返回原始数据
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to fix audio data: {ex.Message}");
            return corruptedData;
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}