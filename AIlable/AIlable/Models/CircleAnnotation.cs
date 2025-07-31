using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class CircleAnnotation : Annotation
{
    [ObservableProperty] private Point2D _center;
    [ObservableProperty] private double _radius;

    public CircleAnnotation() : base(AnnotationType.Circle)
    {
        _center = Point2D.Zero;
        _radius = 0;
    }

    public CircleAnnotation(Point2D center, double radius) : base(AnnotationType.Circle)
    {
        _center = center;
        _radius = radius;
    }

    public override double GetArea()
    {
        return Math.PI * Radius * Radius;
    }

    public override Point2D GetCenter()
    {
        return Center;
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D> { Center };
    }

    public override void SetPoints(List<Point2D> points)
    {
        if (points.Count >= 1)
        {
            Center = points[0];
            UpdateModifiedTime();
        }
    }

    public override bool ContainsPoint(Point2D point)
    {
        return Center.DistanceTo(point) <= Radius;
    }

    public override Annotation Clone()
    {
        var clone = new CircleAnnotation(Center, Radius)
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

    partial void OnCenterChanged(Point2D value)
    {
        UpdateModifiedTime();
    }

    partial void OnRadiusChanged(double value)
    {
        UpdateModifiedTime();
    }
}