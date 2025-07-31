using Avalonia.Controls;
using AIlable.Services;

namespace AIlable.Controls;

public partial class NotificationToast : UserControl
{
    public NotificationToast()
    {
        InitializeComponent();
        DataContext = NotificationService.Instance;
    }

    /// <summary>
    /// 显示成功通知
    /// </summary>
    public static async void ShowSuccess(string message, int duration = 1000)
    {
        await NotificationService.Instance.ShowNotificationAsync($"✅ {message}", NotificationType.Success, duration);
    }

    /// <summary>
    /// 显示警告通知
    /// </summary>
    public static async void ShowWarning(string message, int duration = 1000)
    {
        await NotificationService.Instance.ShowNotificationAsync($"⚠️ {message}", NotificationType.Warning, duration);
    }

    /// <summary>
    /// 显示错误通知
    /// </summary>
    public static async void ShowError(string message, int duration = 1000)
    {
        await NotificationService.Instance.ShowNotificationAsync($"❌ {message}", NotificationType.Error, duration);
    }

    /// <summary>
    /// 显示信息通知
    /// </summary>
    public static async void ShowInfo(string message, int duration = 1000)
    {
        await NotificationService.Instance.ShowNotificationAsync($"ℹ️ {message}", NotificationType.Info, duration);
    }
}
