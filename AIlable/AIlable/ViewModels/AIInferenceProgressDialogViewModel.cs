using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIlable.ViewModels;

public partial class AIInferenceProgressDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "AI推理进度";
    [ObservableProperty] private string _currentTask = "准备开始...";
    [ObservableProperty] private string _currentImage = "";
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private string _progressText = "0%";
    [ObservableProperty] private int _processedCount = 0;
    [ObservableProperty] private int _totalCount = 0;
    [ObservableProperty] private int _detectedObjects = 0;
    [ObservableProperty] private bool _isPaused = false;
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private bool _isCompleted = false;
    [ObservableProperty] private bool _isCancelled = false;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _elapsedTime = "00:00";

    private DateTime _startTime;
    private Timer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;

    public AIInferenceProgressDialogViewModel()
    {
        // 移除按钮命令，只显示进度信息
    }

    public bool DialogResult { get; private set; }

    public void StartProgress(int totalCount, CancellationTokenSource cancellationTokenSource)
    {
        TotalCount = totalCount;
        ProcessedCount = 0;
        DetectedObjects = 0;
        Progress = 0;
        IsRunning = true;
        IsCompleted = false;
        IsCancelled = false;
        _cancellationTokenSource = cancellationTokenSource;
        
        _startTime = DateTime.Now;
        _timer = new Timer(UpdateElapsedTime, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        
        StatusMessage = $"开始处理 {totalCount} 张图像...";
        UpdateProgressText();
    }

    public void UpdateProgress(int processedCount, string currentImageName, int newDetectedObjects = 0)
    {
        ProcessedCount = processedCount;
        CurrentImage = currentImageName;
        DetectedObjects += newDetectedObjects;
        
        if (TotalCount > 0)
        {
            Progress = (double)ProcessedCount / TotalCount * 100;
        }
        
        CurrentTask = $"正在处理: {currentImageName}";
        StatusMessage = $"已处理 {ProcessedCount}/{TotalCount} 张图像，检测到 {DetectedObjects} 个对象";
        UpdateProgressText();
        
        // 命令已移除，无需刷新
    }

    public void CompleteProgress(bool success = true)
    {
        IsRunning = false;
        IsCompleted = true;
        Progress = 100;
        
        _timer?.Dispose();
        _timer = null;
        
        if (success)
        {
            CurrentTask = "推理完成！";
            StatusMessage = $"✅ 成功处理 {ProcessedCount} 张图像，检测到 {DetectedObjects} 个对象";
            DialogResult = true;
        }
        else
        {
            CurrentTask = "推理失败";
            StatusMessage = "❌ 推理过程中发生错误";
        }
        
        UpdateProgressText();
        
        // 命令已移除，无需刷新
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        CurrentTask = paused ? "已暂停" : "继续处理...";
    }

    public void SetCancelled()
    {
        IsRunning = false;
        IsCancelled = true;
        CurrentTask = "已取消";
        StatusMessage = "推理过程已取消";

        _timer?.Dispose();
        _timer = null;
    }

    private void UpdateProgressText()
    {
        ProgressText = $"{Progress:F0}%";
    }

    private void UpdateElapsedTime(object? state)
    {
        var elapsed = DateTime.Now - _startTime;
        ElapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    // 事件已移除，进度对话框只显示信息，不处理控制逻辑
}
