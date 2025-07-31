using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class LineAnnotation : Annotation
{
    [ObservableProperty] private Point2D _startPoint;
    [ObservableProperty] private Point2D _endPoint;

    public LineAnnotation() : base(AnnotationType.Line)
    {
        _startPoint = Point2D.Zero;
        _endPoint = Point2D.Zero;
    }

    public LineAnnotation(Point2D startPoint, Point2D endPoint) : base(AnnotationType.Line)
    {
        _startPoint = startPoint;
        _endPoint = endPoint;
    }

    public double Length => StartPoint.DistanceTo(EndPoint);

    public override double GetArea()
    {
        return 0; // Lines have no area
    }

    public override Point2D GetCenter()
    {
        return new Point2D(
            (StartPoint.X + EndPoint.X) / 2,
            (StartPoint.Y + EndPoint.Y) / 2
        );
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D> { StartPoint, EndPoint };
    }

    public override void SetPoints(List<Point2D> points)
    {
        if (points.Count >= 2)
        {
            StartPoint = points[0];
            EndPoint = points[1];
            UpdateModifiedTime();
        }
    }

    public override bool ContainsPoint(Point2D point)
    {
        const double tolerance = 5.0; // Pixel tolerance
        
        // Check if point is near the line
        double distance = DistanceToLine(point);
        return distance <= tolerance;
    }

    private double DistanceToLine(Point2D point)
    {
        double A = EndPoint.X - StartPoint.X;
        double B = EndPoint.Y - StartPoint.Y;
        double C = point.X - StartPoint.X;
        double D = point.Y - StartPoint.Y;

        double dot = A * C + B * D;
        double lenSq = A * A + B * B;
        
        if (lenSq == 0) return point.DistanceTo(StartPoint);

        double param = dot / lenSq;

        Point2D closest;
        if (param < 0)
            closest = StartPoint;
        else if (param > 1)
            closest = EndPoint;
        else
            closest = new Point2D(StartPoint.X + param * A, StartPoint.Y + param * B);

        return point.DistanceTo(closest);
    }

    public override Annotation Clone()
    {
        var clone = new LineAnnotation(StartPoint, EndPoint)
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

    partial void OnStartPointChanged(Point2D value)
    {
        UpdateModifiedTime();
    }

    partial void OnEndPointChanged(Point2D value)
    {
        UpdateModifiedTime();
    }
}