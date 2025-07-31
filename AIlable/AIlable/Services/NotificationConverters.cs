using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AIlable.Services;

/// <summary>
/// 通知类型转换器
/// </summary>
public static class NotificationTypeConverter
{
    public static readonly IValueConverter IsSuccess = new FuncValueConverter<NotificationType, bool>(
        type => type == NotificationType.Success);

    public static readonly IValueConverter IsWarning = new FuncValueConverter<NotificationType, bool>(
        type => type == NotificationType.Warning);

    public static readonly IValueConverter IsError = new FuncValueConverter<NotificationType, bool>(
        type => type == NotificationType.Error);

    public static readonly IValueConverter IsInfo = new FuncValueConverter<NotificationType, bool>(
        type => type == NotificationType.Info);
}

/// <summary>
/// 布尔值反转转换器
/// </summary>
public class BooleanInverseConverter : IValueConverter
{
    public static readonly BooleanInverseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }
}
