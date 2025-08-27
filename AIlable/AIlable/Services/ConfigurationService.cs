using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

public interface IConfigurationService
{
    Task<List<AIProviderConfig>> LoadProvidersAsync();
    Task SaveProvidersAsync(List<AIProviderConfig> providers);
    Task<AIProviderConfig?> GetLastUsedProviderAsync();
    Task SaveLastUsedProviderAsync(AIProviderConfig provider);
    Task CleanConfigurationFilesAsync();
    Task ResetConfigurationFilesAsync();
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _providersFilePath;
    private readonly string _settingsFilePath;
    
    public ConfigurationService()
    {
        // 使用用户数据目录
        _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIlable");
        _providersFilePath = Path.Combine(_configDirectory, "providers.json");  
        _settingsFilePath = Path.Combine(_configDirectory, "settings.json");
        
        // 确保配置目录存在
        Directory.CreateDirectory(_configDirectory);
        
        Console.WriteLine($"[DEBUG CONFIG] Config directory: {_configDirectory}");
    }
    
    public async Task<List<AIProviderConfig>> LoadProvidersAsync()
    {
        try
        {
            if (!File.Exists(_providersFilePath))
            {
                Console.WriteLine($"[DEBUG CONFIG] Providers file not found, returning defaults");
                return new List<AIProviderConfig>();
            }
            
            var json = await File.ReadAllTextAsync(_providersFilePath);
            var providers = JsonSerializer.Deserialize<List<AIProviderConfig>>(json, GetJsonOptions());
            
            // 清理加载的配置中的API Key
            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    provider.ApiKey = CleanApiKey(provider.ApiKey);
                }
            }
            
            Console.WriteLine($"[DEBUG CONFIG] Loaded {providers?.Count ?? 0} providers from file");
            return providers ?? new List<AIProviderConfig>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to load providers: {ex.Message}");
            return new List<AIProviderConfig>();
        }
    }
    
    public async Task SaveProvidersAsync(List<AIProviderConfig> providers)
    {
        try
        {
            var json = JsonSerializer.Serialize(providers, GetJsonOptions());
            await File.WriteAllTextAsync(_providersFilePath, json);
            
            Console.WriteLine($"[DEBUG CONFIG] Saved {providers.Count} providers to file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to save providers: {ex.Message}");
        }
    }
    
    public async Task<AIProviderConfig?> GetLastUsedProviderAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }
            
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (settings?.TryGetValue("lastUsedProvider", out var providerData) == true)
            {
                var providerJson = providerData.ToString();
                if (!string.IsNullOrEmpty(providerJson))
                {
                    var provider = JsonSerializer.Deserialize<AIProviderConfig>(providerJson, GetJsonOptions());
                    if (provider != null)
                    {
                        // 清理API Key
                        provider.ApiKey = CleanApiKey(provider.ApiKey);
                        Console.WriteLine($"[DEBUG CONFIG] Loaded last used provider: {provider.DisplayName}");
                        return provider;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to load last used provider: {ex.Message}");
        }
        
        return null;
    }
    
    public async Task SaveLastUsedProviderAsync(AIProviderConfig provider)
    {
        try
        {
            Dictionary<string, object> settings;
            
            if (File.Exists(_settingsFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(_settingsFilePath);
                settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new Dictionary<string, object>();
            }
            else
            {
                settings = new Dictionary<string, object>();
            }
            
            var providerJson = JsonSerializer.Serialize(provider, GetJsonOptions());
            settings["lastUsedProvider"] = providerJson;
            
            var json = JsonSerializer.Serialize(settings, GetJsonOptions());
            await File.WriteAllTextAsync(_settingsFilePath, json);
            
            Console.WriteLine($"[DEBUG CONFIG] Saved last used provider: {provider.DisplayName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to save last used provider: {ex.Message}");
        }
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
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
                Console.WriteLine($"[DEBUG CONFIG] Removed non-ASCII character: '{c}' (code: {(int)c})");
            }
        }
        
        var finalResult = result.ToString();
        if (finalResult != cleaned)
        {
            Console.WriteLine($"[DEBUG CONFIG] API Key cleaned: original length {cleaned.Length} -> final length {finalResult.Length}");
        }
        
        return finalResult;
    }
    
    /// <summary>
    /// 清理并重新保存配置文件，移除所有非ASCII字符
    /// </summary>
    public async Task CleanConfigurationFilesAsync()
    {
        try
        {
            Console.WriteLine("[DEBUG CONFIG] Starting configuration files cleanup...");
            
            // 清理providers.json
            if (File.Exists(_providersFilePath))
            {
                var providers = await LoadProvidersAsync();
                await SaveProvidersAsync(providers); // 保存时会自动清理
                Console.WriteLine($"[DEBUG CONFIG] Cleaned providers.json");
            }
            
            // 清理settings.json
            if (File.Exists(_settingsFilePath))
            {
                var lastUsed = await GetLastUsedProviderAsync();
                if (lastUsed != null)
                {
                    await SaveLastUsedProviderAsync(lastUsed); // 保存时会自动清理
                    Console.WriteLine($"[DEBUG CONFIG] Cleaned settings.json");
                }
            }
            
            Console.WriteLine("[DEBUG CONFIG] Configuration cleanup completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to clean configuration files: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 完全重置配置文件，删除所有保存的配置
    /// </summary>
    public Task ResetConfigurationFilesAsync()
    {
        try
        {
            Console.WriteLine("[DEBUG CONFIG] Resetting all configuration files...");
            
            // 删除providers.json
            if (File.Exists(_providersFilePath))
            {
                File.Delete(_providersFilePath);
                Console.WriteLine($"[DEBUG CONFIG] Deleted providers.json");
            }
            
            // 删除settings.json
            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
                Console.WriteLine($"[DEBUG CONFIG] Deleted settings.json");
            }
            
            Console.WriteLine("[DEBUG CONFIG] Configuration reset completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR CONFIG] Failed to reset configuration files: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
}