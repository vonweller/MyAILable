using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIlable.Services;

public interface IVideoService
{
    Task<List<byte[]>?> ExtractVideoFramesAsync(string videoPath, int maxFrames = 10);
    Task<List<byte[]>?> LoadImageSequenceAsync(List<string> imagePaths);
    bool IsVideoFile(string filePath);
    bool IsImageFile(string filePath);
    Task<List<string>?> SelectVideoFramesAsync(string title);
}

public class VideoService : IVideoService
{
    private readonly IFileDialogService? _fileDialogService;
    
    public VideoService(IFileDialogService? fileDialogService = null)
    {
        _fileDialogService = fileDialogService;
    }
    
    public Task<List<byte[]>?> ExtractVideoFramesAsync(string videoPath, int maxFrames = 10)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                Console.WriteLine($"[ERROR] Video file not found: {videoPath}");
                return Task.FromResult<List<byte[]>?>(null);
            }
            
            // 注意：这里需要使用FFmpeg或类似的库来提取视频帧
            // 由于这是一个Avalonia项目，我们暂时返回空，实际项目中需要集成视频处理库
            Console.WriteLine($"[WARNING] Video frame extraction not implemented yet: {videoPath}");
            Console.WriteLine($"[INFO] To implement: Use FFmpeg.NET or similar library to extract {maxFrames} frames");
            
            return Task.FromResult<List<byte[]>?>(new List<byte[]>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to extract video frames: {ex.Message}");
            return Task.FromResult<List<byte[]>?>(null);
        }
    }
    
    public async Task<List<byte[]>?> LoadImageSequenceAsync(List<string> imagePaths)
    {
        try
        {
            var frames = new List<byte[]>();
            
            foreach (var imagePath in imagePaths)
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"[WARNING] Image file not found: {imagePath}");
                    continue;
                }
                
                var imageData = await File.ReadAllBytesAsync(imagePath);
                frames.Add(imageData);
                Console.WriteLine($"[DEBUG] Loaded image frame: {imagePath}, size: {imageData.Length} bytes");
            }
            
            Console.WriteLine($"[DEBUG] Loaded {frames.Count} image frames from {imagePaths.Count} paths");
            return frames.Count > 0 ? frames : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load image sequence: {ex.Message}");
            return null;
        }
    }
    
    public bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv" };
        return videoExtensions.Contains(extension);
    }
    
    public bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
        return imageExtensions.Contains(extension);
    }
    
    public async Task<List<string>?> SelectVideoFramesAsync(string title)
    {
        if (_fileDialogService == null)
        {
            Console.WriteLine("[ERROR] FileDialogService not available for video frame selection");
            return null;
        }
        
        try
        {
            // 选择多个图片文件作为视频帧
            var imagePaths = await _fileDialogService.ShowOpenMultipleFilesDialogAsync(
                title,
                new[] {
                    FileDialogService.ImageFiles,
                    FileDialogService.AllFiles
                });
            
            var frameList = imagePaths.ToList();
            Console.WriteLine($"[DEBUG] Selected {frameList.Count} image files as video frames");
            
            return frameList.Count > 0 ? frameList : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to select video frames: {ex.Message}");
            return null;
        }
    }
}