using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace AIlable.Services;

/// <summary>
/// 通知项
/// </summary>
public class NotificationItem : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private string _message = string.Empty;
    private NotificationType _type = NotificationType.Info;

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged();
        }
    }

    public NotificationType Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 通知服务，用于显示临时提示信息
/// </summary>
public class NotificationService : INotifyPropertyChanged
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new NotificationService();

    private ObservableCollection<NotificationItem> _notifications = new();

    /// <summary>
    /// 当前显示的通知列表
    /// </summary>
    public ObservableCollection<NotificationItem> Notifications
    {
        get => _notifications;
        private set
        {
            _notifications = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 显示通知
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="type">通知类型</param>
    /// <param name="duration">显示时长（毫秒），默认2000ms</param>
    public async Task ShowNotificationAsync(string message, NotificationType type = NotificationType.Info, int duration = 2000)
    {
        var notification = new NotificationItem
        {
            Message = message,
            Type = type
        };

        // 添加到通知列表
        Notifications.Add(notification);

        // 等待指定时间后移除
        await Task.Delay(duration);

        // 先设置为不可见（触发淡出动画）
        notification.IsVisible = false;

        // 等待动画完成后移除
        await Task.Delay(300);
        Notifications.Remove(notification);
    }

    /// <summary>
    /// 清除所有通知
    /// </summary>
    public void ClearAllNotifications()
    {
        Notifications.Clear();
    }

    /// <summary>
    /// 移除指定通知
    /// </summary>
    public async void RemoveNotification(NotificationItem notification)
    {
        if (Notifications.Contains(notification))
        {
            notification.IsVisible = false;
            await Task.Delay(300); // 等待淡出动画
            Notifications.Remove(notification);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
