using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 增强的图像服务，包含缓存和性能优化
/// </summary>
public static class ImageServiceEnhanced
{
    private static readonly Dictionary<string, Bitmap> _bitmapCache = new();
    private static readonly Dictionary<string, (int width, int height)> _dimensionCache = new();
    private static readonly object _cacheLock = new();
    private const int MaxCacheSize = 50; // 最大缓存图像数量

    /// <summary>
    /// 加载图像，使用缓存优化
    /// </summary>
    public static async Task<Bitmap?> LoadImageWithCacheAsync(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        lock (_cacheLock)
        {
            // 检查缓存
            if (_bitmapCache.TryGetValue(imagePath, out var cachedBitmap))
            {
                return cachedBitmap;
            }
        }

        try
        {
            // 异步加载图像
            var bitmap = await Task.Run(() =>
            {
                using var fileStream = File.OpenRead(imagePath);
                return new Bitmap(fileStream);
            });

            lock (_cacheLock)
            {
                // 管理缓存大小
                if (_bitmapCache.Count >= MaxCacheSize)
                {
                    // 移除最老的条目（简单实现）
                    var firstKey = "";
                    foreach (var key in _bitmapCache.Keys)
                    {
                        firstKey = key;
                        break;
                    }
                    if (!string.IsNullOrEmpty(firstKey))
                    {
                        _bitmapCache[firstKey]?.Dispose();
                        _bitmapCache.Remove(firstKey);
                    }
                }

                _bitmapCache[imagePath] = bitmap;
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading image {imagePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取图像尺寸，使用缓存优化
    /// </summary>
    public static (int width, int height) GetImageDimensionsWithCache(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return (0, 0);

        lock (_cacheLock)
        {
            if (_dimensionCache.TryGetValue(imagePath, out var cachedDimensions))
                return cachedDimensions;
        }

        try
        {
            using var image = Image.Load<Rgba32>(imagePath);
            var dimensions = (image.Width, image.Height);

            lock (_cacheLock)
            {
                _dimensionCache[imagePath] = dimensions;
            }

            return dimensions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting image dimensions {imagePath}: {ex.Message}");
            return (0, 0);
        }
    }

    /// <summary>
    /// 创建标注图像，使用优化版本
    /// </summary>
    public static Task<AnnotationImage?> CreateAnnotationImageOptimizedAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Task.FromResult<AnnotationImage?>(null);

        try
        {
            var fileName = Path.GetFileName(filePath);
            var (width, height) = GetImageDimensionsWithCache(filePath);

            if (width == 0 || height == 0)
                return Task.FromResult<AnnotationImage?>(null);

            var image = new AnnotationImage
            {
                FileName = fileName,
                FilePath = filePath
            };
            image.SetImageDimensions(width, height);

            return Task.FromResult<AnnotationImage?>(image);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating annotation image: {ex.Message}");
            return Task.FromResult<AnnotationImage?>(null);
        }
    }

    /// <summary>
    /// 预加载图像到缓存
    /// </summary>
    public static async Task PreloadImagesAsync(IEnumerable<string> imagePaths)
    {
        var tasks = new List<Task>();
        
        foreach (var imagePath in imagePaths)
        {
            tasks.Add(LoadImageWithCacheAsync(imagePath));
            
            // 限制并发任务数量
            if (tasks.Count >= 5)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// 清理图像缓存
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var bitmap in _bitmapCache.Values)
            {
                bitmap?.Dispose();
            }
            _bitmapCache.Clear();
            _dimensionCache.Clear();
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public static CacheStatistics GetCacheStatistics()
    {
        lock (_cacheLock)
        {
            return new CacheStatistics
            {
                BitmapCacheCount = _bitmapCache.Count,
                DimensionCacheCount = _dimensionCache.Count,
                MaxCacheSize = MaxCacheSize
            };
        }
    }

    /// <summary>
    /// 生成图像缩略图
    /// </summary>
    public static async Task<Bitmap?> GenerateThumbnailAsync(string imagePath, int maxSize = 200)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                using var image = Image.Load<Rgba32>(imagePath);
                
                // 计算缩略图尺寸
                double scale = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                int newWidth = (int)(image.Width * scale);
                int newHeight = (int)(image.Height * scale);

                // 调整图像大小
                image.Mutate(x => x.Resize(newWidth, newHeight));

                // 转换为Avalonia Bitmap
                using var memoryStream = new MemoryStream();
                image.SaveAsPng(memoryStream);
                memoryStream.Position = 0;

                return new Bitmap(memoryStream);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating thumbnail for {imagePath}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public int BitmapCacheCount { get; set; }
    public int DimensionCacheCount { get; set; }
    public int MaxCacheSize { get; set; }
}