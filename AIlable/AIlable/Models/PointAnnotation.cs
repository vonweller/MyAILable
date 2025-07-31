using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class PointAnnotation : Annotation
{
    [ObservableProperty] private Point2D _position;
    [ObservableProperty] private double _size;

    public PointAnnotation() : base(AnnotationType.Point)
    {
        _position = Point2D.Zero;
        _size = 8.0; // Default point size
    }

    public PointAnnotation(Point2D position, double size = 8.0) : base(AnnotationType.Point)
    {
        _position = position;
        _size = size;
    }

    public override double GetArea()
    {
        return Math.PI * (Size / 2) * (Size / 2); // Circle area with diameter = Size
    }

    public override Point2D GetCenter()
    {
        return Position;
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D> { Position };
    }

    public override void SetPoints(List<Point2D> points)
    {
        if (points.Count >= 1)
        {
            Position = points[0];
            UpdateModifiedTime();
        }
    }

    public override bool ContainsPoint(Point2D point)
    {
        double distance = Position.DistanceTo(point);
        return distance <= Size / 2;
    }

    public override Annotation Clone()
    {
        var clone = new PointAnnotation(Position, Size)
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

    partial void OnPositionChanged(Point2D value)
    {
        UpdateModifiedTime();
    }

    partial void OnSizeChanged(double value)
    {
        UpdateModifiedTime();
    }
}