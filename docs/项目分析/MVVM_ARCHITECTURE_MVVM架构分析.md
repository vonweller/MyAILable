# AIlable项目 - MVVM架构分析

## 概述
AIlable项目采用标准的MVVM（Model-View-ViewModel）架构模式，结合Avalonia UI框架和CommunityToolkit.Mvvm库实现现代化的数据绑定和命令处理机制。

## 1. Model层 - 数据模型

### 1.1 核心数据模型

#### 基础标注模型 (Annotation.cs)
```csharp
public abstract partial class Annotation : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _color = "#FF0000";
    [ObservableProperty] private double _strokeWidth = 2.0;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected = false;
    [ObservableProperty] private DateTime _createdTime = DateTime.Now;
    [ObservableProperty] private DateTime _modifiedTime = DateTime.Now;
    
    // 抽象方法定义标注行为
    public abstract double GetArea();
    public abstract Point2D GetCenter();
    public abstract List<Point2D> GetPoints();
    public abstract void SetPoints(List<Point2D> points);
    public abstract bool ContainsPoint(Point2D point);
    public abstract Annotation Clone();
}
```

**设计特点：**
- 使用 `ObservableObject` 基类支持属性变更通知
- `[ObservableProperty]` 特性自动生成属性和通知代码
- 抽象类设计，定义标注的通用行为接口
- 支持元数据存储和时间戳跟踪

#### 具体标注类型实现

**矩形标注 (RectangleAnnotation.cs)**
```csharp
public partial class RectangleAnnotation : Annotation
{
    [ObservableProperty] private Point2D _topLeft;
    [ObservableProperty] private Point2D _bottomRight;
    
    // 计算属性
    public double Width => Math.Abs(BottomRight.X - TopLeft.X);
    public double Height => Math.Abs(BottomRight.Y - TopLeft.Y);
    public Point2D TopRight => new(BottomRight.X, TopLeft.Y);
    public Point2D BottomLeft => new(TopLeft.X, BottomRight.Y);
    
    // 实现抽象方法
    public override double GetArea() => Width * Height;
    public override Point2D GetCenter() => new((TopLeft.X + BottomRight.X) / 2, (TopLeft.Y + BottomRight.Y) / 2);
}
```

**其他标注类型：**
- `CircleAnnotation` - 圆形标注
- `PolygonAnnotation` - 多边形标注  
- `LineAnnotation` - 线条标注
- `PointAnnotation` - 点标注
- `KeypointAnnotation` - 关键点标注
- `OrientedBoundingBoxAnnotation` - 有向边界框标注

#### 项目和图像模型

**标注项目 (AnnotationProject.cs)**
```csharp
public partial class AnnotationProject : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ObservableCollection<AnnotationImage> _images = new();
    [ObservableProperty] private ObservableCollection<string> _labels = new();
    [ObservableProperty] private DateTime _createdTime = DateTime.Now;
    [ObservableProperty] private DateTime _modifiedTime = DateTime.Now;
}
```

**标注图像 (AnnotationImage.cs)**
```csharp
public partial class AnnotationImage : ObservableObject
{
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;
    [ObservableProperty] private ObservableCollection<Annotation> _annotations = new();
}
```

#### AI聊天模型

**聊天消息 (ChatMessage.cs)**
```csharp
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isUser;
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private MessageType _messageType = MessageType.Text;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private byte[]? _audioData;
    [ObservableProperty] private List<string>? _videoFramePaths;
}
```

### 1.2 枚举和配置模型

**工具枚举 (ToolEnums.cs & Enums.cs)**
```csharp
public enum AnnotationTool
{
    Select, Rectangle, Circle, Polygon, Line, Point, OrientedBoundingBox, Keypoint
}

public enum AnnotationType
{
    Rectangle, Circle, Polygon, Line, Point, Keypoint, OrientedBoundingBox
}

public enum DrawingState
{
    None, Drawing, Editing, Moving
}
```

**AI提供商配置 (AIProviderConfig.cs)**
```csharp
public partial class AIProviderConfig : ObservableObject
{
    [ObservableProperty] private AIProviderType _providerType;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiUrl = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private double _temperature = 0.7;
    [ObservableProperty] private int _maxTokens = 4096;
}
```

## 2. View层 - 用户界面

### 2.1 主视图结构

**主视图 (MainView.axaml)**
- 采用 `DockPanel` 布局，包含菜单栏、状态栏和主内容区域
- 使用 `Grid` 实现三栏布局：项目浏览器、图像画布、工具面板
- 支持 `GridSplitter` 调整面板大小
- 条件显示：标注视图和AI聊天视图切换

**关键XAML特性：**
```xml
<UserControl x:DataType="vm:MainViewModel"
             xmlns:vm="clr-namespace:AIlable.ViewModels">
  
  <!-- 键盘快捷键绑定 -->
  <UserControl.KeyBindings>
    <KeyBinding Gesture="Ctrl+F" Command="{Binding FitToWindowCommand}" />
    <KeyBinding Gesture="Ctrl+S" Command="{Binding SaveProjectCommand}" />
  </UserControl.KeyBindings>
  
  <!-- 数据绑定示例 -->
  <TextBlock Text="{Binding StatusText}" />
  <Button Command="{Binding CreateNewProjectCommand}" />
  <ListBox ItemsSource="{Binding CurrentProject.Images}" 
           SelectedItem="{Binding CurrentImage}" />
</UserControl>
```

### 2.2 自定义控件

**图像画布 (ImageCanvas.cs)**
```csharp
public class ImageCanvas : Control
{
    // 依赖属性定义
    public static readonly StyledProperty<Bitmap?> ImageProperty = 
        AvaloniaProperty.Register<ImageCanvas, Bitmap?>(nameof(Image));
    
    public static readonly StyledProperty<ObservableCollection<Annotation>> AnnotationsProperty = 
        AvaloniaProperty.Register<ImageCanvas, ObservableCollection<Annotation>>(nameof(Annotations));
    
    // 渲染逻辑
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        RenderImage(context);
        RenderAnnotations(context);
    }
}
```

**其他自定义控件：**
- `CollapsiblePanel` - 可折叠面板
- `NotificationToast` - 通知提示
- `MarkdownTextBlock` - Markdown文本显示

### 2.3 样式和主题

**样式定义 (CommonStyles.axaml)**
```xml
<Style Selector="Button.tool-button">
  <Setter Property="Background" Value="#3498DB" />
  <Setter Property="Foreground" Value="White" />
  <Setter Property="CornerRadius" Value="8" />
</Style>

<Style Selector="Button.tool-button:pointerover">
  <Setter Property="Background" Value="#2980B9" />
</Style>
```

**主题支持：**
- `LightTheme.axaml` - 浅色主题
- `DarkTheme.axaml` - 深色主题
- 动态主题切换支持

### 2.4 ViewLocator机制

**视图定位器 (ViewLocator.cs)**
```csharp
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;
        
        // 约定：ViewModel -> View 命名转换
        var name = param.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);
        
        if (type != null)
            return (Control)Activator.CreateInstance(type)!;
            
        return new TextBlock { Text = "Not Found: " + name };
    }
    
    public bool Match(object? data) => data is ViewModelBase;
}
```

## 3. ViewModel层 - 业务逻辑

### 3.1 基础ViewModel

**ViewModel基类 (ViewModelBase.cs)**
```csharp
public class ViewModelBase : ObservableObject
{
    // 提供通用的ViewModel功能
    // 继承自ObservableObject，支持属性变更通知
}
```

### 3.2 主ViewModel

**主视图模型 (MainViewModel.cs)**
```csharp
public partial class MainViewModel : ViewModelBase
{
    // === 核心数据属性 ===
    [ObservableProperty] private AnnotationProject? _currentProject;
    [ObservableProperty] private AnnotationImage? _currentImage;
    [ObservableProperty] private Bitmap? _currentImageBitmap;
    [ObservableProperty] private ObservableCollection<Annotation> _annotations;
    [ObservableProperty] private Annotation? _selectedAnnotation;
    
    // === 状态属性 ===
    [ObservableProperty] private bool _hasImage;
    [ObservableProperty] private bool _hasProject;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private Models.AnnotationTool _activeTool;
    [ObservableProperty] private DrawingState _drawingState;
    
    // === 标签管理 ===
    [ObservableProperty] private ObservableCollection<string> _availableLabels;
    [ObservableProperty] private string _currentLabel;
    [ObservableProperty] private int _currentLabelIndex;
    
    // === AI功能控制 ===
    [ObservableProperty] private bool _isAnnotationRunning;
    [ObservableProperty] private double _annotationProgress;
    [ObservableProperty] private string _annotationProgressText;
    
    // === 视图切换 ===
    [ObservableProperty] private bool _isAIChatViewActive;
    [ObservableProperty] private AIChatViewModel? _aiChatViewModel;
}
```

**命令定义：**
```csharp
// 文件操作命令
public ICommand LoadImageCommand { get; }
public ICommand CreateNewProjectCommand { get; }
public ICommand SaveProjectCommand { get; }
public ICommand ExportProjectCommand { get; }

// 工具选择命令
public ICommand SelectToolCommand { get; }
public ICommand RectangleToolCommand { get; }
public ICommand CircleToolCommand { get; }

// AI功能命令
public ICommand RunAIInferenceCommand { get; }
public ICommand StartAIAnnotationCommand { get; }
public ICommand ConfigureAIModelCommand { get; }

// 标签管理命令
public ICommand NextLabelCommand { get; }
public ICommand AddNewLabelCommand { get; }
public ICommand DeleteCurrentLabelCommand { get; }

// 撤销重做命令
public ICommand UndoCommand { get; }
public ICommand RedoCommand { get; }
```

### 3.3 AI聊天ViewModel

**AI聊天视图模型 (AIChatViewModel.cs)**
```csharp
public partial class AIChatViewModel : ViewModelBase
{
    // === 聊天数据 ===
    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    
    // === AI配置 ===
    [ObservableProperty] private ObservableCollection<AIProviderConfig> _availableProviders = new();
    [ObservableProperty] private AIProviderConfig? _selectedProvider;
    [ObservableProperty] private bool _isConfigured;
    [ObservableProperty] private string _statusText;
    
    // === 语音功能 ===
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _recordingTime;
    [ObservableProperty] private bool _isRecordingSupported;
    
    // === 命令定义 ===
    public ICommand SendMessageCommand { get; }
    public ICommand AttachImageCommand { get; }
    public ICommand AttachAudioCommand { get; }
    public ICommand StartVoiceRecordingCommand { get; }
    public ICommand StopVoiceRecordingCommand { get; }
    public ICommand ClearChatCommand { get; }
    public ICommand SaveChatCommand { get; }
}
```

### 3.4 其他专用ViewModel

**导出对话框ViewModel (ExportDialogViewModel.cs)**
```csharp
public partial class ExportDialogViewModel : ViewModelBase
{
    [ObservableProperty] private ExportFormatInfo _selectedFormat;
    [ObservableProperty] private string _outputPath = "";
    [ObservableProperty] private bool _includeImages;
    [ObservableProperty] private bool _splitTrainVal;
    [ObservableProperty] private double _trainRatio = 0.8;
}
```

**AI模型配置ViewModel (AIModelConfigDialogViewModel.cs)**
```csharp
public partial class AIModelConfigDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _modelStatus = "未加载";
    [ObservableProperty] private string _currentModelInfo = "无";
    [ObservableProperty] private string _currentModelType = "无";
    [ObservableProperty] private IBrush _modelStatusColor = Brushes.Gray;
    [ObservableProperty] private string _modelFilePath = "";
}
```

## 4. 数据绑定机制

### 4.1 属性绑定

**单向绑定：**
```xml
<TextBlock Text="{Binding StatusText}" />
<ProgressBar Value="{Binding AnnotationProgress}" />
```

**双向绑定：**
```xml
<TextBox Text="{Binding InputText}" />
<ComboBox SelectedItem="{Binding CurrentLabel}" />
<ListBox SelectedIndex="{Binding CurrentImageIndex}" />
```

**条件显示绑定：**
```xml
<Border IsVisible="{Binding HasProject}" />
<StackPanel IsVisible="{Binding !HasImage}" />
<Grid IsVisible="{Binding IsAIChatViewActive}" />
```

### 4.2 命令绑定

**基础命令绑定：**
```xml
<Button Command="{Binding SaveProjectCommand}" />
<MenuItem Command="{Binding CreateNewProjectCommand}" />
```

**带参数命令绑定：**
```xml
<Button Command="{Binding PlayAudioCommand}" 
        CommandParameter="{Binding}" />
```

**键盘快捷键绑定：**
```xml
<KeyBinding Gesture="Ctrl+S" Command="{Binding SaveProjectCommand}" />
<KeyBinding Gesture="Ctrl+Z" Command="{Binding UndoCommand}" />
```

### 4.3 集合绑定

**列表绑定：**
```xml
<ListBox ItemsSource="{Binding CurrentProject.Images}"
         SelectedItem="{Binding CurrentImage}">
  <ListBox.ItemTemplate>
    <DataTemplate>
      <StackPanel>
        <TextBlock Text="{Binding FileName}" />
        <TextBlock Text="{Binding Annotations.Count, StringFormat='标注: {0}'}" />
      </StackPanel>
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

**动态集合更新：**
```csharp
// ObservableCollection自动通知UI更新
Messages.Add(userMessage);
Annotations.Remove(selectedAnnotation);
AvailableLabels.Clear();
```

## 5. 命令处理机制

### 5.1 RelayCommand使用

**同步命令：**
```csharp
SelectToolCommand = new RelayCommand(() => SetActiveTool(Models.AnnotationTool.Select));
ToggleThemeCommand = new RelayCommand(ToggleTheme);
UndoCommand = new RelayCommand(() => _undoRedoService.Undo(), () => _undoRedoService.CanUndo);
```

**异步命令：**
```csharp
LoadImageCommand = new AsyncRelayCommand(LoadImageAsync);
SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(InputText));
```

### 5.2 命令状态管理

**动态更新命令可执行状态：**
```csharp
partial void OnSelectedAnnotationChanged(Annotation? value)
{
    (DeleteSelectedAnnotationCommand as RelayCommand)?.NotifyCanExecuteChanged();
}

partial void OnInputTextChanged(string value)
{
    ((AsyncRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
}
```

## 6. 依赖注入集成

### 6.1 服务注册

**App.axaml.cs中的服务配置：**
```csharp
public override void OnFrameworkInitializationCompleted()
{
    var services = new ServiceCollection();
    
    // 注册服务
    services.AddSingleton<IFileDialogService, FileDialogService>();
    services.AddSingleton<IThemeService, ThemeService>();
    services.AddSingleton<IAIChatService, AIChatService>();
    services.AddSingleton<IUndoRedoService, UndoRedoService>();
    
    // 注册ViewModels
    services.AddTransient<MainViewModel>();
    services.AddTransient<AIChatViewModel>();
    
    var serviceProvider = services.BuildServiceProvider();
    
    // 创建主窗口
    var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
    var mainWindow = new MainWindow { DataContext = mainViewModel };
}
```

### 6.2 ViewModel依赖注入

**构造函数注入：**
```csharp
public MainViewModel(
    IFileDialogService fileDialogService,
    IThemeService themeService,
    IAIChatService aiChatService,
    IUndoRedoService undoRedoService)
{
    _fileDialogService = fileDialogService;
    _themeService = themeService;
    _aiChatService = aiChatService;
    _undoRedoService = undoRedoService;
    
    InitializeCommands();
}
```

## 7. MVVM架构优势

### 7.1 关注点分离
- **Model**: 纯数据模型，不依赖UI框架
- **View**: 纯UI展示，通过数据绑定与ViewModel交互
- **ViewModel**: 业务逻辑和状态管理，可独立测试

### 7.2 数据绑定优势
- 自动UI更新：属性变更自动反映到界面
- 双向同步：用户输入自动更新ViewModel状态
- 类型安全：编译时检查绑定路径

### 7.3 命令模式优势
- 解耦UI和业务逻辑
- 支持异步操作
- 动态控制命令可执行状态
- 统一的错误处理机制

### 7.4 可测试性
- ViewModel可独立于UI进行单元测试
- 依赖注入支持Mock测试
- 命令和属性变更可验证

## 8. 最佳实践总结

### 8.1 属性定义
- 使用 `[ObservableProperty]` 简化属性定义
- 在属性变更时执行相关逻辑（partial方法）
- 合理使用计算属性减少冗余状态

### 8.2 命令设计
- 区分同步和异步命令
- 实现命令可执行条件检查
- 及时更新命令状态

### 8.3 数据绑定
- 使用强类型绑定（x:DataType）
- 合理使用转换器处理数据格式
- 避免在绑定中进行复杂计算

### 8.4 性能优化
- 使用ObservableCollection进行集合绑定
- 避免频繁的属性变更通知
- 合理使用虚拟化控件处理大数据集

这种MVVM架构设计使AIlable项目具有良好的可维护性、可扩展性和可测试性，为跨平台开发提供了坚实的基础。