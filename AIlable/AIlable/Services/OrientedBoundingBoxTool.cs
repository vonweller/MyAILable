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
}
