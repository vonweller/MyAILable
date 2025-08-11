using System.Collections.Generic;
using System.Linq;
using AIlable.Models;
using AIlable.ViewModels;

namespace AIlable.Services;

public class AddAnnotationCommand : IUndoableCommand
{
    private readonly MainViewModel _viewModel;
    private readonly Annotation _annotation;
    private readonly AnnotationImage _targetImage;
    
    public string Description => $"添加 {GetAnnotationTypeDescription(_annotation.Type)} 标注";

    public AddAnnotationCommand(MainViewModel viewModel, Annotation annotation, AnnotationImage targetImage)
    {
        _viewModel = viewModel;
        _annotation = annotation;
        _targetImage = targetImage;
    }

    public void Execute()
    {
        _targetImage.AddAnnotation(_annotation);
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Add(_annotation);
            _viewModel.SelectedAnnotation = _annotation;
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    public void Undo()
    {
        _targetImage.RemoveAnnotation(_annotation);
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Remove(_annotation);
            if (_viewModel.SelectedAnnotation == _annotation)
            {
                _viewModel.SelectedAnnotation = null;
            }
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    private string GetAnnotationTypeDescription(AnnotationType type)
    {
        return type switch
        {
            AnnotationType.Rectangle => "矩形",
            AnnotationType.Circle => "圆形",
            AnnotationType.Polygon => "多边形",
            AnnotationType.Line => "线条",
            AnnotationType.Point => "点",
            AnnotationType.OrientedBoundingBox => "有向边界框",
            AnnotationType.Keypoint => "关键点",
            _ => "标注"
        };
    }
}

public class RemoveAnnotationCommand : IUndoableCommand
{
    private readonly MainViewModel _viewModel;
    private readonly Annotation _annotation;
    private readonly AnnotationImage _targetImage;
    private readonly bool _wasSelected;
    
    public string Description => $"删除 {GetAnnotationTypeDescription(_annotation.Type)} 标注";

    public RemoveAnnotationCommand(MainViewModel viewModel, Annotation annotation, AnnotationImage targetImage)
    {
        _viewModel = viewModel;
        _annotation = annotation;
        _targetImage = targetImage;
        _wasSelected = viewModel.SelectedAnnotation == annotation;
    }

    public void Execute()
    {
        _targetImage.RemoveAnnotation(_annotation);
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Remove(_annotation);
            if (_viewModel.SelectedAnnotation == _annotation)
            {
                _viewModel.SelectedAnnotation = null;
            }
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    public void Undo()
    {
        _targetImage.AddAnnotation(_annotation);
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Add(_annotation);
            if (_wasSelected)
            {
                _viewModel.SelectedAnnotation = _annotation;
            }
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    private string GetAnnotationTypeDescription(AnnotationType type)
    {
        return type switch
        {
            AnnotationType.Rectangle => "矩形",
            AnnotationType.Circle => "圆形",
            AnnotationType.Polygon => "多边形",
            AnnotationType.Line => "线条",
            AnnotationType.Point => "点",
            AnnotationType.OrientedBoundingBox => "有向边界框",
            AnnotationType.Keypoint => "关键点",
            _ => "标注"
        };
    }
}

public class ClearAllAnnotationsCommand : IUndoableCommand
{
    private readonly MainViewModel _viewModel;
    private readonly AnnotationImage _targetImage;
    private readonly List<Annotation> _removedAnnotations;
    private readonly Annotation? _previousSelection;
    
    public string Description => $"清除所有标注 ({_removedAnnotations.Count}个)";

    public ClearAllAnnotationsCommand(MainViewModel viewModel, AnnotationImage targetImage)
    {
        _viewModel = viewModel;
        _targetImage = targetImage;
        _removedAnnotations = targetImage.Annotations.ToList();
        _previousSelection = viewModel.SelectedAnnotation;
    }

    public void Execute()
    {
        var annotationsToRemove = _targetImage.Annotations.ToList();
        foreach (var annotation in annotationsToRemove)
        {
            _targetImage.RemoveAnnotation(annotation);
        }
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Clear();
            _viewModel.SelectedAnnotation = null;
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    public void Undo()
    {
        foreach (var annotation in _removedAnnotations)
        {
            _targetImage.AddAnnotation(annotation);
        }
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            _viewModel.Annotations.Clear();
            foreach (var annotation in _removedAnnotations)
            {
                _viewModel.Annotations.Add(annotation);
            }
            _viewModel.SelectedAnnotation = _previousSelection;
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }
}

public class BatchAnnotationCommand : IUndoableCommand
{
    private readonly MainViewModel _viewModel;
    private readonly AnnotationImage _targetImage;
    private readonly List<Annotation> _addedAnnotations;
    
    public string Description => $"批量添加标注 ({_addedAnnotations.Count}个)";

    public BatchAnnotationCommand(MainViewModel viewModel, AnnotationImage targetImage, List<Annotation> annotations)
    {
        _viewModel = viewModel;
        _targetImage = targetImage;
        _addedAnnotations = annotations.ToList();
    }

    public void Execute()
    {
        foreach (var annotation in _addedAnnotations)
        {
            _targetImage.AddAnnotation(annotation);
        }
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            foreach (var annotation in _addedAnnotations)
            {
                _viewModel.Annotations.Add(annotation);
            }
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }

    public void Undo()
    {
        foreach (var annotation in _addedAnnotations)
        {
            _targetImage.RemoveAnnotation(annotation);
        }
        
        // 如果是当前图像，更新UI
        if (_viewModel.CurrentImage == _targetImage)
        {
            foreach (var annotation in _addedAnnotations)
            {
                _viewModel.Annotations.Remove(annotation);
            }
        }
        
        _viewModel.CurrentProject?.MarkDirty();
    }
}