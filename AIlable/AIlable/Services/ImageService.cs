using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using AIlable.Models;

namespace AIlable.Services;

public class ImageService
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp" };

    public static bool IsSupportedImageFormat(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(SupportedExtensions, ext => ext == extension);
    }

    public static async Task<Bitmap?> LoadImageAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return null;

            await using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error loading image {filePath}: {ex.Message}");
            return null;
        }
    }

    public static Bitmap? LoadImage(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return null;

            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading image {filePath}: {ex.Message}");
            return null;
        }
    }

    public static async Task<(int width, int height)> GetImageDimensionsAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return (0, 0);

            using var image = await SixLabors.ImageSharp.Image.LoadAsync(filePath);
            return (image.Width, image.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting image dimensions {filePath}: {ex.Message}");
            return (0, 0);
        }
    }

    public static (int width, int height) GetImageDimensions(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return (0, 0);

            using var image = SixLabors.ImageSharp.Image.Load(filePath);
            return (image.Width, image.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting image dimensions {filePath}: {ex.Message}");
            return (0, 0);
        }
    }

    public static async Task<AnnotationImage?> CreateAnnotationImageAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return null;

            var annotationImage = new AnnotationImage(filePath);
            var (width, height) = await GetImageDimensionsAsync(filePath);
            annotationImage.SetImageDimensions(width, height);

            return annotationImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating annotation image {filePath}: {ex.Message}");
            return null;
        }
    }

    public static AnnotationImage? CreateAnnotationImage(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedImageFormat(filePath))
                return null;

            var annotationImage = new AnnotationImage(filePath);
            var (width, height) = GetImageDimensions(filePath);
            annotationImage.SetImageDimensions(width, height);

            return annotationImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating annotation image {filePath}: {ex.Message}");
            return null;
        }
    }

    public static Point2D ScreenToImageCoordinates(Avalonia.Point screenPoint, Avalonia.Size imageSize, Avalonia.Size viewportSize, double zoomFactor)
    {
        // Convert screen coordinates to image coordinates considering zoom and viewport
        var scaleX = imageSize.Width / (viewportSize.Width * zoomFactor);
        var scaleY = imageSize.Height / (viewportSize.Height * zoomFactor);

        return new Point2D(
            screenPoint.X * scaleX,
            screenPoint.Y * scaleY
        );
    }

    public static Avalonia.Point ImageToScreenCoordinates(Point2D imagePoint, Avalonia.Size imageSize, Avalonia.Size viewportSize, double zoomFactor)
    {
        // Convert image coordinates to screen coordinates considering zoom and viewport
        var scaleX = (viewportSize.Width * zoomFactor) / imageSize.Width;
        var scaleY = (viewportSize.Height * zoomFactor) / imageSize.Height;

        return new Avalonia.Point(
            imagePoint.X * scaleX,
            imagePoint.Y * scaleY
        );
    }

    public static double CalculateFitToWindowScale(Avalonia.Size imageSize, Avalonia.Size windowSize)
    {
        if (imageSize.Width == 0 || imageSize.Height == 0 || windowSize.Width == 0 || windowSize.Height == 0)
            return 1.0;

        var scaleX = windowSize.Width / imageSize.Width;
        var scaleY = windowSize.Height / imageSize.Height;

        return Math.Min(scaleX, scaleY);
    }
}