using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace AIlable.Services;

public interface IAudioPlayerService
{
    Task<bool> PlayAudioAsync(byte[] audioData, string format = "wav");
    Task<bool> PlayAudioFromBase64Async(string base64Audio, string format = "wav");
    bool IsSupported { get; }
}

public class AudioPlayerService : IAudioPlayerService
{
    public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    public async Task<bool> PlayAudioAsync(byte[] audioData, string format = "wav")
    {
        if (!IsSupported || audioData == null || audioData.Length == 0)
        {
            Console.WriteLine("[WARNING] Audio playback not supported or no audio data");
            return false;
        }

        try
        {
            Console.WriteLine($"[DEBUG] Original audio data size: {audioData.Length} bytes");
            Console.WriteLine($"[DEBUG] Audio data header (first 16 bytes): {BitConverter.ToString(audioData.Take(16).ToArray())}");
            
            // 检查是否是标准WAV格式
            byte[] processedAudioData = audioData;
            if (!IsValidWavFile(audioData))
            {
                Console.WriteLine("[DEBUG] Audio data is not standard WAV format, attempting to convert...");
                processedAudioData = ConvertToWav(audioData);
                Console.WriteLine($"[DEBUG] Converted audio data size: {processedAudioData.Length} bytes");
                Console.WriteLine($"[DEBUG] Converted audio header (first 16 bytes): {BitConverter.ToString(processedAudioData.Take(16).ToArray())}");
            }
            
            // 创建临时音频文件
            var tempPath = Path.GetTempFileName() + $".{format}";
            await File.WriteAllBytesAsync(tempPath, processedAudioData);
            
            Console.WriteLine($"[DEBUG] Playing audio file: {tempPath}, size: {processedAudioData.Length} bytes");
            
            // 验证音频文件是否正确写入
            if (File.Exists(tempPath))
            {
                var fileInfo = new FileInfo(tempPath);
                Console.WriteLine($"[DEBUG] Temp audio file created: {fileInfo.Length} bytes at {tempPath}");
            }
            
            // 根据平台播放音频
            var success = await PlayAudioFileAsync(tempPath);
            
            // 延迟清理，确保播放完成
            _ = Task.Delay(10000).ContinueWith(_ => 
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                        Console.WriteLine($"[DEBUG] Cleaned up temp audio file: {tempPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to cleanup temp audio file: {ex.Message}");
                }
            });
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to play audio: {ex.Message}");
            return false;
        }
    }
    
    private bool IsValidWavFile(byte[] audioData)
    {
        if (audioData.Length < 12) return false;
        
        // 检查WAV文件头：RIFF....WAVE
        return audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46 && // "RIFF"
               audioData[8] == 0x57 && audioData[9] == 0x41 && audioData[10] == 0x56 && audioData[11] == 0x45;   // "WAVE"
    }
    
    private byte[] ConvertToWav(byte[] rawAudioData)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Raw audio sample (first 32 bytes): {BitConverter.ToString(rawAudioData.Take(32).ToArray())}");
            
            // 检查数据是否看起来像有效的PCM数据
            if (rawAudioData.All(b => b == 0))
            {
                Console.WriteLine("[WARNING] Audio data is all zeros, cannot convert");
                return rawAudioData;
            }
            
            // 尝试不同的采样率和格式参数
            var configs = new[]
            {
                new { SampleRate = 24000, Channels = 1, BitsPerSample = 16 },
                new { SampleRate = 22050, Channels = 1, BitsPerSample = 16 },
                new { SampleRate = 16000, Channels = 1, BitsPerSample = 16 },
                new { SampleRate = 8000, Channels = 1, BitsPerSample = 16 },
            };
            
            foreach (var config in configs)
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Trying conversion with {config.SampleRate}Hz, {config.Channels} channel(s), {config.BitsPerSample} bits");
                    
                    using var memoryStream = new MemoryStream();
                    using var writer = new BinaryWriter(memoryStream);
                    
                    // WAV文件头
                    writer.Write("RIFF".ToCharArray());  // ChunkID
                    writer.Write((uint)(36 + rawAudioData.Length));  // ChunkSize
                    writer.Write("WAVE".ToCharArray());  // Format
                    
                    // fmt子块
                    writer.Write("fmt ".ToCharArray());  // Subchunk1ID
                    writer.Write((uint)16);              // Subchunk1Size (PCM)
                    writer.Write((ushort)1);             // AudioFormat (PCM)
                    writer.Write((ushort)config.Channels);      // NumChannels
                    writer.Write((uint)config.SampleRate);      // SampleRate
                    writer.Write((uint)(config.SampleRate * config.Channels * config.BitsPerSample / 8)); // ByteRate
                    writer.Write((ushort)(config.Channels * config.BitsPerSample / 8));            // BlockAlign
                    writer.Write((ushort)config.BitsPerSample); // BitsPerSample
                    
                    // data子块
                    writer.Write("data".ToCharArray());  // Subchunk2ID
                    writer.Write((uint)rawAudioData.Length); // Subchunk2Size
                    writer.Write(rawAudioData);          // 音频数据
                    
                    writer.Flush();
                    var wavData = memoryStream.ToArray();
                    
                    Console.WriteLine($"[DEBUG] Successfully converted to WAV with {config.SampleRate}Hz");
                    return wavData;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed conversion with {config.SampleRate}Hz: {ex.Message}");
                    continue;
                }
            }
            
            Console.WriteLine("[WARNING] All conversion attempts failed, returning original data");
            return rawAudioData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to convert audio to WAV: {ex.Message}");
            return rawAudioData; // 转换失败时返回原始数据
        }
    }

    public async Task<bool> PlayAudioFromBase64Async(string base64Audio, string format = "wav")
    {
        if (string.IsNullOrEmpty(base64Audio))
        {
            Console.WriteLine("[WARNING] No base64 audio data provided");
            return false;
        }

        try
        {
            var audioData = Convert.FromBase64String(base64Audio);
            Console.WriteLine($"[DEBUG] Decoded base64 audio, size: {audioData.Length} bytes");
            return await PlayAudioAsync(audioData, format);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to decode base64 audio: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PlayAudioFileAsync(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: 使用 mplayer 或 Windows Media Player
                return await PlayAudioWindowsAsync(filePath);
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux: 使用 aplay 或 paplay
                return await PlayAudioLinuxAsync(filePath);
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS: 使用 afplay
                return await PlayAudioMacAsync(filePath);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Platform audio playback failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PlayAudioWindowsAsync(string filePath)
    {
        try
        {
            // 优先使用系统默认播放器（能正常播放）
            var success = await PlayAudioWindowsDefaultAsync(filePath);
            if (success)
            {
                return true;
            }
            
            // 如果系统播放器失败，尝试PowerShell
            Console.WriteLine("[DEBUG] Default player failed, trying PowerShell...");
            return await PlayAudioWindowsPowerShellAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Windows audio playback failed: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> PlayAudioWindowsDefaultAsync(string filePath)
    {
        try
        {
            // 使用系统默认播放器，允许显示窗口以确保播放
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                CreateNoWindow = false, // 允许窗口显示
                WindowStyle = ProcessWindowStyle.Normal // 正常窗口，确保播放
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                Console.WriteLine($"[DEBUG] Started default audio player for: {filePath}");
                await Task.Delay(1000); // 给播放器启动时间
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Default audio player failed: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> PlayAudioWindowsPowerShellAsync(string filePath)
    {
        try
        {
            // 方法2：尝试使用 PowerShell 播放音频（改进版）
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"" +
                           $"Add-Type -AssemblyName presentationCore; " +
                           $"$mediaPlayer = New-Object system.windows.media.mediaplayer; " +
                           $"$mediaPlayer.open([uri]::new('{filePath}')); " +
                           $"$mediaPlayer.Play(); " +
                           $"while($mediaPlayer.Position -lt $mediaPlayer.NaturalDuration.TimeSpan) {{ Start-Sleep -Milliseconds 100 }}; " +
                           $"$mediaPlayer.Stop(); $mediaPlayer.Close()\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                Console.WriteLine($"[DEBUG] PowerShell audio output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"[DEBUG] PowerShell audio error: {error}");
                }
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"[DEBUG] Windows audio playback completed successfully");
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Windows PowerShell audio playback failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PlayAudioLinuxAsync(string filePath)
    {
        try
        {
            // 尝试使用 aplay
            var startInfo = new ProcessStartInfo
            {
                FileName = "aplay",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                Console.WriteLine($"[DEBUG] Linux audio playback completed with exit code: {process.ExitCode}");
                return process.ExitCode == 0;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Linux audio playback failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PlayAudioMacAsync(string filePath)
    {
        try
        {
            // 使用 afplay
            var startInfo = new ProcessStartInfo
            {
                FileName = "afplay",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                Console.WriteLine($"[DEBUG] macOS audio playback completed with exit code: {process.ExitCode}");
                return process.ExitCode == 0;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] macOS audio playback failed: {ex.Message}");
            return false;
        }
    }
}