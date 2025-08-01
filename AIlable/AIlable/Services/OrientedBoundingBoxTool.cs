using System;
using System.Collections.Generic;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 有向边界框标注工具 (Oriented Bounding Box - OBB)
/// 支持旋转的矩形标注
/// </summary>
public class OrientedBoundingBoxTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.OrientedBoundingBox;
    public override string Name => "有向边界框工具";
    public override string Description => "绘制有向边界框标注";

    private bool _isRotating = false;
    private Point2D _rotationStartPoint;
    private double _initialAngle = 0;

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        return new Models.OrientedBoundingBoxAnnotation(startPoint.X, startPoint.Y, 0, 0, 0)
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        if (annotation is Models.OrientedBoundingBoxAnnotation obb && _startPoint.HasValue)
        {
            // 计算中心点
            var centerX = (_startPoint.Value.X + currentPoint.X) / 2.0;
            var centerY = (_startPoint.Value.Y + currentPoint.Y) / 2.0;

            // 计算宽度和高度
            var width = Math.Abs(currentPoint.X - _startPoint.Value.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Value.Y);

            obb.CenterX = centerX;
            obb.CenterY = centerY;
            obb.Width = width;
            obb.Height = height;
            obb.Angle = 0; // 初始角度为0
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is Models.OrientedBoundingBoxAnnotation obb)
        {
            return obb.Width >= 5 && obb.Height >= 5; // 最小尺寸检查
        }
        return false;
    }

    /// <summary>
    /// 旋转有向边界框
    /// </summary>
    public void RotateAnnotation(Models.OrientedBoundingBoxAnnotation annotation, double deltaAngle)
    {
        annotation.Rotate(deltaAngle);
    }

    /// <summary>
    /// 开始旋转操作
    /// </summary>
    public void StartRotation(Models.OrientedBoundingBoxAnnotation annotation, Point2D rotationPoint)
    {
        _isRotating = true;
        _rotationStartPoint = rotationPoint;
        _initialAngle = annotation.Angle;
    }

    /// <summary>
    /// 更新旋转
    /// </summary>
    public void UpdateRotation(Models.OrientedBoundingBoxAnnotation annotation, Point2D currentPoint)
    {
        if (!_isRotating) return;

        var center = annotation.GetCenter();
        
        // 计算起始角度
        var startAngle = Math.Atan2(_rotationStartPoint.Y - center.Y, _rotationStartPoint.X - center.X);
        
        // 计算当前角度
        var currentAngle = Math.Atan2(currentPoint.Y - center.Y, currentPoint.X - center.X);
        
        // 计算角度差（转换为度）
        var deltaAngle = (currentAngle - startAngle) * 180.0 / Math.PI;
        
        // 设置新角度
        annotation.Angle = _initialAngle + deltaAngle;
        
        // 保持角度在0-360度范围内
        while (annotation.Angle < 0) annotation.Angle += 360;
        while (annotation.Angle >= 360) annotation.Angle -= 360;
    }

    /// <summary>
    /// 结束旋转操作
    /// </summary>
    public void EndRotation()
    {
        _isRotating = false;
    }

    /// <summary>
    /// 检查点是否在旋转控制柄上
    /// </summary>
    public bool IsPointOnRotationHandle(Models.OrientedBoundingBoxAnnotation annotation, Point2D point, double zoomFactor)
    {
        var center = annotation.GetCenter();
        var rotationHandleOffset = 30 / zoomFactor; // 适应缩放
        var rotationHandlePoint = new Point2D(center.X, center.Y - rotationHandleOffset);
        
        var distance = point.DistanceTo(rotationHandlePoint);
        return distance <= 8 / zoomFactor; // 8像素的点击区域，适应缩放
    }

    /// <summary>
    /// 检查点是否在角点控制柄上
    /// </summary>
    public int GetCornerHandleIndex(Models.OrientedBoundingBoxAnnotation annotation, Point2D point, double zoomFactor)
    {
        var obbPoints = annotation.GetPoints();
        var threshold = 6 / zoomFactor; // 6像素的点击区域，适应缩放
        
        for (int i = 0; i < obbPoints.Count; i++)
        {
            var distance = point.DistanceTo(obbPoints[i]);
            if (distance <= threshold)
            {
                return i;
            }
        }
        
        return -1; // 没有找到角点
    }

    /// <summary>
    /// 更新角点位置（调整大小）
    /// </summary>
    public void UpdateCornerHandle(Models.OrientedBoundingBoxAnnotation annotation, int cornerIndex, Point2D newPosition)
    {
        if (cornerIndex < 0 || cornerIndex >= 4) return;
        
        var points = annotation.GetPoints();
        if (points.Count < 4) return;
        
        // 更新指定角点的位置
        points[cornerIndex] = newPosition;
        
        // 从更新后的点重新计算OBB参数
        annotation.SetPoints(points);
    }
}
