using System;

namespace AIlable.Models;

/// <summary>
/// AI推理进度信息
/// </summary>
public class InferenceProgress
{
    /// <summary>
    /// 总任务数
    /// </summary>
    public int TotalTasks { get; set; }
    
    /// <summary>
    /// 已完成任务数
    /// </summary>
    public int CompletedTasks { get; set; }
    
    /// <summary>
    /// 失败任务数
    /// </summary>
    public int FailedTasks { get; set; }
    
    /// <summary>
    /// 进度百分比 (0-100)
    /// </summary>
    public double ProgressPercentage => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0;
    
    /// <summary>
    /// 已用时间
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }
    
    /// <summary>
    /// 预计剩余时间
    /// </summary>
    public TimeSpan EstimatedRemainingTime { get; set; }
    
    /// <summary>
    /// 当前处理的图片路径
    /// </summary>
    public string CurrentImagePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 平均处理时间 (毫秒)
    /// </summary>
    public double AverageProcessingTime { get; set; }
    
    /// <summary>
    /// 当前内存使用量 (字节)
    /// </summary>
    public long MemoryUsage { get; set; }
    
    /// <summary>
    /// 处理速度 (图片/秒)
    /// </summary>
    public double ProcessingSpeed => AverageProcessingTime > 0 ? 1000.0 / AverageProcessingTime : 0;
    
    /// <summary>
    /// 成功率百分比
    /// </summary>
    public double SuccessRate => TotalTasks > 0 ? (double)(CompletedTasks - FailedTasks) / TotalTasks * 100 : 0;
    
    /// <summary>
    /// 格式化的内存使用量显示
    /// </summary>
    public string FormattedMemoryUsage
    {
        get
        {
            if (MemoryUsage < 1024) return $"{MemoryUsage} B";
            if (MemoryUsage < 1024 * 1024) return $"{MemoryUsage / 1024.0:F1} KB";
            if (MemoryUsage < 1024 * 1024 * 1024) return $"{MemoryUsage / (1024.0 * 1024):F1} MB";
            return $"{MemoryUsage / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
    
    /// <summary>
    /// 格式化的剩余时间显示
    /// </summary>
    public string FormattedRemainingTime
    {
        get
        {
            if (EstimatedRemainingTime.TotalSeconds < 60)
                return $"{EstimatedRemainingTime.TotalSeconds:F0}秒";
            if (EstimatedRemainingTime.TotalMinutes < 60)
                return $"{EstimatedRemainingTime.TotalMinutes:F1}分钟";
            return $"{EstimatedRemainingTime.TotalHours:F1}小时";
        }
    }
}