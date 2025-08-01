using System;
using System.Collections.Generic;
using System.IO;
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
                    Console.WriteLine($"[DEBUG CONFIG] Loaded last used provider: {provider?.DisplayName}");
                    return provider;
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
}