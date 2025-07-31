using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

/// <summary>
/// 有向边界框标注类 (Oriented Bounding Box - OBB)
/// 支持旋转的矩形标注
/// </summary>
public partial class OrientedBoundingBoxAnnotation : Annotation
{
    [ObservableProperty] private double _centerX;
    [ObservableProperty] private double _centerY;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private double _angle; // 旋转角度（度）
    [ObservableProperty] private List<Point2D> _points;

    public OrientedBoundingBoxAnnotation() : base(AnnotationType.OrientedBoundingBox)
    {
        _centerX = 0;
        _centerY = 0;
        _width = 0;
        _height = 0;
        _angle = 0;
        _points = new List<Point2D>();
    }

    public OrientedBoundingBoxAnnotation(double centerX, double centerY, double width, double height, double angle = 0) 
        : base(AnnotationType.OrientedBoundingBox)
    {
        _centerX = centerX;
        _centerY = centerY;
        _width = width;
        _height = height;
        _angle = angle;
        _points = new List<Point2D>();
        UpdatePoints();
    }

    public override double GetArea()
    {
        return Width * Height;
    }

    public override Point2D GetCenter()
    {
        return new Point2D(CenterX, CenterY);
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D>(Points);
    }

    public override void SetPoints(List<Point2D> points)
    {
        if (points.Count >= 4)
        {
            Points = new List<Point2D>(points);
            
            // 从点计算中心、尺寸和角度
            RecalculateFromPoints();
            UpdateModifiedTime();
        }
    }

    public override bool ContainsPoint(Point2D point)
    {
        if (Points.Count < 4) return false;

        // 使用射线投射算法检查点是否在多边形内
        bool inside = false;
        int j = Points.Count - 1;

        for (int i = 0; i < Points.Count; i++)
        {
            var pi = Points[i];
            var pj = Points[j];

            if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public override Annotation Clone()
    {
        return new OrientedBoundingBoxAnnotation(CenterX, CenterY, Width, Height, Angle)
        {
            Id = Guid.NewGuid().ToString(),
            Label = Label,
            Color = Color,
            StrokeWidth = StrokeWidth,
            IsVisible = IsVisible,
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }

    /// <summary>
    /// 更新角点坐标
    /// </summary>
    public void UpdatePoints()
    {
        var halfWidth = Width / 2.0;
        var halfHeight = Height / 2.0;
        var angleRad = Angle * Math.PI / 180.0;
        
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        
        // 计算四个角点相对于中心的坐标
        var corners = new[]
        {
            new { x = -halfWidth, y = -halfHeight }, // 左上
            new { x = halfWidth, y = -halfHeight },  // 右上
            new { x = halfWidth, y = halfHeight },   // 右下
            new { x = -halfWidth, y = halfHeight }   // 左下
        };
        
        Points.Clear();
        
        // 应用旋转变换并转换为绝对坐标
        foreach (var corner in corners)
        {
            var rotatedX = corner.x * cos - corner.y * sin + CenterX;
            var rotatedY = corner.x * sin + corner.y * cos + CenterY;
            Points.Add(new Point2D(rotatedX, rotatedY));
        }
    }

    /// <summary>
    /// 从角点重新计算中心、尺寸和角度
    /// </summary>
    private void RecalculateFromPoints()
    {
        if (Points.Count < 4) return;

        // 计算中心点
        var centerX = Points.Average(p => p.X);
        var centerY = Points.Average(p => p.Y);

        // 计算第一条边的向量来确定角度
        var edge1 = new Point2D(Points[1].X - Points[0].X, Points[1].Y - Points[0].Y);
        var angle = Math.Atan2(edge1.Y, edge1.X) * 180.0 / Math.PI;

        // 计算宽度和高度
        var width = Points[0].DistanceTo(Points[1]);
        var height = Points[1].DistanceTo(Points[2]);

        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
        Angle = angle;
    }

    /// <summary>
    /// 旋转边界框
    /// </summary>
    public void Rotate(double deltaAngle)
    {
        Angle += deltaAngle;
        
        // 保持角度在0-360度范围内
        while (Angle < 0) Angle += 360;
        while (Angle >= 360) Angle -= 360;
        
        UpdatePoints();
        UpdateModifiedTime();
    }

    /// <summary>
    /// 转换为YOLO OBB格式
    /// 格式: class_id center_x center_y width height angle
    /// </summary>
    public string ToYoloObbFormat(int imageWidth, int imageHeight, Dictionary<string, int> labelMap)
    {
        if (!labelMap.TryGetValue(Label, out int classId))
        {
            classId = 0; // 默认类别
        }
        
        // 归一化坐标 (0-1)
        var normalizedCenterX = CenterX / imageWidth;
        var normalizedCenterY = CenterY / imageHeight;
        var normalizedWidth = Width / imageWidth;
        var normalizedHeight = Height / imageHeight;
        var normalizedAngle = Angle / 180.0; // 归一化角度到0-2范围
        
        return $"{classId} {normalizedCenterX:F6} {normalizedCenterY:F6} {normalizedWidth:F6} {normalizedHeight:F6} {normalizedAngle:F6}";
    }

    /// <summary>
    /// 转换为DOTA格式 (8个坐标点)
    /// 格式: x1 y1 x2 y2 x3 y3 x4 y4 category difficulty
    /// </summary>
    public string ToDotaFormat(Dictionary<string, string> labelMap, int difficulty = 0)
    {
        if (Points.Count < 4) return string.Empty;

        var category = labelMap.ContainsKey(Label) ? Label : "unknown";

        var coords = string.Join(" ", Points.Select(p => $"{p.X:F1} {p.Y:F1}"));
        return $"{coords} {category} {difficulty}";
    }

    /// <summary>
    /// 获取边界框信息用于显示
    /// </summary>
    public string GetDisplayInfo()
    {
        return $"OBB: 中心({CenterX:F1}, {CenterY:F1}), 尺寸({Width:F1}×{Height:F1}), 角度({Angle:F1}°)";
    }

    // 属性变化时更新点坐标
    partial void OnCenterXChanged(double value) => UpdatePoints();
    partial void OnCenterYChanged(double value) => UpdatePoints();
    partial void OnWidthChanged(double value) => UpdatePoints();
    partial void OnHeightChanged(double value) => UpdatePoints();
    partial void OnAngleChanged(double value) => UpdatePoints();
}
