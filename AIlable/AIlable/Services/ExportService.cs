using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIlable.Models;

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
                    // Get or create category
                    if (!categoryIdMap.ContainsKey(annotation.Label))
                    {
                        categoryIdMap[annotation.Label] = categoryIdCounter++;
                        cocoDataset.Categories.Add(new CocoCategory
                        {
                            Id = categoryIdMap[annotation.Label],
                            Name = annotation.Label,
                            SuperCategory = "object"
                        });
                    }

                    var cocoAnnotation = new CocoAnnotation
                    {
                        Id = annotationIdCounter++,
                        ImageId = imageIdCounter,
                        CategoryId = categoryIdMap[annotation.Label],
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

    public static async Task<bool> ExportToYoloAsync(AnnotationProject project, string outputDirectory, bool useSegmentationFormat = true)
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

            // Create classes.txt
            var classNames = project.GetUsedLabels().OrderBy(x => x).ToList();
            var classesPath = Path.Combine(outputDirectory, "classes.txt");
            await File.WriteAllLinesAsync(classesPath, classNames);

            // Create YAML configuration file
            await CreateYoloYamlAsync(outputDirectory, classNames, useSegmentationFormat);

            // Process each image (only images with annotations)
            foreach (var image in project.Images)
            {
                // Skip images without annotations
                if (image.Annotations == null || !image.Annotations.Any())
                {
                    continue;
                }

                // For now, put all images in train directory (can be enhanced later with train/val split)
                var labelFileName = Path.ChangeExtension(image.FileName, ".txt");
                var labelPath = Path.Combine(labelsTrainDir, labelFileName);
                var labelLines = new List<string>();

                foreach (var annotation in image.Annotations)
                {
                    var classIndex = classNames.IndexOf(annotation.Label);
                    if (classIndex < 0) continue;

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
                    }
                }

                await File.WriteAllLinesAsync(labelPath, labelLines);

                // Copy image to images/train directory
                var imagePath = Path.Combine(imagesTrainDir, image.FileName);
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
            Console.WriteLine($"Error exporting to YOLO format: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
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
                    VocObject vocObject = null;

                    switch (annotation)
                    {
                        case RectangleAnnotation rect:
                            vocObject = new VocObject
                            {
                                Name = annotation.Label,
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
                                Name = annotation.Label,
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
                                Name = annotation.Label,
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
        var formatNote = useSegmentationFormat
            ? "# Format: Segmentation (polygon vertices)"
            : "# Format: Bounding boxes";

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

        var yamlPath = Path.Combine(outputDirectory, "data.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);
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