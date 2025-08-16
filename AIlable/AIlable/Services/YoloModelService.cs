using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// YOLO模型推理服务
/// </summary>
public class YoloModelService : IAIModelService
{
    private InferenceSession? _session;
    private string[]? _classNames;
    private readonly List<string> _projectLabels;
    
    public bool IsModelLoaded => _session != null;
    public string ModelName { get; private set; } = string.Empty;
    public AIModelType ModelType => AIModelType.YOLO;
    
    // YOLO模型默认输入尺寸
    private const int InputWidth = 640;
    private const int InputHeight = 640;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="projectLabels">项目标签列表，如果为null则使用默认COCO标签</param>
    public YoloModelService(List<string>? projectLabels = null)
    {
        _projectLabels = projectLabels ?? new List<string>();
    }
    
    public async Task<bool> LoadModelAsync(string modelPath)
    {
        try
        {
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Model file not found: {modelPath}");
                return false;
            }
            
            // 创建ONNX运行时会话
            var sessionOptions = new SessionOptions();
            _session = new InferenceSession(modelPath, sessionOptions);
            
            ModelName = Path.GetFileNameWithoutExtension(modelPath);
            
            // 尝试加载类别名称文件
            await LoadClassNamesAsync(modelPath);
            
            Console.WriteLine($"YOLO model loaded successfully: {ModelName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading YOLO model: {ex.Message}");
            UnloadModel();
            return false;
        }
    }
    
    private async Task LoadClassNamesAsync(string modelPath)
    {
        try
        {
            // 优先使用项目标签
            if (_projectLabels != null && _projectLabels.Count > 0)
            {
                _classNames = _projectLabels.ToArray();
                Console.WriteLine($"使用项目标签: {_classNames.Length} 个类别");
                foreach (var label in _classNames)
                {
                    Console.WriteLine($"  - {label}");
                }
                return;
            }
            
            // 尝试查找同名的.names或.txt文件
            var modelDir = Path.GetDirectoryName(modelPath);
            var modelName = Path.GetFileNameWithoutExtension(modelPath);
            
            var possibleClassFiles = new[]
            {
                Path.Combine(modelDir!, $"{modelName}.names"),
                Path.Combine(modelDir!, $"{modelName}.txt"),
                Path.Combine(modelDir!, "classes.names"),
                Path.Combine(modelDir!, "classes.txt")
            };
            
            foreach (var classFile in possibleClassFiles)
            {
                if (File.Exists(classFile))
                {
                    var lines = await File.ReadAllLinesAsync(classFile);
                    _classNames = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                    Console.WriteLine($"从文件加载 {_classNames.Length} 个类别: {classFile}");
                    return;
                }
            }
            
            // 如果找不到类别文件，使用默认的COCO类别
            _classNames = GetCocoClassNames();
            Console.WriteLine("使用默认COCO类别名称");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载类别名称出错: {ex.Message}");
            _classNames = GetCocoClassNames();
        }
    }
    
    private static string[] GetCocoClassNames()
    {
        return new[]
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
            "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
            "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
            "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
            "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
            "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop",
            "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
            "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };
    }
    
    /// <summary>
    /// 更新项目标签
    /// </summary>
    /// <param name="projectLabels">新的项目标签列表</param>
    public void UpdateProjectLabels(List<string> projectLabels)
    {
        _projectLabels.Clear();
        _projectLabels.AddRange(projectLabels);
        
        // 如果模型已加载，更新类别名称
        if (IsModelLoaded)
        {
            if (_projectLabels.Count > 0)
            {
                _classNames = _projectLabels.ToArray();
                Console.WriteLine($"更新模型类别为项目标签: {_classNames.Length} 个类别");
            }
        }
    }
    
    public void UnloadModel()
    {
        _session?.Dispose();
        _session = null;
        _classNames = null;
        ModelName = string.Empty;
    }
    
    public async Task<IEnumerable<Annotation>> InferAsync(string imagePath, float confidenceThreshold = 0.5f)
    {
        if (!IsModelLoaded || !File.Exists(imagePath))
        {
            return Array.Empty<Annotation>();
        }
        
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            
            // 预处理图像
            var input = PreprocessImage(image);
            
            // 运行推理
            var inputName = _session!.InputMetadata.Keys.FirstOrDefault() ?? "images";
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, input) };
            
            using var results = _session.Run(inputs);
            var output = results.FirstOrDefault()?.AsTensor<float>();
            
            if (output == null)
                return Array.Empty<Annotation>();
            
            // 后处理结果
            return PostprocessResults(output, originalWidth, originalHeight, confidenceThreshold);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during YOLO inference: {ex.Message}");
            return Array.Empty<Annotation>();
        }
    }
    
    public async Task<Dictionary<string, IEnumerable<Annotation>>> InferBatchAsync(IEnumerable<string> imagePaths, float confidenceThreshold = 0.5f)
    {
        var imagePathsList = imagePaths.ToList();
        var results = new Dictionary<string, IEnumerable<Annotation>>();
        
        // 获取系统CPU核心数，限制并发数以避免过度占用资源
        var maxConcurrency = Math.Min(Environment.ProcessorCount, 4);
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        Console.WriteLine($"开始批量推理 {imagePathsList.Count} 张图片，最大并发数: {maxConcurrency}");
        
        var tasks = imagePathsList.Select(async imagePath =>
        {
            await semaphore.WaitAsync();
            try
            {
                var annotations = await InferAsync(imagePath, confidenceThreshold);
                lock (results)
                {
                    results[imagePath] = annotations;
                }
                Console.WriteLine($"完成推理: {Path.GetFileName(imagePath)} ({results.Count}/{imagePathsList.Count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推理失败 {imagePath}: {ex.Message}");
                lock (results)
                {
                    results[imagePath] = Array.Empty<Annotation>();
                }
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        semaphore.Dispose();
        
        Console.WriteLine($"批量推理完成，共处理 {results.Count} 张图片");
        return results;
    }
    
    private Tensor<float> PreprocessImage(Image<Rgb24> image)
    {
        // 调整图像大小并保持长宽比
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(InputWidth, InputHeight),
            Mode = ResizeMode.Pad,
            PadColor = Color.Black
        }));
        
        // 创建输入张量 [batch_size, channels, height, width]
        var tensor = new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });
        
        // 将图像数据转换为张量，并进行归一化 (0-255 -> 0-1)
        for (int y = 0; y < InputHeight; y++)
        {
            for (int x = 0; x < InputWidth; x++)
            {
                var pixel = image[x, y];
                tensor[0, 0, y, x] = pixel.R / 255.0f; // R
                tensor[0, 1, y, x] = pixel.G / 255.0f; // G  
                tensor[0, 2, y, x] = pixel.B / 255.0f; // B
            }
        }
        
        return tensor;
    }
    
    private List<Annotation> PostprocessResults(Tensor<float> output, int originalWidth, int originalHeight, float confidenceThreshold)
    {
        var annotations = new List<Annotation>();

        var outputDims = output.Dimensions.ToArray();
        Console.WriteLine($"YOLO输出维度: [{string.Join(", ", outputDims)}]");

        // 检查是否是已经过NMS处理的输出格式 [1, 300, 6]
        if (outputDims.Length == 3 && outputDims[1] <= 300 && outputDims[2] == 6)
        {
            // 格式: [1, 300, 6] - 已经过NMS处理，每个检测包含: [x1, y1, x2, y2, confidence, class_id]
            var numDetections = outputDims[1]; // 300
            Console.WriteLine($"检测到NMS处理后的YOLO格式，最大检测数: {numDetections}");

            // 计算缩放比例和填充偏移（用于坐标转换）
            var scaleX = (float)InputWidth / originalWidth;
            var scaleY = (float)InputHeight / originalHeight;
            var scale = Math.Min(scaleX, scaleY);
            
            var scaledWidth = originalWidth * scale;
            var scaledHeight = originalHeight * scale;
            var padX = (InputWidth - scaledWidth) / 2;
            var padY = (InputHeight - scaledHeight) / 2;
            
            Console.WriteLine($"原始尺寸: ({originalWidth}, {originalHeight}), 缩放比例: {scale:F3}, padding: ({padX:F2}, {padY:F2})");

            for (int i = 0; i < numDetections; i++)
            {
                // 提取置信度和类别ID
                var confidence = output[0, i, 4];
                var classId = (int)output[0, i, 5];

                // 过滤低置信度检测
                if (confidence < confidenceThreshold)
                {
                    Console.WriteLine($"过滤低置信度检测: {confidence:F2} < {confidenceThreshold:F2}");
                    continue;
                }

                // 提取边界框坐标 (已经是xyxy格式)
                var x1_model = output[0, i, 0];
                var y1_model = output[0, i, 1];
                var x2_model = output[0, i, 2];
                var y2_model = output[0, i, 3];

                Console.WriteLine($"模型输出坐标: ({x1_model:F2}, {y1_model:F2}) - ({x2_model:F2}, {y2_model:F2}), 置信度: {confidence:F2}, 类别: {classId}");

                // 转换坐标：从640x640模型坐标系转换到原始图像坐标系
                // 1. 减去填充偏移
                // 2. 除以缩放比例
                var x1 = (x1_model - padX) / scale;
                var y1 = (y1_model - padY) / scale;
                var x2 = (x2_model - padX) / scale;
                var y2 = (y2_model - padY) / scale;

                Console.WriteLine($"转换后坐标: ({x1:F2}, {y1:F2}) - ({x2:F2}, {y2:F2})");

                // 确保坐标在图像范围内
                x1 = Math.Max(0, Math.Min(originalWidth, x1));
                y1 = Math.Max(0, Math.Min(originalHeight, y1));
                x2 = Math.Max(0, Math.Min(originalWidth, x2));
                y2 = Math.Max(0, Math.Min(originalHeight, y2));

                // 跳过无效的边界框
                if (x2 <= x1 || y2 <= y1)
                {
                    Console.WriteLine($"跳过无效边界框: ({x1:F2}, {y1:F2}) - ({x2:F2}, {y2:F2})");
                    continue;
                }

                // 创建矩形标注
                var className = GetClassName(classId);
                var labelColor = LabelColorService.GetColorForLabel(className);
                var annotation = new RectangleAnnotation
                {
                    Id = Guid.NewGuid().ToString(),
                    Label = $"{className} ({confidence:F2})",
                    TopLeft = new Point2D(x1, y1),
                    BottomRight = new Point2D(x2, y2),
                    Color = labelColor, // 使用LabelColorService分配的颜色
                    StrokeWidth = 2,
                    IsVisible = true
                };

                annotations.Add(annotation);
                Console.WriteLine($"添加标注: {className} ({confidence:F2}) at ({x1:F0}, {y1:F0}) - ({x2:F0}, {y2:F0})");
            }
        }
        else if (outputDims.Length == 3 && outputDims[1] > outputDims[2])
        {
            // 格式: [1, 84, 8400] -> 需要转置和NMS处理
            var numFeatures = outputDims[1];  // 84
            var numDetections = outputDims[2]; // 8400

            Console.WriteLine($"检测到YOLOv8原始格式，特征数: {numFeatures}, 检测数: {numDetections}，需要NMS处理");
            
            // 收集所有检测结果用于NMS
            var detections = new List<Detection>();
            
            for (int i = 0; i < numDetections; i++)
            {
                // 提取边界框坐标 (中心点格式)
                var centerX = output[0, 0, i];
                var centerY = output[0, 1, i];
                var width = output[0, 2, i];
                var height = output[0, 3, i];

                // 找到最高置信度的类别 (从第4个特征开始是类别分数)
                var maxClassScore = 0.0f;
                var maxClassIndex = 0;

                for (int j = 4; j < numFeatures; j++)
                {
                    var classScore = output[0, j, i];
                    if (classScore > maxClassScore)
                    {
                        maxClassScore = classScore;
                        maxClassIndex = j - 4; // 类别索引从0开始
                    }
                }

                // 过滤低置信度检测
                if (maxClassScore < confidenceThreshold)
                    continue;
                
                // 转换为xyxy格式
                var x1 = centerX - width / 2;
                var y1 = centerY - height / 2;
                var x2 = centerX + width / 2;
                var y2 = centerY + height / 2;
                
                detections.Add(new Detection
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Confidence = maxClassScore,
                    ClassId = maxClassIndex
                });
            }
            
            // 应用NMS
            var nmsResults = ApplyNMS(detections, 0.45f); // IoU阈值
            
            // 转换NMS结果为标注
            var scaleX = (float)InputWidth / originalWidth;
            var scaleY = (float)InputHeight / originalHeight;
            var scale = Math.Min(scaleX, scaleY);
            
            var scaledWidth = originalWidth * scale;
            var scaledHeight = originalHeight * scale;
            var padX = (InputWidth - scaledWidth) / 2;
            var padY = (InputHeight - scaledHeight) / 2;
            
            foreach (var detection in nmsResults)
            {
                // 坐标转换
                var x1 = (detection.X1 - padX) / scale;
                var y1 = (detection.Y1 - padY) / scale;
                var x2 = (detection.X2 - padX) / scale;
                var y2 = (detection.Y2 - padY) / scale;

                // 确保坐标在图像范围内
                x1 = Math.Max(0, Math.Min(originalWidth, x1));
                y1 = Math.Max(0, Math.Min(originalHeight, y1));
                x2 = Math.Max(0, Math.Min(originalWidth, x2));
                y2 = Math.Max(0, Math.Min(originalHeight, y2));

                if (x2 <= x1 || y2 <= y1) continue;

                var className = GetClassName(detection.ClassId);
                var labelColor = LabelColorService.GetColorForLabel(className);
                var annotation = new RectangleAnnotation
                {
                    Id = Guid.NewGuid().ToString(),
                    Label = $"{className} ({detection.Confidence:F2})",
                    TopLeft = new Point2D(x1, y1),
                    BottomRight = new Point2D(x2, y2),
                    Color = labelColor, // 使用LabelColorService分配的颜色
                    StrokeWidth = 2,
                    IsVisible = true
                };

                annotations.Add(annotation);
            }
        }
        else
        {
            Console.WriteLine($"未知的YOLO输出格式: [{string.Join(", ", outputDims)}]");
        }

        Console.WriteLine($"YOLO推理完成，检测到 {annotations.Count} 个对象");
        return annotations;
    }
    
    private string GetClassName(int classIndex)
    {
        if (_classNames != null && classIndex >= 0 && classIndex < _classNames.Length)
        {
            return _classNames[classIndex];
        }
        return $"class_{classIndex}";
    }
    
    // 检测结果类，用于NMS处理
    private class Detection
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
        public float Area => (X2 - X1) * (Y2 - Y1);
    }
    
    // NMS（非极大值抑制）实现
    private List<Detection> ApplyNMS(List<Detection> detections, float iouThreshold)
    {
        if (detections.Count == 0) return new List<Detection>();
        
        // 按置信度降序排序
        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        
        var result = new List<Detection>();
        var suppressed = new bool[detections.Count];
        
        for (int i = 0; i < detections.Count; i++)
        {
            if (suppressed[i]) continue;
            
            result.Add(detections[i]);
            
            // 抑制重叠的检测框
            for (int j = i + 1; j < detections.Count; j++)
            {
                if (suppressed[j]) continue;
                
                // 计算IoU
                var iou = CalculateIoU(detections[i], detections[j]);
                if (iou > iouThreshold)
                {
                    suppressed[j] = true;
                }
            }
        }
        
        Console.WriteLine($"NMS: {detections.Count} -> {result.Count} 检测结果");
        return result;
    }
    
    // 计算两个边界框的IoU（交并比）
    private float CalculateIoU(Detection a, Detection b)
    {
        // 计算交集区域
        var intersectionX1 = Math.Max(a.X1, b.X1);
        var intersectionY1 = Math.Max(a.Y1, b.Y1);
        var intersectionX2 = Math.Min(a.X2, b.X2);
        var intersectionY2 = Math.Min(a.Y2, b.Y2);
        
        if (intersectionX2 <= intersectionX1 || intersectionY2 <= intersectionY1)
            return 0.0f;
        
        var intersectionArea = (intersectionX2 - intersectionX1) * (intersectionY2 - intersectionY1);
        var unionArea = a.Area + b.Area - intersectionArea;
        
        return unionArea > 0 ? intersectionArea / unionArea : 0.0f;
    }
}