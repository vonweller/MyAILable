using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    
    public bool IsModelLoaded => _session != null;
    public string ModelName { get; private set; } = string.Empty;
    public AIModelType ModelType => AIModelType.YOLO;
    
    // YOLO模型默认输入尺寸
    private const int InputWidth = 640;
    private const int InputHeight = 640;
    
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
                    Console.WriteLine($"Loaded {_classNames.Length} class names from {classFile}");
                    return;
                }
            }
            
            // 如果找不到类别文件，使用默认的COCO类别
            _classNames = GetCocoClassNames();
            Console.WriteLine("Using default COCO class names");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading class names: {ex.Message}");
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
        var results = new Dictionary<string, IEnumerable<Annotation>>();
        
        foreach (var imagePath in imagePaths)
        {
            var annotations = await InferAsync(imagePath, confidenceThreshold);
            results[imagePath] = annotations;
        }
        
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

        // YOLOv8输出格式: [batch_size, 84, 8400] 其中84 = 4(坐标) + 80(类别分数)
        // 需要转置为 [batch_size, 8400, 84] 格式进行处理
        var outputDims = output.Dimensions.ToArray();
        Console.WriteLine($"YOLO输出维度: [{string.Join(", ", outputDims)}]");

        int numDetections, numFeatures;

        if (outputDims.Length == 3 && outputDims[1] > outputDims[2])
        {
            // 格式: [1, 84, 8400] -> 需要转置
            numFeatures = outputDims[1];  // 84
            numDetections = outputDims[2]; // 8400

            Console.WriteLine($"检测到YOLOv8格式，特征数: {numFeatures}, 检测数: {numDetections}");

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

                // 使用类别分数作为最终置信度
                if (maxClassScore < confidenceThreshold)
                    continue;

                // 添加调试信息
                Console.WriteLine($"检测到目标: 中心({centerX:F2}, {centerY:F2}), 尺寸({width:F2}, {height:F2}), 置信度: {maxClassScore:F2}");

                // 计算缩放比例 - 修正逻辑
                var scaleX = (float)InputWidth / originalWidth;
                var scaleY = (float)InputHeight / originalHeight;
                var scale = Math.Min(scaleX, scaleY);

                // 计算实际缩放后的尺寸
                var scaledWidth = originalWidth * scale;
                var scaledHeight = originalHeight * scale;

                // 计算padding偏移
                var padX = (InputWidth - scaledWidth) / 2;
                var padY = (InputHeight - scaledHeight) / 2;

                Console.WriteLine($"原始尺寸: ({originalWidth}, {originalHeight}), 缩放比例: {scale:F3}, padding: ({padX:F2}, {padY:F2})");

                // 转换坐标 (从模型输出坐标转换到原始图像坐标)
                // 模型输出的坐标是在640x640输入图像上的，需要转换回原始图像
                var x1 = (centerX - width / 2 - padX) / scale;
                var y1 = (centerY - height / 2 - padY) / scale;
                var x2 = (centerX + width / 2 - padX) / scale;
                var y2 = (centerY + height / 2 - padY) / scale;

                Console.WriteLine($"转换后坐标: ({x1:F2}, {y1:F2}) - ({x2:F2}, {y2:F2})");

                // 确保坐标在图像范围内
                x1 = Math.Max(0, Math.Min(originalWidth, x1));
                y1 = Math.Max(0, Math.Min(originalHeight, y1));
                x2 = Math.Max(0, Math.Min(originalWidth, x2));
                y2 = Math.Max(0, Math.Min(originalHeight, y2));

                // 跳过无效的边界框
                if (x2 <= x1 || y2 <= y1)
                    continue;

                // 创建矩形标注
                var className = GetClassName(maxClassIndex);
                var annotation = new RectangleAnnotation
                {
                    Id = Guid.NewGuid().ToString(),
                    Label = $"{className} ({maxClassScore:F2})",
                    TopLeft = new Point2D(x1, y1),
                    BottomRight = new Point2D(x2, y2),
                    Color = "#FF0000", // 红色
                    StrokeWidth = 2,
                    IsVisible = true
                };

                annotations.Add(annotation);
            }
        }
        else
        {
            // 处理其他格式 (如旧版YOLO格式)
            Console.WriteLine("检测到其他YOLO格式，使用兼容模式");
            numDetections = outputDims[1];
            var numClasses = outputDims[2] - 5; // 减去 x, y, w, h, confidence

            for (int i = 0; i < numDetections; i++)
            {
                var confidence = output[0, i, 4];
                if (confidence < confidenceThreshold)
                    continue;

                // 找到最高置信度的类别
                var maxClassScore = 0.0f;
                var maxClassIndex = 0;

                for (int j = 0; j < numClasses; j++)
                {
                    var classScore = output[0, i, 5 + j];
                    if (classScore > maxClassScore)
                    {
                        maxClassScore = classScore;
                        maxClassIndex = j;
                    }
                }

                var finalConfidence = confidence * maxClassScore;
                if (finalConfidence < confidenceThreshold)
                    continue;

                // 提取边界框坐标
                var centerX = output[0, i, 0];
                var centerY = output[0, i, 1];
                var width = output[0, i, 2];
                var height = output[0, i, 3];

                // 转换到原始图像坐标
                var scaleX = (float)originalWidth / InputWidth;
                var scaleY = (float)originalHeight / InputHeight;

                var x1 = (centerX - width / 2) * scaleX;
                var y1 = (centerY - height / 2) * scaleY;
                var x2 = (centerX + width / 2) * scaleX;
                var y2 = (centerY + height / 2) * scaleY;

                // 确保坐标在图像范围内
                x1 = Math.Max(0, Math.Min(originalWidth, x1));
                y1 = Math.Max(0, Math.Min(originalHeight, y1));
                x2 = Math.Max(0, Math.Min(originalWidth, x2));
                y2 = Math.Max(0, Math.Min(originalHeight, y2));

                // 跳过无效的边界框
                if (x2 <= x1 || y2 <= y1)
                    continue;

                // 创建矩形标注
                var className = GetClassName(maxClassIndex);
                var annotation = new RectangleAnnotation
                {
                    Id = Guid.NewGuid().ToString(),
                    Label = $"{className} ({finalConfidence:F2})",
                    TopLeft = new Point2D(x1, y1),
                    BottomRight = new Point2D(x2, y2),
                    Color = "#FF0000", // 红色
                    StrokeWidth = 2,
                    IsVisible = true
                };

                annotations.Add(annotation);
            }
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
}