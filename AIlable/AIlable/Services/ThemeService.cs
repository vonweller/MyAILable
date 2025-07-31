using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Services;

public enum AppTheme
{
    Light,
    Dark
}

public partial class ThemeService : ObservableObject
{
    [ObservableProperty] private AppTheme _currentTheme = AppTheme.Light;
    
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public event EventHandler<AppTheme>? ThemeChanged;

    private ThemeService()
    {
        // 从设置中加载主题（这里先默认为Light）
        LoadThemeFromSettings();
        // 延迟应用主题，避免在构造函数中操作Application.Current
    }

    public void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme) return;

        CurrentTheme = theme;

        // 只有在Application.Current可用时才应用主题
        if (Application.Current != null)
        {
            ApplyTheme(theme);
        }

        SaveThemeToSettings(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    public void ToggleTheme()
    {
        SetTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
    }

    public void Initialize()
    {
        // 在应用程序完全初始化后应用当前主题
        if (Application.Current != null)
        {
            ApplyTheme(CurrentTheme);
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        try
        {
            // 清除现有的主题样式（保留FluentTheme）
            var stylesToRemove = app.Styles.Where(s => s is StyleInclude si &&
                (si.Source?.ToString().Contains("AIlable/Styles") == true)).ToList();

            foreach (var style in stylesToRemove)
            {
                app.Styles.Remove(style);
            }

            // 根据主题添加对应的样式
            switch (theme)
            {
                case AppTheme.Light:
                    app.Styles.Add(new StyleInclude(new Uri("avares://AIlable/Styles/LightTheme.axaml"))
                    {
                        Source = new Uri("avares://AIlable/Styles/LightTheme.axaml")
                    });
                    break;
                case AppTheme.Dark:
                    app.Styles.Add(new StyleInclude(new Uri("avares://AIlable/Styles/DarkTheme.axaml"))
                    {
                        Source = new Uri("avares://AIlable/Styles/DarkTheme.axaml")
                    });
                    break;
            }

            // 添加通用样式
            app.Styles.Add(new StyleInclude(new Uri("avares://AIlable/Styles/CommonStyles.axaml"))
            {
                Source = new Uri("avares://AIlable/Styles/CommonStyles.axaml")
            });
        }
        catch (Exception ex)
        {
            // 如果主题加载失败，记录错误但不崩溃应用
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    private void LoadThemeFromSettings()
    {
        // TODO: 从配置文件或注册表加载主题设置
        // 这里先默认为Light主题
        CurrentTheme = AppTheme.Light;
    }

    private void SaveThemeToSettings(AppTheme theme)
    {
        // TODO: 保存主题设置到配置文件或注册表
        // 这里暂时不实现持久化
    }

    public string GetThemeDisplayName(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "明亮模式",
            AppTheme.Dark => "黑暗模式",
            _ => "未知主题"
        };
    }

    public string GetThemeIcon(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "☀️",
            AppTheme.Dark => "🌙",
            _ => "🎨"
        };
    }
}
