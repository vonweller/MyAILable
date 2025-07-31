using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIlable.Models;
using AIlable.Services;

namespace AIlable.ViewModels;

public partial class AIInferenceDialogViewModel : ViewModelBase
{
    [ObservableProperty] private double _confidenceThreshold = 0.5;
    [ObservableProperty] private bool _replaceExistingAnnotations = false;
    [ObservableProperty] private bool _processCurrentImageOnly = true;
    [ObservableProperty] private bool _isProcessing = false;
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private string _statusMessage = "准备开始AI推理";
    [ObservableProperty] private string _resultsText = "";
    [ObservableProperty] private bool _hasResults = false;
    [ObservableProperty] private string _modelInfo = "";
    [ObservableProperty] private bool _hasModel = false;
    [ObservableProperty] private bool _hasProject = false;

    private readonly AIModelManager _aiModelManager;
    private readonly AnnotationProject? _currentProject;
    private readonly AnnotationImage? _currentImage;
    private List<Annotation> _inferenceResults = new();

    public AIInferenceDialogViewModel(
        AIModelManager aiModelManager, 
        AnnotationProject? currentProject = null, 
        AnnotationImage? currentImage = null)
    {
        _aiModelManager = aiModelManager;
        _currentProject = currentProject;
        _currentImage = currentImage;
        
        HasModel = _aiModelManager.HasActiveModel;
        HasProject = _currentProject != null;
        ModelInfo = _aiModelManager.GetModelInfo();
        
        StartInferenceCommand = new AsyncRelayCommand(StartInferenceAsync, () => HasModel && !IsProcessing);
        CancelCommand = new RelayCommand(Cancel);
        
        // 如果没有当前图像，强制批量处理模式
        if (_currentImage == null && _currentProject?.Images.Any() == true)
        {
            ProcessCurrentImageOnly = false;
        }
    }

    public ICommand StartInferenceCommand { get; }
    public ICommand CancelCommand { get; }
    
    public List<Annotation> InferenceResults => _inferenceResults;
    public bool DialogResult { get; private set; }

    private async Task StartInferenceAsync()
    {
        if (!HasModel)
        {
            StatusMessage = "错误：未加载AI模型";
            return;
        }

        try
        {
            IsProcessing = true;
            Progress = 0;
            HasResults = false;
            _inferenceResults.Clear();
            
            var resultsBuilder = new StringBuilder();
            resultsBuilder.AppendLine($"AI推理开始 - 置信度阈值: {ConfidenceThreshold:F2}");
            resultsBuilder.AppendLine($"处理模式: {(ProcessCurrentImageOnly ? "当前图像" : "批量处理")}");
            resultsBuilder.AppendLine();

            if (ProcessCurrentImageOnly)
            {
                await ProcessSingleImageAsync(resultsBuilder);
            }
            else
            {
                await ProcessBatchAsync(resultsBuilder);
            }

            HasResults = true;
            DialogResult = true;
            
            resultsBuilder.AppendLine();
            resultsBuilder.AppendLine($"推理完成！共检测到 {_inferenceResults.Count} 个对象");
            
            ResultsText = resultsBuilder.ToString();
            StatusMessage = $"推理完成，检测到 {_inferenceResults.Count} 个对象";
            Progress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"推理失败: {ex.Message}";
            HasResults = true;
            ResultsText = $"错误详情:\n{ex}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ProcessSingleImageAsync(StringBuilder resultsBuilder)
    {
        if (_currentImage == null)
        {
            throw new InvalidOperationException("当前图像为空");
        }

        StatusMessage = "正在处理当前图像...";
        Progress = 20;

        var annotations = await _aiModelManager.InferImageAsync(_currentImage.FilePath, (float)ConfidenceThreshold);
        var annotationsList = annotations.ToList();

        Progress = 80;
        
        resultsBuilder.AppendLine($"图像: {_currentImage.FileName}");
        resultsBuilder.AppendLine($"检测到 {annotationsList.Count} 个对象:");

        foreach (var annotation in annotationsList)
        {
            _inferenceResults.Add(annotation);
            resultsBuilder.AppendLine($"  - {annotation.Label} ({annotation.Type})");
        }

        Progress = 100;
    }

    private async Task ProcessBatchAsync(StringBuilder resultsBuilder)
    {
        if (_currentProject == null || !_currentProject.Images.Any())
        {
            throw new InvalidOperationException("项目为空或没有图像");
        }

        StatusMessage = "正在批量处理项目图像...";
        var images = _currentProject.Images.ToList();
        
        resultsBuilder.AppendLine($"项目: {_currentProject.Name}");
        resultsBuilder.AppendLine($"共 {images.Count} 张图像需要处理");
        resultsBuilder.AppendLine();

        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            StatusMessage = $"正在处理 {image.FileName} ({i + 1}/{images.Count})...";
            Progress = (double)(i * 100) / images.Count;

            try
            {
                var annotations = await _aiModelManager.InferImageAsync(image.FilePath, (float)ConfidenceThreshold);
                var annotationsList = annotations.ToList();

                resultsBuilder.AppendLine($"图像 {i + 1}: {image.FileName}");
                resultsBuilder.AppendLine($"  检测到 {annotationsList.Count} 个对象");

                foreach (var annotation in annotationsList)
                {
                    _inferenceResults.Add(annotation);
                    resultsBuilder.AppendLine($"    - {annotation.Label} ({annotation.Type})");
                }

                resultsBuilder.AppendLine();
            }
            catch (Exception ex)
            {
                resultsBuilder.AppendLine($"图像 {i + 1}: {image.FileName} - 处理失败");
                resultsBuilder.AppendLine($"  错误: {ex.Message}");
                resultsBuilder.AppendLine();
            }
        }

        Progress = 100;
    }

    private void Cancel()
    {
        DialogResult = false;
        
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.AIInferenceDialog>()
                .FirstOrDefault(w => ReferenceEquals(w.DataContext, this));
            window?.Close(false);
        }
    }
}