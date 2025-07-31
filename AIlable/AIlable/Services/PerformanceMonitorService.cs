using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AIlable.Services;

/// <summary>
/// 性能监控服务，用于监控应用程序性能指标
/// </summary>
public class PerformanceMonitorService
{
    private readonly Dictionary<string, Stopwatch> _operations = new();
    private readonly List<PerformanceMetric> _metrics = new();

    public event EventHandler<PerformanceMetric>? MetricRecorded;

    /// <summary>
    /// 开始监控操作
    /// </summary>
    public void StartOperation(string operationName)
    {
        if (_operations.ContainsKey(operationName))
        {
            _operations[operationName].Restart();
        }
        else
        {
            _operations[operationName] = Stopwatch.StartNew();
        }
    }

    /// <summary>
    /// 结束监控操作并记录性能指标
    /// </summary>
    public PerformanceMetric StopOperation(string operationName, string? details = null)
    {
        if (_operations.TryGetValue(operationName, out var stopwatch))
        {
            stopwatch.Stop();
            
            var metric = new PerformanceMetric
            {
                OperationName = operationName,
                Duration = stopwatch.Elapsed,
                Timestamp = DateTime.Now,
                Details = details
            };

            _metrics.Add(metric);
            MetricRecorded?.Invoke(this, metric);
            
            return metric;
        }

        return new PerformanceMetric
        {
            OperationName = operationName,
            Duration = TimeSpan.Zero,
            Timestamp = DateTime.Now,
            Details = "Operation not found"
        };
    }

    /// <summary>
    /// 执行操作并自动监控性能
    /// </summary>
    public async Task<T> MonitorAsync<T>(string operationName, Func<Task<T>> operation, string? details = null)
    {
        StartOperation(operationName);
        try
        {
            var result = await operation();
            StopOperation(operationName, details);
            return result;
        }
        catch (Exception ex)
        {
            StopOperation(operationName, $"Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 执行操作并自动监控性能（无返回值）
    /// </summary>
    public async Task MonitorAsync(string operationName, Func<Task> operation, string? details = null)
    {
        StartOperation(operationName);
        try
        {
            await operation();
            StopOperation(operationName, details);
        }
        catch (Exception ex)
        {
            StopOperation(operationName, $"Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取所有性能指标
    /// </summary>
    public IReadOnlyList<PerformanceMetric> GetMetrics()
    {
        return _metrics.AsReadOnly();
    }

    /// <summary>
    /// 获取特定操作的性能指标
    /// </summary>
    public IEnumerable<PerformanceMetric> GetMetrics(string operationName)
    {
        return _metrics.FindAll(m => m.OperationName == operationName);
    }

    /// <summary>
    /// 清除所有性能指标
    /// </summary>
    public void ClearMetrics()
    {
        _metrics.Clear();
    }

    /// <summary>
    /// 获取性能统计摘要
    /// </summary>
    public PerformanceSummary GetSummary()
    {
        var summary = new PerformanceSummary();
        
        foreach (var metric in _metrics)
        {
            if (!summary.OperationStats.ContainsKey(metric.OperationName))
            {
                summary.OperationStats[metric.OperationName] = new OperationStats();
            }

            var stats = summary.OperationStats[metric.OperationName];
            stats.Count++;
            stats.TotalDuration = stats.TotalDuration.Add(metric.Duration);
            
            if (stats.MinDuration == TimeSpan.Zero || metric.Duration < stats.MinDuration)
                stats.MinDuration = metric.Duration;
            
            if (metric.Duration > stats.MaxDuration)
                stats.MaxDuration = metric.Duration;
        }

        // 计算平均值
        foreach (var stats in summary.OperationStats.Values)
        {
            stats.AverageDuration = TimeSpan.FromTicks(stats.TotalDuration.Ticks / stats.Count);
        }

        return summary;
    }
}

/// <summary>
/// 性能指标
/// </summary>
public class PerformanceMetric
{
    public string OperationName { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// 操作统计信息
/// </summary>
public class OperationStats
{
    public int Count { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// 性能摘要
/// </summary>
public class PerformanceSummary
{
    public Dictionary<string, OperationStats> OperationStats { get; } = new();
}