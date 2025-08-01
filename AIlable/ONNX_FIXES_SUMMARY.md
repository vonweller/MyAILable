# ONNX推理修复总结

## 修复内容

### 1. 支持多种ONNX输出格式

#### 已处理NMS的输出格式 [1, 300, 6]
- **格式**: `[batch_size, max_detections, 6]`
- **数据**: `[x1, y1, x2, y2, confidence, class_id]`
- **特点**: 已经过NMS处理，直接使用坐标转换

#### 原始YOLO输出格式 [1, 84, 8400]  
- **格式**: `[batch_size, features, detections]`
- **数据**: 前4个为中心坐标+宽高，后80个为类别分数
- **处理**: 需要NMS后处理

### 2. 正确的坐标转换逻辑

#### 修复前的问题
```csharp
// 错误的转换方式
var x1 = (centerX - width / 2 - padX) / scale;
```

#### 修复后的正确方式
```csharp
// 正确的坐标转换步骤：
// 1. 计算缩放比例和填充偏移
var scaleX = (float)InputWidth / originalWidth;
var scaleY = (float)InputHeight / originalHeight;
var scale = Math.Min(scaleX, scaleY);

var scaledWidth = originalWidth * scale;
var scaledHeight = originalHeight * scale;
var padX = (InputWidth - scaledWidth) / 2;
var padY = (InputHeight - scaledHeight) / 2;

// 2. 转换坐标：减去填充偏移，除以缩放比例
var x1 = (x1_model - padX) / scale;
var y1 = (y1_model - padY) / scale;
```

### 3. 完整的NMS实现

#### 新增NMS类和方法
```csharp
private class Detection
{
    public float X1, Y1, X2, Y2;
    public float Confidence;
    public int ClassId;
    public float Area => (X2 - X1) * (Y2 - Y1);
}

private List<Detection> ApplyNMS(List<Detection> detections, float iouThreshold)
private float CalculateIoU(Detection a, Detection b)
```

#### NMS处理流程
1. 按置信度降序排序
2. 保留最高置信度的检测框
3. 抑制与其IoU超过阈值的其他框
4. 重复直到处理完所有框

### 4. 增强的调试输出

```csharp
Console.WriteLine($"YOLO输出维度: [{string.Join(", ", outputDims)}]");
Console.WriteLine($"模型输出坐标: ({x1_model:F2}, {y1_model:F2}) - ({x2_model:F2}, {y2_model:F2})");
Console.WriteLine($"转换后坐标: ({x1:F2}, {y1:F2}) - ({x2:F2}, {y2:F2})");
```

## 技术细节

### 支持的模型输出格式

1. **已处理NMS的模型**: `[1, 300, 6]`
   - 直接进行坐标转换
   - 适用于经过后处理的ONNX模型

2. **原始YOLO模型**: `[1, 84, 8400]`  
   - 需要置信度过滤
   - 需要NMS处理
   - 需要坐标格式转换（cxcywh -> xyxy）

### 坐标系统转换

#### 输入预处理
- 原始图像 → 640x640 (保持比例，黑边填充)

#### 输出后处理  
- 640x640坐标 → 原始图像坐标
- 考虑缩放比例和填充偏移

### 性能优化

1. **早期过滤**: 在NMS前先过滤低置信度检测
2. **高效IoU计算**: 优化的交并比计算
3. **内存管理**: 合理的对象创建和销毁

## 使用示例

### 基本用法
```csharp
var yoloService = new YoloModelService();
await yoloService.LoadModelAsync("model.onnx");
var annotations = await yoloService.InferAsync("image.jpg", 0.5f);
```

### 批量处理
```csharp
var imagePaths = new[] { "img1.jpg", "img2.jpg", "img3.jpg" };
var results = await yoloService.InferBatchAsync(imagePaths, 0.5f);
```

## 测试建议

1. **测试不同格式的ONNX模型**
   - 已处理NMS的模型
   - 原始YOLOv8模型
   - 自定义后处理模型

2. **测试不同尺寸的图像**
   - 正方形图像 (640x640)
   - 宽图像 (1920x640) 
   - 高图像 (640x1920)

3. **验证坐标准确性**
   - 检查检测框是否准确覆盖目标
   - 验证不同缩放比例下的坐标转换

## 已知限制

1. 目前仅支持640x640输入尺寸
2. NMS的IoU阈值固定为0.45
3. 最大检测数量限制为300个

## 后续改进建议

1. 支持动态输入尺寸
2. 可配置的NMS参数
3. 支持更多的ONNX输出格式
4. 性能监控和基准测试