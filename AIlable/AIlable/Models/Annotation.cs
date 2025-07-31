using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RectangleAnnotation), "rectangle")]
[JsonDerivedType(typeof(CircleAnnotation), "circle")]
[JsonDerivedType(typeof(LineAnnotation), "line")]
[JsonDerivedType(typeof(PointAnnotation), "point")]
[JsonDerivedType(typeof(PolygonAnnotation), "polygon")]
[JsonDerivedType(typeof(OrientedBoundingBoxAnnotation), "obb")]
public abstract partial class Annotation : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _label;
    [ObservableProperty] private AnnotationType _type;
    [ObservableProperty] private AnnotationState _state;
    [ObservableProperty] private string _color;
    [ObservableProperty] private double _strokeWidth;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private DateTime _modifiedAt;
    [ObservableProperty] private Dictionary<string, object> _metadata;

    protected Annotation(AnnotationType type)
    {
        _id = Guid.NewGuid().ToString();
        _type = type;
        _label = string.Empty;
        _color = "#FF0000";
        _strokeWidth = 2.0;
        _isVisible = true;
        _isSelected = false;
        _state = AnnotationState.Normal;
        _createdAt = DateTime.Now;
        _modifiedAt = DateTime.Now;
        _metadata = new Dictionary<string, object>();
    }

    public abstract double GetArea();
    public abstract Point2D GetCenter();
    public abstract List<Point2D> GetPoints();
    public abstract void SetPoints(List<Point2D> points);
    public abstract bool ContainsPoint(Point2D point);
    public abstract Annotation Clone();

    partial void OnIsSelectedChanged(bool value)
    {
        State = value ? AnnotationState.Selected : AnnotationState.Normal;
    }

    protected void UpdateModifiedTime()
    {
        ModifiedAt = DateTime.Now;
    }
}