using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Layout;

namespace AIlable.Controls
{
    public partial class MarkdownTextBlock : UserControl
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<MarkdownTextBlock, string>(nameof(Text), defaultBindingMode: BindingMode.OneWay);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public MarkdownTextBlock()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0),
                Content = new StackPanel
                {
                    Name = "ContentPanel",
                    Spacing = 12 // 增加间距
                }
            };
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                UpdateContent();
            }
        }

        private void UpdateContent()
        {
            if (Content is ScrollViewer scrollViewer && 
                scrollViewer.Content is StackPanel panel)
            {
                panel.Children.Clear();
                
                if (string.IsNullOrEmpty(Text))
                    return;

                // 解析markdown内容
                ParseMarkdown(Text, panel);
            }
        }

        private void ParseMarkdown(string text, StackPanel panel)
        {
            // 检查是否是思考过程内容（由AI服务标记）
            if (IsThinkingContent(text))
            {
                AddThinkingContent(text, panel);
                return;
            }
            
            // 代码块正则表达式
            var codeBlockRegex = new Regex(@"```(\w+)?\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // 行内代码正则表达式
            var inlineCodeRegex = new Regex(@"`([^`]+)`");
            
            int lastIndex = 0;
            
            // 查找所有代码块
            foreach (Match match in codeBlockRegex.Matches(text))
            {
                // 添加代码块前的普通文本
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    AddTextContent(beforeText, panel);
                }
                
                // 添加代码块
                var language = match.Groups[1].Value;
                var code = match.Groups[2].Value;
                AddCodeBlock(code, language, panel);
                
                lastIndex = match.Index + match.Length;
            }
            
            // 添加剩余的普通文本
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                AddTextContent(remainingText, panel);
            }
        }

        private void AddTextContent(string text, StackPanel panel)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            // 处理行内代码
            var inlineCodeRegex = new Regex(@"`([^`]+)`");
            
            // 如果没有行内代码，直接处理为普通文本
            if (!inlineCodeRegex.IsMatch(text))
            {
                var textBlock = new SelectableTextBlock
                {
                    Text = text.Trim(),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0),
                    LineHeight = 22, // 增加行高
                    MaxWidth = 550 // 调整最大宽度
                };
                panel.Children.Add(textBlock);
                return;
            }
            
            // 如果包含行内代码，则需要混合处理
            var parts = inlineCodeRegex.Split(text);
            var textPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 6
            };
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                    
                if (i % 2 == 1) // 这是行内代码
                {
                    var inlineCode = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 4),
                        Margin = new Thickness(0, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new TextBlock
                        {
                            Text = parts[i],
                            FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                            FontSize = 13,
                            Foreground = new SolidColorBrush(Color.FromRgb(214, 51, 132))
                        }
                    };
                    textPanel.Children.Add(inlineCode);
                }
                else // 普通文本
                {
                    if (!string.IsNullOrWhiteSpace(parts[i]))
                    {
                        var textBlock = new TextBlock
                        {
                            Text = parts[i].Trim(),
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            LineHeight = 22,
                            MaxWidth = 550,
                            Margin = new Thickness(0, 2)
                        };
                        textPanel.Children.Add(textBlock);
                    }
                }
            }
            
            if (textPanel.Children.Count > 0)
            {
                panel.Children.Add(textPanel);
            }
        }

        private void AddCodeBlock(string code, string language, StackPanel panel)
        {
            var codeBlock = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)), // VS Code dark theme
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12),
                Margin = new Thickness(0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 64, 72)),
                BorderThickness = new Thickness(1)
            };

            var codePanel = new StackPanel { Spacing = 8 };

            // 代码块头部 - 总是显示
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            
            // 左侧：语言标签或占位符
            if (!string.IsNullOrEmpty(language))
            {
                var languageLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(97, 175, 239)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4),
                    Child = new TextBlock
                    {
                        Text = language.ToUpper(),
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                };
                DockPanel.SetDock(languageLabel, Dock.Left);
                header.Children.Add(languageLabel);
            }
            else
            {
                // 如果没有语言，显示代码标识
                var codeLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 4),
                    Child = new TextBlock
                    {
                        Text = "CODE",
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                };
                DockPanel.SetDock(codeLabel, Dock.Left);
                header.Children.Add(codeLabel);
            }
            
            // 右侧：复制按钮 - 总是显示
            var copyButton = new Button
            {
                Content = "📋 复制",
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(97, 175, 239)),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(Color.FromRgb(97, 175, 239)),
                Padding = new Thickness(12, 4),
                CornerRadius = new CornerRadius(4),
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            // 设置复制按钮的提示
            ToolTip.SetTip(copyButton, "点击复制代码到剪贴板");
            
            // 复制功能
            copyButton.Click += (s, e) => CopyToClipboard(code);
            
            // 按钮悬停效果
            copyButton.PointerEntered += (s, e) =>
            {
                copyButton.Background = new SolidColorBrush(Color.FromArgb(20, 97, 175, 239));
            };
            copyButton.PointerExited += (s, e) =>
            {
                copyButton.Background = Brushes.Transparent;
            };
            
            DockPanel.SetDock(copyButton, Dock.Right);
            header.Children.Add(copyButton);
            
            codePanel.Children.Add(header);

            // 代码内容
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400,
                Content = new TextBlock
                {
                    Text = code.Trim(),
                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)), // VS Code light text
                    Background = Brushes.Transparent,
                    TextWrapping = TextWrapping.NoWrap,
                    LineHeight = 20
                }
            };

            codePanel.Children.Add(scrollViewer);
            codeBlock.Child = codePanel;
            panel.Children.Add(codeBlock);
        }

        private async void CopyToClipboard(string text)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                    Console.WriteLine($"[DEBUG] 已复制代码到剪贴板，长度: {text.Length} 字符");
                    
                    // 可以在这里添加一个临时的成功提示
                    // 但暂时用控制台输出即可
                }
                else
                {
                    Console.WriteLine("[ERROR] 无法访问剪贴板");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 复制到剪贴板失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查文本是否为思考过程内容
        /// </summary>
        private bool IsThinkingContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // 检查是否包含思考标识符
            return text.Contains("💭 ") || 
                   text.Contains("思考中") || 
                   text.StartsWith("<thinking>") ||
                   text.Contains("reasoning") ||
                   (
                       text.Length > 50 && 
                       !text.Contains("\n\n") && 
                       !text.Contains("```") &&
                       (
                           text.Contains("首先") ||
                           text.Contains("然后") ||
                           text.Contains("所以") ||
                           text.Contains("因为") ||
                           text.Contains("考虑") ||
                           text.Contains("分析")
                       )
                   );
        }
        
        /// <summary>
        /// 添加思考过程内容的特殊样式
        /// </summary>
        private void AddThinkingContent(string text, StackPanel panel)
        {
            var thinkingContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 100, 150, 255)), // 淡蓝色背景
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 150, 255)),
                BorderThickness = new Thickness(2, 0, 0, 0), // 左侧蓝色边框
                CornerRadius = new CornerRadius(0, 4, 4, 0),
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 4),
                MaxWidth = 550
            };
            
            var thinkingPanel = new StackPanel { Spacing = 4 };
            
            // 思考标题
            var header = new StackPanel 
            { 
                Orientation = Avalonia.Layout.Orientation.Horizontal, 
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 4)
            };
            
            var thinkingIcon = new TextBlock
            {
                Text = "💭",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var thinkingLabel = new TextBlock
            {
                Text = "AI思考过程",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 120, 200)),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            header.Children.Add(thinkingIcon);
            header.Children.Add(thinkingLabel);
            thinkingPanel.Children.Add(header);
            
            // 思考内容
            var cleanedText = text
                .Replace("💭 **思考中...** ", "")
                .Replace("💭 ", "")
                .Replace("<thinking>", "")
                .Replace("</thinking>", "")
                .Trim();
            
            var contentBlock = new SelectableTextBlock
            {
                Text = cleanedText,
                FontSize = 13,
                FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                MaxWidth = 520
            };
            
            thinkingPanel.Children.Add(contentBlock);
            thinkingContainer.Child = thinkingPanel;
            panel.Children.Add(thinkingContainer);
        }
    }
}