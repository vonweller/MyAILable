using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using AIlable.ViewModels;
using AIlable.Controls;

namespace AIlable.Views;

public partial class MainView : UserControl
{
    private ImageCanvas? _imageCanvas;

    public MainView()
    {
        InitializeComponent();
        
        // Get reference to ImageCanvas
        _imageCanvas = this.FindControl<ImageCanvas>("ImageCanvas");
        
        // Subscribe to DataContext changes
        DataContextChanged += OnDataContextChanged;
        
        // 启用键盘输入
        Focusable = true;
        KeyDown += OnKeyDown;
        
        // 添加图像列表双击事件
        var imageListBox = this.FindControl<ListBox>("ImageListBox");
        if (imageListBox != null)
        {
            imageListBox.DoubleTapped += OnImageListBoxDoubleTapped;
        }

        // 添加标注模式RadioButton事件处理
        var fastModeRadio = this.FindControl<RadioButton>("FastModeRadio");
        var previewModeRadio = this.FindControl<RadioButton>("PreviewModeRadio");

        if (fastModeRadio != null)
        {
            fastModeRadio.Checked += OnFastModeChecked;
        }

        if (previewModeRadio != null)
        {
            previewModeRadio.Checked += OnPreviewModeChecked;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            // 延迟初始化文件对话框服务，确保父窗口已经完全加载
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                InitializeFileDialogService(viewModel);
            });

            // Subscribe to view model events
            viewModel.FitToWindowRequested += OnFitToWindowRequested;
            viewModel.ResetViewRequested += OnResetViewRequested;

            // Subscribe to annotation state changes
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Subscribe to ImageCanvas events
            if (_imageCanvas != null)
            {
                _imageCanvas.PointerClickedOnImage += OnPointerClickedOnImage;
                _imageCanvas.PointerMovedOnImage += OnPointerMovedOnImage;
                _imageCanvas.AnnotationSelected += OnAnnotationSelected;
                _imageCanvas.GetOBBToolRequested += OnGetOBBToolRequested;

                // Bind current drawing annotation to canvas
                UpdateCurrentDrawingAnnotation();
                
                // Start timer to continuously update drawing annotation
                var timer = new System.Timers.Timer(16); // ~60 FPS
                timer.Elapsed += (_, _) => 
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(UpdateCurrentDrawingAnnotation);
                };
                timer.Start();
            }
        }
    }

    private void InitializeFileDialogService(MainViewModel viewModel)
    {
        try
        {
            // 尝试多种方式获取父窗口
            Avalonia.Controls.Window? parentWindow = null;
            
            // 方法1: 通过TopLevel获取
            parentWindow = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
            
            // 方法2: 如果失败，通过应用程序主窗口
            if (parentWindow == null)
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    parentWindow = desktop.MainWindow;
                }
            }
            
            // 方法3: 如果还是失败，通过视觉树查找
            if (parentWindow == null)
            {
                var current = this.Parent;
                while (current != null)
                {
                    if (current is Avalonia.Controls.Window window)
                    {
                        parentWindow = window;
                        break;
                    }
                    current = (current as Avalonia.StyledElement)?.Parent;
                }
            }

            if (parentWindow != null)
            {
                var fileDialogService = new AIlable.Services.FileDialogService(parentWindow);
                viewModel.SetFileDialogService(fileDialogService);
            }
            else
            {
                // 如果仍然无法获取父窗口，延迟重试
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => InitializeFileDialogService(viewModel));
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing file dialog service: {ex.Message}");
        }
    }

    private void UpdateCurrentDrawingAnnotation()
    {
        if (DataContext is MainViewModel viewModel && _imageCanvas != null)
        {
            _imageCanvas.CurrentDrawingAnnotation = viewModel.GetCurrentDrawingAnnotation();
        }
    }

    private void OnFitToWindowRequested()
    {
        _imageCanvas?.FitToWindow();
    }

    private void OnResetViewRequested()
    {
        _imageCanvas?.ResetView();
    }

    private void OnPointerClickedOnImage(object? sender, AIlable.Models.Point2D imagePoint)
    {
        // Handle click on image for tool operations
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.HandleCanvasClick(imagePoint);
        }
    }

    private void OnPointerMovedOnImage(object? sender, AIlable.Models.Point2D imagePoint)
    {
        // Handle mouse movement for drawing tools
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.HandleCanvasMouseMove(imagePoint);
        }
    }

    private void OnAnnotationSelected(object? sender, AIlable.Models.Annotation annotation)
    {
        // Handle annotation selection
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectAnnotation(annotation);
        }
    }

    private AIlable.Services.OrientedBoundingBoxTool? OnGetOBBToolRequested()
    {
        // 返回OBB工具实例
        if (DataContext is MainViewModel viewModel)
        {
            return viewModel.ToolManager.GetTool(AIlable.Models.AnnotationTool.OrientedBoundingBox) as AIlable.Services.OrientedBoundingBoxTool;
        }
        return null;
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        // 处理Ctrl+快捷键
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.S:
                    // Ctrl+S 保存项目
                    viewModel.SaveProjectCommand?.Execute(null);
                    e.Handled = true;
                    break;
                case Key.O:
                    // Ctrl+O 打开项目
                    viewModel.OpenProjectCommand?.Execute(null);
                    e.Handled = true;
                    break;
                case Key.N:
                    // Ctrl+N 新建项目
                    viewModel.CreateNewProjectCommand?.Execute(null);
                    e.Handled = true;
                    break;
            }
            return;
        }

        // 处理普通键盘快捷键（只有在没有按Ctrl时）
        switch (e.Key)
        {
            case Key.W:
                // 上一张图像
                viewModel.PreviousImage();
                e.Handled = true;
                break;

            case Key.S:
                // 下一张图像
                viewModel.NextImage();
                e.Handled = true;
                break;

            case Key.A:
                // 上一个标签
                viewModel.PreviousLabel();
                e.Handled = true;
                break;

            case Key.D:
                // 下一个标签
                viewModel.NextLabel();
                e.Handled = true;
                break;

            case Key.Escape:
                // ESC键取消当前标注
                viewModel.CancelCurrentDrawing();
                e.Handled = true;
                break;

            case Key.Delete:
                // Delete键删除选中的标注
                if (viewModel.SelectedAnnotation != null)
                {
                    viewModel.RemoveAnnotation(viewModel.SelectedAnnotation);
                    e.Handled = true;
                }
                break;
        }
    }
    
    private void OnImageListBoxDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            if (listBox.SelectedItem is AIlable.Models.AnnotationImage selectedImage)
            {
                // 通过图像找到索引并加载
                var index = viewModel.CurrentProject?.Images.IndexOf(selectedImage);
                if (index.HasValue && index >= 0)
                {
                    viewModel.CurrentImageIndex = index.Value;
                    // LoadImageByIndex会通过属性绑定自动调用
                }
            }
        }
    }

    // 面板切换事件处理
    private void OnToolsClick(object? sender, RoutedEventArgs e)
    {
        TogglePanel("ToolsPanel");
    }

    private void OnLabelsClick(object? sender, RoutedEventArgs e)
    {
        TogglePanel("LabelsPanel");
    }

    private void OnAIClick(object? sender, RoutedEventArgs e)
    {
        TogglePanel("AIPanel");
    }

    private void TogglePanel(string panelName)
    {
        var targetPanel = this.FindControl<Border>(panelName);
        if (targetPanel != null)
        {
            // 简单切换显示/隐藏，不影响其他面板
            targetPanel.IsVisible = !targetPanel.IsVisible;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        // 根据属性变化更新UI状态
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsAnnotationRunning):
                UpdateAnnotationControlsState(viewModel);
                break;
            case nameof(MainViewModel.IsAnnotationPaused):
                UpdatePauseResumeButton(viewModel);
                break;
            case nameof(MainViewModel.AnnotationProgress):
                UpdateProgressDisplay(viewModel);
                break;
        }
    }

    private void UpdateAnnotationControlsState(MainViewModel viewModel)
    {
        var startButton = this.FindControl<Button>("StartAnnotationButton");
        var pauseButton = this.FindControl<Button>("PauseResumeButton");
        var stopButton = this.FindControl<Button>("StopAnnotationButton");
        var progressPanel = this.FindControl<StackPanel>("ProgressPanel");

        if (startButton != null)
        {
            startButton.IsEnabled = !viewModel.IsAnnotationRunning;
            startButton.Content = viewModel.IsAnnotationRunning ? "🔄 标注中..." : "▶️ 开始标注";
        }

        if (pauseButton != null)
        {
            pauseButton.IsEnabled = viewModel.IsAnnotationRunning;
        }

        if (stopButton != null)
        {
            stopButton.IsEnabled = viewModel.IsAnnotationRunning;
        }

        if (progressPanel != null)
        {
            progressPanel.IsVisible = viewModel.IsAnnotationRunning;
        }
    }

    private void UpdatePauseResumeButton(MainViewModel viewModel)
    {
        var pauseButton = this.FindControl<Button>("PauseResumeButton");
        if (pauseButton != null)
        {
            pauseButton.Content = viewModel.IsAnnotationPaused ? "▶️ 继续" : "⏸️ 暂停";
        }
    }

    private void UpdateProgressDisplay(MainViewModel viewModel)
    {
        var progressBar = this.FindControl<ProgressBar>("AnnotationProgress");
        if (progressBar != null)
        {
            progressBar.Value = viewModel.AnnotationProgress;
        }
    }

    // 标注模式RadioButton事件处理
    private void OnFastModeChecked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CurrentAnnotationMode = MainViewModel.AnnotationMode.Fast;
        }
    }

    private void OnPreviewModeChecked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CurrentAnnotationMode = MainViewModel.AnnotationMode.Preview;
        }
    }
}