using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public partial class AnnotationProject : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _projectPath;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private DateTime _modifiedAt;
    [ObservableProperty] private ObservableCollection<AnnotationImage> _images;
    [ObservableProperty] private ObservableCollection<string> _labels;
    [ObservableProperty] private Dictionary<string, object> _settings;
    [ObservableProperty] private Dictionary<string, object> _metadata;
    [ObservableProperty] private string _version;
    [ObservableProperty] private bool _isDirty;

    public AnnotationProject()
    {
        _id = Guid.NewGuid().ToString();
        _name = "New Project";
        _description = string.Empty;
        _projectPath = string.Empty;
        _createdAt = DateTime.Now;
        _modifiedAt = DateTime.Now;
        _images = new ObservableCollection<AnnotationImage>();
        _labels = new ObservableCollection<string>();
        _settings = new Dictionary<string, object>();
        _metadata = new Dictionary<string, object>();
        _version = "1.0.0";
        _isDirty = false;

        SetupCollectionEventHandlers();
        InitializeDefaultSettings();
    }

    public AnnotationProject(string name, string projectPath) : this()
    {
        Name = name;
        ProjectPath = projectPath;
    }

    public string ProjectFileName => Path.ChangeExtension(Name, ".ailproj");
    public string ProjectFilePath => string.IsNullOrEmpty(ProjectPath)
        ? ProjectFileName
        : Path.Combine(ProjectPath, ProjectFileName);
    public int TotalAnnotations => Images.Sum(img => img.Annotations.Count);
    public int AnnotatedImageCount => Images.Count(img => img.IsAnnotated);
    public bool IsEmpty => Images.Count == 0;

    private void SetupCollectionEventHandlers()
    {
        Images.CollectionChanged += (_, _) =>
        {
            ModifiedAt = DateTime.Now;
            IsDirty = true;
        };

        Labels.CollectionChanged += (_, _) =>
        {
            ModifiedAt = DateTime.Now;
            IsDirty = true;
        };
    }

    private void InitializeDefaultSettings()
    {
        Settings["DefaultAnnotationType"] = AnnotationType.Rectangle;
        Settings["DefaultStrokeWidth"] = 2.0;
        Settings["DefaultColor"] = "#FF0000";
        Settings["AutoSave"] = false;
        Settings["AutoSaveInterval"] = 300; // seconds
        Settings["ShowLabels"] = true;
        Settings["ShowGrid"] = false;
        Settings["GridSize"] = 20;
        Settings["ZoomFactor"] = 1.0;
        Settings["MaxZoom"] = 10.0;
        Settings["MinZoom"] = 0.1;
    }

    public void AddImage(AnnotationImage image)
    {
        Images.Add(image);
    }

    public void RemoveImage(AnnotationImage image)
    {
        Images.Remove(image);
    }

    public void RemoveImage(string imageId)
    {
        for (int i = Images.Count - 1; i >= 0; i--)
        {
            if (Images[i].Id == imageId)
            {
                Images.RemoveAt(i);
                break;
            }
        }
    }

    public AnnotationImage? GetImage(string imageId)
    {
        return Images.FirstOrDefault(img => img.Id == imageId);
    }

    public AnnotationImage? GetImageByFileName(string fileName)
    {
        return Images.FirstOrDefault(img => img.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public void AddLabel(string label)
    {
        if (!string.IsNullOrWhiteSpace(label) && !Labels.Contains(label))
        {
            Labels.Add(label);
        }
    }

    public void RemoveLabel(string label)
    {
        Labels.Remove(label);
    }

    public void ClearImages()
    {
        Images.Clear();
    }

    public void ClearLabels()
    {
        Labels.Clear();
    }

    public List<string> GetUsedLabels()
    {
        var usedLabels = new HashSet<string>();
        foreach (var image in Images)
        {
            foreach (var annotation in image.Annotations)
            {
                if (!string.IsNullOrEmpty(annotation.Label))
                {
                    usedLabels.Add(annotation.Label);
                }
            }
        }
        return usedLabels.ToList();
    }

    public void UpdateUsedLabels()
    {
        var usedLabels = GetUsedLabels();
        foreach (var label in usedLabels)
        {
            AddLabel(label);
        }
    }

    public Dictionary<string, int> GetLabelStatistics()
    {
        var stats = new Dictionary<string, int>();
        foreach (var image in Images)
        {
            foreach (var annotation in image.Annotations)
            {
                if (!string.IsNullOrEmpty(annotation.Label))
                {
                    stats[annotation.Label] = stats.ContainsKey(annotation.Label) ? stats[annotation.Label] + 1 : 1;
                }
            }
        }
        return stats;
    }

    public Dictionary<AnnotationType, int> GetTypeStatistics()
    {
        var stats = new Dictionary<AnnotationType, int>();
        foreach (var image in Images)
        {
            foreach (var annotation in image.Annotations)
            {
                stats[annotation.Type] = stats.ContainsKey(annotation.Type) ? stats[annotation.Type] + 1 : 1;
            }
        }
        return stats;
    }

    public bool HasMixedAnnotationTypes()
    {
        var types = new HashSet<AnnotationType>();
        foreach (var image in Images)
        {
            foreach (var annotation in image.Annotations)
            {
                types.Add(annotation.Type);
                // 如果发现超过一种类型，立即返回true
                if (types.Count > 1)
                    return true;
            }
        }
        return false;
    }

    public List<AnnotationType> GetUsedAnnotationTypes()
    {
        var types = new HashSet<AnnotationType>();
        foreach (var image in Images)
        {
            foreach (var annotation in image.Annotations)
            {
                types.Add(annotation.Type);
            }
        }
        return types.ToList();
    }

    public void MarkClean()
    {
        IsDirty = false;
    }

    public void MarkDirty()
    {
        IsDirty = true;
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// 设置项目文件路径并更新相关属性
    /// </summary>
    public void SetProjectFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        ProjectPath = Path.GetDirectoryName(filePath) ?? "";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (!string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            Name = fileNameWithoutExtension;
        }
        ModifiedAt = DateTime.Now;
    }

    public override string ToString()
    {
        return $"{Name} ({Images.Count} images, {TotalAnnotations} annotations)";
    }
}