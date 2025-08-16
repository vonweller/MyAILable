# AIlable项目 - 二次开发指南

## 概述

本指南面向希望基于AIlable项目进行二次开发的开发者，提供详细的功能扩展指南、自定义开发教程和最佳实践建议。AIlable采用模块化设计，支持灵活的功能扩展和定制。

## 快速开始

### 1. 开发环境准备

#### 必需软件
```bash
# .NET SDK 9.0+
dotnet --version

# Git
git --version

# 推荐IDE之一
# - Visual Studio 2022 (Windows)
# - JetBrains Rider (跨平台)
# - VS Code + C# Extension (跨平台)
```

#### 项目克隆和初始化
```bash
# 克隆项目
git clone <your-fork-url>
cd AIlable

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet test

# 启动桌面版本
dotnet run --project AIlable.Desktop
```

### 2. 项目结构理解

```
AIlable/
├── AIlable/                    # 核心库项目
│   ├── Models/                 # 数据模型
│   ├── ViewModels/            # 视图模型
│   ├── Views/                 # 用户界面
│   ├── Services/              # 业务服务
│   ├── Controls/              # 自定义控件
│   ├── Styles/                # 样式和主题
│   └── Assets/                # 资源文件
├── AIlable.Desktop/           # 桌面平台项目
├── AIlable.Android/           # Android平台项目
├── AIlable.iOS/               # iOS平台项目
├── AIlable.Browser/           # Web平台项目
└── docs/                      # 项目文档
```

### 3. 核心概念理解

#### MVVM架构模式
- **Model**: 数据模型和业务逻辑
- **View**: 用户界面（AXAML文件）
- **ViewModel**: 视图逻辑和数据绑定

#### 依赖注入容器
- 服务注册在 `Program.cs` 中完成
- 使用接口抽象实现松耦合
- 支持单例、瞬态、作用域生命周期

#### 服务层架构
- **AI服务**: 模型推理和管理
- **业务服务**: 项目管理、导出等
- **基础服务**: 配置、通知、主题等

## 功能扩展指南

### 1. 添加新的标注类型

#### 步骤1: 创建标注模型

```csharp
// Models/EllipseAnnotation.cs
using System;
using Avalonia;

namespace AIlable.Models
{
    public class EllipseAnnotation : Annotation
    {
        public override AnnotationType Type => AnnotationType.Ellipse;
        
        /// <summary>
        /// 椭圆中心点
        /// </summary>
        public Point2D Center { get; set; } = new();
        
        /// <summary>
        /// 水平半径
        /// </summary>
        public double RadiusX { get; set; }
        
        /// <summary>
        /// 垂直半径
        /// </summary>
        public double RadiusY { get; set; }
        
        /// <summary>
        /// 旋转角度（弧度）
        /// </summary>
        public double Rotation { get; set; }
        
        public override Rect GetBounds()
        {
            // 计算椭圆的边界矩形
            var maxRadius = Math.Max(RadiusX, RadiusY);
            return new Rect(
                Center.X - maxRadius,
                Center.Y - maxRadius,
                maxRadius * 2,
                maxRadius * 2
            );
        }
        
        public override bool Contains(Point point)
        {
            // 椭圆包含点的数学计算
            var dx = point.X - Center.X;
            var dy = point.Y - Center.Y;
            
            // 考虑旋转的椭圆方程
            var cos = Math.Cos(-Rotation);
            var sin = Math.Sin(-Rotation);
            var rotatedX = dx * cos - dy * sin;
            var rotatedY = dx * sin + dy * cos;
            
            return (rotatedX * rotatedX) / (RadiusX * RadiusX) + 
                   (rotatedY * rotatedY) / (RadiusY * RadiusY) <= 1.0;
        }
    }
}
```

#### 步骤2: 扩展枚举类型

```csharp
// Models/Enums.cs
public enum AnnotationType
{
    Rectangle,
    Circle,
    Polygon,
    Line,
    Point,
    Keypoint,
    OrientedBoundingBox,
    Ellipse  // 新增椭圆类型
}

public enum AnnotationTool
{
    Select,
    Rectangle,
    Circle,
    Polygon,
    Line,
    Point,
    Keypoint,
    OrientedBoundingBox,
    Ellipse  // 新增椭圆工具
}
```

#### 步骤3: 实现标注工具

```csharp
// Services/EllipseTool.cs
using System;
using Avalonia;
using Avalonia.Media;
using AIlable.Models;

namespace AIlable.Services
{
    public class EllipseTool : IAnnotationTool
    {
        private Point _startPoint;
        private Point _currentPoint;
        private bool _isDrawing;
        
        public AnnotationType ToolType => AnnotationType.Ellipse;
        public bool IsDrawing => _isDrawing;
        
        public void StartDrawing(Point startPoint)
        {
            _startPoint = startPoint;
            _currentPoint = startPoint;
            _isDrawing = true;
        }
        
        public void UpdateDrawing(Point currentPoint)
        {
            if (!_isDrawing) return;
            _currentPoint = currentPoint;
        }
        
        public Annotation? FinishDrawing()
        {
            if (!_isDrawing) return null;
            
            _isDrawing = false;
            
            // 计算椭圆参数
            var center = new Point2D(
                (_startPoint.X + _currentPoint.X) / 2,
                (_startPoint.Y + _currentPoint.Y) / 2
            );
            
            var radiusX = Math.Abs(_currentPoint.X - _startPoint.X) / 2;
            var radiusY = Math.Abs(_currentPoint.Y - _startPoint.Y) / 2;
            
            return new EllipseAnnotation
            {
                Center = center,
                RadiusX = radiusX,
                RadiusY = radiusY,
                Rotation = 0,
                Label = "椭圆",
                Confidence = 1.0
            };
        }
        
        public void CancelDrawing()
        {
            _isDrawing = false;
        }
        
        public void Render(DrawingContext context, Annotation annotation)
        {
            if (annotation is not EllipseAnnotation ellipse) return;
            
            var pen = new Pen(Brushes.Red, 2);
            var center = new Point(ellipse.Center.X, ellipse.Center.Y);
            
            // 绘制椭圆
            var geometry = new EllipseGeometry(new Rect(
                center.X - ellipse.RadiusX,
                center.Y - ellipse.RadiusY,
                ellipse.RadiusX * 2,
                ellipse.RadiusY * 2
            ));
            
            // 应用旋转变换
            if (Math.Abs(ellipse.Rotation) > 0.001)
            {
                var transform = new RotateTransform(
                    ellipse.Rotation * 180 / Math.PI, 
                    center.X, 
                    center.Y
                );
                geometry.Transform = transform;
            }
            
            context.DrawGeometry(null, pen, geometry);
            
            // 绘制标签
            if (!string.IsNullOrEmpty(ellipse.Label))
            {
                var text = new FormattedText(
                    ellipse.Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    12,
                    Brushes.Red
                );
                
                context.DrawText(text, new Point(center.X, center.Y - ellipse.RadiusY - 15));
            }
        }
        
        public void RenderPreview(DrawingContext context)
        {
            if (!_isDrawing) return;
            
            var pen = new Pen(Brushes.Blue, 1) { DashStyle = DashStyle.Dash };
            var center = new Point(
                (_startPoint.X + _currentPoint.X) / 2,
                (_startPoint.Y + _currentPoint.Y) / 2
            );
            
            var radiusX = Math.Abs(_currentPoint.X - _startPoint.X) / 2;
            var radiusY = Math.Abs(_currentPoint.Y - _startPoint.Y) / 2;
            
            var geometry = new EllipseGeometry(new Rect(
                center.X - radiusX,
                center.Y - radiusY,
                radiusX * 2,
                radiusY * 2
            ));
            
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
```

#### 步骤4: 注册服务和工具

```csharp
// Program.cs 或服务注册文件
public static IServiceCollection AddAnnotationTools(this IServiceCollection services)
{
    // 现有工具...
    services.AddTransient<IAnnotationTool, EllipseTool>();
    
    return services;
}
```

#### 步骤5: 更新UI界面

```xml
<!-- Views/MainView.axaml -->
<Button Name="EllipseToolButton" 
        Classes="tool-button"
        Command="{Binding SelectToolCommand}"
        CommandParameter="{x:Static models:AnnotationTool.Ellipse}"
        ToolTip.Tip="椭圆工具">
    <PathIcon Data="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z" />
</Button>
```

### 2. 集成新的AI模型

#### 步骤1: 实现模型服务接口

```csharp
// Services/CustomModelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AIlable.Models;

namespace AIlable.Services
{
    public class CustomModelService : IAIModelService, IDisposable
    {
        private InferenceSession? _session;
        private CustomModelMetadata? _metadata;
        private bool _disposed = false;
        
        public string ModelName => "Custom Detection Model";
        public bool IsModelLoaded => _session != null;
        
        public async Task LoadModelAsync(string modelPath)
        {
            try
            {
                // 尝试使用GPU加速
                var sessionOptions = new SessionOptions();
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA();
                }
                catch
                {
                    // GPU不可用时回退到CPU
                    Console.WriteLine("CUDA不可用，使用CPU推理");
                }
                
                _session = new InferenceSession(modelPath, sessionOptions);
                
                // 加载模型元数据
                _metadata = await LoadModelMetadataAsync(modelPath);
                
                Console.WriteLine($"模型加载成功: {ModelName}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"模型加载失败: {ex.Message}", ex);
            }
        }
        
        public async Task<List<Annotation>> InferAsync(string imagePath, double confidenceThreshold)
        {
            if (_session == null || _metadata == null)
                throw new InvalidOperationException("模型未加载");
            
            try
            {
                // 1. 图像预处理
                var inputTensor = await PreprocessImageAsync(imagePath);
                
                // 2. 模型推理
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_metadata.InputName, inputTensor)
                };
                
                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();
                
                // 3. 后处理
                var detections = PostprocessOutput(outputTensor, confidenceThreshold);
                
                // 4. 转换为标注对象
                return ConvertToAnnotations(detections, imagePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"推理失败: {ex.Message}", ex);
            }
        }
        
        private async Task<DenseTensor<float>> PreprocessImageAsync(string imagePath)
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            
            // 调整图像大小到模型输入尺寸
            image.Mutate(x => x.Resize(_metadata!.InputWidth, _metadata.InputHeight));
            
            // 创建输入张量 [batch_size, channels, height, width]
            var tensor = new DenseTensor<float>(new[] { 1, 3, _metadata.InputHeight, _metadata.InputWidth });
            
            // 像素值归一化和通道重排
            for (int y = 0; y < _metadata.InputHeight; y++)
            {
                for (int x = 0; x < _metadata.InputWidth; x++)
                {
                    var pixel = image[x, y];
                    
                    // 归一化到 [0, 1] 并按 RGB 顺序排列
                    tensor[0, 0, y, x] = pixel.R / 255.0f;  // R通道
                    tensor[0, 1, y, x] = pixel.G / 255.0f;  // G通道
                    tensor[0, 2, y, x] = pixel.B / 255.0f;  // B通道
                }
            }
            
            return tensor;
        }
        
        private List<Detection> PostprocessOutput(Tensor<float> output, double confidenceThreshold)
        {
            var detections = new List<Detection>();
            
            // 假设输出格式为 [batch_size, num_detections, 6]
            // 其中每个检测包含: [x1, y1, x2, y2, confidence, class_id]
            var batchSize = output.Dimensions[0];
            var numDetections = output.Dimensions[1];
            var detectionSize = output.Dimensions[2];
            
            for (int i = 0; i < numDetections; i++)
            {
                var confidence = output[0, i, 4];
                
                if (confidence < confidenceThreshold) continue;
                
                var detection = new Detection
                {
                    X1 = output[0, i, 0],
                    Y1 = output[0, i, 1],
                    X2 = output[0, i, 2],
                    Y2 = output[0, i, 3],
                    Confidence = confidence,
                    ClassId = (int)output[0, i, 5]
                };
                
                detections.Add(detection);
            }
            
            // 非极大值抑制 (NMS)
            return ApplyNMS(detections, 0.5);
        }
        
        private List<Detection> ApplyNMS(List<Detection> detections, double iouThreshold)
        {
            // 按置信度降序排序
            detections = detections.OrderByDescending(d => d.Confidence).ToList();
            
            var result = new List<Detection>();
            var suppressed = new bool[detections.Count];
            
            for (int i = 0; i < detections.Count; i++)
            {
                if (suppressed[i]) continue;
                
                result.Add(detections[i]);
                
                // 抑制与当前检测重叠度高的其他检测
                for (int j = i + 1; j < detections.Count; j++)
                {
                    if (suppressed[j]) continue;
                    
                    var iou = CalculateIoU(detections[i], detections[j]);
                    if (iou > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }
            
            return result;
        }
        
        private double CalculateIoU(Detection det1, Detection det2)
        {
            // 计算交集
            var x1 = Math.Max(det1.X1, det2.X1);
            var y1 = Math.Max(det1.Y1, det2.Y1);
            var x2 = Math.Min(det1.X2, det2.X2);
            var y2 = Math.Min(det1.Y2, det2.Y2);
            
            if (x2 <= x1 || y2 <= y1) return 0.0;
            
            var intersection = (x2 - x1) * (y2 - y1);
            
            // 计算并集
            var area1 = (det1.X2 - det1.X1) * (det1.Y2 - det1.Y1);
            var area2 = (det2.X2 - det2.X1) * (det2.Y2 - det2.Y1);
            var union = area1 + area2 - intersection;
            
            return intersection / union;
        }
        
        private List<Annotation> ConvertToAnnotations(List<Detection> detections, string imagePath)
        {
            var annotations = new List<Annotation>();
            
            // 获取原始图像尺寸用于坐标转换
            using var image = Image.Load(imagePath);
            var scaleX = (double)image.Width / _metadata!.InputWidth;
            var scaleY = (double)image.Height / _metadata.InputHeight;
            
            foreach (var detection in detections)
            {
                // 将坐标从模型输入尺寸转换回原始图像尺寸
                var x = detection.X1 * scaleX;
                var y = detection.Y1 * scaleY;
                var width = (detection.X2 - detection.X1) * scaleX;
                var height = (detection.Y2 - detection.Y1) * scaleY;
                
                var annotation = new RectangleAnnotation
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Label = GetClassLabel(detection.ClassId),
                    Confidence = detection.Confidence
                };
                
                annotations.Add(annotation);
            }
            
            return annotations;
        }
        
        private string GetClassLabel(int classId)
        {
            // 根据类别ID返回标签名称
            return _metadata?.ClassNames?.ElementAtOrDefault(classId) ?? $"类别_{classId}";
        }
        
        private async Task<CustomModelMetadata> LoadModelMetadataAsync(string modelPath)
        {
            // 从模型文件或配置文件加载元数据
            // 这里使用默认值，实际应用中应该从配置文件读取
            return new CustomModelMetadata
            {
                InputName = "images",
                InputWidth = 640,
                InputHeight = 640,
                ClassNames = new[] { "person", "car", "bicycle", "dog", "cat" }
            };
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
        
        // 内部数据结构
        private class Detection
        {
            public float X1 { get; set; }
            public float Y1 { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }
            public float Confidence { get; set; }
            public int ClassId { get; set; }
        }
        
        private class CustomModelMetadata
        {
            public string InputName { get; set; } = string.Empty;
            public int InputWidth { get; set; }
            public int InputHeight { get; set; }
            public string[] ClassNames { get; set; } = Array.Empty<string>();
        }
    }
}
```

#### 步骤2: 注册模型服务

```csharp
// Program.cs
public static IServiceCollection AddAIServices(this IServiceCollection services)
{
    // 现有服务...
    services.AddTransient<IAIModelService, CustomModelService>();
    
    return services;
}
```

#### 步骤3: 在UI中集成新模型

```csharp
// ViewModels/AIInferenceDialogViewModel.cs
public partial class AIInferenceDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> availableModels = new()
    {
        "YOLO",
        "OBB",
        "Segmentation",
        "Custom"  // 新增自定义模型
    };
    
    private IAIModelService GetModelService(string modelType)
    {
        return modelType switch
        {
            "YOLO" => _serviceProvider.GetRequiredService<YoloModelService>(),
            "OBB" => _serviceProvider.GetRequiredService<OBBModelService>(),
            "Segmentation" => _serviceProvider.GetRequiredService<SegmentationModelService>(),
            "Custom" => _serviceProvider.GetRequiredService<CustomModelService>(),
            _ => throw new ArgumentException($"未知的模型类型: {modelType}")
        };
    }
}
```

### 3. 自定义导出格式

#### 步骤1: 实现导出服务

```csharp
// Services/CustomExportService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using AIlable.Models;

namespace AIlable.Services
{
    public class CustomExportService
    {
        /// <summary>
        /// 导出为自定义JSON格式
        /// </summary>
        public async Task ExportToCustomJsonAsync(AnnotationProject project, string outputPath)
        {
            var exportData = new
            {
                metadata = new
                {
                    project_name = project.Name,
                    description = project.Description,
                    created_at = project.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    last_modified = project.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
                    total_images = project.Images.Count,
                    total_annotations = project.Images.Sum(img => img.Annotations.Count),
                    export_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                labels = project.Labels.Select(kvp => new
                {
                    id = kvp.Key,
                    name = kvp.Value
                }).ToArray(),
                images = project.Images.Select(img => new
                {
                    file_path = img.FilePath,
                    file_name = Path.GetFileName(img.FilePath),
                    width = img.Metadata?.Width ?? 0,
                    height = img.Metadata?.Height ?? 0,
                    annotations = img.Annotations.Select(ann => SerializeAnnotation(ann)).ToArray()
                }).ToArray()
            };
            
            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);
        }
        
        /// <summary>
        /// 导出为XML格式
        /// </summary>
        public async Task ExportToXmlAsync(AnnotationProject project, string outputPath)
        {
            var root = new XElement("AnnotationProject",
                new XAttribute("name", project.Name),
                new XAttribute("created", project.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                
                new XElement("Labels",
                    project.Labels.Select(kvp =>
                        new XElement("Label",
                            new XAttribute("id", kvp.Key),
                            new XAttribute("name", kvp.Value)
                        )
                    )
                ),
                
                new XElement("Images",
                    project.Images.Select(img =>
                        new XElement("Image",
                            new XAttribute("path", img.FilePath),
                            new XAttribute("width", img.Metadata?.Width ?? 0),
                            new XAttribute("height", img.Metadata?.Height ?? 0),
                            
                            new XElement("Annotations",
                                img.Annotations.Select(ann => SerializeAnnotationToXml(ann))
                            )
                        )
                    )
                )
            );
            
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                root
            );
            
            await using var writer = File.CreateText(outputPath);
            await document.SaveAsync(writer, SaveOptions.None, default);
        }
        
        /// <summary>
        /// 导出为CSV格式
        /// </summary>
        public async Task ExportToCsvAsync(AnnotationProject project, string outputPath)
        {
            var lines = new List<string>
            {
                // CSV头部
                "image_path,image_name,annotation_id,annotation_type,label,confidence,x,y,width,height,additional_data"
            };
            
            foreach (var image in project.Images)
            {
                var imageName = Path.GetFileName(image.FilePath);
                
                foreach (var annotation in image.Annotations)
                {
                    var csvLine = $"{image.FilePath},{imageName},{annotation.Id},{annotation.Type}," +
                                 $"{annotation.Label},{annotation.Confidence:F4}," +
                                 $"{GetAnnotationCsvData(annotation)}";
                    
                    lines.Add(csvLine);
                }
            }
            
            await File.WriteAllLinesAsync(outputPath, lines);
        }
        
        /// <summary>
        /// 导出为COCO格式
        /// </summary>
        public async Task ExportToCocoAsync(AnnotationProject project, string outputPath)
        {
            var categories = project.Labels.Select((kvp, index) => new
            {
                id = index + 1,
                name = kvp.Value,
                supercategory = "object"
            }).ToArray();
            
            var images = project.Images.Select((img, index) => new
            {
                id = index + 1,
                file_name = Path.GetFileName(img.FilePath),
                width = img.Metadata?.Width ?? 0,
                height = img.Metadata?.Height ?? 0
            }).ToArray();
            
            var annotations = new List<object>();
            var annotationId = 1;
            
            for (int imgIndex = 0; imgIndex < project.Images.Count; imgIndex++)
            {
                var image = project.Images[imgIndex];
                var imageId = imgIndex + 1;
                
                foreach (var annotation in image.Annotations.OfType<RectangleAnnotation>())
                {
                    var categoryId = Array.FindIndex(categories, c => c.name == annotation.Label) + 1;
                    
                    annotations.Add(new
                    {
                        id = annotationId++,
                        image_id = imageId,
                        category_id = categoryId,
                        bbox = new[] { annotation.X, annotation.Y, annotation.Width, annotation.Height },
                        area = annotation.Width * annotation.Height,
                        iscrowd = 0
                    });
                }
            }
            
            var cocoData = new
            {
                info = new
                {
                    description = project.Description,
                    version = "1.0",
                    year = DateTime.Now.Year,
                    contributor = "AIlable",
                    date_created = DateTime.Now.ToString("yyyy-MM-dd")
                },
                licenses = new[]
                {
                    new { id = 1, name = "Unknown", url = "" }
                },
                images,
                annotations = annotations.ToArray(),
                categories
            };
            
            var json = JsonConvert.SerializeObject(cocoData, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);
        }
        
        private object SerializeAnnotation(Annotation annotation)
        {
            var baseData = new
            {
                id = annotation.Id,
                type = annotation.Type.ToString(),
                label = annotation.Label,
                confidence = annotation.Confidence,
                is_visible = annotation.IsVisible,
                created_at = annotation.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            return annotation switch
            {
                RectangleAnnotation rect => new
                {
                    baseData.id,
                    baseData.type,
                    baseData.label,
                    baseData.confidence,
                    baseData.is_visible,
                    baseData.created_at,
                    geometry = new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height }
                },
                CircleAnnotation circle => new
                {
                    baseData.id,
                    baseData.type,
                    baseData.label,
                    baseData.confidence,
                    baseData.is_visible,
                    baseData.created_at,
                    geometry = new { center_x = circle.Center.X, center_y = circle.Center.Y, radius = circle.Radius }
                },
                PolygonAnnotation polygon => new
                {
                    baseData.id,
                    baseData.type,
                    baseData.label,
                    baseData.confidence,
                    baseData.is_visible,
                    baseData.created_at,
                    geometry = new { points = polygon.Points.Select(p => new { x = p.X, y = p.Y }).ToArray() }
                },
                KeypointAnnotation keypoint => new
                {
                    baseData.id,
                    baseData.type,
                    baseData.label,
                    baseData.confidence,
                    baseData.is_visible,
                    baseData.created_at,
                    geometry = new
                    {
                        keypoints = keypoint.Keypoints.Select(p => new { x = p.X, y = p.Y }).ToArray(),
                        connections = keypoint.Connections
                    }
                },
                _ => baseData
            };
        }
        
        private XElement SerializeAnnotationToXml(Annotation annotation)
        {
            var element = new XElement("Annotation",
                new XAttribute("id", annotation.Id),
                new XAttribute("type", annotation.Type),
                new XAttribute("label", annotation.Label),
                new XAttribute("confidence", annotation.Confidence),
                new XAttribute("visible", annotation.IsVisible)
            );
            
            switch (annotation)
            {
                case RectangleAnnotation rect:
                    element.Add(new XElement("Rectangle",
                        new XAttribute("x", rect.X),
                        new XAttribute("y", rect.Y),
                        new XAttribute("width", rect.Width),
                        new XAttribute("height", rect.Height)
                    ));
                    break;
                    
                case CircleAnnotation circle:
                    element.Add(new XElement("Circle",
                        new XAttribute("centerX", circle.Center.X),
                        new XAttribute("centerY", circle.Center.Y),
                        new XAttribute("radius", circle.Radius)
                    ));
                    break;
                    
                case PolygonAnnotation polygon:
                    element.Add(new XElement("Polygon",
                        polygon.Points.Select(p =>
                            new XElement("Point",
                                new XAttribute("x", p.X),
                                new XAttribute("y", p.Y)
                            )
                        )
                    ));
                    break;
            }
            
            return element;
        }
        
        private string GetAnnotationCsvData(Annotation annotation)
        {
            return annotation switch
            {
                RectangleAnnotation rect => $"{rect.X:F2},{rect.Y:F2},{rect.Width:F2},{rect.Height:F2},",
                CircleAnnotation circle => $"{circle.Center.X:F2},{circle.Center.Y:F2},{circle.Radius:F2},,\"{circle.Center.X:F2};{circle.Center.Y:F2};{circle.Radius:F2}\"",
                PolygonAnnotation polygon => $",,,,\"{string.Join(";", polygon.Points.Select(p => $"{p.X:F2},{p.Y:F2}"))}\"",
                _ => ",,,,"
            };
        }
    }
}
```

## UI定制教程

### 1. 自定义主题

#### 步骤1: 创建主题资源文件

```xml
<!-- Styles/CustomTheme.axaml -->
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- 自定义颜色方案 -->
    <Style.Resources>
        <!-- 主色调 -->
        <SolidColorBrush x:Key="CustomPrimaryBrush">#FF6366F1</SolidColorBrush>
        <SolidColorBrush x:Key="CustomPrimaryHoverBrush">#FF4F46E5</SolidColorBrush>
        <SolidColorBrush x:Key="CustomPrimaryPressedBrush">#FF3730A3</SolidColorBrush>
        
        <!-- 辅助色 -->
        <SolidColorBrush x:Key="CustomSecondaryBrush">#FF10B981</SolidColorBrush>
        <SolidColorBrush x:Key="CustomAccentBrush">#FFF59E0B</SolidColorBrush>
        <SolidColorBrush x:Key="CustomDangerBrush">#FFEF4444</SolidColorBrush>
        
        <!-- 背景色 -->
        <SolidColorBrush x:Key="CustomBackgroundBrush">#FF0F172A</SolidColorBrush>
        <SolidColorBrush x:Key="CustomSurfaceBrush">#FF1E293B</SolidColorBrush>
        <SolidColorBrush x:Key="CustomCardBrush">#FF334155</SolidColorBrush>
        
        <!-- 文本色 -->
        <SolidColorBrush x:Key="CustomTextPrimaryBrush">#FFF8FAFC</SolidColorBrush>
        <SolidColorBrush x:Key="CustomTextSecondaryBrush">#FFCBD5E1</SolidColorBrush>
        <SolidColorBrush x:Key="CustomTextMutedBrush">#FF94A3B8</SolidColorBrush>
        
        <!-- 边框色 -->
        <SolidColorBrush x:Key="CustomBorderBrush">#FF475569</SolidColorBrush>
        <SolidColorBrush x:Key="CustomBorderHoverBrush">#FF64748B</SolidColorBrush>
    </Style.Resources>
    
    <!-- 按钮样式 -->
    <Style Selector="Button.custom-primary">
        <Setter Property="Background" Value="{DynamicResource CustomPrimaryBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="16,8" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Transitions">
            <Transitions>
                <BrushTransition Property="Background" Duration="0:0:0.2" />
                <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.1" />
            </Transitions>
        </Setter>
    </Style>
    
    <Style Selector="Button.custom-primary:pointerover">
        <Setter Property="Background" Value="{DynamicResource CustomPrimaryHoverBrush}" />
        <Setter Property="RenderTransform" Value="scale(1.02)" />
    </Style>
    
    <Style Selector="Button.custom-primary:pressed">
        <Setter Property="Background" Value="{DynamicResource CustomPrimaryPressedBrush}" />
        <Setter Property="RenderTransform" Value="scale(0.98)" />
    </Style>
    
    <!-- 卡片样式 -->
    <Style Selector="Border.custom-card">
        <Setter Property="Background" Value="{DynamicResource CustomCardBrush}" />
        <Setter Property="BorderBrush" Value="{DynamicResource CustomBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="12" />
        <Setter Property="Padding" Value="16" />
        <Setter Property="BoxShadow" Value="0 4 6 -1 rgba(0, 0, 0, 0.1), 0 2 4 -1 rgba(0, 0, 0, 0.06)" />
    </Style>
    
    <!-- 输入框样式 -->
    <Style Selector="TextBox.custom-input">
        <Setter Property="Background" Value="{DynamicResource CustomSurfaceBrush}" />
        <Setter Property="Foreground" Value="{DynamicResource CustomTextPrimaryBrush}" />
        <Setter Property="BorderBrush" Value="{DynamicResource CustomBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="6" />
        <Setter Property="Padding" Value="12,8" />
        <Setter Property="FontSize" Value="14" />
    </Style>
    
    <Style Selector="TextBox.custom-input:focus">
        <Setter Property="BorderBrush" Value="{DynamicResource CustomPrimaryBrush}" />
        <Setter Property="BoxShadow" Value="0 0 0 3 rgba(99, 102, 241, 0.1)" />
    </Style>
    
</Styles>
```

#### 步骤2: 扩展主题服务

```csharp
// Services/ThemeService.cs 扩展
public enum AppTheme
{
    Light,
    Dark,
    Custom,
    HighContrast  // 新增高对比度主题
}

public partial class ThemeService : IThemeService
{
    private readonly Dictionary<AppTheme, string> _themeResources = new()
    {
        { AppTheme.Light, "avares://AIlable/Styles/LightTheme.axaml" },
        { AppTheme.Dark, "avares://AIlable/Styles/DarkTheme.axaml" },
        { AppTheme.Custom, "avares://AIlable/Styles/CustomTheme.axaml" },
        { AppTheme.HighContrast, "avares://AIlable/Styles/HighContrastTheme.axaml" }
    };
    
    public async Task SetThemeAsync(AppTheme theme)
    {
        try
        {
            ApplyTheme(theme);
            await SaveThemePreferenceAsync(theme);
            
            ThemeChanged?.Invoke(theme);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"主题切换失败: {ex.Message}", ex);
        }
    }
    
    public event Action<AppTheme>? ThemeChanged;
}
```

### 2. 自定义控件开发

#### 步骤1: 创建进度环控件

```csharp
// Controls/CircularProgressBar.cs
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AIlable.Controls
{
    public class CircularProgressBar : Control
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Value), 0.0);
        
        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Maximum), 100.0);
        
        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(StrokeThickness), 8.0);
        
        public static readonly StyledProperty<IBrush> ProgressBrushProperty =
            AvaloniaProperty.Register<CircularProgressBar, IBrush>(nameof(ProgressBrush), Brushes.Blue);
        
        public static readonly StyledProperty<IBrush> TrackBrushProperty =
            AvaloniaProperty.Register<CircularProgressBar, IBrush>(nameof(TrackBrush), Brushes.LightGray);
        
        public static readonly StyledProperty<bool> ShowTextProperty =
            AvaloniaProperty.Register<CircularProgressBar, bool>(nameof(ShowText), true);
        
        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, Math.Max(0, Math.Min(Maximum, value)));
        }
        
        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, Math.Max(0, value));
        }
        
        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }
        
        public IBrush ProgressBrush
        {
            get => GetValue(ProgressBrushProperty);
            set => SetValue(ProgressBrushProperty, value);
        }
        
        public IBrush TrackBrush
        {
            get => GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }
        
        public bool ShowText
        {
            get => GetValue(ShowTextProperty);
            set => SetValue(ShowTextProperty, value);
        }
        
        public double Percentage => Maximum > 0 ? (Value / Maximum) * 100 : 0;
        
        static CircularProgressBar()
        {
            AffectsRender<CircularProgressBar>(
                ValueProperty,
                MaximumProperty,
                StrokeThicknessProperty,
                ProgressBrushProperty,
                TrackBrushProperty,
                ShowTextProperty
            );
        }
        
        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            var center = new Point(bounds.Width / 2, bounds.Height / 2);
            var radius = Math.Min(bounds.Width, bounds.Height) / 2 - StrokeThickness / 2;
            
            if (radius <= 0) return;
            
            // 绘制背景圆环
            var trackPen = new Pen(TrackBrush, StrokeThickness);
            context.DrawEllipse(null, trackPen, center, radius, radius);
            
            // 绘制进度弧
            if (Value > 0 && Maximum > 0)
            {
                var progressPen = new Pen(ProgressBrush, StrokeThickness)
                {
                    LineCap = PenLineCap.Round
                };
                
                var startAngle = -90; // 从顶部开始
                var sweepAngle = (Value / Maximum) * 360;
                
                var geometry = new PathGeometry();
                var figure = new PathFigure();
                
                var startPoint = GetPointOnCircle(center, radius, startAngle);
                figure.StartPoint = startPoint;
                
                var endAngle = startAngle + sweepAngle;
                var endPoint = GetPointOnCircle(center, radius, endAngle);
                
                var isLargeArc = sweepAngle > 180;
                var arcSegment = new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = isLargeArc
                };
                
                figure.Segments.Add(arcSegment);
                geometry.Figures.Add(figure);
                
                context.DrawGeometry(null, progressPen, geometry);
            }
            
            // 绘制百分比文本
            if (ShowText)
            {
                var text = $"{Percentage:F0}%";
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    Math.Max(12, radius / 4),
                    ProgressBrush
                );
                
                var textBounds = formattedText.Bounds;
                var textPosition = new Point(
                    center.X - textBounds.Width / 2,
                    center.Y - textBounds.Height / 2
                );
                
                context.DrawText(formattedText, textPosition);
            }
        }
        
        private Point GetPointOnCircle(Point center, double radius, double angleDegrees)
        {
            var angleRadians = angleDegrees * Math.PI / 180;
            return new Point(
                center.X + radius * Math.Cos(angleRadians),
                center.Y + radius * Math.Sin(angleRadians)
            );
        }
    }
}
```

#### 步骤2: 创建控件样式

```xml
<!-- Styles/CustomControls.axaml -->
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="using:AIlable.Controls">
    
    <!-- 圆形进度条样式 -->
    <Style Selector="controls|CircularProgressBar">
        <Setter Property="Width" Value="100" />
        <Setter Property="Height" Value="100" />
        <Setter Property="StrokeThickness" Value="8" />
        <Setter Property="ProgressBrush" Value="{DynamicResource CustomPrimaryBrush}" />
        <Setter Property="TrackBrush" Value="{DynamicResource CustomBorderBrush}" />
    </Style>
    
    <!-- 小尺寸进度条 -->
    <Style Selector="controls|CircularProgressBar.small">
        <Setter Property="Width" Value="60" />
        <Setter Property="Height" Value="60" />
        <Setter Property="StrokeThickness" Value="4" />
    </Style>
    
    <!-- 大尺寸进度条 -->
    <Style Selector="controls|CircularProgressBar.large">
        <Setter Property="Width" Value="150" />
        <Setter Property="Height" Value="150" />
        <Setter Property="StrokeThickness" Value="12" />
    </Style>
    
</Styles>
```

### 3. 响应式布局设计

#### 步骤1: 创建响应式面板

```csharp
// Controls/ResponsivePanel.cs
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AIlable.Controls
{
    public class ResponsivePanel : Panel
    {
        public static readonly StyledProperty<double> SmallBreakpointProperty =
            AvaloniaProperty.Register<ResponsivePanel, double>(nameof(SmallBreakpoint), 576);
        
        public static readonly StyledProperty<double> MediumBreakpointProperty =
            AvaloniaProperty.Register<ResponsivePanel, double>(nameof(MediumBreakpoint), 768);
        
        public static readonly StyledProperty<double> LargeBreakpointProperty =
            AvaloniaProperty.Register<ResponsivePanel, double>(nameof(LargeBreakpoint), 992);
        
        public static readonly AttachedProperty<int> SmallColumnsProperty =
            AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, int>("SmallColumns", 12);
        
        public static readonly AttachedProperty<int> MediumColumnsProperty =
            AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, int>("MediumColumns", 6);
        
        public static readonly AttachedProperty<int> LargeColumnsProperty =
            AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, int>("LargeColumns", 4);
        
        public double SmallBreakpoint
        {
            get => GetValue(SmallBreakpointProperty);
            set => SetValue(SmallBreakpointProperty, value);
        }
        
        public double MediumBreakpoint
        {
            get => GetValue(MediumBreakpointProperty);
            set => SetValue(MediumBreakpointProperty, value);
        }
        
        public double LargeBreakpoint
        {
            get => GetValue(LargeBreakpointProperty);
            set => SetValue(LargeBreakpointProperty, value);
        }
        
        public static int GetSmallColumns(Control control) => control.GetValue(SmallColumnsProperty);
        public static void SetSmallColumns(Control control, int value) => control.SetValue(SmallColumnsProperty, value);
        
        public static int GetMediumColumns(Control control) => control.GetValue(MediumColumnsProperty);
        public static void SetMediumColumns(Control control, int value) => control.SetValue(MediumColumnsProperty, value);
        
        public static int GetLargeColumns(Control control) => control.GetValue(LargeColumnsProperty);
        public static void SetLargeColumns(Control control, int value) => control.SetValue(LargeColumnsProperty, value);
        
        protected override Size MeasureOverride(Size availableSize)
        {
            var totalColumns = 12;
            var currentBreakpoint = GetCurrentBreakpoint(availableSize.Width);
            
            foreach (Control child in Children)
            {
                var columns = GetColumnsForBreakpoint(child, currentBreakpoint);
                var childWidth = (availableSize.Width / totalColumns) * columns;
                child.Measure(new Size(childWidth, availableSize.Height));
            }
            
            return availableSize;
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            var totalColumns = 12;
            var currentBreakpoint = GetCurrentBreakpoint(finalSize.Width);
            var currentX = 0.0;
            var currentY = 0.0;
            var rowHeight = 0.0;
            var usedColumns = 0;
            
            foreach (Control child in Children)
            {
                var columns = GetColumnsForBreakpoint(child, currentBreakpoint);
                var childWidth = (finalSize.Width / totalColumns) * columns;
                
                // 检查是否需要换行
                if (usedColumns + columns > totalColumns)
                {
                    currentX = 0;
                    currentY += rowHeight;
                    rowHeight = 0;
                    usedColumns = 0;
                }
                
                var childHeight = child.DesiredSize.Height;
                child.Arrange(new Rect(currentX, currentY, childWidth, childHeight));
                
                currentX += childWidth;
                usedColumns += columns;
                rowHeight = Math.Max(rowHeight, childHeight);
            }
            
            return finalSize;
        }
        
        private BreakpointSize GetCurrentBreakpoint(double width)
        {
            if (width >= LargeBreakpoint) return BreakpointSize.Large;
            if (width >= MediumBreakpoint) return BreakpointSize.Medium;
            if (width >= SmallBreakpoint) return BreakpointSize.Small;
            return BreakpointSize.ExtraSmall;
        }
        
        private int GetColumnsForBreakpoint(Control control, BreakpointSize breakpoint)
        {
            return breakpoint switch
            {
                BreakpointSize.Large => GetLargeColumns(control),
                BreakpointSize.Medium => GetMediumColumns(control),
                BreakpointSize.Small => GetSmallColumns(control),
                BreakpointSize.ExtraSmall => GetSmallColumns(control),
                _ => 12
            };
        }
        
        private enum BreakpointSize
        {
            ExtraSmall,
            Small,
            Medium,
            Large
        }
    }
}
```

## 性能优化指南

### 1. 多线程处理优化

#### AI推理性能优化（已实现）

```csharp
// 使用已实现的多线程AI推理管理器
public class PerformanceOptimizedInference
{
    private readonly MultiThreadAIModelManager _aiManager;
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    public PerformanceOptimizedInference(MultiThreadAIModelManager aiManager)
    {
        _aiManager = aiManager;
        // 根据CPU核心数设置并发限制
        var maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
    }
    
    public async Task<List<InferenceResult>> OptimizedBatchInferenceAsync(
        List<string> imagePaths,
        IAIModelService modelService,
        double confidenceThreshold,
        IProgress<InferenceProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 使用已实现的多线程推理管理器
        return await _aiManager.BatchInferAsync(
            imagePaths,
            modelService,
            confidenceThreshold,
            cancellationToken,
            progress
        );
    }
}
```

#### 图像处理优化

```csharp
// Services/OptimizedImageService.cs
public class OptimizedImageService : IImageService
{
    private readonly ObjectPool<byte[]> _bufferPool;
    private readonly LRUCache<string, WeakReference<Bitmap>> _imageCache;
    
    public OptimizedImageService()
    {
        _bufferPool = new DefaultObjectPool<byte[]>(
            new BufferPooledObjectPolicy(1024 * 1024)); // 1MB缓冲区
        _imageCache = new LRUCache<string, WeakReference<Bitmap>>(100);
    }
    
    public async Task<Bitmap?> LoadImageOptimizedAsync(string imagePath, Size? targetSize = null)
    {
        // 检查缓存
        if (_imageCache.TryGetValue(imagePath, out var weakRef) && 
            weakRef.TryGetTarget(out var cachedBitmap))
        {
            return cachedBitmap;
        }
        
        var buffer = _bufferPool.Get();
        try
        {
            using var fileStream = File.OpenRead(imagePath);
            using var image = await Image.LoadAsync<Rgba32>(fileStream);
            
            // 如果指定了目标尺寸，进行缩放
            if (targetSize.HasValue)
            {
                var scale = Math.Min(
                    targetSize.Value.Width / image.Width,
                    targetSize.Value.Height / image.Height
                );
                
                if (scale < 1.0)
                {
                    var newWidth = (int)(image.Width * scale);
                    var newHeight = (int)(image.Height * scale);
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }
            }
            
            var bitmap = ConvertToBitmap(image);
            _imageCache[imagePath] = new WeakReference<Bitmap>(bitmap);
            return bitmap;
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }
    
    private class BufferPooledObjectPolicy : IPooledObjectPolicy<byte[]>
    {
        private readonly int _bufferSize;
        
        public BufferPooledObjectPolicy(int bufferSize)
        {
            _bufferSize = bufferSize;
        }
        
        public byte[] Create() => new byte[_bufferSize];
        
        public bool Return(byte[] obj)
        {
            Array.Clear(obj, 0, obj.Length);
            return true;
        }
    }
}
```

### 2. 内存管理优化

#### 图像缓存管理

```csharp
// Services/ImageCacheManager.cs
public class ImageCacheManager : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly long _maxCacheSize = 500 * 1024 * 1024; // 500MB
    private long _currentCacheSize = 0;
    
    public ImageCacheManager()
    {
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    public async Task<Bitmap?> GetOrLoadImageAsync(string imagePath)
    {
        if (_cache.TryGetValue(imagePath, out var entry))
        {
            entry.LastAccessed = DateTime.UtcNow;
            if (entry.WeakReference.TryGetTarget(out var cachedBitmap))
            {
                return cachedBitmap;
            }
            else
            {
                // 弱引用已失效，从缓存中移除
                _cache.TryRemove(imagePath, out _);
            }
        }
        
        // 加载新图像
        var bitmap = await LoadImageAsync(imagePath);
        if (bitmap != null)
        {
            var imageSize = EstimateImageSize(bitmap);
            
            // 检查缓存大小限制
            if (_currentCacheSize + imageSize > _maxCacheSize)
            {
                await EvictLeastRecentlyUsedAsync(imageSize);
            }
            
            var cacheEntry = new CacheEntry
            {
                WeakReference = new WeakReference<Bitmap>(bitmap),
                Size = imageSize,
                LastAccessed = DateTime.UtcNow
            };
            
            _cache[imagePath] = cacheEntry;
            Interlocked.Add(ref _currentCacheSize, imageSize);
        }
        
        return bitmap;
    }
    
    private async Task EvictLeastRecentlyUsedAsync(long requiredSpace)
    {
        var entries = _cache.ToArray()
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .ToList();
        
        long freedSpace = 0;
        foreach (var entry in entries)
        {
            if (_cache.TryRemove(entry.Key, out var removedEntry))
            {
                freedSpace += removedEntry.Size;
                Interlocked.Add(ref _currentCacheSize, -removedEntry.Size);
                
                if (freedSpace >= requiredSpace) break;
            }
        }
    }
    
    private class CacheEntry
    {
        public WeakReference<Bitmap> WeakReference { get; set; } = null!;
        public long Size { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
```

### 3. UI渲染优化

#### 虚拟化渲染

```csharp
// Controls/VirtualizedImageCanvas.cs
public class VirtualizedImageCanvas : Control
{
    private readonly List<Annotation> _visibleAnnotations = new();
    private Rect _viewportBounds;
    
    public override void Render(DrawingContext context)
    {
        // 只渲染可见区域内的标注
        UpdateVisibleAnnotations();
        
        foreach (var annotation in _visibleAnnotations)
        {
            RenderAnnotation(context, annotation);
        }
    }
    
    private void UpdateVisibleAnnotations()
    {
        _visibleAnnotations.Clear();
        
        foreach (var annotation in Annotations)
        {
            var bounds = annotation.GetBounds();
            if (_viewportBounds.Intersects(bounds))
            {
                _visibleAnnotations.Add(annotation);
            }
        }
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == BoundsProperty)
        {
            _viewportBounds = Bounds;
            InvalidateVisual();
        }
    }
}
```

## 部署和发布

### 1. 跨平台打包

#### 自动化构建脚本

```bash
#!/bin/bash
# build-all-platforms.sh

echo "开始构建所有平台..."

# 设置版本号
VERSION="1.0.0"
OUTPUT_DIR="./publish"

# 清理输出目录
rm -rf $OUTPUT_DIR
mkdir -p $OUTPUT_DIR

# 构建桌面版本
echo "构建桌面版本..."
dotnet publish AIlable.Desktop -c Release -r win-x64 --self-contained -o $OUTPUT_DIR/windows-x64
dotnet publish AIlable.Desktop -c Release -r linux-x64 --self-contained -o $OUTPUT_DIR/linux-x64
dotnet publish AIlable.Desktop -c Release -r osx-x64 --self-contained -o $OUTPUT_DIR/macos-x64

# 构建移动版本
echo "构建移动版本..."
dotnet publish AIlable.Android -c Release -o $OUTPUT_DIR/android
dotnet publish AIlable.iOS -c Release -o $OUTPUT_DIR/ios

# 构建Web版本
echo "构建Web版本..."
dotnet publish AIlable.Browser -c Release -o $OUTPUT_DIR/web

# 创建压缩包
echo "创建发布包..."
cd $OUTPUT_DIR
zip -r "AIlable-v$VERSION-windows-x64.zip" windows-x64/
tar -czf "AIlable-v$VERSION-linux-x64.tar.gz" linux-x64/
tar -czf "AIlable-v$VERSION-macos-x64.tar.gz" macos-x64/
zip -r "AIlable-v$VERSION-web.zip" web/

echo "构建完成！发布包位于: $OUTPUT_DIR"
```

#### PowerShell构建脚本（Windows）

```powershell
# build-windows.ps1
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

Write-Host "开始构建Windows版本..." -ForegroundColor Green

$OutputDir = "./publish/windows"
$ProjectPath = "AIlable.Desktop/AIlable.Desktop.csproj"

# 清理输出目录
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force

# 构建自包含版本
Write-Host "构建自包含版本..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:Version=$Version `
    -o "$OutputDir/self-contained"

# 构建框架依赖版本
Write-Host "构建框架依赖版本..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c $Configuration `
    -r win-x64 `
    --no-self-contained `
    -p:Version=$Version `
    -o "$OutputDir/framework-dependent"

# 创建安装包（需要WiX工具）
if (Get-Command "candle.exe" -ErrorAction SilentlyContinue) {
    Write-Host "创建MSI安装包..." -ForegroundColor Yellow
    # WiX安装包创建逻辑
}

Write-Host "Windows构建完成！" -ForegroundColor Green
```

### 2. 应用商店发布

#### Google Play发布准备

```xml
<!-- AIlable.Android/Properties/AndroidManifest.xml -->
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          android:versionCode="1"
          android:versionName="1.0.0"
          android:installLocation="auto">
    
    <!-- 应用权限 -->
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.RECORD_AUDIO" />
    
    <!-- 应用配置 -->
    <application 
        android:label="AIlable"
        android:icon="@mipmap/ic_launcher"
        android:theme="@style/AppTheme"
        android:allowBackup="true"
        android:supportsRtl="true">
        
        <!-- 主活动 -->
        <activity 
            android:name=".MainActivity"
            android:exported="true"
            android:screenOrientation="portrait">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>
        
        <!-- 文件关联 -->
        <activity android:name=".FileAssociationActivity">
            <intent-filter>
                <action android:name="android.intent.action.VIEW" />
                <category android:name="android.intent.category.DEFAULT" />
                <data android:mimeType="application/json" />
            </intent-filter>
        </activity>
    </application>
</manifest>
```

#### App Store发布准备

```xml
<!-- AIlable.iOS/Info.plist -->
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0">
<dict>
    <!-- 应用信息 -->
    <key>CFBundleDisplayName</key>
    <string>AIlable</string>
    <key>CFBundleIdentifier</key>
    <string>com.yourcompany.ailable</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    
    <!-- 系统要求 -->
    <key>MinimumOSVersion</key>
    <string>13.0</string>
    <key>UIDeviceFamily</key>
    <array>
        <integer>1</integer> <!-- iPhone -->
        <integer>2</integer> <!-- iPad -->
    </array>
    
    <!-- 权限描述 -->
    <key>NSMicrophoneUsageDescription</key>
    <string>AIlable需要访问麦克风进行语音录制功能</string>
    <key>NSPhotoLibraryUsageDescription</key>
    <string>AIlable需要访问相册来加载和保存图像</string>
    <key>NSCameraUsageDescription</key>
    <string>AIlable需要访问相机来拍摄图像进行标注</string>
    
    <!-- 文件类型支持 -->
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>AIlable Project</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>ailable</string>
            </array>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
        </dict>
    </array>
</dict>
</plist>
```

## 故障排除

### 1. 常见问题解决

#### 问题1: ONNX模型加载失败

**症状**: 
```
OnnxRuntimeException: Failed to load model
```

**解决方案**:
```csharp
public async Task<bool> TryLoadModelAsync(string modelPath)
{
    try
    {
        // 首先尝试GPU加速
        var sessionOptions = new SessionOptions();
        sessionOptions.AppendExecutionProvider_CUDA();
        _session = new InferenceSession(modelPath, sessionOptions);
        return true;
    }
    catch (OnnxRuntimeException ex) when (ex.Message.Contains("CUDA"))
    {
        try
        {
            // GPU失败时回退到CPU
            _session = new InferenceSession(modelPath);
            Console.WriteLine("GPU不可用，使用CPU推理");
            return true;
        }
        catch (Exception cpuEx)
        {
            Console.WriteLine($"模型加载失败: {cpuEx.Message}");
            return false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"模型加载失败: {ex.Message}");
        return false;
    }
}
```

#### 问题2: 内存溢出

**症状**:
```
OutOfMemoryException: Insufficient memory to continue the execution
```

**解决方案**:
```csharp
public class MemoryOptimizedImageProcessor
{
    private readonly int _maxImageSize = 2048; // 最大图像尺寸
    
    public async Task<Bitmap> ProcessImageSafelyAsync(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        using var image = await Image.LoadAsync<Rgba32>(stream);
        
        // 检查图像尺寸，如果过大则缩放
        if (image.Width > _maxImageSize || image.Height > _maxImageSize)
        {
            var scale = Math.Min(
                (double)_maxImageSize / image.Width,
                (double)_maxImageSize / image.Height
            );
            
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }
        
        return ConvertToBitmap(image);
    }
}
```

#### 问题3: 跨平台文件路径问题

**症状**: 在不同平台上文件路径不正确

**解决方案**:
```csharp
public class CrossPlatformPathHelper
{
    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar)
                                   .Replace('/', Path.DirectorySeparatorChar));
    }
    
    public static string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(NormalizePath(basePath) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(NormalizePath(fullPath));
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString())
                  .Replace('/', Path.DirectorySeparatorChar);
    }
    
    public static string GetAppDataPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }
}
```

### 2. 性能调优

#### CPU使用率优化

```csharp
public class PerformanceOptimizer
{
    public static void OptimizeCpuUsage()
    {
        // 设置线程池最小线程数
        ThreadPool.SetMinThreads(
            Environment.ProcessorCount,
            Environment.ProcessorCount
        );
        
        // 设置垃圾回收模式
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }
    
    public static async Task<T> RunWithTimeout<T>(
        Func<Task<T>> operation, 
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"操作超时: {timeout}");
        }
    }
}
```

#### 内存使用优化

```csharp
public class MemoryOptimizer
{
    public static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    
    public static long GetMemoryUsage()
    {
        return GC.GetTotalMemory(false);
    }
    
    public static void MonitorMemoryUsage(Action<long> onMemoryChanged)
    {
        var timer = new Timer(_ =>
        {
            var memory = GetMemoryUsage();
            onMemoryChanged(memory);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
}
```

## 最佳实践

### 1. 代码规范

#### 命名约定
```csharp
// 类名使用PascalCase
public class ImageAnnotationService { }

// 方法名使用PascalCase
public async Task LoadImageAsync(string path) { }

// 属性名使用PascalCase
public string ImagePath { get; set; }

// 字段名使用camelCase，私有字段使用下划线前缀
private readonly ILogger _logger;

// 常量使用PascalCase
public const int MaxImageSize = 4096;

// 枚举使用PascalCase
public enum AnnotationType
{
    Rectangle,
    Circle,
    Polygon
}
```

#### 异步编程规范
```csharp
// 正确的异步方法实现
public async Task<List<Annotation>> LoadAnnotationsAsync(string projectPath)
{
    try
    {
        var json = await File.ReadAllTextAsync(projectPath);
        return JsonConvert.DeserializeObject<List<Annotation>>(json) ?? new List<Annotation>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "加载标注失败: {ProjectPath}", projectPath);
        throw;
    }
}

// 避免异步方法中的阻塞调用
public async Task ProcessImagesAsync(List<string> imagePaths)
{
    // 错误：使用 .Result 会导致死锁
    // var result = SomeAsyncMethod().Result;
    
    // 正确：使用 await
    var result = await SomeAsyncMethod();
    
    // 并行处理
    var tasks = imagePaths.Select(ProcessImageAsync);
    await Task.WhenAll(tasks);
}
```

### 2. 错误处理

#### 统一异常处理
```csharp
public class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly INotificationService _notificationService;
    
    public async Task HandleExceptionAsync(Exception exception)
    {
        _logger.LogError(exception, "未处理的异常");
        
        var userMessage = exception switch
        {
            FileNotFoundException => "找不到指定的文件",
            UnauthorizedAccessException => "没有访问权限",
            OutOfMemoryException => "内存不足，请关闭其他应用程序",
            OnnxRuntimeException => "AI模型处理失败",
            _ => "发生未知错误，请重试"
        };
        
        await _notificationService.ShowErrorAsync(userMessage);
    }
}
```

### 3. 测试策略

#### 单元测试示例
```csharp
[TestClass]
public class ImageServiceTests
{
    private ImageService _imageService;
    private Mock<ILogger<ImageService>> _mockLogger;
    
    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ImageService>>();
        _imageService = new ImageService(_mockLogger.Object);
    }
    
    [TestMethod]
    public async Task LoadImageAsync_ValidPath_ReturnsImage()
    {
        // Arrange
        var imagePath = "test-image.jpg";
        
        // Act
        var result = await _imageService.LoadImageAsync(imagePath);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Width > 0);
        Assert.IsTrue(result.Height > 0);
    }
    
    [TestMethod]
    public async Task LoadImageAsync_InvalidPath_ThrowsException()
    {
        // Arrange
        var invalidPath = "non-existent-file.jpg";
        
        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(
            () => _imageService.LoadImageAsync(invalidPath)
        );
    }
}
```

## 总结

本二次开发指南为AIlable项目提供了全面的扩展和定制指导：

### ✅ 已涵盖内容
1. **快速开始** - 环境搭建和项目结构理解
2. **功能扩展** - 新标注类型、AI模型、导出格式
3. **UI定制** - 自定义主题、控件、响应式布局
4. **性能优化** - 多线程处理、内存管理、渲染优化
5. **部署发布** - 跨平台打包、应用商店发布
6. **故障排除** - 常见问题解决和性能调优
7. **最佳实践** - 代码规范、错误处理、测试策略

### 🚀 开发建议
1. **模块化开发** - 保持功能模块的独立性和可测试性
2. **性能优先** - 充分利用已实现的多线程AI推理优化
3. **用户体验** - 注重界面响应性和错误提示
4. **跨平台兼容** - 确保新功能在所有平台上正常工作
5. **文档同步** - 及时更新相关文档和注释

### 📈 扩展方向
1. **AI能力增强** - 集成更多AI模型和算法
2. **协作功能** - 支持多用户协作标注
3. **云端集成** - 支持云存储和在线同步
4. **自动化工具** - 智能标注建议和批量处理
5. **插件系统** - 支持第三方插件扩展

通过本指南，开发者可以快速上手AIlable项目的二次开发，实现功能扩展和定制需求。项目的模块化架构和完善的服务层为各种扩展提供了良好的基础。
# AIlable项目 - 二次开发指南

## 概述

本指南面向希望基于AIlable项目进行二次开发的开发者，提供详细的功能扩展指南、自定义开发教程和最佳实践建议。AIlable采用模块化设计，支持灵活的功能扩展和定制。

## 快速开始

### 1. 开发环境准备

#### 必需软件
```bash
# .NET SDK 9.0+
dotnet --version

# Git
git --version

# 推荐IDE之一
# - Visual Studio 2022 (Windows)
# - JetBrains Rider (跨平台)
# - VS Code + C# Extension (跨平台)
```

#### 项目克隆和初始化
```bash
# 克隆项目
git clone <your-fork-url>
cd AIlable

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet test

# 启动桌面版本
dotnet run --project AIlable.Desktop
```

### 2. 项目结构理解

```
AIlable/
├── AIlable/                    # 核心库项目
│   ├── Models/                 # 数据模型
│   ├── ViewModels/            # 视图模型
│   ├── Views/                 # 用户界面
│   ├── Services/              # 业务服务
│   ├── Controls/              # 自定义控件
│   ├── Styles/                # 样式和主题
│   └── Assets/                # 资源文件
├── AIlable.Desktop/           # 桌面平台项目
├── AIlable.Android/           # Android平台项目
├── AIlable.iOS/               # iOS平台项目
├── AIlable.Browser/           # Web平台项目
└── docs/                      # 项目文档
```

### 3. 核心概念理解

#### MVVM架构模式
- **Model**: 数据模型和业务逻辑
- **View**: 用户界面（AXAML文件）
- **ViewModel**: 视图逻辑和数据绑定

#### 依赖注入容器
- 服务注册在 `Program.cs` 中完成
- 使用接口抽象实现松耦合
- 支持单例、瞬态、作用域生命周期

#### 服务层架构
- **AI服务**: 模型推理和管理
- **业务服务**: 项目管理、导出等
- **基础服务**: 配置、通知、主题等

## 功能扩展指南

### 1. 添加新的标注类型

#### 步骤1: 创建标注模型

```csharp
// Models/EllipseAnnotation.cs
using System;
using Avalonia;

namespace AIlable.Models
{
    public class EllipseAnnotation : Annotation
    {
        public override AnnotationType Type => AnnotationType.Ellipse;
        
        /// <summary>
        /// 椭圆中心点
        /// </summary>
        public Point2D Center { get; set; } = new();
        
        /// <summary>
        /// 水平半径
        /// </summary>
        public double RadiusX { get; set; }
        
        /// <summary>
        /// 垂直半径
        /// </summary>
        public double RadiusY { get; set; }
        
        /// <summary>
        /// 旋转角度（弧度）
        /// </summary>
        public double Rotation { get; set; }
        
        public override Rect GetBounds()
        {
            // 计算椭圆的边界矩形
            var maxRadius = Math.Max(RadiusX, RadiusY);
            return new Rect(
                Center.X - maxRadius,
                Center.Y - maxRadius,
                maxRadius * 2,
                maxRadius * 2
            );
        }
        
        public override bool Contains(Point point)
        {
            // 椭圆包含点的数学计算
            var dx = point.X - Center.X;
            var dy = point.Y - Center.Y;
            
            // 考虑旋转的椭圆方程
            var cos = Math.Cos(-Rotation);
            var sin = Math.Sin(-Rotation);
            var rotatedX = dx * cos - dy * sin;
            var rotatedY = dx * sin + dy * cos;
            
            return (rotatedX * rotatedX) / (RadiusX * RadiusX) + 
                   (rotatedY * rotatedY) / (RadiusY * RadiusY) <= 1.0;
        }
    }
}
```

#### 步骤2: 扩展枚举类型

```csharp
// Models/Enums.cs
public enum AnnotationType
{
    Rectangle,
    Circle,
    Polygon,
    Line,
    Point,
    Keypoint,
    OrientedBoundingBox,
    Ellipse  // 新增椭圆类型
}

public enum AnnotationTool
{
    Select,
    Rectangle,
    Circle,
    Polygon,
    Line,
    Point,
    Keypoint,
    OrientedBoundingBox,
    Ellipse  // 新增椭圆工具
}
```

#### 步骤3: 实现标注工具

```csharp
// Services/EllipseTool.cs
using System;
using Avalonia;
using Avalonia.Media;
using AIlable.Models;

namespace AIlable.Services
{
    public class EllipseTool : IAnnotationTool
    {
        private Point _startPoint;
        private Point _currentPoint;
        private bool _isDrawing;
        
        public AnnotationType ToolType => AnnotationType.Ellipse;
        public bool IsDrawing => _isDrawing;
        
        public void StartDrawing(Point startPoint)
        {
            _startPoint = startPoint;
            _currentPoint = startPoint;
            _isDrawing = true;
        }
        
        public void UpdateDrawing(Point currentPoint)
        {
            if (!_isDrawing) return;
            _currentPoint = currentPoint;
        }
        
        public Annotation? FinishDrawing()
        {
            if (!_isDrawing) return null;
            
            _isDrawing = false;
            
            // 计算椭圆参数
            var center = new Point2D(
                (_startPoint.X + _currentPoint.X) / 2,
                (_startPoint.Y + _currentPoint.Y) / 2
            );
            
            var radiusX = Math.Abs(_currentPoint.X - _startPoint.X) / 2;
            var radiusY = Math.Abs(_currentPoint.Y - _startPoint.Y) / 2;
            
            return new EllipseAnnotation
            {
                Center = center,
                RadiusX = radiusX,
                RadiusY = radiusY,
                Rotation = 0,
                Label = "椭圆",
                Confidence = 1.0
            };
        }
        
        public void CancelDrawing()
        {
            _isDrawing = false;
        }
        
        public void Render(DrawingContext context, Annotation annotation)
        {
            if (annotation is not EllipseAnnotation ellipse) return;
            
            var pen = new Pen(Brushes.Red, 2);
            var center = new Point(ellipse.Center.X, ellipse.Center.Y);
            
            // 绘制椭圆
            var geometry = new EllipseGeometry(new Rect(
                center.X - ellipse.RadiusX,
                center.Y - ellipse.RadiusY,
                ellipse.RadiusX * 2,
                ellipse.RadiusY * 2
            ));
            
            // 应用旋转变换
            if (Math.Abs(ellipse.Rotation) > 0.001)
            {
                var transform = new RotateTransform(
                    ellipse.Rotation * 180 / Math.PI, 
                    center.X, 
                    center.Y
                );
                geometry.Transform = transform;
            }
            
            context.DrawGeometry(null, pen, geometry);
            
            // 绘制标签
            if (!string.IsNullOrEmpty(ellipse.Label))
            {
                var text = new FormattedText(
                    ellipse.Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    12,
                    Brushes.Red
                );
                
                context.DrawText(text, new Point(center.X, center.Y - ellipse.RadiusY - 15));
            }
        }
        
        public void RenderPreview(DrawingContext context)
        {
            if (!_isDrawing) return;
            
            var pen = new Pen(Brushes.Blue, 1) { DashStyle = DashStyle.Dash };
            var center = new Point(
                (_startPoint.X + _currentPoint.X) / 2,
                (_startPoint.Y + _currentPoint.Y) / 2
            );
            
            var radiusX = Math.Abs(_currentPoint.X - _startPoint.X) / 2;
            var radiusY = Math.Abs(_currentPoint.Y - _startPoint.Y) / 2;
            
            var geometry = new EllipseGeometry(new Rect(
                center.X - radiusX,
                center.Y - radiusY,
                radiusX * 2,
                radiusY * 2
            ));
            
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
```

#### 步骤4: 注册服务和工具

```csharp
// Program.cs 或服务注册文件
public static IServiceCollection AddAnnotationTools(this IServiceCollection services)
{
    // 现有工具...
    services.AddTransient<IAnnotationTool, EllipseTool>();
    
    return services;
}
```

#### 步骤5: 更新UI界面

```xml
<!-- Views/MainView.axaml -->
<Button Name="EllipseToolButton" 
        Classes="tool-button"
        Command="{Binding SelectToolCommand}"
        CommandParameter="{x:Static models:AnnotationTool.Ellipse}"
        ToolTip.Tip="椭圆工具">
    <PathIcon Data="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z" />
</Button>
```

### 2. 集成新的AI模型

#### 步骤1: 实现模型服务接口

```csharp
// Services/CustomModelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AIlable.Models;

namespace AIlable.Services
{
    public class CustomModelService : IAIModelService, IDisposable
    {
        private InferenceSession? _session;
        private CustomModelMetadata? _metadata;
        private bool _disposed = false;
        
        public string ModelName => "Custom Detection Model";
        public bool IsModelLoaded => _session != null;
        
        public async Task LoadModelAsync(string modelPath)
        {
            try
            {
                // 尝试使用GPU加速
                var sessionOptions = new SessionOptions();
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA();
                }
                catch
                {
                    // GPU不可用时回退到CPU
                    Console.WriteLine("CUDA不可用，使用CPU推理");
                }
                
                _session = new InferenceSession(modelPath, sessionOptions);
                
                // 加载模型元数据
                _metadata = await LoadModelMetadataAsync(modelPath);
                
                Console.WriteLine($"模型加载成功: {ModelName}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"模型加载失败: {ex.Message}", ex);
            }
        }
        
        public async Task<List<Annotation>> InferAsync(string imagePath, double confidenceThreshold)
        {
            if (_session == null || _metadata == null)
                throw new InvalidOperationException("模型未加载");
            
            try
            {
                // 1. 图像预处理
                var inputTensor = await PreprocessImageAsync(imagePath);
                
                // 2. 模型推理
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_metadata.InputName, inputTensor)
                };
                
                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();
                
                // 3. 后处理
                var detections = PostprocessOutput(outputTensor, confidenceThreshold);
                
                // 4. 转换为标注对象
                return ConvertToAnnotations(detections, imagePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"推理失败: {ex.Message}", ex);
            }
        }
        
        private async Task<DenseTensor<float>> PreprocessImageAsync(string imagePath)
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            
            // 调整图像大小到模型输入尺寸
            image.Mutate(x => x.Resize(_metadata!.InputWidth, _metadata.InputHeight));
            
            // 创建输入张量 [batch_size, channels, height, width]
            var tensor = new DenseTensor<float>(new[] { 1, 3, _metadata.InputHeight, _metadata.InputWidth });
            
            // 像素值归一化和通道重排
            for (int y = 0; y < _metadata.InputHeight; y++)
            {
                for (int x = 0; x < _metadata.InputWidth; x++)
                {
                    var pixel = image[x, y];
                    
                    // 归一化到 [0, 1] 并按 RGB 顺序排列
                    tensor[0, 0, y, x] = pixel.R / 255.0f;  // R通道
                    tensor[0, 1, y, x] = pixel.G / 255.0f;  // G通道
                    tensor[0, 2, y, x] = pixel.B / 255.0f;  // B通道
                }
            }
            
            return tensor;
        }
        
        private List<Detection> PostprocessOutput(Tensor<float> output, double confidenceThreshold)
        {
            var detections = new List<Detection>();
            
            // 假设输出格式为 [batch_size, num_detections, 6]
            // 其中每个检测包含: [x1, y1, x2, y2, confidence, class_id]
            var batchSize = output.Dimensions[0];
            var numDetections = output.Dimensions[1];
            var detectionSize = output.Dimensions[2];
            
            for (int i = 0; i < numDetections; i++)
            {
                var confidence = output[0, i, 4];
                
                if (confidence < confidenceThreshold) continue;
                
                var detection = new Detection
                {
                    X1 = output[0, i, 0],
                    Y1 = output[0, i, 1],
                    X2 = output[0, i, 2],
                    Y2 = output[0, i, 3],
                    Confidence = confidence,
                    ClassId = (int)output[0, i, 5]
                };
                
                detections.Add(detection);
            }
            
            // 非极大值抑制 (NMS)
            return ApplyNMS(detections, 0.5);
        }
        
        private List<Detection> ApplyNMS(List<Detection> detections, double iouThreshold)
        {
            // 按置信度降序排序
            detections = detections.OrderByDescending(d => d.Confidence).ToList();
            
            var result = new List<Detection>();
            var suppressed = new bool[detections.Count];
            
            for (int i = 0; i < detections.Count; i++)
            {
                if (suppressed[i]) continue;
                
                result.Add(detections[i]);
                
                // 抑制与当前检测重叠度高的其他检测
                for (int j = i + 1; j < detections.Count; j++)
                {
                    if (suppressed[j]) continue;
                    
                    var iou = CalculateIoU(detections[i], detections[j]);
                    if (iou > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }
            
            return result;
        }
        
        private double CalculateIoU(Detection det1, Detection det2)
        {
            // 计算交集
            var x1 = Math.Max(det1.X1, det2.X1);
            var y1 = Math.Max(det1.Y1, det2.Y1);
            var x2 = Math.Min(det1.X2, det2.X2);
            var y2 = Math.Min(det1.Y2, det2.Y2);
            
            if (x2 <= x1 || y2 <= y1) return 0.0;
            
            var intersection = (x2 - x1) * (y2 - y1);
            
            // 计算并集
            var area1 = (det1.X2 - det1.X1) * (det1.Y2 - det1.Y1);
            var area2 = (det2.X2 - det2.X1) * (det2.Y2 - det2.Y1);
            var union = area1 + area2 - intersection;
            
            return intersection / union;
        }
        
        private List<Annotation> ConvertToAnnotations(List<Detection> detections, string imagePath)
        {
            var annotations = new List<Annotation>();
            
            // 获取原始图像尺寸用于坐标转换
            using var image = Image.Load(imagePath);
            var scaleX = (double)image.Width / _metadata!.InputWidth;
            var scaleY = (double)image.Height / _metadata.InputHeight;
            
            foreach (var detection in detections)
            {
                // 将坐标从模型输入尺寸转换回原始图像尺寸
                var x = detection.X1 * scaleX;
                var y = detection.Y1 * scaleY;
                var width = (detection.X2 - detection.X1) * scaleX;
                var height = (detection.Y2 - detection.Y1) * scaleY;
                
                var annotation = new RectangleAnnotation
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Label = GetClassLabel(detection.ClassId),
                    Confidence = detection.Confidence
                };
                
                annotations.Add(annotation);
            }
            
            return annotations;
        }
        
        private string GetClassLabel(int classId)
        {
            // 根据类别ID返回标签名称
            return _metadata?.ClassNames?.ElementAtOrDefault(classId) ?? $"类别_{classId}";
        }
        
        private async Task<CustomModelMetadata> LoadModelMetadataAsync(string modelPath)
        {
            // 从模型文件或配置文件加载元数据
            // 这里使用默认值，实际应用中应该从配置文件读取
            return new CustomModelMetadata
            {
                InputName = "images",
                InputWidth = 640,
                InputHeight = 640,
                ClassNames = new[] { "person", "car", "bicycle", "dog", "cat" }
            };
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
        
        // 内部数据结构
        private class Detection
        {
            public float X1 { get; set; }
            public float Y1 { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }
            public float Confidence { get; set; }
            public int ClassId { get; set; }
        }
        
        private class CustomModelMetadata
        {
            public string InputName { get; set; } = string.Empty;
            public int InputWidth { get; set; }
            public int InputHeight { get; set; }
            public string[] ClassNames { get; set; } = Array.Empty<string>();
        }
    }
}
```

#### 步骤2: 注册模型服务

```csharp
// Program.cs
public static IServiceCollection AddAIServices(this IServiceCollection services)
{
    // 现有服务...
    services.AddTransient<IAIModelService, CustomModelService>();
    
    return services;
}
```

#### 步骤3: 在UI中集成新模型

```csharp
// ViewModels/AIInferenceDialogViewModel.cs
public partial class AIInferenceDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> availableModels = new()
    {
        "YOLO",
        "OBB",
        "Segmentation",
        "Custom"  // 新增自定义模型
    };
    
    private IAIModelService GetModelService(string modelType)
    {
        return modelType switch
        {
            "YOLO" => _serviceProvider.GetRequiredService<YoloModelService>(),
            "OBB" => _serviceProvider.GetRequiredService<OBBModelService>(),
            "Segmentation" => _serviceProvider.GetRequiredService<SegmentationModelService>(),
            "Custom" => _serviceProvider.GetRequiredService<CustomModelService>(),
            _ => throw new ArgumentException($"未知的模型类型: {modelType}")
        };
    }
}
```

### 3. 自定义导出格式

#### 步骤1: 实现导出服务

```csharp
// Services/CustomExportService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using AIlable.Models;

namespace AIlable.Services
{
    public class CustomExportService
    {
        /// <summary>
        /// 导出为自定义JSON格式
        /// </summary>
        public async Task ExportToCustomJsonAsync(AnnotationProject project, string outputPath)
        {
            var exportData = new
            {
                metadata = new
                {
                    project_name = project.Name,
                    description = project.Description,
                    created_at = project.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    last_modified = project.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
                    total_images = project.Images.Count,
                    total_annotations = project.Images.Sum(img => img.Annotations.Count),
                    export_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                labels = project.Labels.Select(kvp => new
                {
                    id = kvp.Key,
                    name = kvp.Value
                }).ToArray(),
                images = project.Images.Select(img => new
                {
                    file_path = img.FilePath,
                    file_name = Path.GetFileName(img.FilePath),
                    width = img.Metadata?.Width ?? 0,
                    height = img.Metadata?.Height ?? 0,
                    annotations = img.Annotations.Select(ann => SerializeAnnotation(ann)).ToArray()
                }).ToArray()
            };
            
            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);
        }
        
        /// <summary>
        /// 导出为XML格式
        /// </summary>
        public async Task ExportToXmlAsync(AnnotationProject project, string outputPath)
        {
            var root = new XElement("AnnotationProject",
                new XAttribute("name", project.Name),
                new XAttribute("created", project.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                
                new XElement("Labels",
                    project.Labels.Select(kvp =>
                        new XElement("Label",
                            new XAttribute("id", kvp.Key),
                            new XAttribute("name", kvp.Value)
                        )
                    )
                ),
                
                new XElement("Images",
                    project.Images.Select(img =>
                        new XElement("Image",
                            new XAttribute("path", img.FilePath),
                            new XAttribute("width", img.Metadata?.Width ?? 0),
                            new XAttribute("height", img.Metadata?.Height ?? 0),
                            
                            new XElement("Annotations",
                                img.Annotations.Select(ann => SerializeAnnotationToXml(ann))
                            )
                        )
                    )
                )
            );
            
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                root
            );
            
            await using var writer = File.CreateText(outputPath);
            await document.SaveAsync(writer, SaveOptions.None, default);
        }
        
        /// <summary>
        /// 导出为CSV格式
        /// </summary>
        public async Task ExportToCsvAsync(AnnotationProject project, string outputPath)
        {
            var lines = new List<string>
            {
                // CSV头部
                "image_path,image_name,annotation_id,annotation_type,label,confidence,x,y,width,height,additional_data"
            };
            
            foreach (var image in project.Images)
            {
                var imageName = Path.GetFileName(image.FilePath);
                
                foreach (var annotation in image.Annotations)
                {
                    var csvLine = $"{image.FilePath},{imageName},{annotation.Id},{annotation.Type}," +
                                 $"{annotation.Label},{annotation.Confidence:F4}," +
                                 $"{GetAnnotationCsvData(annotation)}";
                    
                    lines.Add(csvLine);
                }
            }
            
            await File.WriteAllLinesAsync(outputPath, lines);
        }
        
        /// <summary>
        /// 导出为COCO格式
        /// </summary>
        public async Task ExportToCocoAsync(AnnotationProject project, string outputPath)
        {
            var categories = project.Labels.Select((kvp, index) => new
            {
                id = index + 1,
                name = kvp.Value,
                supercategory = "object"
            }).ToArray();
            
            var images = project.Images.Select((img, index) => new
            {
                id = index + 1,
                file_name = Path.GetFileName(img.FilePath),
                width = img.Metadata?.Width ?? 0,
                height = img.Metadata?.Height ?? 0
            }).ToArray();
            
            var annotations = new List<object>();
            var annotationId = 1;
            
            for (int imgIndex = 0; imgIndex < project.Images.Count; imgIndex++)
            {
                var image = project.Images[imgIndex];
                var imageId = imgIndex + 1;
                
                foreach (var annotation in image.Annotations.OfType<RectangleAnnotation>())
                {
                    var categoryId = Array.FindIndex(categories, c => c.name == annotation.Label) + 1;
                    
                    annotations.Add(new
                    {
                        id = annotationId++,
                        image_id = imageId,