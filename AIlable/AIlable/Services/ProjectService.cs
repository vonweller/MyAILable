using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

public static class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<bool> SaveProjectAsync(AnnotationProject project, string filePath)
    {
        try
        {
            project.ProjectPath = Path.GetDirectoryName(filePath) ?? "";
            
            var json = JsonSerializer.Serialize(project, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            project.MarkClean();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving project: {ex.Message}");
            return false;
        }
    }

    public static async Task<AnnotationProject?> LoadProjectAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var project = JsonSerializer.Deserialize<AnnotationProject>(json, JsonOptions);
            
            if (project != null)
            {
                project.ProjectPath = Path.GetDirectoryName(filePath) ?? "";
                project.MarkClean();
                
                // Validate and update image paths
                await ValidateImagePathsAsync(project);
            }
            
            return project;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading project: {ex.Message}");
            return null;
        }
    }

    private static async Task ValidateImagePathsAsync(AnnotationProject project)
    {
        for (int i = project.Images.Count - 1; i >= 0; i--)
        {
            var image = project.Images[i];

            // Check if file exists
            if (!File.Exists(image.FilePath))
            {
                // Try relative path from project directory
                var relativePath = Path.Combine(project.ProjectPath, image.FileName);
                if (File.Exists(relativePath))
                {
                    image.FilePath = relativePath;
                }
                else
                {
                    // Image file not found, could ask user or remove from project
                    Console.WriteLine($"Warning: Image file not found: {image.FilePath}");
                }
            }

            // Update image dimensions if needed
            if (image.Width == 0 || image.Height == 0)
            {
                var (width, height) = await ImageService.GetImageDimensionsAsync(image.FilePath);
                image.SetImageDimensions(width, height);
            }
        }
    }

    public static async Task<bool> ExportProjectAsync(AnnotationProject project, ExportFormat format, string outputPath)
    {
        try
        {
            return format switch
            {
                ExportFormat.COCO => await ExportService.ExportToCocoAsync(project, outputPath),
                ExportFormat.VOC => await ExportService.ExportToVocAsync(project, outputPath),
                ExportFormat.YOLO => await ExportService.ExportToYoloAsync(project, outputPath, true), // 默认使用分割格式
                ExportFormat.JSON => await ExportService.ExportProjectAsync(project, outputPath),
                _ => false
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting project: {ex.Message}");
            return false;
        }
    }

    public static AnnotationProject CreateNewProject(string name = "New Project")
    {
        return new AnnotationProject(name, "")
        {
            Description = "Created with AIlable"
        };
    }

    public static bool IsProjectFile(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".ailproj", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDefaultProjectFileName(string projectName)
    {
        var fileName = projectName.Replace(" ", "_");
        // Remove invalid characters
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return $"{fileName}.ailproj";
    }
}