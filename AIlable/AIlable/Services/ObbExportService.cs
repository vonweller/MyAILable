using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// OBB (Oriented Bounding Box) 导出服务
/// 支持YOLO OBB和DOTA格式导出
/// </summary>
public static class ObbExportService
{
    /// <summary>
    /// 导出为YOLO OBB格式
    /// </summary>
    public static async Task<bool> ExportToYoloObbAsync(AnnotationProject project, string outputPath)
    {
        try
        {
            Directory.CreateDirectory(outputPath);
            
            // 创建标签映射
            var labelMap = CreateLabelMap(project.Labels.ToList());
            
            // 导出类别文件
            await ExportClassesFileAsync(Path.Combine(outputPath, "classes.txt"), project.Labels.ToList());
            
            // 导出每张图像的标注
            foreach (var image in project.Images)
            {
                await ExportImageAnnotationsYoloObb(image, outputPath, labelMap);
            }
            
            // 创建数据集配置文件
            await CreateYoloObbConfigAsync(outputPath, project.Labels.ToList());
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"YOLO OBB导出失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 导出为DOTA格式
    /// </summary>
    public static async Task<bool> ExportToDotaAsync(AnnotationProject project, string outputPath)
    {
        try
        {
            Directory.CreateDirectory(outputPath);
            
            var imagesDir = Path.Combine(outputPath, "images");
            var labelsDir = Path.Combine(outputPath, "labelTxt");
            
            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(labelsDir);
            
            // 创建标签映射
            var labelMap = CreateLabelMap(project.Labels.ToList());
            
            // 导出每张图像的标注
            foreach (var image in project.Images)
            {
                await ExportImageAnnotationsDota(image, labelsDir, labelMap);
                
                // 复制图像文件到输出目录
                if (File.Exists(image.FilePath))
                {
                    var destPath = Path.Combine(imagesDir, Path.GetFileName(image.FilePath));
                    File.Copy(image.FilePath, destPath, true);
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DOTA导出失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 导出单张图像的YOLO OBB标注
    /// </summary>
    private static async Task ExportImageAnnotationsYoloObb(AnnotationImage image, string outputPath, Dictionary<string, int> labelMap)
    {
        var fileName = Path.GetFileNameWithoutExtension(image.FileName);
        var labelFile = Path.Combine(outputPath, $"{fileName}.txt");
        
        var lines = new List<string>();
        
        foreach (var annotation in image.Annotations)
        {
            string? line = null;
            
            if (annotation is OrientedBoundingBoxAnnotation obb)
            {
                line = obb.ToYoloObbFormat(image.Width, image.Height, labelMap);
            }
            else if (annotation is RectangleAnnotation rect)
            {
                // 将普通矩形转换为OBB格式
                line = ConvertRectangleToYoloObb(rect, image.Width, image.Height, labelMap);
            }
            
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }
        
        await File.WriteAllLinesAsync(labelFile, lines);
    }

    /// <summary>
    /// 导出单张图像的DOTA标注
    /// </summary>
    private static async Task ExportImageAnnotationsDota(AnnotationImage image, string outputPath, Dictionary<string, int> labelMap)
    {
        var fileName = Path.GetFileNameWithoutExtension(image.FileName);
        var labelFile = Path.Combine(outputPath, $"{fileName}.txt");
        
        var lines = new List<string>();
        
        foreach (var annotation in image.Annotations)
        {
            string? line = null;
            
            if (annotation is OrientedBoundingBoxAnnotation obb)
            {
                line = obb.ToDotaFormat(CreateStringLabelMap(labelMap));
            }
            else if (annotation is RectangleAnnotation rect)
            {
                // 将普通矩形转换为DOTA格式
                line = ConvertRectangleToDota(rect, CreateStringLabelMap(labelMap));
            }
            
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }
        
        await File.WriteAllLinesAsync(labelFile, lines);
    }

    /// <summary>
    /// 将矩形标注转换为YOLO OBB格式
    /// </summary>
    private static string ConvertRectangleToYoloObb(RectangleAnnotation rect, int imageWidth, int imageHeight, Dictionary<string, int> labelMap)
    {
        if (!labelMap.TryGetValue(rect.Label, out int classId))
        {
            classId = 0;
        }
        
        var centerX = (rect.TopLeft.X + rect.BottomRight.X) / 2.0;
        var centerY = (rect.TopLeft.Y + rect.BottomRight.Y) / 2.0;
        var width = Math.Abs(rect.BottomRight.X - rect.TopLeft.X);
        var height = Math.Abs(rect.BottomRight.Y - rect.TopLeft.Y);
        
        // 归一化坐标
        var normalizedCenterX = centerX / imageWidth;
        var normalizedCenterY = centerY / imageHeight;
        var normalizedWidth = width / imageWidth;
        var normalizedHeight = height / imageHeight;
        var normalizedAngle = 0.0; // 矩形角度为0
        
        return $"{classId} {normalizedCenterX:F6} {normalizedCenterY:F6} {normalizedWidth:F6} {normalizedHeight:F6} {normalizedAngle:F6}";
    }

    /// <summary>
    /// 将矩形标注转换为DOTA格式
    /// </summary>
    private static string ConvertRectangleToDota(RectangleAnnotation rect, Dictionary<string, string> labelMap)
    {
        var category = labelMap.ContainsKey(rect.Label) ? rect.Label : "unknown";
        
        // 获取四个角点
        var x1 = rect.TopLeft.X;
        var y1 = rect.TopLeft.Y;
        var x2 = rect.BottomRight.X;
        var y2 = rect.TopLeft.Y;
        var x3 = rect.BottomRight.X;
        var y3 = rect.BottomRight.Y;
        var x4 = rect.TopLeft.X;
        var y4 = rect.BottomRight.Y;
        
        return $"{x1:F1} {y1:F1} {x2:F1} {y2:F1} {x3:F1} {y3:F1} {x4:F1} {y4:F1} {category} 0";
    }

    /// <summary>
    /// 创建标签映射
    /// </summary>
    private static Dictionary<string, int> CreateLabelMap(List<string> labels)
    {
        var labelMap = new Dictionary<string, int>();
        for (int i = 0; i < labels.Count; i++)
        {
            labelMap[labels[i]] = i;
        }
        return labelMap;
    }

    /// <summary>
    /// 创建字符串标签映射
    /// </summary>
    private static Dictionary<string, string> CreateStringLabelMap(Dictionary<string, int> intMap)
    {
        return intMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Key);
    }

    /// <summary>
    /// 导出类别文件
    /// </summary>
    private static async Task ExportClassesFileAsync(string filePath, List<string> labels)
    {
        await File.WriteAllLinesAsync(filePath, labels);
    }

    /// <summary>
    /// 创建YOLO OBB配置文件
    /// </summary>
    private static async Task CreateYoloObbConfigAsync(string outputPath, List<string> labels)
    {
        var configContent = $@"# YOLO OBB Dataset Configuration
# Generated by AIlable

# Dataset paths
path: {outputPath}
train: images
val: images

# Classes
nc: {labels.Count}
names: [{string.Join(", ", labels.Select(l => $"'{l}'"))}]

# Task type
task: obb  # Oriented Bounding Box detection
";
        
        var configFile = Path.Combine(outputPath, "data.yaml");
        await File.WriteAllTextAsync(configFile, configContent);
    }

    /// <summary>
    /// 验证OBB标注数据
    /// </summary>
    public static bool ValidateObbAnnotations(AnnotationProject project)
    {
        foreach (var image in project.Images)
        {
            foreach (var annotation in image.Annotations)
            {
                if (annotation is OrientedBoundingBoxAnnotation obb)
                {
                    // 检查OBB参数是否有效
                    if (obb.Width <= 0 || obb.Height <= 0)
                    {
                        Console.WriteLine($"无效的OBB尺寸: {obb.Width}x{obb.Height}");
                        return false;
                    }
                    
                    if (obb.CenterX < 0 || obb.CenterY < 0 || 
                        obb.CenterX > image.Width || obb.CenterY > image.Height)
                    {
                        Console.WriteLine($"OBB中心点超出图像范围: ({obb.CenterX}, {obb.CenterY})");
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
}
