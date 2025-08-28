﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIlable.Models;
using AIlable.Services;
using AIlable.Controls;
using AIlable.Views;

namespace AIlable.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "AIlable - Image Annotation Tool 作者：Vonweller QQ529538187";
    [ObservableProperty] private AnnotationProject? _currentProject;
    [ObservableProperty] private AnnotationImage? _currentImage;
    [ObservableProperty] private Bitmap? _currentImageBitmap;
    [ObservableProperty] private ObservableCollection<Annotation> _annotations;
    [ObservableProperty] private Annotation? _selectedAnnotation;
    [ObservableProperty] private bool _hasImage;
    [ObservableProperty] private bool _hasProject;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private Models.AnnotationTool _activeTool;
    [ObservableProperty] private DrawingState _drawingState;
    [ObservableProperty] private ObservableCollection<string> _availableLabels;
    [ObservableProperty] private string _currentLabel;
    [ObservableProperty] private int _currentLabelIndex;
    [ObservableProperty] private int _currentImageIndex;
    
    // 视图模式控制
    [ObservableProperty] private bool _isAIChatViewActive = false;
    [ObservableProperty] private AIChatViewModel? _aiChatViewModel;

    // AI模型状态相关属性
    public string AIModelStatus => _aiModelManager.HasActiveModel ? "✅ 已加载" : "❌ 未加载";
    public bool HasAIModel => _aiModelManager.HasActiveModel;
    public string CurrentAIModelName => _aiModelManager.ActiveModel?.ModelName ?? "无";
    
    // 撤销/重做状态属性
    public bool CanUndo => _undoRedoService.CanUndo;
    public bool CanRedo => _undoRedoService.CanRedo;
    public string? LastUndoDescription => _undoRedoService.LastUndoDescription;
    public string? LastRedoDescription => _undoRedoService.LastRedoDescription;

    // AI标注控制相关属性
    [ObservableProperty] private bool _isAnnotationRunning = false;
    [ObservableProperty] private bool _isAnnotationPaused = false;
    [ObservableProperty] private double _annotationProgress = 0;
    [ObservableProperty] private string _annotationProgressText = "";
    [ObservableProperty] private bool _isAnnotationInProgress = false;
    [ObservableProperty] private bool _hasDetailedProgress = false;
    [ObservableProperty] private int _processedCount = 0;
    [ObservableProperty] private int _totalCount = 0;
    private CancellationTokenSource? _annotationCancellationTokenSource;

    // 标注模式枚举
    public enum AnnotationMode
    {
        Fast,    // 极速标注
        Preview  // 预览标注
    }

    [ObservableProperty] private AnnotationMode _currentAnnotationMode = AnnotationMode.Fast;

    // 摄像头相关属性
    [ObservableProperty] private bool _isCameraActive = false;
    [ObservableProperty] private string _cameraStatus = "摄像头未启动";
    [ObservableProperty] private bool _isCameraLoading = false;
    [ObservableProperty] private ICameraService? _cameraService;
    [ObservableProperty] private int _captureCount = 0;

    partial void OnCurrentImageIndexChanged(int value)
    {
        LoadImageByIndex(value);
    }

    private readonly ToolManager _toolManager;
    private readonly AIModelManager _aiModelManager;
    private readonly PerformanceMonitorService _performanceMonitor;
    private readonly UserExperienceService _userExperienceService;
    private readonly SmartToolSwitchingService _smartToolSwitching;
    private readonly UndoRedoService _undoRedoService;
    private IFileDialogService? _fileDialogService;

    public MainViewModel()
    {
        _annotations = new ObservableCollection<Annotation>();
        _statusText = "Ready";
        _activeTool = Models.AnnotationTool.Select;
        _drawingState = DrawingState.None;
        
        // 初始化标签系统
        _availableLabels = new ObservableCollection<string> 
        { 
            "person", "car", "bike", "dog", "cat", "object", "building", "tree", "sign"
        };
        _currentLabel = _availableLabels[0];
        _currentLabelIndex = 0;
        
        _toolManager = new ToolManager();
        _aiModelManager = new AIModelManager();
        _performanceMonitor = new PerformanceMonitorService();
        _userExperienceService = new UserExperienceService();
        _smartToolSwitching = new SmartToolSwitchingService(_toolManager);
        _undoRedoService = new UndoRedoService();
        
        // 初始化摄像头服务
        _cameraService = new CameraService();
        
        LoadImageCommand = new AsyncRelayCommand(LoadImageAsync);
        CreateNewProjectCommand = new AsyncRelayCommand(CreateNewProjectAsync);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
        SaveProjectAsCommand = new AsyncRelayCommand(SaveProjectAsAsync);
        AddImagesCommand = new AsyncRelayCommand(AddImagesAsync);
        ExportProjectCommand = new AsyncRelayCommand(ExportProjectAsync);
        ExportCocoCommand = new AsyncRelayCommand(() => ExportSpecificFormatAsync("COCO"));
        ExportYoloCommand = new AsyncRelayCommand(() => ExportSpecificFormatAsync("YOLO"));
        ExportVocCommand = new AsyncRelayCommand(() => ExportSpecificFormatAsync("VOC"));
        FitToWindowCommand = new RelayCommand(() => FitToWindowRequested?.Invoke());
        ResetViewCommand = new RelayCommand(() => ResetViewRequested?.Invoke());
        
        // AI模型命令
        LoadAIModelCommand = new AsyncRelayCommand(LoadAIModelAsync);
        RunAIInferenceCommand = new AsyncRelayCommand(RunAIInferenceAsync);
        RunAutoInferenceCommand = new AsyncRelayCommand(RunAutoInferenceAsync);
        RunBatchAIInferenceCommand = new AsyncRelayCommand(RunBatchAIInferenceAsync);
        ConfigureAIModelCommand = new AsyncRelayCommand(ConfigureAIModelAsync);

        // AI标注控制命令
        StartAIAnnotationCommand = new AsyncRelayCommand(StartAIAnnotationAsync);
        PauseResumeAnnotationCommand = new RelayCommand(PauseResumeAnnotation);
        StopAnnotationCommand = new RelayCommand(StopAnnotation);
        
        // Tool commands
        SelectToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Select));
        RectangleToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Rectangle));
        CircleToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Circle));
        PolygonToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Polygon));
        LineToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Line));
        PointToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Point));
        OrientedBoundingBoxToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.OrientedBoundingBox));
        KeypointToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Keypoint));
        
        // 标签切换命令
        NextLabelCommand = new RelayCommand(NextLabel);
        PreviousLabelCommand = new RelayCommand(PreviousLabel);
        AddNewLabelCommand = new AsyncRelayCommand(AddNewLabelAsync);
        DeleteCurrentLabelCommand = new AsyncRelayCommand(DeleteCurrentLabelAsync, () => AvailableLabels.Count > 1);

        // 标注管理命令
        DeleteSelectedAnnotationCommand = new RelayCommand(DeleteSelectedAnnotation, () => SelectedAnnotation != null);
        ClearAllAnnotationsCommand = new RelayCommand(ClearAllAnnotations, () => Annotations.Count > 0);

        // 主题切换命令
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        // 图像导航命令
        NextImageCommand = new RelayCommand(NextImage);
        PreviousImageCommand = new RelayCommand(PreviousImage);
        
        // AI聊天命令
        OpenAIChatCommand = new AsyncRelayCommand(OpenAIChatAsync);
        BackToAnnotationCommand = new RelayCommand(BackToAnnotation);
        
        // 撤销/重做命令
        UndoCommand = new RelayCommand(() => _undoRedoService.Undo(), () => _undoRedoService.CanUndo);
        RedoCommand = new RelayCommand(() => _undoRedoService.Redo(), () => _undoRedoService.CanRedo);
        
        // 摄像头命令
        StartCameraCommand = new AsyncRelayCommand(StartCameraAsync);
        StopCameraCommand = new AsyncRelayCommand(StopCameraAsync);
        CaptureCameraImageCommand = new AsyncRelayCommand(CaptureCameraImageAsync);
        
        // 订阅撤销服务的属性变化以更新命令状态
        _undoRedoService.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(UndoRedoService.CanUndo))
            {
                (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(LastUndoDescription));
            }
            if (e.PropertyName == nameof(UndoRedoService.CanRedo))
            {
                (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(LastRedoDescription));
            }
        };

        // Subscribe to tool manager events
        _toolManager.ActiveToolChanged += OnActiveToolChangedInternal;
    }

    public ICommand LoadImageCommand { get; }
    public ICommand CreateNewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand SaveProjectAsCommand { get; }
    public ICommand AddImagesCommand { get; }
    public ICommand ExportProjectCommand { get; }
    public ICommand ExportCocoCommand { get; }
    public ICommand ExportYoloCommand { get; }
    public ICommand ExportVocCommand { get; }
    public ICommand FitToWindowCommand { get; }
    public ICommand ResetViewCommand { get; }
    
    // AI模型命令
    public ICommand LoadAIModelCommand { get; }
    public ICommand RunAIInferenceCommand { get; }
    public ICommand RunAutoInferenceCommand { get; }
    public ICommand RunBatchAIInferenceCommand { get; }
    public ICommand ConfigureAIModelCommand { get; }

    // AI标注控制命令
    public ICommand StartAIAnnotationCommand { get; }
    public ICommand PauseResumeAnnotationCommand { get; }
    public ICommand StopAnnotationCommand { get; }
    
    // Tool commands
    public ICommand SelectToolCommand { get; }
    public ICommand RectangleToolCommand { get; }
    public ICommand CircleToolCommand { get; }
    public ICommand PolygonToolCommand { get; }
    public ICommand LineToolCommand { get; }
    public ICommand PointToolCommand { get; }
    public ICommand OrientedBoundingBoxToolCommand { get; }
    public ICommand KeypointToolCommand { get; }
    
    // 标签命令
    public ICommand NextLabelCommand { get; }
    public ICommand PreviousLabelCommand { get; }
    public ICommand AddNewLabelCommand { get; }
    public ICommand DeleteCurrentLabelCommand { get; }

    // 标注管理命令
    public ICommand DeleteSelectedAnnotationCommand { get; }
    public ICommand ClearAllAnnotationsCommand { get; }

    // 主题命令
    public ICommand ToggleThemeCommand { get; }
    
    // AI聊天命令
    public ICommand OpenAIChatCommand { get; }
    public ICommand BackToAnnotationCommand { get; }
    
    // 撤销/重做命令
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    
    // 图像导航命令
    public ICommand NextImageCommand { get; }
    public ICommand PreviousImageCommand { get; }
    
    // 摄像头命令
    public ICommand StartCameraCommand { get; }
    public ICommand StopCameraCommand { get; }
    public ICommand CaptureCameraImageCommand { get; }

    public ToolManager ToolManager => _toolManager;
    public AIModelManager AIModelManager => _aiModelManager;
    public PerformanceMonitorService PerformanceMonitor => _performanceMonitor;
    public UserExperienceService UserExperience => _userExperienceService;

    // Events for view interaction
    public event System.Action? FitToWindowRequested;
    public event System.Action? ResetViewRequested;
    public event System.Action? CameraPreviewStartRequested;

    public void SetFileDialogService(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
    }

    private void SetActiveTool(Models.AnnotationTool tool)
    {
        _toolManager.ActiveTool = tool;
    }

    private void OnActiveToolChangedInternal(Models.AnnotationTool newTool)
    {
        ActiveTool = newTool;
        StatusText = $"已选择: {GetToolDisplayName(newTool)}";
    }

    private string GetToolDisplayName(Models.AnnotationTool tool)
    {
        return tool switch
        {
            Models.AnnotationTool.Select => "选择工具",
            Models.AnnotationTool.Rectangle => "矩形工具",
            Models.AnnotationTool.Circle => "圆形工具", 
            Models.AnnotationTool.Polygon => "多边形工具",
            Models.AnnotationTool.Line => "线条工具",
            Models.AnnotationTool.Point => "点工具",
            Models.AnnotationTool.OrientedBoundingBox => "有向边界框工具",
            Models.AnnotationTool.Keypoint => "关键点姿态工具",
            Models.AnnotationTool.Pan => "平移工具",
            Models.AnnotationTool.Zoom => "缩放工具",
            _ => "未知工具"
        };
    }

    public void HandleCanvasClick(Point2D imagePoint)
    {
        var activeTool = _toolManager.GetActiveTool();
        if (activeTool == null)
        {
            StatusText = $"点击位置: ({imagePoint.X:F1}, {imagePoint.Y:F1})";
            return;
        }

        if (ActiveTool == Models.AnnotationTool.Select)
        {
            // Selection handled by canvas
            return;
        }

        // Handle drawing tools
        if (!activeTool.IsDrawing)
        {
            // Start drawing
            activeTool.StartDrawing(imagePoint);
            DrawingState = DrawingState.Drawing;
            StatusText = $"开始绘制 {GetToolDisplayName(ActiveTool)}";
        }
        else
        {
            // Continue or finish drawing
            if (activeTool is PolygonTool polygonTool)
            {
                // Special handling for polygon tool
                // First check if this click should complete the polygon
                var result = activeTool.FinishDrawing(imagePoint);
                if (result != null)
                {
                    // Polygon completed
                    AddAnnotationWithUndo(result);
                    DrawingState = DrawingState.None;
                    StatusText = $"完成绘制多边形";
                }
                else
                {
                    // Add vertex to continue drawing polygon
                    polygonTool.AddVertex(imagePoint);
                    StatusText = $"继续绘制多边形 (顶点: {polygonTool.CurrentAnnotation?.GetPoints().Count})";
                }
            }
            else if (activeTool is KeypointTool keypointTool)
            {
                // 特殊处理关键点工具的两阶段流程
                var result = activeTool.FinishDrawing(imagePoint);
                
                if (keypointTool.GetCurrentState() == KeypointTool.KeypointAnnotationState.PlacingKeypoints)
                {
                    // 在关键点标记阶段，点击由ImageCanvas的HandleKeypointAnnotationInteraction处理
                    var markedLabels = keypointTool.GetMarkedKeypointLabels();
                    StatusText = $"标记关键点: {CurrentLabel} | 已标记: {markedLabels.Count}个 (按Enter完成)";
                }
                else if (result != null)
                {
                    // 边界框绘制完成，进入关键点标记阶段
                    StatusText = $"边界框完成，现在选择标签并点击标记关键点位置 (当前标签: {CurrentLabel})";
                }
            }
            else
            {
                // Finish drawing for other tools
                var result = activeTool.FinishDrawing(imagePoint);
                if (result != null)
                {
                    AddAnnotationWithUndo(result);
                    DrawingState = DrawingState.None;
                    StatusText = $"完成绘制 {GetToolDisplayName(ActiveTool)}";
                }
            }
        }
    }

    public void HandleCanvasMouseMove(Point2D imagePoint)
    {
        var activeTool = _toolManager.GetActiveTool();
        if (activeTool != null && activeTool.IsDrawing)
        {
            activeTool.UpdateDrawing(imagePoint);
        }
    }

    public Annotation? GetCurrentDrawingAnnotation()
    {
        var activeTool = _toolManager.GetActiveTool();
        return activeTool?.CurrentAnnotation;
    }

    public void CancelCurrentDrawing()
    {
        var activeTool = _toolManager.GetActiveTool();
        if (activeTool != null && activeTool.IsDrawing)
        {
            // 特殊处理关键点工具
            if (activeTool is KeypointTool keypointTool)
            {
                keypointTool.CancelCurrentAnnotation();
            }
            
            activeTool.CancelDrawing();
            DrawingState = DrawingState.None;
            StatusText = "已取消绘制";
        }
    }

    private async Task LoadImageAsync()
    {
        if (_fileDialogService == null)
        {
            StatusText = "文件对话框服务未初始化";
            return;
        }

        try
        {
            var filePath = await _fileDialogService.ShowOpenFileDialogAsync(
                "选择图像文件", 
                new[] { FileDialogService.ImageFiles, FileDialogService.AllFiles });

            if (!string.IsNullOrEmpty(filePath))
            {
                await LoadImageFromPath(filePath);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"加载图像失败: {ex.Message}";
        }
    }

    private async Task OpenProjectAsync()
    {
        if (_fileDialogService == null)
        {
            StatusText = "文件对话框服务未初始化";
            return;
        }

        try
        {
            var filePath = await _fileDialogService.ShowOpenFileDialogAsync(
                "打开项目文件",
                new[] { FileDialogService.ProjectFiles, FileDialogService.AllFiles });

            if (!string.IsNullOrEmpty(filePath))
            {
                StatusText = "正在加载项目...";
                Console.WriteLine($"尝试加载项目文件: {filePath}");
                
                var project = await ProjectService.LoadProjectAsync(filePath);
                
                if (project != null)
                {
                    // 设置当前项目，OnCurrentProjectChanged会处理状态更新
                    CurrentProject = project;
                    StatusText = $"项目加载成功: {project.Name}，包含 {project.Images.Count} 张图像，{project.TotalAnnotations} 个标注";
                    Console.WriteLine($"项目加载成功，图像数量: {project.Images.Count}");
                }
                else
                {
                    StatusText = "项目加载失败 - 文件可能已损坏或格式不正确";
                    Console.WriteLine("项目加载失败 - ProjectService.LoadProjectAsync返回null");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"打开项目失败: {ex.Message}";
            Console.WriteLine($"打开项目异常: {ex}");
        }
    }

    private async Task SaveProjectAsync()
    {
        if (CurrentProject == null)
        {
            StatusText = "没有项目需要保存";
            return;
        }

        try
        {
            // 如果项目路径为空或者项目文件不存在，使用另存为
            if (string.IsNullOrEmpty(CurrentProject.ProjectPath) ||
                !File.Exists(CurrentProject.ProjectFilePath))
            {
                await SaveProjectAsAsync();
                return;
            }

            var filePath = CurrentProject.ProjectFilePath;
            StatusText = "正在保存项目...";

            var success = await ProjectService.SaveProjectAsync(CurrentProject, filePath);
            if (success)
            {
                StatusText = "项目保存成功";
                // 更新窗口标题以反映保存状态
                Title = $"AIlable - {CurrentProject.Name}";
            }
            else
            {
                StatusText = "项目保存失败";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"保存项目失败: {ex.Message}";
            Console.WriteLine($"保存项目异常: {ex}");
        }
    }

    private async Task SaveProjectAsAsync()
    {
        if (CurrentProject == null || _fileDialogService == null)
        {
            StatusText = "无法保存项目";
            return;
        }

        try
        {
            var defaultFileName = ProjectService.GetDefaultProjectFileName(CurrentProject.Name);
            var filePath = await _fileDialogService.ShowSaveFileDialogAsync(
                "保存项目文件",
                defaultFileName,
                new[] { FileDialogService.ProjectFiles, FileDialogService.AllFiles });

            if (!string.IsNullOrEmpty(filePath))
            {
                StatusText = "正在保存项目...";
                var success = await ProjectService.SaveProjectAsync(CurrentProject, filePath);

                if (success)
                {
                    StatusText = "项目保存成功";
                    // 更新项目路径和名称
                    CurrentProject.SetProjectFilePath(filePath);
                    // 更新窗口标题
                    Title = $"AIlable - {CurrentProject.Name}";
                }
                else
                {
                    StatusText = "项目保存失败";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"保存项目失败: {ex.Message}";
            Console.WriteLine($"保存项目异常: {ex}");
        }
    }

    private async Task AddImagesAsync()
    {
        if (_fileDialogService == null)
        {
            StatusText = "文件对话框服务未初始化";
            return;
        }

        try
        {
            // 让用户选择导入方式：文件或文件夹
            var choice = await ShowImportChoiceDialog();
            
            List<string> filePaths = new List<string>();
            
            if (choice == "folder")
            {
                // 选择文件夹
                var folderPath = await _fileDialogService.ShowSelectFolderDialogAsync("选择图像文件夹");
                if (!string.IsNullOrEmpty(folderPath))
                {
                    // 递归查找文件夹中的图像文件
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp" };
                    filePaths = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .ToList();
                }
            }
            else if (choice == "files")
            {
                // 选择多个文件
                var selectedFiles = await _fileDialogService.ShowOpenMultipleFilesDialogAsync(
                    "添加图像文件",
                    new[] { FileDialogService.ImageFiles, FileDialogService.AllFiles });
                filePaths.AddRange(selectedFiles);
            }
            else
            {
                return; // 用户取消
            }

            if (filePaths.Any())
            {
                // Create project if doesn't exist
                if (CurrentProject == null)
                {
                    CurrentProject = new AnnotationProject
                    {
                        Name = "New Project",
                        Description = "A new annotation project"
                    };
                    HasProject = true;
                }

                StatusText = "正在添加图像...";
                int addedCount = 0;

                foreach (var filePath in filePaths)
                {
                    var existingImage = CurrentProject!.GetImageByFileName(Path.GetFileName(filePath));
                    if (existingImage == null)
                    {
                        var annotationImage = await ImageService.CreateAnnotationImageAsync(filePath);
                        if (annotationImage != null)
                        {
                            CurrentProject.AddImage(annotationImage);
                            addedCount++;
                        }
                    }
                }

                StatusText = $"已添加 {addedCount} 张图像到项目";
                
                // 如果成功添加了图像且当前没有显示图像，自动加载第一张
                if (addedCount > 0 && !HasImage && CurrentProject.Images.Count > 0)
                {
                    CurrentImageIndex = 0;
                    LoadImageByIndex(0);

                    // 延迟一点确保图像加载完成后自动适应窗口
                    _ = System.Threading.Tasks.Task.Delay(150).ContinueWith(_ =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            FitToWindowRequested?.Invoke();
                        });
                    });
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加图像失败: {ex.Message}";
        }
    }

    private async Task ExportProjectAsync()
    {
        if (CurrentProject == null || _fileDialogService == null)
        {
            StatusText = "没有项目可导出";
            return;
        }

        try
        {
            // 创建并显示导出对话框
            var exportViewModel = new ExportDialogViewModel();
            var dialog = new AIlable.Views.ExportDialog(exportViewModel);

            // 获取父窗口
            var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var result = await dialog.ShowDialog<bool?>(parentWindow!);

            if (result == true)
            {
                StatusText = "正在导出数据集...";
                
                // 根据用户选择执行导出
                var success = await ExportDatasetAsync(
                    exportViewModel.SelectedFormat.Format,
                    exportViewModel.OutputPath,
                    exportViewModel.IncludeImages,
                    exportViewModel.SplitTrainVal,
                    exportViewModel.TrainRatio);

                StatusText = success ? "数据集导出成功" : "数据集导出失败";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导出数据集失败: {ex.Message}";
        }
    }

    private async Task ExportSpecificFormatAsync(string format)
    {
        if (CurrentProject == null || _fileDialogService == null)
        {
            StatusText = "没有项目可导出";
            return;
        }

        try
        {
            // 检查混合标注类型
            if (CurrentProject.HasMixedAnnotationTypes())
            {
                var annotationTypes = CurrentProject.GetUsedAnnotationTypes();
                bool useSegmentationFormat = format == "YOLO"; // YOLO默认使用分割格式

                var warningViewModel = new MixedAnnotationWarningDialogViewModel(
                    annotationTypes,
                    format,
                    useSegmentationFormat);

                var warningDialog = new Views.MixedAnnotationWarningDialog(warningViewModel);

                // 获取父窗口
                var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                var continueExport = await warningDialog.ShowDialog<bool?>(parentWindow!);

                if (continueExport != true)
                {
                    StatusText = "已取消导出";
                    return;
                }
            }

            // 选择输出目录
            var outputPath = await _fileDialogService.ShowSelectFolderDialogAsync("选择导出目录");
            if (string.IsNullOrEmpty(outputPath))
                return;

            StatusText = $"正在导出{format}格式...";

            bool success = false;
            switch (format)
            {
                case "COCO":
                    success = await ExportService.ExportToCocoAsync(CurrentProject, outputPath);
                    break;
                case "YOLO":
                    success = await ExportYoloWithOptionsAsync(CurrentProject, outputPath);
                    break;
                case "VOC":
                    success = await ExportService.ExportToVocAsync(CurrentProject, outputPath);
                    break;
            }

            StatusText = success ? $"{format}格式导出成功" : $"{format}格式导出失败";
        }
        catch (Exception ex)
        {
            StatusText = $"导出{format}格式失败: {ex.Message}";
        }
    }

    private async Task<bool> ExportDatasetAsync(ExportFormat format, string outputPath, bool includeImages, bool splitTrainVal, double trainRatio)
    {
        try
        {
            // 检查混合标注类型
            if (CurrentProject!.HasMixedAnnotationTypes())
            {
                var annotationTypes = CurrentProject.GetUsedAnnotationTypes();
                bool useSegmentationFormat = format == ExportFormat.YOLO; // YOLO默认使用分割格式

                var warningViewModel = new MixedAnnotationWarningDialogViewModel(
                    annotationTypes,
                    format.ToString(),
                    useSegmentationFormat);

                var warningDialog = new Views.MixedAnnotationWarningDialog(warningViewModel);

                // 获取父窗口
                var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                var continueExport = await warningDialog.ShowDialog<bool?>(parentWindow!);

                if (continueExport != true)
                {
                    StatusText = "已取消导出";
                    return false;
                }
            }

            // 确保输出目录存在
            Directory.CreateDirectory(outputPath);

            // 根据格式进行导出
            switch (format)
            {
                case ExportFormat.COCO:
                    return await ExportService.ExportToCocoAsync(CurrentProject!, outputPath);

                case ExportFormat.VOC:
                    return await ExportService.ExportToVocAsync(CurrentProject!, outputPath);

                case ExportFormat.YOLO:
                    return await ExportYoloWithOptionsAsync(CurrentProject!, outputPath);

                case ExportFormat.TXT:
                    // 对于TXT格式，我们需要实现一个简单的导出
                    return await ExportToTxtAsync(CurrentProject!, outputPath);

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败: {ex.Message}";
            Console.WriteLine($"Export error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    
    private async Task<bool> ExportToTxtAsync(AnnotationProject project, string outputPath)
    {
        try
        {
            var annotationsFile = Path.Combine(outputPath, "annotations.txt");
            var lines = new List<string>();
            
            foreach (var image in project.Images)
            {
                foreach (var annotation in image.Annotations)
                {
                    // 从标注标签中提取纯净的标签名称（去除置信度分数）
                    var cleanLabel = ExportService.ExtractCleanLabel(annotation.Label);
                    var center = annotation.GetCenter();
                    lines.Add($"{image.FileName},{annotation.Type},{cleanLabel},{center.X},{center.Y}");
                }
            }
            
            await File.WriteAllLinesAsync(annotationsFile, lines);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LoadImageFromPath(string filePath)
    {
        try
        {
            await _userExperienceService.ExecuteWithStatusAsync(
                "正在加载图像...",
                async (progress, cancellationToken) =>
                {
                    // 报告进度：开始创建标注图像
                    progress.Report(new ProgressInfo { Percentage = 20, Message = "创建标注图像..." });

                    var annotationImage = await _performanceMonitor.MonitorAsync(
                        "CreateAnnotationImage",
                        () => ImageServiceEnhanced.CreateAnnotationImageOptimizedAsync(filePath),
                        filePath);

                    if (annotationImage == null)
                    {
                        throw new InvalidOperationException("Failed to load image");
                    }

                    // 报告进度：加载位图
                    progress.Report(new ProgressInfo { Percentage = 60, Message = "加载图像位图..." });

                    var bitmap = await _performanceMonitor.MonitorAsync(
                        "LoadImageBitmap",
                        () => ImageServiceEnhanced.LoadImageWithCacheAsync(filePath),
                        filePath);

                    if (bitmap == null)
                    {
                        throw new InvalidOperationException("Failed to load image bitmap");
                    }

                    // 报告进度：更新UI
                    progress.Report(new ProgressInfo { Percentage = 80, Message = "更新界面..." });

                    CurrentImage = annotationImage;
                    CurrentImageBitmap = bitmap;
                    HasImage = true;

                    // 更新标注集合
                    Annotations.Clear();
                    foreach (var annotation in annotationImage.Annotations)
                    {
                        Annotations.Add(annotation);
                    }

                    // 添加到当前项目
                    if (CurrentProject != null)
                    {
                        var existingImage = CurrentProject.GetImageByFileName(annotationImage.FileName);
                        if (existingImage == null)
                        {
                            CurrentProject.AddImage(annotationImage);
                        }
                    }

                    progress.Report(new ProgressInfo { Percentage = 100, Message = "图像加载完成" });
                },
                $"已加载: {Path.GetFileName(filePath)}"
            );
        }
        catch (Exception ex)
        {
            _userExperienceService.ShowError($"加载图像失败: {ex.Message}");
        }
    }

    private async Task CreateNewProjectAsync()
    {
        try
        {
            // 创建新项目
            var newProject = ProjectService.CreateNewProject("New Project");

            // 添加默认标签
            newProject.Labels.Add("person");
            newProject.Labels.Add("car");
            newProject.Labels.Add("object");

            // 设置当前项目，OnCurrentProjectChanged会处理状态更新
            CurrentProject = newProject;
            
            // 清空撤销历史
            _undoRedoService.Clear();
            
            StatusText = "已创建新项目，请添加图像开始标注";

            // 自动打开添加图像对话框
            await Task.Delay(100); // 给UI一点时间更新
            await AddImagesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"创建新项目失败: {ex.Message}";
            Console.WriteLine($"创建新项目异常: {ex}");
        }
    }

    public void AddAnnotation(Annotation annotation)
    {
        if (CurrentImage == null) return;

        // 设置标注的标签为当前选择的标签
        annotation.Label = CurrentLabel;

        CurrentImage.AddAnnotation(annotation);
        Annotations.Add(annotation);
        StatusText = $"添加了 [{CurrentLabel}] {annotation.Type} 标注";

        // 通知命令状态变化
        (ClearAllAnnotationsCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
    
    /// <summary>
    /// 使用撤销功能添加标注
    /// </summary>
    public void AddAnnotationWithUndo(Annotation annotation)
    {
        if (CurrentImage == null) return;

        // 设置标注的标签为当前选择的标签
        annotation.Label = CurrentLabel;

        var command = new AddAnnotationCommand(this, annotation, CurrentImage);
        _undoRedoService.ExecuteCommand(command);
        
        StatusText = $"添加了 [{CurrentLabel}] {annotation.Type} 标注";
    }

    public void RemoveAnnotation(Annotation annotation)
    {
        if (CurrentImage == null) return;

        CurrentImage.RemoveAnnotation(annotation);
        Annotations.Remove(annotation);

        if (SelectedAnnotation == annotation)
        {
            SelectedAnnotation = null;
        }

        StatusText = $"已删除 {annotation.Type} 标注";

        // 通知命令状态变化
        (DeleteSelectedAnnotationCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ClearAllAnnotationsCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
    
    /// <summary>
    /// 使用撤销功能删除标注
    /// </summary>
    public void RemoveAnnotationWithUndo(Annotation annotation)
    {
        if (CurrentImage == null) return;

        var command = new RemoveAnnotationCommand(this, annotation, CurrentImage);
        _undoRedoService.ExecuteCommand(command);
        
        StatusText = $"已删除 {annotation.Type} 标注";
    }

    public void SelectAnnotation(Annotation? annotation)
    {
        if (SelectedAnnotation != null)
        {
            SelectedAnnotation.IsSelected = false;
        }

        SelectedAnnotation = annotation;

        if (SelectedAnnotation != null)
        {
            SelectedAnnotation.IsSelected = true;
            StatusText = $"已选择 {annotation?.Type} 标注: {annotation?.Label}";
        }
        else
        {
            StatusText = "未选择标注";
        }

        // 通知命令状态变化
        (DeleteSelectedAnnotationCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
    
    /// <summary>
    /// 完成标注（特别是右键完成的关键点标注）
    /// </summary>
    public void CompleteAnnotation(Annotation annotation)
    {
        if (annotation != null && CurrentImage != null)
        {
            // 确保标注被添加到当前图像
            if (!CurrentImage.Annotations.Contains(annotation))
            {
                // 使用撤销/重做系统添加标注
                var command = new AddAnnotationCommand(this, annotation, CurrentImage);
                _undoRedoService.ExecuteCommand(command);
                
                StatusText = $"已完成 {annotation.Type} 标注: {annotation.Label}";
            }
            
            // 选择刚完成的标注
            SelectAnnotation(annotation);
        }
    }

    partial void OnCurrentImageChanged(AnnotationImage? value)
    {
        HasImage = value != null;

        if (value != null)
        {
            // Update title with current image name
            Title = $"AIlable - {value.FileName}";

            // 自动适应窗口
            FitToWindowRequested?.Invoke();
        }
        else
        {
            Title = "AIlable - Image Annotation Tool 作者：Vonweller QQ529538187";
        }
    }

    partial void OnCurrentProjectChanged(AnnotationProject? value)
    {
        HasProject = value != null;

        if (value != null)
        {
            // 更新标签列表
            AvailableLabels = new ObservableCollection<string>(value.Labels);
            if (AvailableLabels.Count > 0)
            {
                CurrentLabel = AvailableLabels[0];
                CurrentLabelIndex = 0;
                UpdateToolsWithCurrentLabel();
            }
            else
            {
                // 如果项目没有标签，添加默认标签
                value.Labels.Add("object");
                AvailableLabels = new ObservableCollection<string>(value.Labels);
                CurrentLabel = AvailableLabels[0];
                CurrentLabelIndex = 0;
                UpdateToolsWithCurrentLabel();
            }
            
            // 更新AI模型管理器的项目标签
            _aiModelManager.SetProjectLabels(value.Labels.ToList());
            Console.WriteLine($"已更新AI模型项目标签: {string.Join(", ", value.Labels)}");

            // 更新窗口标题
            Title = $"AIlable - {value.Name}";

            // 如果项目有图像，设置当前图像索引
            if (value.Images.Count > 0)
            {
                CurrentImageIndex = 0;
                // 延迟一点确保图像加载完成后自动适应窗口
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        FitToWindowRequested?.Invoke();
                    });
                });
            }
            else
            {
                // 清空当前图像状态
                CurrentImage = null;
                CurrentImageBitmap = null;
                HasImage = false;
                Annotations.Clear();
            }
        }
        else
        {
            // 清空所有状态
            AvailableLabels.Clear();
            CurrentLabel = "";
            CurrentLabelIndex = 0;
            CurrentImage = null;
            CurrentImageBitmap = null;
            HasImage = false;
            Annotations.Clear();
            CurrentImageIndex = 0;
            Title = "AIlable - Image Annotation Tool 作者：Vonweller QQ529538187";
            
            // 清空AI模型标签
            _aiModelManager.SetProjectLabels(new List<string>());
            Console.WriteLine("已清空AI模型项目标签");
        }
    }

    // AI模型相关方法
    private async Task ConfigureAIModelAsync()
    {
        if (_fileDialogService == null)
        {
            StatusText = "文件对话框服务未初始化";
            return;
        }

        try
        {
            var dialogViewModel = new AIModelConfigDialogViewModel(_aiModelManager, _fileDialogService);
            var dialog = new Views.AIModelConfigDialog(dialogViewModel);
            
            // 获取父窗口
            var parentWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var result = await dialog.ShowDialog<bool?>(parentWindow!);
            
            if (result == true)
            {
                StatusText = _aiModelManager.HasActiveModel
                    ? $"AI模型配置完成: {_aiModelManager.GetModelInfo()}"
                    : "AI模型配置完成";

                // 设置智能工具切换的模型类型
                if (_aiModelManager.HasActiveModel && _aiModelManager.ActiveModel != null)
                {
                    _smartToolSwitching.SetCurrentModelType(_aiModelManager.ActiveModel.ModelType);
                }

                // 通知界面更新AI模型状态
                OnPropertyChanged(nameof(AIModelStatus));
                OnPropertyChanged(nameof(HasAIModel));
                OnPropertyChanged(nameof(CurrentAIModelName));
            }
        }
        catch (Exception ex)
        {
            StatusText = $"AI模型配置失败: {ex.Message}";
        }
    }

    private async Task LoadAIModelAsync()
    {
        // 使用新的配置对话框替代简单的文件选择
        await ConfigureAIModelAsync();
    }

    private async Task RunAIInferenceAsync()
    {
        if (!_aiModelManager.HasActiveModel)
        {
            StatusText = "请先加载AI模型";
            return;
        }

        try
        {
            // 创建AI推理对话框
            var dialogViewModel = new AIInferenceDialogViewModel(
                _aiModelManager, 
                CurrentProject, 
                CurrentImage);

            var dialog = new Views.AIInferenceDialog(dialogViewModel);
            
            // 获取父窗口
            var parentWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var result = await dialog.ShowDialog<bool?>(parentWindow!);
            
            if (result == true && dialogViewModel.InferenceResults.Any())
            {
                // 处理推理结果
                if (dialogViewModel.ReplaceExistingAnnotations && CurrentImage != null)
                {
                    // 清空现有标注
                    var existingAnnotations = Annotations.ToList();
                    foreach (var annotation in existingAnnotations)
                    {
                        RemoveAnnotation(annotation);
                    }
                }

                // 添加AI检测到的标注
                if (dialogViewModel.ProcessCurrentImageOnly && CurrentImage != null)
                {
                    // 单图像模式：直接添加到当前图像
                    foreach (var annotation in dialogViewModel.InferenceResults)
                    {
                        AddAnnotation(annotation);
                    }
                }
                else if (CurrentProject != null)
                {
                    // 批量模式：分配标注到对应图像
                    var resultsByImage = new Dictionary<string, List<Annotation>>();
                    
                    // 按图像文件名分组结果（这里需要改进实现）
                    foreach (var annotation in dialogViewModel.InferenceResults)
                    {
                        // 暂时添加到当前图像，实际实现需要根据推理结果的来源图像进行分配
                        if (CurrentImage != null)
                        {
                            AddAnnotation(annotation);
                        }
                    }
                }
                
                StatusText = $"AI推理完成，检测到 {dialogViewModel.InferenceResults.Count} 个对象";
            }
            else if (result == true)
            {
                StatusText = "AI推理完成，未检测到对象";
            }
            else
            {
                StatusText = "AI推理已取消";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"AI推理失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 自动推理所有未标注的图像
    /// </summary>
    private async Task RunAutoInferenceAsync()
    {
        try
        {
            if (!_aiModelManager.HasActiveModel)
            {
                StatusText = "❌ 请先加载AI模型";
                return;
            }

            if (CurrentProject == null)
            {
                StatusText = "❌ 请先创建或打开项目";
                return;
            }

            // 获取所有未标注的图像
            var unannotatedImages = CurrentProject.Images
                .Where(img => img.Annotations.Count == 0)
                .ToList();

            if (unannotatedImages.Count == 0)
            {
                StatusText = "✅ 所有图像都已标注，无需AI推理";
                return;
            }

            StatusText = $"🤖 发现 {unannotatedImages.Count} 张未标注图像，开始AI自动推理...";

            var processedCount = 0;
            var totalAnnotations = 0;

            foreach (var image in unannotatedImages)
            {
                try
                {
                    // 切换到当前正在处理的图像，让用户能看到预览
                    var imageIndex = CurrentProject.Images.IndexOf(image);
                    if (imageIndex >= 0)
                    {
                        CurrentImageIndex = imageIndex;
                        // 等待图像加载完成
                        await Task.Delay(200);
                    }

                    StatusText = $"🤖 正在推理: {image.FileName} ({processedCount + 1}/{unannotatedImages.Count})";

                            // 对单张图像进行推理，使用默认置信度阈值
                            var annotations = await _aiModelManager.InferImageAsync(image.FilePath, 0.5f);

                    // 智能工具切换 - 根据推理结果切换到合适的工具
                    if (annotations.Any())
                    {
                        _smartToolSwitching.SwitchToolBasedOnInferenceResult(annotations);
                        var toolSuggestion = _smartToolSwitching.GetToolSwitchSuggestion(annotations);
                        Console.WriteLine($"工具建议: {toolSuggestion}");
                    }

                    // 添加推理结果到图像
                    foreach (var annotation in annotations)
                    {
                        image.AddAnnotation(annotation);
                        totalAnnotations++;
                    }

                    // 刷新当前图像的标注显示
                    RefreshCurrentImageAnnotations();

                    processedCount++;
                    StatusText = $"🤖 AI推理进度: {processedCount}/{unannotatedImages.Count} ({(processedCount * 100.0 / unannotatedImages.Count):F0}%) - 已检测 {annotations.Count()} 个对象";

                    // 添加延迟，让用户能看到结果
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"推理图像 {image.FileName} 失败: {ex.Message}");
                    StatusText = $"🤖 推理失败: {image.FileName} - {ex.Message}";
                    await Task.Delay(500);
                }
            }

            StatusText = $"✅ AI自动推理完成！已处理 {processedCount} 张图像，检测到 {totalAnnotations} 个对象";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ AI自动推理失败: {ex.Message}";
        }
    }

    private async Task RunBatchAIInferenceAsync()
    {
        if (!_aiModelManager.HasActiveModel)
        {
            StatusText = "请先加载AI模型";
            return;
        }

        if (CurrentProject == null || !CurrentProject.Images.Any())
        {
            StatusText = "请先添加图像到项目";
            return;
        }

        try
        {
            // 创建AI推理对话框，强制批量模式
            var dialogViewModel = new AIInferenceDialogViewModel(
                _aiModelManager, 
                CurrentProject, 
                CurrentImage)
            {
                ProcessCurrentImageOnly = false // 强制批量处理模式
            };

            var dialog = new Views.AIInferenceDialog(dialogViewModel);
            
            // 获取父窗口
            var parentWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var result = await dialog.ShowDialog<bool?>(parentWindow!);
            
            if (result == true && dialogViewModel.InferenceResults.Any())
            {
                // 批量处理结果需要分配到各个图像
                await ProcessBatchInferenceResults(dialogViewModel);
                
                StatusText = $"批量AI推理完成，共检测到 {dialogViewModel.InferenceResults.Count} 个对象";
                
                // 刷新当前图像的标注显示
                RefreshCurrentImageAnnotations();
            }
            else if (result == true)
            {
                StatusText = "批量AI推理完成，未检测到对象";
            }
            else
            {
                StatusText = "批量AI推理已取消";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"批量AI推理失败: {ex.Message}";
        }
    }

    private async Task ProcessBatchInferenceResults(AIInferenceDialogViewModel dialogViewModel)
    {
        if (CurrentProject == null) return;

        // 改进的批量推理结果处理
        // 重新运行推理以获得每个图像的具体结果
        var imagePaths = CurrentProject.Images.Select(img => img.FilePath);
        var results = await _aiModelManager.InferBatchAsync(imagePaths, (float)dialogViewModel.ConfidenceThreshold);

        // 清空现有标注（如果选择了替换模式）
        if (dialogViewModel.ReplaceExistingAnnotations)
        {
            foreach (var image in CurrentProject.Images)
            {
                var annotationsToRemove = image.Annotations.ToList();
                foreach (var annotation in annotationsToRemove)
                {
                    image.RemoveAnnotation(annotation);
                }
            }
        }

        // 分配推理结果到对应图像
        foreach (var (imagePath, annotations) in results)
        {
            var image = CurrentProject.Images.FirstOrDefault(img => img.FilePath == imagePath);
            if (image != null)
            {
                foreach (var annotation in annotations)
                {
                    image.AddAnnotation(annotation);
                }
            }
        }
    }

    private void RefreshCurrentImageAnnotations()
    {
        if (CurrentImage != null)
        {
            Annotations.Clear();
            foreach (var annotation in CurrentImage.Annotations)
            {
                Annotations.Add(annotation);
            }
        }
    }
    
    // 标签管理方法
    public void NextLabel()
    {
        if (AvailableLabels.Count > 0)
        {
            CurrentLabelIndex = (CurrentLabelIndex + 1) % AvailableLabels.Count;
            CurrentLabel = AvailableLabels[CurrentLabelIndex];
            UpdateToolsWithCurrentLabel();
            StatusText = $"当前标签: {CurrentLabel}";

            // 显示切换提示
            NotificationToast.ShowInfo($"切换到标签: {CurrentLabel} ({CurrentLabelIndex + 1}/{AvailableLabels.Count})");
        }
    }

    public void PreviousLabel()
    {
        if (AvailableLabels.Count > 0)
        {
            CurrentLabelIndex = (CurrentLabelIndex - 1 + AvailableLabels.Count) % AvailableLabels.Count;
            CurrentLabel = AvailableLabels[CurrentLabelIndex];
            UpdateToolsWithCurrentLabel();
            StatusText = $"当前标签: {CurrentLabel}";

            // 显示切换提示
            NotificationToast.ShowInfo($"切换到标签: {CurrentLabel} ({CurrentLabelIndex + 1}/{AvailableLabels.Count})");
        }
    }
    
    public void AddLabelIfNotExists(string label)
    {
        if (!AvailableLabels.Contains(label))
        {
            AvailableLabels.Add(label);
        }
    }

    /// <summary>
    /// 删除当前标签
    /// </summary>
    private async Task DeleteCurrentLabelAsync()
    {
        if (CurrentProject == null || AvailableLabels.Count <= 1)
        {
            StatusText = "无法删除标签：至少需要保留一个标签";
            NotificationToast.ShowWarning("至少需要保留一个标签");
            return;
        }

        try
        {
            var labelToDelete = CurrentLabel;

            // 检查是否有标注使用了这个标签
            var hasAnnotationsWithLabel = CurrentProject.Images
                .SelectMany(img => img.Annotations)
                .Any(ann => ann.Label == labelToDelete);

            if (hasAnnotationsWithLabel)
            {
                // 需要用户确认
                var confirmResult = await ShowDeleteLabelConfirmationAsync(labelToDelete);
                if (!confirmResult)
                {
                    return;
                }
            }

            // 删除标签
            var indexToDelete = CurrentLabelIndex;
            CurrentProject.Labels.Remove(labelToDelete);
            AvailableLabels.RemoveAt(indexToDelete);

            // 如果删除的是最后一个标签，选择前一个
            if (indexToDelete >= AvailableLabels.Count)
            {
                CurrentLabelIndex = AvailableLabels.Count - 1;
            }
            else
            {
                CurrentLabelIndex = indexToDelete;
            }

            CurrentLabel = AvailableLabels[CurrentLabelIndex];
            UpdateToolsWithCurrentLabel();

            // 如果有使用该标签的标注，将它们的标签改为当前标签
            if (hasAnnotationsWithLabel)
            {
                var updatedCount = 0;
                foreach (var image in CurrentProject.Images)
                {
                    foreach (var annotation in image.Annotations.Where(ann => ann.Label == labelToDelete))
                    {
                        annotation.Label = CurrentLabel;
                        updatedCount++;
                    }
                }

                StatusText = $"已删除标签 '{labelToDelete}'，{updatedCount} 个标注已更新为 '{CurrentLabel}'";
                NotificationToast.ShowSuccess($"已删除标签 '{labelToDelete}'，{updatedCount} 个标注已更新");
            }
            else
            {
                StatusText = $"已删除标签: {labelToDelete}";
                NotificationToast.ShowSuccess($"已删除标签: {labelToDelete}");
            }

            // 更新AI模型标签
            _aiModelManager.SetProjectLabels(CurrentProject.Labels.ToList());
            
            // 刷新当前图像的标注显示
            RefreshCurrentImageAnnotations();

            // 更新命令状态
            (DeleteCurrentLabelCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"删除标签失败: {ex.Message}";
            NotificationToast.ShowError($"删除标签失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示删除标签确认对话框
    /// </summary>
    private async Task<bool> ShowDeleteLabelConfirmationAsync(string labelName)
    {
        try
        {
            // 获取主窗口
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (mainWindow == null) return false;

            // 创建确认对话框
            var dialog = new Avalonia.Controls.Window
            {
                Title = "确认删除标签",
                Width = 400,
                Height = 200,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var stackPanel = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            stackPanel.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = $"确定要删除标签 '{labelName}' 吗？",
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Medium
            });

            stackPanel.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = "使用此标签的所有标注将被更新为当前选择的标签。",
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Gray
            });

            var buttonPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10
            };

            var cancelButton = new Avalonia.Controls.Button
            {
                Content = "取消",
                Width = 80,
                Height = 32
            };

            var confirmButton = new Avalonia.Controls.Button
            {
                Content = "删除",
                Width = 80,
                Height = 32,
                Classes = { "danger" }
            };

            bool result = false;

            cancelButton.Click += (s, e) => dialog.Close(false);
            confirmButton.Click += (s, e) =>
            {
                result = true;
                dialog.Close(true);
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(confirmButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            await dialog.ShowDialog(mainWindow);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示确认对话框失败: {ex.Message}");
            return false;
        }
    }

    private async Task AddNewLabelAsync()
    {
        if (CurrentProject == null)
        {
            StatusText = "请先创建或打开项目";
            return;
        }

        try
        {
            // 创建输入对话框
            var dialog = new Avalonia.Controls.Window
            {
                Title = "添加新标签",
                Width = 300,
                Height = 150,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var stackPanel = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            var textBlock = new Avalonia.Controls.TextBlock
            {
                Text = "请输入新标签名称:",
                FontSize = 14
            };

            var textBox = new Avalonia.Controls.TextBox
            {
                Watermark = "标签名称",
                FontSize = 13
            };

            var buttonPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10
            };

            var okButton = new Avalonia.Controls.Button
            {
                Content = "确定",
                Width = 60,
                Height = 30
            };

            var cancelButton = new Avalonia.Controls.Button
            {
                Content = "取消",
                Width = 60,
                Height = 30
            };

            string? result = null;
            okButton.Click += (s, e) =>
            {
                result = textBox.Text?.Trim();
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            // 获取父窗口
            var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            await dialog.ShowDialog(parentWindow!);

            // 处理结果
            if (!string.IsNullOrEmpty(result))
            {
                if (AvailableLabels.Contains(result))
                {
                    StatusText = $"标签 '{result}' 已存在";
                    NotificationToast.ShowWarning($"标签 '{result}' 已存在");
                }
                else
                {
                    // 添加到项目和UI
                    CurrentProject.Labels.Add(result);
                    AvailableLabels.Add(result);

                    // 设置为当前标签
                    CurrentLabel = result;
                    CurrentLabelIndex = AvailableLabels.Count - 1;
                    UpdateToolsWithCurrentLabel();
                    
                    // 更新AI模型标签
                    _aiModelManager.SetProjectLabels(CurrentProject.Labels.ToList());

                    StatusText = $"已添加新标签: {result}";
                    NotificationToast.ShowSuccess($"已添加新标签: {result}");

                    // 更新删除命令状态
                    (DeleteCurrentLabelCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"添加标签失败: {ex.Message}";
        }
    }

    partial void OnCurrentLabelChanged(string value)
    {
        UpdateToolsWithCurrentLabel();

        // 如果是通过ComboBox选择变化的，显示通知
        if (!string.IsNullOrEmpty(value) && AvailableLabels.Contains(value))
        {
            var index = AvailableLabels.IndexOf(value);
            if (index != CurrentLabelIndex)
            {
                CurrentLabelIndex = index;
                NotificationToast.ShowInfo($"选择标签: {value} ({index + 1}/{AvailableLabels.Count})");
            }
        }
    }

    /// <summary>
    /// 更新所有工具的当前标签和颜色
    /// </summary>
    private void UpdateToolsWithCurrentLabel()
    {
        if (!string.IsNullOrEmpty(CurrentLabel))
        {
            var color = Services.LabelColorService.GetColorForLabel(CurrentLabel);
            _toolManager.UpdateCurrentLabelAndColor(CurrentLabel, color);
        }
    }

    // 图像导航方法
    public void NextImage()
    {
        if (CurrentProject != null && CurrentProject.Images.Count > 0)
        {
            CurrentImageIndex = (CurrentImageIndex + 1) % CurrentProject.Images.Count;
            StatusText = $"图像 {CurrentImageIndex + 1}/{CurrentProject.Images.Count}";
        }
    }
    
    public void PreviousImage()
    {
        if (CurrentProject != null && CurrentProject.Images.Count > 0)
        {
            CurrentImageIndex = (CurrentImageIndex - 1 + CurrentProject.Images.Count) % CurrentProject.Images.Count;
            StatusText = $"图像 {CurrentImageIndex + 1}/{CurrentProject.Images.Count}";
        }
    }
    
    private void LoadImageByIndex(int index)
    {
        Console.WriteLine($"LoadImageByIndex 被调用，索引: {index}");
        
        if (CurrentProject == null)
        {
            Console.WriteLine("LoadImageByIndex: CurrentProject 为 null");
            return;
        }
        
        if (index < 0 || index >= CurrentProject.Images.Count)
        {
            Console.WriteLine($"LoadImageByIndex: 索引超出范围。索引: {index}, 图像数量: {CurrentProject.Images.Count}");
            return;
        }
        
        var image = CurrentProject.Images[index];
        Console.WriteLine($"LoadImageByIndex: 尝试加载图像: {image.FilePath}");
        
        try
        {
            if (!File.Exists(image.FilePath))
            {
                Console.WriteLine($"LoadImageByIndex: 图像文件不存在: {image.FilePath}");
                StatusText = $"图像文件不存在: {Path.GetFileName(image.FilePath)}";
                return;
            }
            
            using var stream = File.OpenRead(image.FilePath);
            CurrentImageBitmap = new Bitmap(stream);
            CurrentImage = image;
            HasImage = true;

            Console.WriteLine($"LoadImageByIndex: 成功加载图像，标注数量: {image.Annotations.Count}");

            // 加载该图像的标注
            RefreshCurrentImageAnnotations();
            
            // 切换图像时清空撤销历史
            _undoRedoService.Clear();

            // 自动适应窗口 - 延迟一点确保图像已完全加载
            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    FitToWindowRequested?.Invoke();
                });
            });

            StatusText = $"已加载图像: {image.FileName} ({index + 1}/{CurrentProject.Images.Count})";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadImageByIndex: 加载图像异常: {ex}");
            StatusText = $"加载图像失败: {ex.Message}";
        }
    }
    
    private async Task<string> ShowImportChoiceDialog()
    {
        // 简单的选择对话框，实际应用中可以创建专门的对话框
        // 这里使用消息框模拟选择
        try
        {
            var choice = await ShowChoiceDialogAsync("选择导入方式", 
                "请选择图像导入方式：", 
                "导入文件夹", 
                "选择文件");
            return choice ? "folder" : "files";
        }
        catch
        {
            return "files"; // 默认选择文件
        }
    }
    
    private Task<bool> ShowChoiceDialogAsync(string title, string message, string option1, string option2)
    {
        // 这是一个简化实现，实际应用中应该创建自定义对话框
        // 目前返回 true 表示选择第一个选项（文件夹），false 表示第二个选项（文件）
        return Task.FromResult(true); // 默认选择文件夹
    }

    // 标注管理方法
    private void DeleteSelectedAnnotation()
    {
        if (SelectedAnnotation != null)
        {
            RemoveAnnotationWithUndo(SelectedAnnotation);
        }
    }

    private void ClearAllAnnotations()
    {
        if (CurrentImage == null) return;

        var command = new ClearAllAnnotationsCommand(this, CurrentImage);
        _undoRedoService.ExecuteCommand(command);
        
        var annotationsCount = CurrentImage.Annotations.Count;
        StatusText = $"已清空所有标注 ({annotationsCount} 个)";
    }

    private async Task<bool> ExportYoloWithOptionsAsync(AnnotationProject project, string outputPath)
    {
        try
        {
            // 获取项目标注统计信息
            var annotationTypes = project.GetUsedAnnotationTypes();
            var typeStatistics = project.GetTypeStatistics();

            // 创建YOLO导出对话框
            var yoloViewModel = new YoloExportDialogViewModel(annotationTypes, typeStatistics)
            {
                OutputPath = outputPath
            };

            var yoloDialog = new Views.YoloExportDialog(yoloViewModel);

            // 获取父窗口
            var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var result = await yoloDialog.ShowDialog<bool?>(parentWindow!);

            if (result == true)
            {
                // 使用用户选择的设置导出
                return await ExportService.ExportToYoloAsync(
                    project,
                    yoloViewModel.OutputPath,
                    yoloViewModel.SelectedFormat);
            }

            return false;
        }
        catch (Exception ex)
        {
            StatusText = $"YOLO导出失败: {ex.Message}";
            return false;
        }
    }

    // 主题切换方法
    private void ToggleTheme()
    {
        try
        {
            ThemeService.Instance.ToggleTheme();
            var currentTheme = ThemeService.Instance.CurrentTheme;
            var themeName = ThemeService.Instance.GetThemeDisplayName(currentTheme);
            StatusText = $"已切换到{themeName}";
        }
        catch (Exception ex)
        {
            StatusText = $"主题切换失败: {ex.Message}";
        }
    }

    #region AI标注控制方法

    /// <summary>
    /// 开始AI标注 - 先弹出简单的置信度设置对话框，然后执行对应的推理模式
    /// </summary>
    private async Task StartAIAnnotationAsync()
    {
        if (!_aiModelManager.HasActiveModel)
        {
            StatusText = "请先配置AI模型";
            return;
        }

        if (CurrentProject?.Images == null || !CurrentProject.Images.Any())
        {
            StatusText = "没有可标注的图片";
            return;
        }

        try
        {
            // 弹出简单的置信度设置对话框
            var confidenceThreshold = await ShowConfidenceSettingDialogAsync();
            
            if (confidenceThreshold == null)
            {
                StatusText = "AI标注已取消";
                return;
            }

            // 用户确认了设置，现在开始实际的推理流程
            IsAnnotationRunning = true;
            IsAnnotationPaused = false;
            IsAnnotationInProgress = true;
            AnnotationProgress = 0;
            ProcessedCount = 0;
            TotalCount = CurrentProject.Images.Count;
            HasDetailedProgress = true;
            _annotationCancellationTokenSource = new CancellationTokenSource();

            StatusText = "开始AI标注...";

            // 根据标注模式选择处理方式，传递置信度阈值
            if (CurrentAnnotationMode == AnnotationMode.Fast)
            {
                await RunFastAnnotationAsync(_annotationCancellationTokenSource.Token, (float)confidenceThreshold.Value);
            }
            else
            {
                // 预览模式：支持暂停的自动推理未标注图片功能
                await RunPreviewAnnotationAsync(_annotationCancellationTokenSource.Token, (float)confidenceThreshold.Value);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "AI标注已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"AI标注失败: {ex.Message}";
        }
        finally
        {
            IsAnnotationRunning = false;
            IsAnnotationPaused = false;
            IsAnnotationInProgress = false;
            AnnotationProgress = 0;
            AnnotationProgressText = "";
            HasDetailedProgress = false;
            ProcessedCount = 0;
            TotalCount = 0;
            _annotationCancellationTokenSource?.Dispose();
            _annotationCancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 显示美观的置信度设置对话框
    /// </summary>
    private async Task<double?> ShowConfidenceSettingDialogAsync()
    {
        try
        {
            // 创建美观的置信度设置对话框
            var dialog = new ConfidenceSettingDialog();
            dialog.SetInitialConfidence(0.5f); // 设置默认值

            // 获取父窗口
            var parentWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            // 显示对话框
            await dialog.ShowDialog(parentWindow!);

            // 如果用户确认，返回置信度值
            if (dialog.IsConfirmed)
            {
                return dialog.ConfidenceThreshold;
            }

            return null; // 用户取消
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示置信度设置对话框失败: {ex.Message}");
            return 0.5; // 返回默认值
        }
    }

    /// <summary>
    /// 暂停/继续标注
    /// </summary>
    private void PauseResumeAnnotation()
    {
        if (!IsAnnotationRunning) return;

        IsAnnotationPaused = !IsAnnotationPaused;
        StatusText = IsAnnotationPaused ? "AI标注已暂停" : "AI标注已继续";
    }

    /// <summary>
    /// 停止标注
    /// </summary>
    private void StopAnnotation()
    {
        if (!IsAnnotationRunning) return;

        _annotationCancellationTokenSource?.Cancel();
        StatusText = "正在停止AI标注...";
    }

    /// <summary>
    /// 极速标注模式
    /// </summary>
    private async Task RunFastAnnotationAsync(CancellationToken cancellationToken, float confidenceThreshold = 0.5f)
    {
        var images = CurrentProject!.Images.ToList();
        var totalImages = images.Count;

        for (int i = 0; i < totalImages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 等待暂停状态
            while (IsAnnotationPaused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            var image = images[i];
            AnnotationProgressText = $"正在处理: {Path.GetFileName(image.FilePath)} ({i + 1}/{totalImages})";
            AnnotationProgress = (double)(i + 1) / totalImages * 100;
            ProcessedCount = i + 1;

            try
            {
                // 加载图片并运行推理，传递置信度阈值
                var annotations = await _aiModelManager.InferImageAsync(image.FilePath, confidenceThreshold);

                if (annotations?.Any() == true)
                {
                    // 直接应用标注结果
                    image.Annotations.Clear();
                    foreach (var annotation in annotations)
                    {
                        image.Annotations.Add(annotation);
                    }

                    // 如果是当前图片，刷新显示
                    if (image == CurrentImage)
                    {
                        RefreshCurrentImageAnnotations();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理图片 {image.FilePath} 时出错: {ex.Message}");
            }

            // 短暂延迟，避免UI卡顿
            await Task.Delay(50, cancellationToken);
        }

        StatusText = $"极速标注完成，处理了 {totalImages} 张图片";
    }





    /// <summary>
    /// 预览标注模式 - 使用进度对话框显示推理过程
    /// </summary>
    private async Task RunPreviewAnnotationAsync(CancellationToken cancellationToken, float confidenceThreshold = 0.5f)
    {
        try
        {
            if (!_aiModelManager.HasActiveModel)
            {
                StatusText = "❌ 请先加载AI模型";
                return;
            }

            if (CurrentProject == null)
            {
                StatusText = "❌ 请先创建或打开项目";
                return;
            }

            // 获取所有未标注的图像
            var unannotatedImages = CurrentProject.Images
                .Where(img => img.Annotations.Count == 0)
                .ToList();

            if (unannotatedImages.Count == 0)
            {
                StatusText = "✅ 所有图像都已标注，无需AI推理";
                return;
            }

            // 创建进度对话框
            var progressViewModel = new AIInferenceProgressDialogViewModel();
            var progressDialog = new Views.AIInferenceProgressDialog(progressViewModel);

            // 获取父窗口
            var parentWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            // 开始进度跟踪
            progressViewModel.StartProgress(unannotatedImages.Count, _annotationCancellationTokenSource!);

            // 显示非模态对话框，这样用户可以操作主界面的控制按钮
            progressDialog.Show(parentWindow!);

            // 执行推理任务
            var inferenceTask = Task.Run(async () =>
            {
                var processedCount = 0;
                var totalAnnotations = 0;

                try
                {
                    for (int i = 0; i < unannotatedImages.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 等待暂停状态
                        while (IsAnnotationPaused && !cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(100, cancellationToken);
                        }

                        // 更新暂停状态到进度对话框
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            progressViewModel.SetPaused(IsAnnotationPaused);
                        });

                        var image = unannotatedImages[i];

                        // 更新进度对话框和主界面进度
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            progressViewModel.UpdateProgress(processedCount, image.FileName);
                            // 同步更新主界面进度
                            AnnotationProgressText = $"正在处理: {image.FileName} ({processedCount + 1}/{unannotatedImages.Count})";
                            AnnotationProgress = (double)(processedCount + 1) / unannotatedImages.Count * 100;
                            ProcessedCount = processedCount + 1;
                            TotalCount = unannotatedImages.Count;
                        });

                        try
                        {
                            // 切换到当前正在处理的图像，让用户能看到预览
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var imageIndex = CurrentProject.Images.IndexOf(image);
                                if (imageIndex >= 0)
                                {
                                    CurrentImageIndex = imageIndex;
                                }
                            });

                            // 等待图像加载完成
                            await Task.Delay(200, cancellationToken);

                            // 对单张图像进行推理，使用用户设置的置信度阈值
                            var annotations = await _aiModelManager.InferImageAsync(image.FilePath, confidenceThreshold);

                            // 添加推理结果到图像
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                foreach (var annotation in annotations)
                                {
                                    image.AddAnnotation(annotation);
                                    totalAnnotations++;
                                }

                                // 刷新当前图像的标注显示
                                RefreshCurrentImageAnnotations();
                            });

                            processedCount++;

                            // 更新进度
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                progressViewModel.UpdateProgress(processedCount, image.FileName, annotations.Count());
                                // 同步更新主界面进度
                                AnnotationProgressText = $"已完成: {image.FileName} ({processedCount}/{unannotatedImages.Count})";
                                AnnotationProgress = (double)processedCount / unannotatedImages.Count * 100;
                                ProcessedCount = processedCount;
                            });

                            // 添加延迟，让用户能看到结果
                            await Task.Delay(800, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // 重新抛出取消异常
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"推理图像 {image.FileName} 失败: {ex.Message}");
                        }
                    }

                    // 完成进度
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progressViewModel.CompleteProgress(true);
                        StatusText = $"✅ 预览标注完成！已处理 {processedCount} 张图像，检测到 {totalAnnotations} 个对象";
                        // 2秒后自动关闭对话框
                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                progressDialog.Close();
                            });
                        });
                    });
                }
                catch (OperationCanceledException)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progressViewModel.SetCancelled();
                        StatusText = "预览标注已取消";
                        // 关闭进度对话框
                        progressDialog.Close();
                    });
                }
                catch (Exception ex)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progressViewModel.CompleteProgress(false);
                        StatusText = $"预览标注失败: {ex.Message}";
                        // 3秒后自动关闭对话框
                        Task.Delay(3000).ContinueWith(_ =>
                        {
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                progressDialog.Close();
                            });
                        });
                    });
                }
            });

            // 等待推理任务完成
            await inferenceTask;
        }
        finally
        {
            IsAnnotationRunning = false;
            IsAnnotationPaused = false;
            AnnotationProgress = 0;
            AnnotationProgressText = "";
        }
    }

    
    private async Task OpenAIChatAsync()
    {
        try
        {
            Console.WriteLine("[DEBUG MAIN] Switching to AI Chat view");
            
            // 创建或获取AI聊天ViewModel
            if (AiChatViewModel == null)
            {
                AiChatViewModel = new AIChatViewModel(new AIChatService(), _fileDialogService);
                Console.WriteLine("[DEBUG MAIN] Created new AIChatViewModel");
            }
            
            // 切换到AI聊天视图
            IsAIChatViewActive = true;
            StatusText = "AI聊天模式 - 点击'返回标注'返回项目视图";
            
            Console.WriteLine("[DEBUG MAIN] Switched to AI Chat view successfully");
        }
        catch (Exception ex)
        {
            StatusText = $"切换AI聊天失败: {ex.Message}";
            Console.WriteLine($"[ERROR MAIN] Failed to switch to AI Chat: {ex.Message}");
        }
        
        await Task.CompletedTask; // 消除async warning
    }
    
    private void BackToAnnotation()
    {
        Console.WriteLine("[DEBUG MAIN] Switching back to annotation view");
        IsAIChatViewActive = false;
        StatusText = HasProject ? "项目已打开 - 开始标注" : "欢迎使用AIlable - 创建或打开项目开始标注";
        Console.WriteLine("[DEBUG MAIN] Returned to annotation view successfully");
    }

    #endregion
    
    #region 摄像头功能
    
    /// <summary>
    /// 启动摄像头
    /// </summary>
    private async Task StartCameraAsync()
    {
        if (CameraService == null)
        {
            StatusText = "摄像头服务未初始化";
            return;
        }

        try
        {
            IsCameraLoading = true;
            CameraStatus = "正在检查摄像头设备...";
            StatusText = "正在检查摄像头设备...";

            // 首先检查可用的摄像头设备
            var availableCameras = await CameraService.GetAvailableCamerasAsync();
            if (availableCameras.Count == 0)
            {
                CameraStatus = "未检测到摄像头设备";
                StatusText = "未检测到可用的摄像头设备，请检查设备连接和权限";
                await ShowCameraErrorDialog("摄像头设备未找到", 
                    "请检查：\n1. 摄像头是否正确连接\n2. 是否有其他应用程序正在使用摄像头\n3. 应用是否有访问摄像头的权限");
                return;
            }

            CameraStatus = $"正在初始化摄像头 ({availableCameras.Count} 个设备可用)...";
            StatusText = "正在初始化摄像头...";

            // 初始化摄像头
            var initSuccess = await CameraService.InitializeCameraAsync();
            if (!initSuccess)
            {
                CameraStatus = "摄像头初始化失败";
                StatusText = "摄像头初始化失败";
                await ShowCameraErrorDialog("摄像头初始化失败", 
                    "可能原因：\n1. 摄像头正被其他应用使用\n2. 没有访问摄像头的权限\n3. 摄像头驱动问题\n\n请关闭其他可能使用摄像头的应用程序后重试。");
                return;
            }

            // 添加测试事件订阅
            CameraService.FrameAvailable += OnTestFrameAvailable;
            
            CameraStatus = "正在启动摄像头预览...";
            StatusText = "正在启动摄像头预览...";

            // 开始预览
            var startSuccess = await CameraService.StartPreviewAsync();
            if (startSuccess)
            {
                IsCameraActive = true;
                CameraStatus = "摄像头运行中 - 按空格键捕获";
                StatusText = "摄像头已启动，按空格键或点击捕获按钮拍照";
                
                Console.WriteLine("摄像头成功启动，等待1秒后通知预览控件...");
                
                // 等待一段时间让摄像头稳定
                await Task.Delay(1000);
                
                // 通知摄像头预览控件开始预览
                CameraPreviewStartRequested?.Invoke();
                
                // 显示成功提示
                await ShowCameraSuccessMessage("摄像头启动成功！您可以开始拍照了。");
            }
            else
            {
                CameraStatus = "启动摄像头预览失败";
                StatusText = "启动摄像头预览失败";
                await ShowCameraErrorDialog("摄像头预览失败", 
                    "无法启动摄像头预览，请检查摄像头设备状态。");
            }
        }
        catch (Exception ex)
        {
            CameraStatus = $"摄像头启动失败: {ex.Message}";
            StatusText = $"摄像头启动失败: {ex.Message}";
            Console.WriteLine($"摄像头启动异常: {ex}");
            await ShowCameraErrorDialog("摄像头启动异常", 
                $"发生未预期的错误：{ex.Message}\n\n请检查系统日志获取更多信息。");
        }
        finally
        {
            IsCameraLoading = false;
        }
    }

    /// <summary>
    /// 停止摄像头
    /// </summary>
    private async Task StopCameraAsync()
    {
        if (CameraService == null || !IsCameraActive)
            return;

        try
        {
            await CameraService.StopPreviewAsync();
            IsCameraActive = false;
            CameraStatus = "摄像头已停止";
            StatusText = "摄像头已停止";
        }
        catch (Exception ex)
        {
            CameraStatus = $"停止摄像头失败: {ex.Message}";
            StatusText = $"停止摄像头失败: {ex.Message}";
            Console.WriteLine($"停止摄像头异常: {ex}");
        }
    }

    /// <summary>
    /// 捕获摄像头图像并添加到项目
    /// </summary>
    private async Task CaptureCameraImageAsync()
    {
        if (CameraService == null || !IsCameraActive)
        {
            StatusText = "摄像头未启动";
            return;
        }

        try
        {
            // 确保有项目
            if (CurrentProject == null)
            {
                CurrentProject = new AnnotationProject
                {
                    Name = "摄像头项目",
                    Description = "通过摄像头捕获的图像标注项目"
                };
                HasProject = true;
            }

            CameraStatus = "正在捕获图像...";
            StatusText = "正在捕获图像...";

            // 生成唯一的文件名
            CaptureCount++;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"camera_capture_{timestamp}_{CaptureCount:D3}.jpg";
            
            // 确保项目目录存在
            string projectDir;
            if (!string.IsNullOrEmpty(CurrentProject.ProjectPath))
            {
                projectDir = Path.GetDirectoryName(CurrentProject.ProjectPath) ?? Path.GetTempPath();
            }
            else
            {
                projectDir = Path.Combine(Path.GetTempPath(), "AIlable_Camera");
                Directory.CreateDirectory(projectDir);
            }
            
            var outputPath = Path.Combine(projectDir, fileName);

            // 保存捕获的图像
            var saveSuccess = await CameraService.SaveCapturedImageAsync(outputPath);
            if (!saveSuccess)
            {
                CameraStatus = "图像保存失败";
                StatusText = "图像保存失败";
                return;
            }

            // 创建AnnotationImage并添加到项目
            var annotationImage = await ImageService.CreateAnnotationImageAsync(outputPath);
            if (annotationImage != null)
            {
                CurrentProject.AddImage(annotationImage);
                
                // 切换到新捕获的图像
                var newImageIndex = CurrentProject.Images.Count - 1;
                CurrentImageIndex = newImageIndex;
                LoadImageByIndex(newImageIndex);
                
                // 延迟适应窗口
                _ = Task.Delay(150).ContinueWith(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        FitToWindowRequested?.Invoke();
                    });
                });

                CameraStatus = $"图像已捕获 ({CaptureCount})";
                StatusText = $"已成功捕获并保存图像: {fileName}";
                
                Console.WriteLine($"摄像头图像已保存: {outputPath}");
            }
            else
            {
                CameraStatus = "创建图像对象失败";
                StatusText = "创建图像对象失败";
            }
        }
        catch (Exception ex)
        {
            CameraStatus = $"捕获图像失败: {ex.Message}";
            StatusText = $"捕获图像失败: {ex.Message}";
            Console.WriteLine($"捕获摄像头图像异常: {ex}");
        }
    }
    
    /// <summary>
    /// 显示摄像头错误对话框
    /// </summary>
    private async Task ShowCameraErrorDialog(string title, string message)
    {
        try
        {
            // 在实际应用中，这里可以使用Avalonia的MessageBox或自定义对话框
            // 目前使用控制台输出和状态栏显示
            Console.WriteLine($"[摄像头错误] {title}: {message}");
            StatusText = $"❗ {title}";
            
            // 在未来版本中，可以添加弹窗对话框
            // 例如：
            // await MessageBox.Show(title, message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示错误对话框失败: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 显示摄像头成功消息
    /// </summary>
    private async Task ShowCameraSuccessMessage(string message)
    {
        try
        {
            Console.WriteLine($"[摄像头成功] {message}");
            StatusText = $"✅ {message}";
            
            // 在未来版本中，可以添加成功提示动画或通知
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示成功消息失败: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 测试FrameAvailable事件处理
    /// </summary>
    private void OnTestFrameAvailable(object? sender, Avalonia.Media.Imaging.Bitmap bitmap)
    {
        // 确保在UI线程上更新状态
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnTestFrameAvailable(sender, bitmap));
            return;
        }
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] MainViewModel: 收到FrameAvailable事件! 图像尺寸: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
        
        // 更新状态以显示我们收到了帧
        StatusText = $"✅ 摄像头正常工作 - 收到 {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height} 画面";
    }
    
    #endregion
}