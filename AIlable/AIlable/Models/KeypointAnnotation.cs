using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

/// <summary>
/// 关键点标注类，用于人体姿态估计
/// 支持COCO格式的17个关键点
/// </summary>
public partial class KeypointAnnotation : Annotation
{
    /// <summary>
    /// COCO人体姿态关键点定义
    /// </summary>
    public static readonly string[] CocoKeypointNames = 
    {
        "nose",           // 0: 鼻子
        "left_eye",       // 1: 左眼
        "right_eye",      // 2: 右眼
        "left_ear",       // 3: 左耳
        "right_ear",      // 4: 右耳
        "left_shoulder",  // 5: 左肩
        "right_shoulder", // 6: 右肩
        "left_elbow",     // 7: 左肘
        "right_elbow",    // 8: 右肘
        "left_wrist",     // 9: 左腕
        "right_wrist",    // 10: 右腕
        "left_hip",       // 11: 左髋
        "right_hip",      // 12: 右髋
        "left_knee",      // 13: 左膝
        "right_knee",     // 14: 右膝
        "left_ankle",     // 15: 左踝
        "right_ankle"     // 16: 右踝
    };

    /// <summary>
    /// 人体骨骼连接定义（用于绘制骨架）
    /// 按照COCO标准格式定义，索引基于0-based的关键点数组 (0-16)
    /// COCO 17关键点标准骨架连接
    /// </summary>
    public static readonly int[,] CocoSkeleton = 
    {
        {0, 1}, {0, 2}, {1, 3}, {2, 4},           // 头部连接
        {5, 6}, {5, 7}, {7, 9}, {6, 8}, {8, 10}, // 上躯连接
        {5, 11}, {6, 12}, {11, 12},              // 躯干连接
        {11, 13}, {13, 15}, {12, 14}, {14, 16}   // 下躯连接
    };

    /// <summary>
    /// 边界框信息（用于框选人体区域）
    /// </summary>
    public Point2D BoundingBoxStart { get; set; }
    public Point2D BoundingBoxEnd { get; set; }
    public bool HasBoundingBox { get; set; } = false;
    
    [ObservableProperty] private List<Keypoint> _keypoints;
    [ObservableProperty] private bool _showSkeleton = true;

    public KeypointAnnotation() : base(AnnotationType.Keypoint)
    {
        _keypoints = new List<Keypoint>(); // 开始时为空列表
    }

    public KeypointAnnotation(Point2D boundingBoxStart) : base(AnnotationType.Keypoint)
    {
        _keypoints = new List<Keypoint>();
        BoundingBoxStart = boundingBoxStart;
        BoundingBoxEnd = boundingBoxStart;
        HasBoundingBox = true;
    }

    /// <summary>
    /// 添加一个新的关键点
    /// </summary>
    public void AddKeypoint(Point2D position, string keypointLabel)
    {
        // 检查是否已经存在相同标签的关键点
        var existingKeypoint = Keypoints.FirstOrDefault(k => k.Label == keypointLabel);
        if (existingKeypoint != null)
        {
            // 更新现有关键点的位置
            existingKeypoint.Position = position;
            existingKeypoint.Visibility = KeypointVisibility.Visible;
        }
        else
        {
            // 添加新的关键点
            var keypoint = new Keypoint
            {
                Id = Keypoints.Count,
                Label = keypointLabel,
                Name = keypointLabel, // 关键点标签就是名称
                Position = position,
                Visibility = KeypointVisibility.Visible
            };
            Keypoints.Add(keypoint);
        }
        
        UpdateModifiedTime();
    }

    /// <summary>
    /// 移除指定标签的关键点
    /// </summary>
    public bool RemoveKeypoint(string keypointLabel)
    {
        var keypoint = Keypoints.FirstOrDefault(k => k.Label == keypointLabel);
        if (keypoint != null)
        {
            Keypoints.Remove(keypoint);
            // 重新分配ID
            for (int i = 0; i < Keypoints.Count; i++)
            {
                Keypoints[i].Id = i;
            }
            UpdateModifiedTime();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 切换关键点的可见性状态（可见 -> 遮挡 -> 不标注 -> 可见）
    /// 学习自X-AnyLabeling的优秀实践
    /// </summary>
    public void CycleKeypointVisibility(Keypoint keypoint)
    {
        switch (keypoint.Visibility)
        {
            case KeypointVisibility.Visible:
                keypoint.Visibility = KeypointVisibility.Occluded;
                break;
            case KeypointVisibility.Occluded:
                keypoint.Visibility = KeypointVisibility.NotAnnotated;
                break;
            case KeypointVisibility.NotAnnotated:
                keypoint.Visibility = KeypointVisibility.Visible;
                break;
        }
        UpdateModifiedTime();
    }
    
    /// <summary>
    /// 获取COCO格式的关键点数据（兼容X-AnyLabeling和标准格式）
    /// 格式：[x1, y1, v1, x2, y2, v2, ...] 其中v: 0=不可见, 1=遮挡, 2=可见
    /// </summary>
    public List<double> GetCocoKeypoints()
    {
        var cocoKeypoints = new List<double>();
        
        // 为COCO 17个标准关键点创建数组
        var keypointData = new double[17 * 3]; // x, y, visibility for 17 keypoints
        
        // 初始化为不可见
        for (int i = 0; i < 17; i++)
        {
            keypointData[i * 3] = 0;     // x
            keypointData[i * 3 + 1] = 0; // y  
            keypointData[i * 3 + 2] = 0; // visibility (0 = not annotated)
        }
        
        // 填充已标注的关键点
        foreach (var keypoint in Keypoints)
        {
            int cocoIndex = GetCocoIndexByLabel(keypoint.Label);
            if (cocoIndex >= 0 && cocoIndex < 17)
            {
                keypointData[cocoIndex * 3] = keypoint.Position.X;
                keypointData[cocoIndex * 3 + 1] = keypoint.Position.Y;
                keypointData[cocoIndex * 3 + 2] = keypoint.Visibility switch
                {
                    KeypointVisibility.NotAnnotated => 0,
                    KeypointVisibility.Occluded => 1,
                    KeypointVisibility.Visible => 2,
                    _ => 0
                };
            }
        }
        
        return keypointData.ToList();
    }
    
    /// <summary>
    /// 根据标签名称获取COCO索引
    /// </summary>
    private int GetCocoIndexByLabel(string label)
    {
        for (int i = 0; i < CocoKeypointNames.Length; i++)
        {
            if (CocoKeypointNames[i].Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// 从COCO格式数据创建关键点标注
    /// </summary>
    public static KeypointAnnotation FromCocoKeypoints(List<double> cocoKeypoints, string label = "person")
    {
        var annotation = new KeypointAnnotation
        {
            Label = label
        };
        
        for (int i = 0; i < 17 && i * 3 + 2 < cocoKeypoints.Count; i++)
        {
            double x = cocoKeypoints[i * 3];
            double y = cocoKeypoints[i * 3 + 1];
            int visibility = (int)cocoKeypoints[i * 3 + 2];
            
            if (visibility > 0) // 只添加有效的关键点
            {
                var keypoint = new Keypoint
                {
                    Id = i,
                    Label = CocoKeypointNames[i],
                    Name = CocoKeypointNames[i],
                    Position = new Point2D(x, y),
                    Visibility = visibility switch
                    {
                        1 => KeypointVisibility.Occluded,
                        2 => KeypointVisibility.Visible,
                        _ => KeypointVisibility.NotAnnotated
                    }
                };
                annotation.Keypoints.Add(keypoint);
            }
        }
        
        return annotation;
    }

    /// <summary>
    /// 设置边界框
    /// </summary>
    public void SetBoundingBox(Point2D start, Point2D end)
    {
        BoundingBoxStart = start;
        BoundingBoxEnd = end;
        HasBoundingBox = true;
        UpdateModifiedTime();
    }

    /// <summary>
    /// 初始化17个COCO关键点
    /// </summary>
    private List<Keypoint> InitializeKeypoints()
    {
        var keypoints = new List<Keypoint>();
        for (int i = 0; i < CocoKeypointNames.Length; i++)
        {
            keypoints.Add(new Keypoint
            {
                Id = i,
                Name = CocoKeypointNames[i],
                Position = new Point2D(0, 0),
                Visibility = KeypointVisibility.NotAnnotated
            });
        }
        return keypoints;
    }

    /// <summary>
    /// 设置初始位置（人体轮廓的大致布局）
    /// </summary>
    private void SetInitialPosition(Point2D center)
    {
        // 设置人体关键点的大致相对位置
        var scale = 60.0; // 增加基础缩放因子，使姿态更大更清晰
        
        // 头部
        Keypoints[0].Position = new Point2D(center.X, center.Y - scale * 2);          // nose
        Keypoints[1].Position = new Point2D(center.X - scale * 0.2, center.Y - scale * 2.1); // left_eye
        Keypoints[2].Position = new Point2D(center.X + scale * 0.2, center.Y - scale * 2.1); // right_eye
        Keypoints[3].Position = new Point2D(center.X - scale * 0.4, center.Y - scale * 2);   // left_ear
        Keypoints[4].Position = new Point2D(center.X + scale * 0.4, center.Y - scale * 2);   // right_ear
        
        // 上身
        Keypoints[5].Position = new Point2D(center.X - scale * 0.8, center.Y - scale * 1);   // left_shoulder
        Keypoints[6].Position = new Point2D(center.X + scale * 0.8, center.Y - scale * 1);   // right_shoulder
        Keypoints[7].Position = new Point2D(center.X - scale * 1.2, center.Y - scale * 0.2); // left_elbow
        Keypoints[8].Position = new Point2D(center.X + scale * 1.2, center.Y - scale * 0.2); // right_elbow
        Keypoints[9].Position = new Point2D(center.X - scale * 1.4, center.Y + scale * 0.4); // left_wrist
        Keypoints[10].Position = new Point2D(center.X + scale * 1.4, center.Y + scale * 0.4); // right_wrist
        
        // 下身
        Keypoints[11].Position = new Point2D(center.X - scale * 0.4, center.Y + scale * 0.8); // left_hip
        Keypoints[12].Position = new Point2D(center.X + scale * 0.4, center.Y + scale * 0.8); // right_hip
        Keypoints[13].Position = new Point2D(center.X - scale * 0.4, center.Y + scale * 1.8); // left_knee
        Keypoints[14].Position = new Point2D(center.X + scale * 0.4, center.Y + scale * 1.8); // right_knee
        Keypoints[15].Position = new Point2D(center.X - scale * 0.4, center.Y + scale * 2.8); // left_ankle
        Keypoints[16].Position = new Point2D(center.X + scale * 0.4, center.Y + scale * 2.8); // right_ankle
        
        // 设置所有关键点为可见状态
        foreach (var keypoint in Keypoints)
        {
            keypoint.Visibility = KeypointVisibility.Visible;
        }
    }

    public override double GetArea()
    {
        // 计算关键点边界框的面积
        var visiblePoints = Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
        if (visiblePoints.Count < 2) return 0;
        
        var minX = visiblePoints.Min(k => k.Position.X);
        var maxX = visiblePoints.Max(k => k.Position.X);
        var minY = visiblePoints.Min(k => k.Position.Y);
        var maxY = visiblePoints.Max(k => k.Position.Y);
        
        return (maxX - minX) * (maxY - minY);
    }

    public override Point2D GetCenter()
    {
        var visiblePoints = Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
        if (visiblePoints.Count == 0) return new Point2D(0, 0);
        
        var centerX = visiblePoints.Average(k => k.Position.X);
        var centerY = visiblePoints.Average(k => k.Position.Y);
        return new Point2D(centerX, centerY);
    }

    public override List<Point2D> GetPoints()
    {
        return Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated)
                      .Select(k => k.Position)
                      .ToList();
    }

    public override void SetPoints(List<Point2D> points)
    {
        var visibleKeypoints = Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
        
        for (int i = 0; i < Math.Min(points.Count, visibleKeypoints.Count); i++)
        {
            visibleKeypoints[i].Position = points[i];
        }
        
        UpdateModifiedTime();
    }

    public override bool ContainsPoint(Point2D point)
    {
        // 检查是否点击在任何关键点附近
        const double threshold = 15.0; // 15像素的点击区域
        
        return Keypoints.Any(k => k.Visibility != KeypointVisibility.NotAnnotated && 
                                k.Position.DistanceTo(point) <= threshold);
    }

    public override Annotation Clone()
    {
        var clone = new KeypointAnnotation
        {
            Id = Guid.NewGuid().ToString(),
            Label = Label,
            Color = Color,
            StrokeWidth = StrokeWidth,
            IsVisible = IsVisible,
            ShowSkeleton = ShowSkeleton,
            Metadata = new Dictionary<string, object>(Metadata)
        };
        
        // 深拷贝关键点
        clone.Keypoints = Keypoints.Select(k => new Keypoint
        {
            Id = k.Id,
            Name = k.Name,
            Label = k.Label,
            Position = k.Position,
            Visibility = k.Visibility
        }).ToList();
        
        return clone;
    }

    /// <summary>
    /// 获取指定位置最近的关键点
    /// </summary>
    public Keypoint? GetNearestKeypoint(Point2D point, double threshold = 15.0)
    {
        return Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated)
                      .OrderBy(k => k.Position.DistanceTo(point))
                      .FirstOrDefault(k => k.Position.DistanceTo(point) <= threshold);
    }

    /// <summary>
    /// 转换为YOLO Pose格式（2024-2025最新标准）
    /// 格式: class_id x_center y_center width height px1 py1 v1 px2 py2 v2 ... px17 py17 v17
    /// 包含边界框和17个关键点坐标及可见性
    /// </summary>
    public string ToYoloPoseFormat(int imageWidth, int imageHeight, Dictionary<string, int> labelMap)
    {
        if (!labelMap.TryGetValue(Label, out int classId))
        {
            classId = 0; // 默认类别
        }
        
        // 计算边界框信息（归一化坐标）
        double bboxCenterX, bboxCenterY, bboxWidth, bboxHeight;
        
        if (HasBoundingBox)
        {
            // 使用用户绘制的边界框
            var minX = Math.Min(BoundingBoxStart.X, BoundingBoxEnd.X);
            var minY = Math.Min(BoundingBoxStart.Y, BoundingBoxEnd.Y);
            var maxX = Math.Max(BoundingBoxStart.X, BoundingBoxEnd.X);
            var maxY = Math.Max(BoundingBoxStart.Y, BoundingBoxEnd.Y);
            
            bboxCenterX = (minX + maxX) / 2.0 / imageWidth;
            bboxCenterY = (minY + maxY) / 2.0 / imageHeight;
            bboxWidth = (maxX - minX) / imageWidth;
            bboxHeight = (maxY - minY) / imageHeight;
        }
        else if (Keypoints.Any(k => k.Visibility != KeypointVisibility.NotAnnotated))
        {
            // 根据可见关键点计算边界框
            var visibleKeypoints = Keypoints.Where(k => k.Visibility != KeypointVisibility.NotAnnotated).ToList();
            var minX = visibleKeypoints.Min(k => k.Position.X);
            var minY = visibleKeypoints.Min(k => k.Position.Y);
            var maxX = visibleKeypoints.Max(k => k.Position.X);
            var maxY = visibleKeypoints.Max(k => k.Position.Y);
            
            // 添加一些边距
            var padding = 0.1;
            var paddingX = (maxX - minX) * padding;
            var paddingY = (maxY - minY) * padding;
            
            bboxCenterX = (minX + maxX) / 2.0 / imageWidth;
            bboxCenterY = (minY + maxY) / 2.0 / imageHeight;
            bboxWidth = (maxX - minX + paddingX * 2) / imageWidth;
            bboxHeight = (maxY - minY + paddingY * 2) / imageHeight;
        }
        else
        {
            // 没有有效数据，使用默认值
            bboxCenterX = bboxCenterY = bboxWidth = bboxHeight = 0;
        }
        
        // 确保边界框坐标在[0,1]范围内
        bboxCenterX = Math.Max(0, Math.Min(1, bboxCenterX));
        bboxCenterY = Math.Max(0, Math.Min(1, bboxCenterY));
        bboxWidth = Math.Max(0, Math.Min(1, bboxWidth));
        bboxHeight = Math.Max(0, Math.Min(1, bboxHeight));
        
        // 构建YOLO格式字符串：class_id + bbox + keypoints
        var poseData = $"{classId} {bboxCenterX:F6} {bboxCenterY:F6} {bboxWidth:F6} {bboxHeight:F6}";
        
        // 确保输出17个标准COCO关键点
        var cocoKeypoints = new double[17 * 3]; // x, y, visibility for 17 keypoints
        
        // 初始化为不可见
        for (int i = 0; i < 17; i++)
        {
            cocoKeypoints[i * 3] = 0;     // x
            cocoKeypoints[i * 3 + 1] = 0; // y  
            cocoKeypoints[i * 3 + 2] = 0; // visibility (0 = not annotated)
        }
        
        // 填充已标注的关键点
        foreach (var keypoint in Keypoints)
        {
            int cocoIndex = GetCocoIndexByLabel(keypoint.Label);
            if (cocoIndex >= 0 && cocoIndex < 17)
            {
                var normalizedX = keypoint.Position.X / imageWidth;
                var normalizedY = keypoint.Position.Y / imageHeight;
                
                // 确保坐标在[0,1]范围内
                normalizedX = Math.Max(0, Math.Min(1, normalizedX));
                normalizedY = Math.Max(0, Math.Min(1, normalizedY));
                
                cocoKeypoints[cocoIndex * 3] = normalizedX;
                cocoKeypoints[cocoIndex * 3 + 1] = normalizedY;
                cocoKeypoints[cocoIndex * 3 + 2] = keypoint.Visibility switch
                {
                    KeypointVisibility.NotAnnotated => 0,
                    KeypointVisibility.Occluded => 1,
                    KeypointVisibility.Visible => 2,
                    _ => 0
                };
            }
        }
        
        // 添加所有17个关键点的数据
        for (int i = 0; i < 17; i++)
        {
            poseData += $" {cocoKeypoints[i * 3]:F6} {cocoKeypoints[i * 3 + 1]:F6} {(int)cocoKeypoints[i * 3 + 2]}";
        }
        
        return poseData;
    }

    /// <summary>
    /// 获取姿态信息用于显示
    /// </summary>
    public string GetDisplayInfo()
    {
        var visibleCount = Keypoints.Count(k => k.Visibility == KeypointVisibility.Visible);
        var occludedCount = Keypoints.Count(k => k.Visibility == KeypointVisibility.Occluded);
        return $"姿态: {visibleCount}个可见关键点, {occludedCount}个遮挡关键点";
    }
}

/// <summary>
/// 关键点结构
/// </summary>
public class Keypoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty; // 新增：关键点的标签
    public Point2D Position { get; set; }
    public KeypointVisibility Visibility { get; set; }
}

/// <summary>
/// 关键点可见性状态
/// </summary>
public enum KeypointVisibility
{
    NotAnnotated = 0,  // 未标注
    Visible = 2,       // 可见
    Occluded = 1       // 被遮挡但存在
}