using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class RectangleAnnotation : Annotation
{
    [ObservableProperty] private Point2D _topLeft;
    [ObservableProperty] private Point2D _bottomRight;

    public RectangleAnnotation() : base(AnnotationType.Rectangle)
    {
        _topLeft = Point2D.Zero;
        _bottomRight = Point2D.Zero;
    }

    public RectangleAnnotation(Point2D topLeft, Point2D bottomRight) : base(AnnotationType.Rectangle)
    {
        _topLeft = topLeft;
        _bottomRight = bottomRight;
    }

    public double Width => Math.Abs(BottomRight.X - TopLeft.X);
    public double Height => Math.Abs(BottomRight.Y - TopLeft.Y);

    public Point2D TopRight => new(BottomRight.X, TopLeft.Y);
    public Point2D BottomLeft => new(TopLeft.X, BottomRight.Y);

    public override double GetArea()
    {
        return Width * Height;
    }

    public override Point2D GetCenter()
    {
        return new Point2D(
            (TopLeft.X + BottomRight.X) / 2,
            (TopLeft.Y + BottomRight.Y) / 2
        );
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D> { TopLeft, TopRight, BottomRight, BottomLeft };
    }

    public override void SetPoints(List<Point2D> points)
    {
        if (points.Count >= 2)
        {
            TopLeft = points[0];
            BottomRight = points[2];
            UpdateModifiedTime();
        }
    }

    public override bool ContainsPoint(Point2D point)
    {
        return point.X >= Math.Min(TopLeft.X, BottomRight.X) &&
               point.X <= Math.Max(TopLeft.X, BottomRight.X) &&
               point.Y >= Math.Min(TopLeft.Y, BottomRight.Y) &&
               point.Y <= Math.Max(TopLeft.Y, BottomRight.Y);
    }

    public override Annotation Clone()
    {
        var clone = new RectangleAnnotation(TopLeft, BottomRight)
        {
            Label = Label,
            Color = Color,
            StrokeWidth = StrokeWidth,
            IsVisible = IsVisible
        };
        
        foreach (var item in Metadata)
        {
            clone.Metadata[item.Key] = item.Value;
        }
        
        return clone;
    }

    partial void OnTopLeftChanged(Point2D value)
    {
        UpdateModifiedTime();
    }

    partial void OnBottomRightChanged(Point2D value)
    {
        UpdateModifiedTime();
    }
}