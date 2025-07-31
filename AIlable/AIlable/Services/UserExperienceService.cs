using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Services;

/// <summary>
/// 用户体验优化服务，提供统一的状态反馈和进度指示
/// </summary>
public partial class UserExperienceService : ObservableObject
{
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _busyMessage = "";
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private bool _isProgressIndeterminate = false;
    [ObservableProperty] private string _statusMessage = "Ready";
    
    private CancellationTokenSource? _currentOperationCancellation;

    /// <summary>
    /// 操作状态变化事件
    /// </summary>
    public event EventHandler<OperationStatusEventArgs>? OperationStatusChanged;

    /// <summary>
    /// 开始长时间运行的操作
    /// </summary>
    public void StartOperation(string message, bool isIndeterminate = true)
    {
        IsBusy = true;
        BusyMessage = message;
        IsProgressIndeterminate = isIndeterminate;
        Progress = 0;
        StatusMessage = message;
        
        _currentOperationCancellation?.Cancel();
        _currentOperationCancellation = new CancellationTokenSource();
        
        OperationStatusChanged?.Invoke(this, new OperationStatusEventArgs
        {
            IsOperationStarted = true,
            Message = message,
            IsIndeterminate = isIndeterminate
        });
    }

    /// <summary>
    /// 更新操作进度
    /// </summary>
    public void UpdateProgress(double progress, string? message = null)
    {
        Progress = Math.Max(0, Math.Min(100, progress));
        IsProgressIndeterminate = false;
        
        if (!string.IsNullOrEmpty(message))
        {
            BusyMessage = message;
            StatusMessage = message;
        }
        
        OperationStatusChanged?.Invoke(this, new OperationStatusEventArgs
        {
            IsProgressUpdate = true,
            Progress = Progress,
            Message = message ?? BusyMessage
        });
    }

    /// <summary>
    /// 完成操作
    /// </summary>
    public void CompleteOperation(string? completionMessage = null)
    {
        IsBusy = false;
        BusyMessage = "";
        IsProgressIndeterminate = false;
        Progress = 100;
        
        var message = completionMessage ?? "操作完成";
        StatusMessage = message;
        
        _currentOperationCancellation?.Cancel();
        _currentOperationCancellation = null;
        
        OperationStatusChanged?.Invoke(this, new OperationStatusEventArgs
        {
            IsOperationCompleted = true,
            Message = message
        });

        // 延迟重置状态消息
        Task.Delay(3000).ContinueWith(_ =>
        {
            if (!IsBusy)
            {
                StatusMessage = "Ready";
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// 取消当前操作
    /// </summary>
    public void CancelOperation()
    {
        _currentOperationCancellation?.Cancel();
        IsBusy = false;
        BusyMessage = "";
        IsProgressIndeterminate = false;
        Progress = 0;
        StatusMessage = "操作已取消";
        
        OperationStatusChanged?.Invoke(this, new OperationStatusEventArgs
        {
            IsOperationCancelled = true,
            Message = "操作已取消"
        });
    }

    /// <summary>
    /// 显示临时状态消息
    /// </summary>
    public async Task ShowTemporaryStatusAsync(string message, TimeSpan duration)
    {
        var originalMessage = StatusMessage;
        StatusMessage = message;
        
        await Task.Delay(duration);
        
        if (!IsBusy)
        {
            StatusMessage = originalMessage;
        }
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    public void ShowError(string errorMessage)
    {
        IsBusy = false;
        BusyMessage = "";
        IsProgressIndeterminate = false;
        Progress = 0;
        StatusMessage = $"错误: {errorMessage}";
        
        OperationStatusChanged?.Invoke(this, new OperationStatusEventArgs
        {
            IsError = true,
            Message = errorMessage
        });
    }

    /// <summary>
    /// 获取当前操作的取消令牌
    /// </summary>
    public CancellationToken GetCancellationToken()
    {
        return _currentOperationCancellation?.Token ?? CancellationToken.None;
    }

    /// <summary>
    /// 执行带有自动状态管理的异步操作
    /// </summary>
    public async Task<T> ExecuteWithStatusAsync<T>(
        string operationName,
        Func<IProgress<ProgressInfo>, CancellationToken, Task<T>> operation,
        string? completionMessage = null)
    {
        StartOperation(operationName);
        
        var progress = new Progress<ProgressInfo>(info =>
        {
            UpdateProgress(info.Percentage, info.Message);
        });

        try
        {
            var result = await operation(progress, GetCancellationToken());
            CompleteOperation(completionMessage);
            return result;
        }
        catch (OperationCanceledException)
        {
            CancelOperation();
            throw;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 执行带有自动状态管理的异步操作（无返回值）
    /// </summary>
    public async Task ExecuteWithStatusAsync(
        string operationName,
        Func<IProgress<ProgressInfo>, CancellationToken, Task> operation,
        string? completionMessage = null)
    {
        await ExecuteWithStatusAsync<object?>(operationName, async (progress, token) =>
        {
            await operation(progress, token);
            return null;
        }, completionMessage);
    }
}

/// <summary>
/// 进度信息
/// </summary>
public class ProgressInfo
{
    public double Percentage { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// 操作状态事件参数
/// </summary>
public class OperationStatusEventArgs : EventArgs
{
    public bool IsOperationStarted { get; set; }
    public bool IsOperationCompleted { get; set; }
    public bool IsOperationCancelled { get; set; }
    public bool IsProgressUpdate { get; set; }
    public bool IsError { get; set; }
    public string Message { get; set; } = "";
    public double Progress { get; set; }
    public bool IsIndeterminate { get; set; }
}