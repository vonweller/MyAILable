using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using AIlable.Models;
using AIlable.Services;

namespace AIlable.ViewModels;

/// <summary>
/// 增强的AI推理进度对话框ViewModel，支持多线程推理监控
/// </summary>
public partial class EnhancedAIInferenceProgressDialogViewModel : ViewModelBase
{
    private readonly MultiThreadAIModelManager _aiModelManager;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<string> _imagePaths;
    private readonly float _confidenceThreshold;
    
    [ObservableProperty]
    private string _title = "AI推理进度";
    
    [ObservableProperty]
    private string _statusText = "准备开始推理...";
    
    [ObservableProperty]
    private double _progressValue = 0;
    
    [ObservableProperty]
    private string _progressText = "0%";
    
    [ObservableProperty]
    private string _elapsedTimeText = "00:00:00";
    
    [ObservableProperty]
    private string _remainingTimeText = "计算中...";
    
    [ObservableProperty]
    private string _currentImageText = "";
    
    [ObservableProperty]
    private string _speedText = "0 图片/秒";
    
    [ObservableProperty]
    private string _memoryUsageText = "0 MB";
    
    [ObservableProperty]
    private string _successRateText = "100%";
    
    [ObservableProperty]
    private bool _isRunning = false;
    
    [ObservableProperty]
    private bool _canCancel = true;
    
    [ObservableProperty]
    private bool _showDetails = false;
    
    [ObservableProperty]
    private string _detailsButtonText = "显示详情";
    
    // 详细统计信息
    [ObservableProperty]
    private int _totalTasks = 0;
    
    [ObservableProperty]
    private int _completedTasks = 0;
    
    [ObservableProperty]
    private int _failedTasks = 0;
    
    [ObservableProperty]
    private int _successfulTasks = 0;
    
    [ObservableProperty]
    private double _averageProcessingTime = 0;
    
    // 实时日志
    public ObservableCollection<string> LogMessages { get; } = new();
    
    // 推理结果
    public Dictionary<string, IEnumerable<Annotation>> Results { get; private set; } = new();
    
    // 任务完成事件
    public event EventHandler<Dictionary<string, IEnumerable<Annotation>>>? InferenceCompleted;
    public event EventHandler? InferenceCancelled;
    
    public ICommand CancelCommand { get; }
    public ICommand ToggleDetailsCommand { get; }
    public ICommand ClearLogCommand { get; }
    
    public EnhancedAIInferenceProgressDialogViewModel(
        MultiThreadAIModelManager aiModelManager,
        IEnumerable<string> imagePaths,
        float confidenceThreshold = 0.5f)
    {
        _aiModelManager = aiModelManager;
        _imagePaths = imagePaths.ToList();
        _confidenceThreshold = confidenceThreshold;
        
        TotalTasks = _imagePaths.Count;
        
        CancelCommand = new RelayCommand(CancelInference, () => CanCancel);
        ToggleDetailsCommand = new RelayCommand(ToggleDetails);
        ClearLogCommand = new RelayCommand(() => LogMessages.Clear());
        
        // 初始化显示
        UpdateProgressDisplay(new InferenceProgress
        {
            TotalTasks = TotalTasks,
            CompletedTasks = 0,
            FailedTasks = 0
        });
    }
    
    /// <summary>
    /// 开始推理任务
    /// </summary>
    public async Task StartInferenceAsync()
    {
        if (IsRunning || !_aiModelManager.HasActiveModel)
            return;
        
        IsRunning = true;
        CanCancel = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        AddLogMessage("开始AI推理任务...");
        AddLogMessage($"模型信息: {_aiModelManager.GetModelInfo()}");
        AddLogMessage($"图片数量: {TotalTasks}");
        AddLogMessage($"置信度阈值: {_confidenceThreshold:F2}");
        AddLogMessage($"多线程支持: {(_aiModelManager.IsMultiThreadSupported() ? "是" : "否")}");
        
        try
        {
            var progress = new Progress<InferenceProgress>(UpdateProgressDisplay);
            
            Results = await _aiModelManager.InferBatchAdvancedAsync(
                _imagePaths,
                _confidenceThreshold,
                progress,
                _cancellationTokenSource.Token);
            
            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddLogMessage("推理任务已取消");
                StatusText = "任务已取消";
                InferenceCancelled?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                AddLogMessage("推理任务完成");
                StatusText = "推理完成";
                InferenceCompleted?.Invoke(this, Results);
            }
        }
        catch (OperationCanceledException)
        {
            AddLogMessage("推理任务被用户取消");
            StatusText = "任务已取消";
            InferenceCancelled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AddLogMessage($"推理任务出错: {ex.Message}");
            StatusText = "推理出错";
            Console.WriteLine($"Inference error: {ex}");
        }
        finally
        {
            IsRunning = false;
            CanCancel = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
    
    /// <summary>
    /// 取消推理任务
    /// </summary>
    private void CancelInference()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            AddLogMessage("正在取消推理任务...");
            _cancellationTokenSource.Cancel();
            CanCancel = false;
        }
    }
    
    /// <summary>
    /// 切换详情显示
    /// </summary>
    private void ToggleDetails()
    {
        ShowDetails = !ShowDetails;
        DetailsButtonText = ShowDetails ? "隐藏详情" : "显示详情";
    }
    
    /// <summary>
    /// 更新进度显示
    /// </summary>
    private void UpdateProgressDisplay(InferenceProgress progress)
    {
        // 基本进度信息
        ProgressValue = progress.ProgressPercentage;
        ProgressText = $"{progress.ProgressPercentage:F1}%";
        
        CompletedTasks = progress.CompletedTasks;
        FailedTasks = progress.FailedTasks;
        SuccessfulTasks = CompletedTasks - FailedTasks;
        
        // 时间信息
        ElapsedTimeText = FormatTimeSpan(progress.ElapsedTime);
        RemainingTimeText = progress.FormattedRemainingTime;
        
        // 当前处理的图片
        CurrentImageText = string.IsNullOrEmpty(progress.CurrentImagePath) 
            ? "等待中..." 
            : $"正在处理: {progress.CurrentImagePath}";
        
        // 性能统计
        SpeedText = $"{progress.ProcessingSpeed:F1} 图片/秒";
        MemoryUsageText = progress.FormattedMemoryUsage;
        SuccessRateText = $"{progress.SuccessRate:F1}%";
        AverageProcessingTime = progress.AverageProcessingTime;
        
        // 状态文本
        if (progress.CompletedTasks == 0)
        {
            StatusText = "准备开始推理...";
        }
        else if (progress.CompletedTasks < progress.TotalTasks)
        {
            StatusText = $"正在推理 ({progress.CompletedTasks}/{progress.TotalTasks})";
        }
        else
        {
            StatusText = "推理完成";
        }
        
        // 添加进度日志
        if (progress.CompletedTasks > 0 && progress.CompletedTasks % 10 == 0)
        {
            AddLogMessage($"已完成 {progress.CompletedTasks}/{progress.TotalTasks} 张图片");
        }
    }
    
    /// <summary>
    /// 添加日志消息
    /// </summary>
    private void AddLogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        // 在UI线程上添加日志
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogMessages.Add(logEntry);
            
            // 限制日志数量，避免内存过多占用
            while (LogMessages.Count > 1000)
            {
                LogMessages.RemoveAt(0);
            }
        });
    }
    
    /// <summary>
    /// 格式化时间跨度
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
            return timeSpan.ToString(@"hh\:mm\:ss");
        else
            return timeSpan.ToString(@"mm\:ss");
    }
    
    /// <summary>
    /// 获取推理摘要信息
    /// </summary>
    public string GetInferenceSummary()
    {
        if (Results.Count == 0)
            return "没有推理结果";
        
        var totalAnnotations = Results.Values.Sum(annotations => annotations.Count());
        var successfulImages = Results.Count(kvp => kvp.Value.Any());
        
        return $"推理完成: {Results.Count} 张图片, {totalAnnotations} 个检测结果, " +
               $"成功率: {(double)successfulImages / Results.Count * 100:F1}%";
    }
    
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        // 当CanCancel属性改变时，更新命令的可执行状态
        if (e.PropertyName == nameof(CanCancel))
        {
            ((RelayCommand)CancelCommand).NotifyCanExecuteChanged();
        }
    }
}