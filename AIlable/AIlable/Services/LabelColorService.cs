using System;
using System.Collections.Generic;

namespace AIlable.Services;

/// <summary>
/// 标签颜色管理服务，为不同标签分配不同颜色
/// </summary>
public static class LabelColorService
{
    // 预定义的颜色列表，使用对比度较高的颜色
    private static readonly string[] PredefinedColors = new[]
    {
        "#FF0000", // 红色
        "#00FF00", // 绿色
        "#0000FF", // 蓝色
        "#FFFF00", // 黄色
        "#FF00FF", // 洋红
        "#00FFFF", // 青色
        "#FF8000", // 橙色
        "#8000FF", // 紫色
        "#FF0080", // 粉红
        "#80FF00", // 黄绿
        "#0080FF", // 天蓝
        "#FF8080", // 浅红
        "#80FF80", // 浅绿
        "#8080FF", // 浅蓝
        "#FFFF80", // 浅黄
        "#FF80FF", // 浅洋红
        "#80FFFF", // 浅青
        "#C0C0C0", // 银色
        "#808080", // 灰色
        "#800000", // 深红
        "#008000", // 深绿
        "#000080", // 深蓝
        "#808000", // 橄榄
        "#800080", // 紫红
        "#008080"  // 深青
    };

    private static readonly Dictionary<string, string> _labelColorMap = new();
    private static int _colorIndex = 0;

    /// <summary>
    /// 获取指定标签的颜色
    /// </summary>
    /// <param name="label">标签名称</param>
    /// <returns>颜色的十六进制字符串</returns>
    public static string GetColorForLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return PredefinedColors[0];

        // 如果已经分配过颜色，直接返回
        if (_labelColorMap.TryGetValue(label, out var existingColor))
            return existingColor;

        // 分配新颜色
        var color = PredefinedColors[_colorIndex % PredefinedColors.Length];
        _labelColorMap[label] = color;
        _colorIndex++;

        return color;
    }

    /// <summary>
    /// 设置标签的颜色
    /// </summary>
    /// <param name="label">标签名称</param>
    /// <param name="color">颜色的十六进制字符串</param>
    public static void SetColorForLabel(string label, string color)
    {
        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(color))
        {
            _labelColorMap[label] = color;
        }
    }

    /// <summary>
    /// 获取所有标签的颜色映射
    /// </summary>
    /// <returns>标签到颜色的映射字典</returns>
    public static Dictionary<string, string> GetAllLabelColors()
    {
        return new Dictionary<string, string>(_labelColorMap);
    }

    /// <summary>
    /// 清除所有标签颜色映射
    /// </summary>
    public static void ClearAllColors()
    {
        _labelColorMap.Clear();
        _colorIndex = 0;
    }

    /// <summary>
    /// 移除指定标签的颜色映射
    /// </summary>
    /// <param name="label">标签名称</param>
    public static void RemoveLabel(string label)
    {
        if (!string.IsNullOrEmpty(label))
        {
            _labelColorMap.Remove(label);
        }
    }

    /// <summary>
    /// 生成随机颜色
    /// </summary>
    /// <returns>随机颜色的十六进制字符串</returns>
    public static string GenerateRandomColor()
    {
        var random = new Random();
        var r = random.Next(0, 256);
        var g = random.Next(0, 256);
        var b = random.Next(0, 256);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
