using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AIlable.Views;

public partial class ConfidenceSettingDialog : Window
{
    private Slider? _confidenceSlider;
    private TextBlock? _confidenceValueText;
    private Button? _startButton;
    private Button? _cancelButton;
    
    public float ConfidenceThreshold { get; private set; } = 0.5f;
    public bool IsConfirmed { get; private set; } = false;
    
    public ConfidenceSettingDialog()
    {
        InitializeComponent();
        InitializeControls();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private void InitializeControls()
    {
        _confidenceSlider = this.FindControl<Slider>("ConfidenceSlider");
        _confidenceValueText = this.FindControl<TextBlock>("ConfidenceValueText");
        _startButton = this.FindControl<Button>("StartButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        
        if (_confidenceSlider != null)
        {
            _confidenceSlider.ValueChanged += OnConfidenceChanged;
            // 设置初始值
            UpdateConfidenceDisplay(_confidenceSlider.Value);
        }
        
        if (_startButton != null)
        {
            _startButton.Click += OnStartClicked;
        }
        
        if (_cancelButton != null)
        {
            _cancelButton.Click += OnCancelClicked;
        }
    }
    
    private void OnConfidenceChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateConfidenceDisplay(e.NewValue);
    }
    
    private void UpdateConfidenceDisplay(double value)
    {
        ConfidenceThreshold = (float)value;
        
        if (_confidenceValueText != null)
        {
            var percentage = (int)(value * 100);
            _confidenceValueText.Text = $"{percentage}%";
            
            // 根据置信度值改变颜色
            if (value < 0.4)
            {
                _confidenceValueText.Foreground = Avalonia.Media.Brushes.Orange;
            }
            else if (value < 0.7)
            {
                _confidenceValueText.Foreground = Avalonia.Media.Brushes.DodgerBlue;
            }
            else
            {
                _confidenceValueText.Foreground = Avalonia.Media.Brushes.Green;
            }
        }
    }
    
    private void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }
    
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
    
    /// <summary>
    /// 设置初始置信度值
    /// </summary>
    /// <param name="confidence">置信度值 (0.1-1.0)</param>
    public void SetInitialConfidence(float confidence)
    {
        confidence = Math.Max(0.1f, Math.Min(1.0f, confidence));
        
        if (_confidenceSlider != null)
        {
            _confidenceSlider.Value = confidence;
        }
        
        UpdateConfidenceDisplay(confidence);
    }
}