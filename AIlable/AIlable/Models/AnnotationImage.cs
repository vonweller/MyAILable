using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class AnnotationImage : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _fileName;
    [ObservableProperty] private string _filePath;
    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;
    [ObservableProperty] private long _fileSize;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private DateTime _modifiedAt;
    [ObservableProperty] private ObservableCollection<Annotation> _annotations;
    [ObservableProperty] private Dictionary<string, object> _metadata;
    [ObservableProperty] private bool _isAnnotated;
    [ObservableProperty] private string _format;

    public AnnotationImage()
    {
        _id = Guid.NewGuid().ToString();
        _fileName = string.Empty;
        _filePath = string.Empty;
        _width = 0;
        _height = 0;
        _fileSize = 0;
        _createdAt = DateTime.Now;
        _modifiedAt = DateTime.Now;
        _annotations = new ObservableCollection<Annotation>();
        _metadata = new Dictionary<string, object>();
        _isAnnotated = false;
        _format = string.Empty;

        _annotations.CollectionChanged += (_, _) =>
        {
            IsAnnotated = Annotations.Count > 0;
            ModifiedAt = DateTime.Now;
        };
    }

    public AnnotationImage(string filePath) : this()
    {
        if (File.Exists(filePath))
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            
            var fileInfo = new FileInfo(filePath);
            FileSize = fileInfo.Length;
            CreatedAt = fileInfo.CreationTime;
        }
    }

    public bool IsValid => File.Exists(FilePath) && Width > 0 && Height > 0;

    public void AddAnnotation(Annotation annotation)
    {
        Annotations.Add(annotation);
    }

    public void RemoveAnnotation(Annotation annotation)
    {
        Annotations.Remove(annotation);
    }

    public void RemoveAnnotation(string annotationId)
    {
        for (int i = Annotations.Count - 1; i >= 0; i--)
        {
            if (Annotations[i].Id == annotationId)
            {
                Annotations.RemoveAt(i);
                break;
            }
        }
    }

    public Annotation? GetAnnotation(string annotationId)
    {
        foreach (var annotation in Annotations)
        {
            if (annotation.Id == annotationId)
                return annotation;
        }
        return null;
    }

    public List<Annotation> GetAnnotationsByLabel(string label)
    {
        var result = new List<Annotation>();
        foreach (var annotation in Annotations)
        {
            if (annotation.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
                result.Add(annotation);
        }
        return result;
    }

    public List<Annotation> GetAnnotationsByType(AnnotationType type)
    {
        var result = new List<Annotation>();
        foreach (var annotation in Annotations)
        {
            if (annotation.Type == type)
                result.Add(annotation);
        }
        return result;
    }

    public void ClearAnnotations()
    {
        Annotations.Clear();
    }

    public void SetImageDimensions(int width, int height)
    {
        Width = width;
        Height = height;
        ModifiedAt = DateTime.Now;
    }

    public override string ToString()
    {
        return $"{FileName} ({Annotations.Count} annotations)";
    }
}