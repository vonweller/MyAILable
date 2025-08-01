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
    /// </summary>
    public static readonly int[,] CocoSkeleton = 
    {
        {16, 14}, {14, 12}, {17, 15}, {15, 13}, {12, 13},
        {6, 12}, {7, 13}, {6, 7}, {6, 8}, {7, 9},
        {8, 10}, {9, 11}, {2, 3}, {1, 2}, {1, 3},
        {2, 4}, {3, 5}, {4, 6}, {5, 7}
    };

    [ObservableProperty] private List<Keypoint> _keypoints;
    [ObservableProperty] private bool _showSkeleton = true;

    public KeypointAnnotation() : base(AnnotationType.Keypoint)
    {
        _keypoints = InitializeKeypoints();
    }

    public KeypointAnnotation(Point2D position) : base(AnnotationType.Keypoint)
    {
        _keypoints = InitializeKeypoints();
        // 将所有关键点初始化到指定位置附近
        SetInitialPosition(position);
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
        var scale = 50.0; // 基础缩放因子
        
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
    /// 转换为YOLO Pose格式
    /// 格式: class_id x1 y1 v1 x2 y2 v2 ... x17 y17 v17
    /// </summary>
    public string ToYoloPoseFormat(int imageWidth, int imageHeight, Dictionary<string, int> labelMap)
    {
        if (!labelMap.TryGetValue(Label, out int classId))
        {
            classId = 0; // 默认类别
        }
        
        var poseData = $"{classId}";
        
        foreach (var keypoint in Keypoints)
        {
            var normalizedX = keypoint.Position.X / imageWidth;
            var normalizedY = keypoint.Position.Y / imageHeight;
            var visibility = (int)keypoint.Visibility;
            
            // 确保坐标在[0,1]范围内
            normalizedX = Math.Max(0, Math.Min(1, normalizedX));
            normalizedY = Math.Max(0, Math.Min(1, normalizedY));
            
            poseData += $" {normalizedX:F6} {normalizedY:F6} {visibility}";
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