using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIlable.Services;

public interface IAudioService
{
    Task<byte[]?> LoadAudioFileAsync(string filePath);
    Task SaveAudioFileAsync(string filePath, byte[] audioData);
    bool IsAudioFile(string filePath);
    string GetAudioMimeType(string filePath);
    Task PlayAudioAsync(byte[] audioData);
}

public class AudioService : IAudioService
{
    public async Task<byte[]?> LoadAudioFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[ERROR] Audio file not found: {filePath}");
                return null;
            }
            
            var audioData = await File.ReadAllBytesAsync(filePath);
            Console.WriteLine($"[DEBUG] Audio file loaded: {filePath}, size: {audioData.Length} bytes");
            return audioData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load audio file: {ex.Message}");
            return null;
        }
    }
    
    public async Task SaveAudioFileAsync(string filePath, byte[] audioData)
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllBytesAsync(filePath, audioData);
            Console.WriteLine($"[DEBUG] Audio file saved: {filePath}, size: {audioData.Length} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to save audio file: {ex.Message}");
            throw;
        }
    }
    
    public bool IsAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var audioExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg", ".m4a", ".aac" };
        return audioExtensions.Contains(extension);
    }
    
    public string GetAudioMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            _ => "audio/wav" // 默认
        };
    }
    
    public async Task PlayAudioAsync(byte[] audioData)
    {
        try
        {
            // 创建临时文件播放音频
            var tempPath = Path.GetTempFileName() + ".wav";
            await File.WriteAllBytesAsync(tempPath, audioData);
            
            // 在Windows上使用系统默认音频播放器
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                System.Diagnostics.Process.Start("aplay", tempPath);
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("afplay", tempPath);
            }
            
            Console.WriteLine($"[DEBUG] Playing audio file: {tempPath}");
            
            // 延迟删除临时文件
            _ = Task.Delay(30000).ContinueWith(_ =>
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to play audio: {ex.Message}");
        }
    }
}