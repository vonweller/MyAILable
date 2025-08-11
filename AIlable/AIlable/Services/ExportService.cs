using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIlable.Models;
using AIlable.ViewModels;

namespace AIlable.Services;

// COCO格式数据结构
public class CocoDataset
{
    [JsonPropertyName("info")]
    public CocoInfo Info { get; set; } = new();
    
    [JsonPropertyName("licenses")]
    public List<CocoLicense> Licenses { get; set; } = new();
    
    [JsonPropertyName("images")]
    public List<CocoImage> Images { get; set; } = new();
    
    [JsonPropertyName("annotations")]
    public List<CocoAnnotation> Annotations { get; set; } = new();
    
    [JsonPropertyName("categories")]
    public List<CocoCategory> Categories { get; set; } = new();
}

public class CocoInfo
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "AIlable Dataset";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    [JsonPropertyName("year")]
    public int Year { get; set; } = DateTime.Now.Year;
    
    [JsonPropertyName("contributor")]
    public string Contributor { get; set; } = "AIlable";
    
    [JsonPropertyName("date_created")]
    public string DateCreated { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
}

public class CocoLicense
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

public class CocoImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = "";
    
    [JsonPropertyName("license")]
    public int License { get; set; }
    
    [JsonPropertyName("flickr_url")]
    public string FlickrUrl { get; set; } = "";
    
    [JsonPropertyName("coco_url")]
    public string CocoUrl { get; set; } = "";
    
    [JsonPropertyName("date_captured")]
    public string DateCaptured { get; set; } = "";
}

public class CocoAnnotation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("image_id")]
    public int ImageId { get; set; }
    
    [JsonPropertyName("category_id")]
    public int CategoryId { get; set; }
    
    [JsonPropertyName("segmentation")]
    public List<List<double>> Segmentation { get; set; } = new();
    
    [JsonPropertyName("area")]
    public double Area { get; set; }
    
    [JsonPropertyName("bbox")]
    public List<double> Bbox { get; set; } = new();
    
    [JsonPropertyName("iscrowd")]
    public int IsCrowd { get; set; }
}

public class CocoCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("supercategory")]
    public string SuperCategory { get; set; } = "";
}

// VOC格式数据结构
public class VocAnnotation
{
    public string Folder { get; set; } = "";
    public string Filename { get; set; } = "";
    public string Path { get; set; } = "";
    public VocSource Source { get; set; } = new();
    public VocSize Size { get; set; } = new();
    public int Segmented { get; set; }
    public List<VocObject> Objects { get; set; } = new();
}

public class VocSource
{
    public string Database { get; set; } = "AIlable";
    public string Annotation { get; set; } = "AIlable";
    public string Image { get; set; } = "AIlable";
}

public class VocSize
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; } = 3;
}

public class VocObject
{
    public string Name { get; set; } = "";
    public string Pose { get; set; } = "Unspecified";
    public int Truncated { get; set; }
    public int Difficult { get; set; }
    public VocBndBox BndBox { get; set; } = new();
}

public class VocBndBox
{
    public int Xmin { get; set; }
    public int Ymin { get; set; }
    public int Xmax { get; set; }
    public int Ymax { get; set; }
}

public static class ExportService
{
    public static async Task<bool> ExportToCocoAsync(AnnotationProject project, string outputPath)
    {
        try
        {
            if (project == null)
            {
                Console.WriteLine("Project is null");
                return false;
            }

            if (project.Images == null || !project.Images.Any())
            {
                Console.WriteLine("No images in project");
                return false;
            }

            // Create COCO dataset structure
            var cocoDataset = new CocoDataset
            {
                Info = new CocoInfo
                {
                    Description = $"Dataset exported from AIlable - {project.Name}",
                    Url = "",
                    Version = "1.0",
                    Year = DateTime.Now.Year,
                    Contributor = "AIlable",
                    DateCreated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
                },
                Licenses = new List<CocoLicense>
                {
                    new CocoLicense
                    {
                        Id = 1,
                        Name = "Unknown",
                        Url = ""
                    }
                }
            };

            var imageIdCounter = 1;
            var annotationIdCounter = 1;
            var categoryIdMap = new Dictionary<string, int>();
            var categoryIdCounter = 1;

            // Process each image (only images with annotations)
            foreach (var image in project.Images)
            {
                // Skip images without annotations
                if (image.Annotations == null || !image.Annotations.Any())
                {
                    continue;
                }

                var cocoImage = new CocoImage
                {
                    Id = imageIdCounter,
                    License = 1,
                    Width = image.Width,
                    Height = image.Height,
                    FileName = image.FileName,
                    DateCaptured = image.CreatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz")
                };
                cocoDataset.Images.Add(cocoImage);

                // Process annotations for this image
                foreach (var annotation in image.Annotations)
                {
                    // 从标注标签中提取纯净的标签名称（去除置信度分数）
                    var cleanLabel = ExtractCleanLabel(annotation.Label);
                    
                    // Get or create category
                    if (!categoryIdMap.ContainsKey(cleanLabel))
                    {
                        categoryIdMap[cleanLabel] = categoryIdCounter++;
                        cocoDataset.Categories.Add(new CocoCategory
                        {
                            Id = categoryIdMap[cleanLabel],
                            Name = cleanLabel,
                            SuperCategory = "object"
                        });
                    }

                    var cocoAnnotation = new CocoAnnotation
                    {
                        Id = annotationIdCounter++,
                        ImageId = imageIdCounter,
                        CategoryId = categoryIdMap[cleanLabel],
                        Area = annotation.GetArea(),
                        IsCrowd = 0
                    };

                    // Convert annotation to COCO format
                    switch (annotation)
                    {
                        case RectangleAnnotation rect:
                            cocoAnnotation.Bbox = new List<double>
                            {
                                Math.Min(rect.TopLeft.X, rect.BottomRight.X),
                                Math.Min(rect.TopLeft.Y, rect.BottomRight.Y),
                                Math.Abs(rect.BottomRight.X - rect.TopLeft.X),
                                Math.Abs(rect.BottomRight.Y - rect.TopLeft.Y)
                            };
                            cocoAnnotation.Segmentation.Add(new List<double>
                            {
                                rect.TopLeft.X, rect.TopLeft.Y,
                                rect.BottomRight.X, rect.TopLeft.Y,
                                rect.BottomRight.X, rect.BottomRight.Y,
                                rect.TopLeft.X, rect.BottomRight.Y
                            });
                            break;

                        case PolygonAnnotation polygon:
                            var segmentation = new List<double>();
                            foreach (var vertex in polygon.Vertices)
                            {
                                segmentation.Add(vertex.X);
                                segmentation.Add(vertex.Y);
                            }
                            cocoAnnotation.Segmentation.Add(segmentation);
                            
                            // Calculate bounding box
                            var minX = polygon.Vertices.Min(v => v.X);
                            var minY = polygon.Vertices.Min(v => v.Y);
                            var maxX = polygon.Vertices.Max(v => v.X);
                            var maxY = polygon.Vertices.Max(v => v.Y);
                            cocoAnnotation.Bbox = new List<double> { minX, minY, maxX - minX, maxY - minY };
                            break;

                        case CircleAnnotation circle:
                            // Convert circle to polygon approximation
                            var circleSegmentation = new List<double>();
                            const int segments = 32;
                            for (int i = 0; i < segments; i++)
                            {
                                var angle = 2 * Math.PI * i / segments;
                                var x = circle.Center.X + circle.Radius * Math.Cos(angle);
                                var y = circle.Center.Y + circle.Radius * Math.Sin(angle);
                                circleSegmentation.Add(x);
                                circleSegmentation.Add(y);
                            }
                            cocoAnnotation.Segmentation.Add(circleSegmentation);
                            cocoAnnotation.Bbox = new List<double>
                            {
                                circle.Center.X - circle.Radius,
                                circle.Center.Y - circle.Radius,
                                circle.Radius * 2,
                                circle.Radius * 2
                            };
                            break;

                        case OrientedBoundingBoxAnnotation obb:
                            // Convert OBB to polygon segmentation using corner points
                            var obbSegmentation = new List<double>();
                            var obbPoints = obb.GetPoints();
                            foreach (var point in obbPoints)
                            {
                                obbSegmentation.Add(point.X);
                                obbSegmentation.Add(point.Y);
                            }
                            cocoAnnotation.Segmentation.Add(obbSegmentation);
                            
                            // Calculate axis-aligned bounding box from OBB points
                            var obbMinX = obbPoints.Min(p => p.X);
                            var obbMinY = obbPoints.Min(p => p.Y);
                            var obbMaxX = obbPoints.Max(p => p.X);
                            var obbMaxY = obbPoints.Max(p => p.Y);
                            cocoAnnotation.Bbox = new List<double> { obbMinX, obbMinY, obbMaxX - obbMinX, obbMaxY - obbMinY };
                            break;

                        case LineAnnotation line:
                            // Convert line to minimal polygon
                            var lineSegmentation = new List<double>
                            {
                                line.StartPoint.X, line.StartPoint.Y,
                                line.EndPoint.X, line.EndPoint.Y,
                                line.EndPoint.X + 1, line.EndPoint.Y + 1,
                                line.StartPoint.X + 1, line.StartPoint.Y + 1
                            };
                            cocoAnnotation.Segmentation.Add(lineSegmentation);
                            
                            // Calculate bounding box
                            var lineMinX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                            var lineMinY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                            var lineMaxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                            var lineMaxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);
                            
                            // Ensure minimum bounding box size
                            if (lineMaxX - lineMinX < 1.0) lineMaxX = lineMinX + 1.0;
                            if (lineMaxY - lineMinY < 1.0) lineMaxY = lineMinY + 1.0;
                            
                            cocoAnnotation.Bbox = new List<double> { lineMinX, lineMinY, lineMaxX - lineMinX, lineMaxY - lineMinY };
                            break;

                        case PointAnnotation point:
                            // Convert point to small circle approximation
                            var pointSegmentation = new List<double>();
                            var pointRadius = point.Size / 2.0;
                            const int pointSegments = 8;
                            for (int i = 0; i < pointSegments; i++)
                            {
                                var angle = 2 * Math.PI * i / pointSegments;
                                var x = point.Position.X + pointRadius * Math.Cos(angle);
                                var y = point.Position.Y + pointRadius * Math.Sin(angle);
                                pointSegmentation.Add(x);
                                pointSegmentation.Add(y);
                            }
                            cocoAnnotation.Segmentation.Add(pointSegmentation);
                            cocoAnnotation.Bbox = new List<double>
                            {
                                point.Position.X - pointRadius,
                                point.Position.Y - pointRadius,
                                pointRadius * 2,
                                pointRadius * 2
                            };
                            break;

                        case KeypointAnnotation keypoint:
                            // COCO关键点格式
                            var keypointData = new List<double>();
                            var visibleKeypoints = keypoint.Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
                            
                            // 添加所有17个关键点的坐标和可见性
                            foreach (var kp in keypoint.Keypoints)
                            {
                                keypointData.Add(kp.Position.X);
                                keypointData.Add(kp.Position.Y);
                                keypointData.Add((double)kp.Visibility);
                            }
                            
                            // COCO格式的关键点数据存储在keypoints字段中，但由于我们使用通用CocoAnnotation类，
                            // 这里我们将关键点数据存储在segmentation中作为特殊格式
                            cocoAnnotation.Segmentation.Add(keypointData);
                            
                            // 计算关键点的边界框
                            if (visibleKeypoints.Any())
                            {
                                var keypointMinX = visibleKeypoints.Min(k => k.Position.X);
                                var keypointMinY = visibleKeypoints.Min(k => k.Position.Y);
                                var keypointMaxX = visibleKeypoints.Max(k => k.Position.X);
                                var keypointMaxY = visibleKeypoints.Max(k => k.Position.Y);
                                cocoAnnotation.Bbox = new List<double> { keypointMinX, keypointMinY, keypointMaxX - keypointMinX, keypointMaxY - keypointMinY };
                            }
                            else
                            {
                                cocoAnnotation.Bbox = new List<double> { 0, 0, 0, 0 };
                            }
                            
                            // 设置关键点的面积和数量信息
                            cocoAnnotation.Area = keypoint.GetArea();
                            break;
                    }

                    cocoDataset.Annotations.Add(cocoAnnotation);
                }

                imageIdCounter++;
            }

            // Save to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(cocoDataset, options);
            var jsonFilePath = Path.Combine(outputPath, "annotations.json");
            await File.WriteAllTextAsync(jsonFilePath, json);

            // Copy images to output directory
            var imagesDir = Path.Combine(outputPath, "images");
            Directory.CreateDirectory(imagesDir);

            foreach (var image in project.Images)
            {
                // Skip images without annotations
                if (image.Annotations == null || !image.Annotations.Any())
                {
                    continue;
                }

                var imagePath = Path.Combine(imagesDir, image.FileName);
                if (File.Exists(image.FilePath))
                {
                    File.Copy(image.FilePath, imagePath, true);
                }
                else
                {
                    Console.WriteLine($"Warning: Source image not found: {image.FilePath}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting to COCO format: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public static async Task<bool> ExportToYoloAsync(AnnotationProject project, string outputDirectory, YoloExportFormat format, float trainRatio = 0.8f)
    {
        // 为了向后兼容，将新格式转换为原来的参数
        bool useSegmentationFormat = format == YoloExportFormat.Segmentation;
        return await ExportToYoloAsync(project, outputDirectory, useSegmentationFormat, trainRatio);
    }

    public static async Task<bool> ExportToYoloAsync(AnnotationProject project, string outputDirectory, bool useSegmentationFormat = true, float trainRatio = 0.8f)
    {
        try
        {
            if (project == null)
            {
                Console.WriteLine("Project is null");
                return false;
            }

            if (project.Images == null || !project.Images.Any())
            {
                Console.WriteLine("No images in project");
                return false;
            }

            // Create directories following YOLO official structure
            var imagesTrainDir = Path.Combine(outputDirectory, "images", "train");
            var imagesValDir = Path.Combine(outputDirectory, "images", "val");
            var labelsTrainDir = Path.Combine(outputDirectory, "labels", "train");
            var labelsValDir = Path.Combine(outputDirectory, "labels", "val");

            Directory.CreateDirectory(imagesTrainDir);
            Directory.CreateDirectory(imagesValDir);
            Directory.CreateDirectory(labelsTrainDir);
            Directory.CreateDirectory(labelsValDir);

            // Create classes.txt - 使用项目定义的标签，而不是从标注中提取的标签（可能包含置信度分数）
            var classNames = project.Labels.Where(label => !string.IsNullOrWhiteSpace(label)).OrderBy(x => x).ToList();
            var classesPath = Path.Combine(outputDirectory, "classes.txt");
            await File.WriteAllLinesAsync(classesPath, classNames);

            // Create YAML configuration file
            await CreateYoloYamlAsync(outputDirectory, classNames, useSegmentationFormat);

            // 过滤有标注的图像
            var imagesWithAnnotations = project.Images.Where(img => img.Annotations != null && img.Annotations.Any()).ToList();
            
            if (!imagesWithAnnotations.Any())
            {
                Console.WriteLine("No images with annotations found");
                return false;
            }

            // 随机打乱图像顺序以确保train/val分割的随机性
            var random = new Random(42); // 使用固定种子确保结果可重复
            var shuffledImages = imagesWithAnnotations.OrderBy(x => random.Next()).ToList();
            
            // 按比例分割train/val
            var trainCount = (int)(shuffledImages.Count * trainRatio);
            var trainImages = shuffledImages.Take(trainCount).ToList();
            var valImages = shuffledImages.Skip(trainCount).ToList();
            
            Console.WriteLine($"数据集分割: 总共 {shuffledImages.Count} 张图像");
            Console.WriteLine($"训练集: {trainImages.Count} 张图像 ({trainRatio*100:F1}%)");
            Console.WriteLine($"验证集: {valImages.Count} 张图像 ({(1-trainRatio)*100:F1}%)");

            // 处理训练集
            await ProcessImageSet(trainImages, imagesTrainDir, labelsTrainDir, classNames, useSegmentationFormat, "train");
            
            // 处理验证集
            await ProcessImageSet(valImages, imagesValDir, labelsValDir, classNames, useSegmentationFormat, "val");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting to YOLO format: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    
    /// <summary>
    /// 处理图像集合（训练集或验证集）
    /// </summary>
    private static async Task ProcessImageSet(List<AnnotationImage> images, string imagesDir, string labelsDir, 
        List<string> classNames, bool useSegmentationFormat, string setName)
    {
        foreach (var image in images)
        {
            var labelFileName = Path.ChangeExtension(image.FileName, ".txt");
            var labelPath = Path.Combine(labelsDir, labelFileName);
            var labelLines = new List<string>();

            foreach (var annotation in image.Annotations)
            {
                // 从标注标签中提取纯净的标签名称（去除置信度分数）
                var cleanLabel = ExtractCleanLabel(annotation.Label);
                var classIndex = classNames.IndexOf(cleanLabel);
                if (classIndex < 0) 
                {
                    Console.WriteLine($"Warning: Label '{annotation.Label}' (clean: '{cleanLabel}') not found in class names. Skipping annotation.");
                    continue;
                }

                switch (annotation)
                {
                    case RectangleAnnotation rect:
                        // Convert to YOLO format (normalized coordinates)
                        var centerX = (rect.TopLeft.X + rect.BottomRight.X) / 2.0 / image.Width;
                        var centerY = (rect.TopLeft.Y + rect.BottomRight.Y) / 2.0 / image.Height;
                        var width = Math.Abs(rect.BottomRight.X - rect.TopLeft.X) / image.Width;
                        var height = Math.Abs(rect.BottomRight.Y - rect.TopLeft.Y) / image.Height;

                        labelLines.Add($"{classIndex} {centerX:F6} {centerY:F6} {width:F6} {height:F6}");
                        break;

                    case PolygonAnnotation polygon:
                        if (useSegmentationFormat && polygon.Vertices.Count >= 3)
                        {
                            // YOLO segmentation format: class_id x1 y1 x2 y2 x3 y3 ... xn yn
                            // All coordinates are normalized (0-1)
                            var polygonLine = $"{classIndex}";
                            foreach (var vertex in polygon.Vertices)
                            {
                                var normalizedX = vertex.X / image.Width;
                                var normalizedY = vertex.Y / image.Height;

                                // Clamp values to [0, 1] range
                                normalizedX = Math.Max(0, Math.Min(1, normalizedX));
                                normalizedY = Math.Max(0, Math.Min(1, normalizedY));

                                polygonLine += $" {normalizedX:F6} {normalizedY:F6}";
                            }
                            labelLines.Add(polygonLine);
                        }
                        else
                        {
                            // Fallback to bounding box format for compatibility
                            var minX = polygon.Vertices.Min(v => v.X);
                            var minY = polygon.Vertices.Min(v => v.Y);
                            var maxX = polygon.Vertices.Max(v => v.X);
                            var maxY = polygon.Vertices.Max(v => v.Y);

                            var polyCenterX = (minX + maxX) / 2.0 / image.Width;
                            var polyCenterY = (minY + maxY) / 2.0 / image.Height;
                            var polyWidth = (maxX - minX) / image.Width;
                            var polyHeight = (maxY - minY) / image.Height;

                            labelLines.Add($"{classIndex} {polyCenterX:F6} {polyCenterY:F6} {polyWidth:F6} {polyHeight:F6}");
                        }
                        break;

                    case CircleAnnotation circle:
                        // Convert circle to bounding box
                        var circleMinX = circle.Center.X - circle.Radius;
                        var circleMinY = circle.Center.Y - circle.Radius;
                        var circleMaxX = circle.Center.X + circle.Radius;
                        var circleMaxY = circle.Center.Y + circle.Radius;

                        var circleCenterX = circle.Center.X / image.Width;
                        var circleCenterY = circle.Center.Y / image.Height;
                        var circleWidth = (circle.Radius * 2) / image.Width;
                        var circleHeight = (circle.Radius * 2) / image.Height;

                        labelLines.Add($"{classIndex} {circleCenterX:F6} {circleCenterY:F6} {circleWidth:F6} {circleHeight:F6}");
                        break;

                    case OrientedBoundingBoxAnnotation obb:
                        // 使用YOLO OBB格式: class_id center_x center_y width height angle
                        var obbCenterX = obb.CenterX / image.Width;
                        var obbCenterY = obb.CenterY / image.Height;
                        var obbWidth = obb.Width / image.Width;
                        var obbHeight = obb.Height / image.Height;
                        var obbAngle = obb.Angle / 180.0; // 归一化角度到0-2范围

                        labelLines.Add($"{classIndex} {obbCenterX:F6} {obbCenterY:F6} {obbWidth:F6} {obbHeight:F6} {obbAngle:F6}");
                        break;

                    case LineAnnotation line:
                        // Convert line to bounding box (for YOLO compatibility)
                        var lineMinX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                        var lineMinY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                        var lineMaxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                        var lineMaxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);
                        
                        // Ensure minimum bounding box size for very thin lines
                        if (lineMaxX - lineMinX < 1.0) lineMaxX = lineMinX + 1.0;
                        if (lineMaxY - lineMinY < 1.0) lineMaxY = lineMinY + 1.0;

                        var lineCenterX = (lineMinX + lineMaxX) / 2.0 / image.Width;
                        var lineCenterY = (lineMinY + lineMaxY) / 2.0 / image.Height;
                        var lineWidth = (lineMaxX - lineMinX) / image.Width;
                        var lineHeight = (lineMaxY - lineMinY) / image.Height;

                        labelLines.Add($"{classIndex} {lineCenterX:F6} {lineCenterY:F6} {lineWidth:F6} {lineHeight:F6}");
                        break;

                    case PointAnnotation point:
                        // Convert point to small bounding box (for YOLO compatibility) 
                        var pointSize = point.Size / 2.0; // radius
                        var pointCenterX = point.Position.X / image.Width;
                        var pointCenterY = point.Position.Y / image.Height;
                        var pointWidth = (pointSize * 2) / image.Width;
                        var pointHeight = (pointSize * 2) / image.Height;

                        labelLines.Add($"{classIndex} {pointCenterX:F6} {pointCenterY:F6} {pointWidth:F6} {pointHeight:F6}");
                        break;

                    case KeypointAnnotation keypoint:
                        // YOLO Pose格式: class_id x1 y1 v1 x2 y2 v2 ... x17 y17 v17
                        var keypointLine = keypoint.ToYoloPoseFormat(image.Width, image.Height, classNames.ToDictionary(name => name, name => classNames.IndexOf(name)));
                        labelLines.Add(keypointLine);
                        break;
                }
            }

            await File.WriteAllLinesAsync(labelPath, labelLines);

            // Copy image to appropriate directory
            var imagePath = Path.Combine(imagesDir, image.FileName);
            if (File.Exists(image.FilePath))
            {
                File.Copy(image.FilePath, imagePath, true);
            }
            else
            {
                Console.WriteLine($"Warning: Source image not found: {image.FilePath}");
            }
        }
        
        Console.WriteLine($"已处理 {setName} 集: {images.Count} 张图像");
    }

    /// <summary>
    /// 从包含置信度分数的标签中提取纯净的标签名称
    /// 例如: "lz (0.51)" -> "lz", "putong (0.82)" -> "putong"
    /// </summary>
    public static string ExtractCleanLabel(string labelWithConfidence)
    {
        if (string.IsNullOrWhiteSpace(labelWithConfidence))
            return string.Empty;
            
        // 查找括号的位置
        var parenIndex = labelWithConfidence.IndexOf('(');
        if (parenIndex > 0)
        {
            // 提取括号前的部分并去除空格
            return labelWithConfidence.Substring(0, parenIndex).Trim();
        }
        
        // 如果没有括号，返回原标签（去除前后空格）
        return labelWithConfidence.Trim();
    }

    public static async Task<bool> ExportToVocAsync(AnnotationProject project, string outputDirectory)
    {
        try
        {
            if (project == null)
            {
                Console.WriteLine("Project is null");
                return false;
            }

            if (project.Images == null || !project.Images.Any())
            {
                Console.WriteLine("No images in project");
                return false;
            }

            var annotationsDir = Path.Combine(outputDirectory, "Annotations");
            Directory.CreateDirectory(annotationsDir);

            foreach (var image in project.Images)
            {
                // Skip images without annotations
                if (image.Annotations == null || !image.Annotations.Any())
                {
                    continue;
                }

                var vocAnnotation = new VocAnnotation
                {
                    Folder = "images",
                    Filename = image.FileName,
                    Path = Path.Combine("images", image.FileName),
                    Source = new VocSource
                    {
                        Database = "AIlable",
                        Annotation = "AIlable",
                        Image = "AIlable"
                    },
                    Size = new VocSize
                    {
                        Width = image.Width,
                        Height = image.Height,
                        Depth = 3
                    },
                    Segmented = 0
                };

                foreach (var annotation in image.Annotations)
                {
                    // 从标注标签中提取纯净的标签名称（去除置信度分数）
                    var cleanLabel = ExtractCleanLabel(annotation.Label);
                    VocObject vocObject = null;

                    switch (annotation)
                    {
                        case RectangleAnnotation rect:
                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)Math.Min(rect.TopLeft.X, rect.BottomRight.X),
                                    Ymin = (int)Math.Min(rect.TopLeft.Y, rect.BottomRight.Y),
                                    Xmax = (int)Math.Max(rect.TopLeft.X, rect.BottomRight.X),
                                    Ymax = (int)Math.Max(rect.TopLeft.Y, rect.BottomRight.Y)
                                }
                            };
                            break;

                        case PolygonAnnotation polygon:
                            // Convert polygon to bounding box for VOC format
                            var minX = polygon.Vertices.Min(v => v.X);
                            var minY = polygon.Vertices.Min(v => v.Y);
                            var maxX = polygon.Vertices.Max(v => v.X);
                            var maxY = polygon.Vertices.Max(v => v.Y);

                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)minX,
                                    Ymin = (int)minY,
                                    Xmax = (int)maxX,
                                    Ymax = (int)maxY
                                }
                            };
                            break;

                        case CircleAnnotation circle:
                            // Convert circle to bounding box
                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)(circle.Center.X - circle.Radius),
                                    Ymin = (int)(circle.Center.Y - circle.Radius),
                                    Xmax = (int)(circle.Center.X + circle.Radius),
                                    Ymax = (int)(circle.Center.Y + circle.Radius)
                                }
                            };
                            break;

                        case OrientedBoundingBoxAnnotation obb:
                            // Convert OBB to axis-aligned bounding box for VOC format
                            var obbPoints = obb.GetPoints();
                            var obbMinX = obbPoints.Min(p => p.X);
                            var obbMinY = obbPoints.Min(p => p.Y);
                            var obbMaxX = obbPoints.Max(p => p.X);
                            var obbMaxY = obbPoints.Max(p => p.Y);

                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)obbMinX,
                                    Ymin = (int)obbMinY,
                                    Xmax = (int)obbMaxX,
                                    Ymax = (int)obbMaxY
                                }
                            };
                            break;

                        case LineAnnotation line:
                            // Convert line to bounding box for VOC format
                            var lineMinX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                            var lineMinY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                            var lineMaxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                            var lineMaxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);
                            
                            // Ensure minimum bounding box size
                            if (lineMaxX - lineMinX < 1.0) lineMaxX = lineMinX + 1.0;
                            if (lineMaxY - lineMinY < 1.0) lineMaxY = lineMinY + 1.0;

                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)lineMinX,
                                    Ymin = (int)lineMinY,
                                    Xmax = (int)lineMaxX,
                                    Ymax = (int)lineMaxY
                                }
                            };
                            break;

                        case PointAnnotation point:
                            // Convert point to small bounding box for VOC format
                            var pointRadius = point.Size / 2.0;
                            vocObject = new VocObject
                            {
                                Name = cleanLabel,
                                Pose = "Unspecified",
                                Truncated = 0,
                                Difficult = 0,
                                BndBox = new VocBndBox
                                {
                                    Xmin = (int)(point.Position.X - pointRadius),
                                    Ymin = (int)(point.Position.Y - pointRadius),
                                    Xmax = (int)(point.Position.X + pointRadius),
                                    Ymax = (int)(point.Position.Y + pointRadius)
                                }
                            };
                            break;

                        case KeypointAnnotation keypoint:
                            // VOC格式不直接支持关键点，将其转换为边界框
                            var visibleKeypointsVoc = keypoint.Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
                            if (visibleKeypointsVoc.Any())
                            {
                                var vocMinX = visibleKeypointsVoc.Min(k => k.Position.X);
                                var vocMinY = visibleKeypointsVoc.Min(k => k.Position.Y);
                                var vocMaxX = visibleKeypointsVoc.Max(k => k.Position.X);
                                var vocMaxY = visibleKeypointsVoc.Max(k => k.Position.Y);

                                vocObject = new VocObject
                                {
                                    Name = cleanLabel,
                                    Pose = "Person", // 姿态标注通常是人体
                                    Truncated = 0,
                                    Difficult = 0,
                                    BndBox = new VocBndBox
                                    {
                                        Xmin = (int)vocMinX,
                                        Ymin = (int)vocMinY,
                                        Xmax = (int)vocMaxX,
                                        Ymax = (int)vocMaxY
                                    }
                                };
                            }
                            break;
                    }

                    if (vocObject != null)
                    {
                        vocAnnotation.Objects.Add(vocObject);
                    }
                }

                // Generate XML
                var xmlFileName = Path.ChangeExtension(image.FileName, ".xml");
                var xmlPath = Path.Combine(annotationsDir, xmlFileName);
                await SaveVocXmlAsync(vocAnnotation, xmlPath);

                // Copy image to images directory
                var imagesDir = Path.Combine(outputDirectory, "images");
                Directory.CreateDirectory(imagesDir);
                var imagePath = Path.Combine(imagesDir, image.FileName);
                if (File.Exists(image.FilePath))
                {
                    File.Copy(image.FilePath, imagePath, true);
                }
                else
                {
                    Console.WriteLine($"Warning: Source image not found: {image.FilePath}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting to VOC format: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private static async Task SaveVocXmlAsync(VocAnnotation vocAnnotation, string filePath)
    {
        var xml = $@"<annotation>
    <folder>{vocAnnotation.Folder}</folder>
    <filename>{vocAnnotation.Filename}</filename>
    <path>{vocAnnotation.Path}</path>
    <source>
        <database>{vocAnnotation.Source.Database}</database>
        <annotation>{vocAnnotation.Source.Annotation}</annotation>
        <image>{vocAnnotation.Source.Image}</image>
    </source>
    <size>
        <width>{vocAnnotation.Size.Width}</width>
        <height>{vocAnnotation.Size.Height}</height>
        <depth>{vocAnnotation.Size.Depth}</depth>
    </size>
    <segmented>{vocAnnotation.Segmented}</segmented>";

        foreach (var obj in vocAnnotation.Objects)
        {
            xml += $@"
    <object>
        <name>{obj.Name}</name>
        <pose>{obj.Pose}</pose>
        <truncated>{obj.Truncated}</truncated>
        <difficult>{obj.Difficult}</difficult>
        <bndbox>
            <xmin>{obj.BndBox.Xmin}</xmin>
            <ymin>{obj.BndBox.Ymin}</ymin>
            <xmax>{obj.BndBox.Xmax}</xmax>
            <ymax>{obj.BndBox.Ymax}</ymax>
        </bndbox>
    </object>";
        }

        xml += "\n</annotation>";

        await File.WriteAllTextAsync(filePath, xml);
    }

    private static async Task CreateYoloYamlAsync(string outputDirectory, List<string> classNames, bool useSegmentationFormat = true)
    {
        // 检查是否包含姿态标注
        var yamlContent = await GenerateYamlContentAsync(outputDirectory, classNames, useSegmentationFormat);
        
        var yamlPath = Path.Combine(outputDirectory, "data.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);
    }
    
    private static async Task<string> GenerateYamlContentAsync(string outputDirectory, List<string> classNames, bool useSegmentationFormat = true)
    {
        // 检查是否有姿态标注（通过检查labels文件）
        bool hasPoseAnnotations = await CheckForPoseAnnotationsAsync(outputDirectory);
        
        string formatNote;
        if (hasPoseAnnotations)
        {
            formatNote = "# Format: Pose estimation (bounding box + keypoints)";
        }
        else if (useSegmentationFormat)
        {
            formatNote = "# Format: Segmentation (polygon vertices)";
        }
        else
        {
            formatNote = "# Format: Bounding boxes";
        }

        var yamlContent = $@"# YOLO dataset configuration
# Generated by AIlable
{formatNote}

# Dataset paths (relative to this file)
path: .  # dataset root dir
train: images/train  # train images (relative to 'path')
val: images/val      # val images (relative to 'path')
test:                # test images (optional)

# Classes
nc: {classNames.Count}  # number of classes
names:
";

        // Add class names in proper YAML list format
        for (int i = 0; i < classNames.Count; i++)
        {
            yamlContent += $"  {i}: {classNames[i]}\n";
        }

        // Add pose-specific configuration if needed
        if (hasPoseAnnotations)
        {
            yamlContent += @"
# Keypoint configuration for pose estimation
kpt_shape: [17, 3]  # COCO format: 17 keypoints, 3 dims (x, y, visibility)
flip_idx: [0, 2, 1, 4, 3, 6, 5, 8, 7, 10, 9, 12, 11, 14, 13, 16, 15]  # left-right keypoint pairs for data augmentation

# COCO 17 keypoint names (for reference)
keypoint_names:
  - nose
  - left_eye
  - right_eye
  - left_ear
  - right_ear
  - left_shoulder
  - right_shoulder
  - left_elbow
  - right_elbow
  - left_wrist
  - right_wrist
  - left_hip
  - right_hip
  - left_knee
  - right_knee
  - left_ankle
  - right_ankle
";
        }

        return yamlContent;
    }
    
    private static async Task<bool> CheckForPoseAnnotationsAsync(string outputDirectory)
    {
        try
        {
            var labelsTrainDir = Path.Combine(outputDirectory, "labels", "train");
            var labelsValDir = Path.Combine(outputDirectory, "labels", "val");
            
            var labelDirs = new[] { labelsTrainDir, labelsValDir }.Where(Directory.Exists);
            
            foreach (var labelDir in labelDirs)
            {
                var labelFiles = Directory.GetFiles(labelDir, "*.txt").Take(5); // 检查前5个文件
                
                foreach (var labelFile in labelFiles)
                {
                    var lines = await File.ReadAllLinesAsync(labelFile);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        // YOLO Pose format: class_id bbox(4) + keypoints(51) = 56 parts total
                        // class_id(1) + bbox(4) + 17 keypoints * 3(x,y,v) = 56
                        if (parts.Length == 56)
                        {
                            return true; // 发现姿态标注
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to check for pose annotations: {ex.Message}");
        }
        
        return false;
    }

    public static async Task<bool> ExportProjectAsync(AnnotationProject project, string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(project, options);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting project: {ex.Message}");
            return false;
        }
    }
}