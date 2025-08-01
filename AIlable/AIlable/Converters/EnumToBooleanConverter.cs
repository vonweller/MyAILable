using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace AIlable.Converters;

/// <summary>
/// 将枚举值转换为布尔值的转换器，用于RadioButton绑定
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public static readonly EnumToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        // 将参数转换为枚举值进行比较
        if (Enum.TryParse(value.GetType(), parameter.ToString(), out var enumValue))
        {
            return value.Equals(enumValue);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is not null)
        {
            // 将参数字符串转换回枚举值
            if (Enum.TryParse(targetType, parameter.ToString(), out var enumValue))
            {
                return enumValue;
            }
        }

        return BindingOperations.DoNothing;
    }
}