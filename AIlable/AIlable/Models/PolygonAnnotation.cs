using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class PolygonAnnotation : Annotation
{
    [ObservableProperty] private ObservableCollection<Point2D> _vertices;
    [ObservableProperty] private Point2D? _previewEndPoint;

    public PolygonAnnotation() : base(AnnotationType.Polygon)
    {
        _vertices = new ObservableCollection<Point2D>();
        _vertices.CollectionChanged += (_, _) => UpdateModifiedTime();
    }

    public PolygonAnnotation(IEnumerable<Point2D> vertices) : base(AnnotationType.Polygon)
    {
        _vertices = new ObservableCollection<Point2D>(vertices);
        _vertices.CollectionChanged += (_, _) => UpdateModifiedTime();
    }

    public bool IsClosed => Vertices.Count >= 3;

    public override double GetArea()
    {
        if (!IsClosed) return 0;

        double area = 0;
        int n = Vertices.Count;
        
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += Vertices[i].X * Vertices[j].Y;
            area -= Vertices[j].X * Vertices[i].Y;
        }
        
        return Math.Abs(area) / 2.0;
    }

    public override Point2D GetCenter()
    {
        if (Vertices.Count == 0) return Point2D.Zero;

        double x = Vertices.Average(p => p.X);
        double y = Vertices.Average(p => p.Y);
        return new Point2D(x, y);
    }

    public override List<Point2D> GetPoints()
    {
        return new List<Point2D>(Vertices);
    }

    public override void SetPoints(List<Point2D> points)
    {
        Vertices.Clear();
        foreach (var point in points)
        {
            Vertices.Add(point);
        }
        UpdateModifiedTime();
    }

    public override bool ContainsPoint(Point2D point)
    {
        if (!IsClosed) return false;

        bool inside = false;
        int n = Vertices.Count;
        
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((Vertices[i].Y > point.Y) != (Vertices[j].Y > point.Y)) &&
                (point.X < (Vertices[j].X - Vertices[i].X) * (point.Y - Vertices[i].Y) / (Vertices[j].Y - Vertices[i].Y) + Vertices[i].X))
            {
                inside = !inside;
            }
        }
        
        return inside;
    }

    public void AddVertex(Point2D vertex)
    {
        Vertices.Add(vertex);
    }

    public void RemoveVertex(int index)
    {
        if (index >= 0 && index < Vertices.Count)
        {
            Vertices.RemoveAt(index);
        }
    }

    public void InsertVertex(int index, Point2D vertex)
    {
        if (index >= 0 && index <= Vertices.Count)
        {
            Vertices.Insert(index, vertex);
        }
    }

    public override Annotation Clone()
    {
        var clone = new PolygonAnnotation(Vertices)
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
}