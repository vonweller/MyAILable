using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using AIlable.Services;

namespace AIlable.Controls
{
    public partial class CameraPreviewControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DirectProperty<CameraPreviewControl, ICameraService?> CameraServiceProperty =
            AvaloniaProperty.RegisterDirect<CameraPreviewControl, ICameraService?>(
                nameof(CameraService),
                o => o.CameraService,
                (o, v) => o.CameraService = v);

        public static readonly DirectProperty<CameraPreviewControl, bool> IsPreviewActiveProperty =
            AvaloniaProperty.RegisterDirect<CameraPreviewControl, bool>(
                nameof(IsPreviewActive),
                o => o.IsPreviewActive,
                (o, v) => o.IsPreviewActive = v);

        public static readonly DirectProperty<CameraPreviewControl, bool> IsLoadingProperty =
            AvaloniaProperty.RegisterDirect<CameraPreviewControl, bool>(
                nameof(IsLoading),
                o => o.IsLoading,
                (o, v) => o.IsLoading = v);

        public static readonly DirectProperty<CameraPreviewControl, string> StatusTextProperty =
            AvaloniaProperty.RegisterDirect<CameraPreviewControl, string>(
                nameof(StatusText),
                o => o.StatusText,
                (o, v) => o.StatusText = v);

        private ICameraService? _cameraService;
        private bool _isPreviewActive;
        private bool _isLoading;
        private string _statusText = "就绪";
        private Image? _previewImage;

        public ICameraService? CameraService
        {
            get => _cameraService;
            set
            {
                // 取消订阅旧的服务事件
                if (_cameraService != null)
                {
                    _cameraService.FrameAvailable -= OnFrameAvailable;
                    _cameraService.CameraError -= OnCameraError;
                    Console.WriteLine("取消订阅旧摄像头服务事件");
                }
                
                // 设置新的服务
                var oldValue = _cameraService;
                _cameraService = value;
                
                // 订阅新的服务事件
                if (_cameraService != null)
                {
                    _cameraService.FrameAvailable += OnFrameAvailable;
                    _cameraService.CameraError += OnCameraError;
                    Console.WriteLine($"订阅新摄像头服务事件，服务状态: IsInitialized={_cameraService.IsInitialized}, IsRunning={_cameraService.IsRunning}");
                    
                    // 如果摄像头已经在运行，立即激活预览
                    if (_cameraService.IsRunning)
                    {
                        Console.WriteLine("摄像头已在运行，立即激活预览状态");
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsPreviewActive = true;
                            UpdateStatus("摄像头运行中 - 按空格键捕获");
                        });
                    }
                }
                
                // 触发属性变化通知
                RaisePropertyChanged(CameraServiceProperty, oldValue, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraService)));
            }
        }

        public bool IsPreviewActive
        {
            get => _isPreviewActive;
            set => SetAndRaise(IsPreviewActiveProperty, ref _isPreviewActive, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetAndRaise(StatusTextProperty, ref _statusText, value);
        }

        // 事件
        public event EventHandler? CaptureRequested;
        public event EventHandler<string>? ErrorOccurred;

        public CameraPreviewControl()
        {
            InitializeComponent();
            DataContext = this;
            
            // 获取预览图像控件引用
            Loaded += OnControlLoaded;

            // 注册键盘事件
            this.KeyDown += OnKeyDown;
            this.Focusable = true;
            
            Console.WriteLine("摄像头预览控件初始化开始");
        }
        
        private void OnControlLoaded(object? sender, RoutedEventArgs e)
        {
            _previewImage = this.FindControl<Image>("PreviewImage");
            Console.WriteLine($"控件加载完成，找到PreviewImage控件: {_previewImage != null}");
            
            // 测试：创建一个简单的测试图像来验证显示是否正常
            TestImageDisplay();
        }
        
        /// <summary>
        /// 测试图像显示功能
        /// </summary>
        private void TestImageDisplay()
        {
            try
            {
                if (_previewImage != null)
                {
                    Console.WriteLine("开始测试图像显示...");
                    
                    // 创建一个简单的测试图像（红色背景）
                    var testBitmap = CreateTestBitmap();
                    if (testBitmap != null)
                    {
                        Console.WriteLine("设置测试图像...");
                        _previewImage.Source = testBitmap;
                        IsPreviewActive = true;
                        UpdateStatus("显示测试图像 - 如果能看到红色图像说明UI正常");
                        
                        // 5秒后清除测试图像
                        Task.Delay(5000).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!IsCameraConnected())
                                {
                                    _previewImage.Source = null;
                                    IsPreviewActive = false;
                                    UpdateStatus("测试图像已清除，等待摄像头连接");
                                }
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试图像显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建测试位图
        /// </summary>
        private Bitmap? CreateTestBitmap()
        {
            try
            {
                // 创建一个简单的红色测试图像
                var width = 320;
                var height = 240;
                
                // 使用更安全的方式创建测试图像
                var pixelData = new byte[width * height * 4]; // BGRA format
                
                // 填充红色像素数据
                for (int i = 0; i < pixelData.Length; i += 4)
                {
                    pixelData[i] = 0;     // B
                    pixelData[i + 1] = 0; // G
                    pixelData[i + 2] = 255; // R
                    pixelData[i + 3] = 255; // A
                }
                
                var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                    new Avalonia.PixelSize(width, height), 
                    new Avalonia.Vector(96, 96), 
                    Avalonia.Platform.PixelFormat.Bgra8888, 
                    Avalonia.Platform.AlphaFormat.Premul);
                
                using (var fb = bitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        pixelData, 0, fb.Address, pixelData.Length);
                }
                
                Console.WriteLine($"创建测试图像成功: {width}x{height}");
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建测试图像失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查摄像头是否已连接
        /// </summary>
        private bool IsCameraConnected()
        {
            return CameraService != null && CameraService.IsInitialized && CameraService.IsRunning;
        }

        public async Task StartPreviewAsync()
        {
            Console.WriteLine("CameraPreviewControl.StartPreviewAsync 被调用");
            
            if (CameraService == null)
            {
                UpdateStatus("摄像头服务未初始化");
                Console.WriteLine("摄像头服务为null");
                return;
            }

            try
            {
                Console.WriteLine("开始设置预览状态...");
                IsLoading = true;
                UpdateStatus("正在准备摄像头预览...");

                // 确保预览图像控件已找到
                if (_previewImage == null)
                {
                    _previewImage = this.FindControl<Image>("PreviewImage");
                    Console.WriteLine($"重新查找PreviewImage控件: {_previewImage != null}");
                }
                
                // 直接设置预览为激活状态，因为摄像头已由MainViewModel启动
                if (CameraService.IsInitialized && CameraService.IsRunning)
                {
                    IsPreviewActive = true;
                    UpdateStatus("摄像头运行中 - 按空格键捕获");
                    Console.WriteLine("摄像头预览状态已激活，IsPreviewActive已设为true");
                }
                else
                {
                    Console.WriteLine($"摄像头状态检查: IsInitialized={CameraService.IsInitialized}, IsRunning={CameraService.IsRunning}");
                    UpdateStatus("等待摄像头启动...");
                    
                    // 等待一段时间后重新检查
                    await Task.Delay(500);
                    if (CameraService.IsInitialized && CameraService.IsRunning)
                    {
                        IsPreviewActive = true;
                        UpdateStatus("摄像头运行中 - 按空格键捕获");
                        Console.WriteLine("延迟检查后摄像头预览状态已激活");
                    }
                    else
                    {
                        UpdateStatus("摄像头未能正常启动");
                        Console.WriteLine("摄像头未能正常启动或预览失败");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"设置摄像头预览状态失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
                Console.WriteLine($"设置摄像头预览状态异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task StopPreviewAsync()
        {
            if (CameraService == null || !IsPreviewActive)
                return;

            try
            {
                await CameraService.StopPreviewAsync();
                IsPreviewActive = false;
                UpdateStatus("摄像头已停止");

                // 清空预览图像
                if (_previewImage != null)
                {
                    _previewImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"停止摄像头失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        public async Task<Bitmap?> CaptureImageAsync()
        {
            if (CameraService == null || !IsPreviewActive)
                return null;

            try
            {
                UpdateStatus("正在捕获图像...");
                var bitmap = await CameraService.CaptureFrameAsync();
                
                if (bitmap != null)
                {
                    UpdateStatus("图像捕获成功！");
                    // 触发捕获事件
                    CaptureRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    UpdateStatus("图像捕获失败");
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                UpdateStatus($"捕获图像失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
                return null;
            }
        }

        private void OnFrameAvailable(object? sender, Bitmap bitmap)
        {
            // 确保在UI线程上更新预览图像
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnFrameAvailable(sender, bitmap));
                return;
            }
            
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] OnFrameAvailable被调用: bitmap={bitmap != null}, _previewImage={_previewImage != null}, IsPreviewActive={IsPreviewActive}");
                
                if (bitmap != null)
                {
                    Console.WriteLine($"[{timestamp}] 收到有效帧: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                }
                
                if (_previewImage == null)
                {
                    Console.WriteLine($"[{timestamp}] _previewImage为null，尝试重新查找...");
                    _previewImage = this.FindControl<Image>("PreviewImage");
                    Console.WriteLine($"[{timestamp}] 查找结果: {_previewImage != null}");
                }
                
                if (_previewImage != null && bitmap != null)
                {
                    Console.WriteLine($"[{timestamp}] 正在设置预览图像: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                    
                    // 设置图像源
                    _previewImage.Source = bitmap;
                    
                    // 如果预览未激活，自动激活
                    if (!IsPreviewActive)
                    {
                        IsPreviewActive = true;
                        Console.WriteLine($"[{timestamp}] 自动激活预览状态");
                    }
                    
                    // 更新状态
                    UpdateStatus($"摄像头运行中 - 按空格键捕获 ({bitmap.PixelSize.Width}x{bitmap.PixelSize.Height})");
                    
                    Console.WriteLine($"[{timestamp}] 预览图像已更新，显示状态: IsVisible={_previewImage.IsVisible}");
                }
                else
                {
                    var reasons = new List<string>();
                    if (_previewImage == null) reasons.Add("_previewImage为null");
                    if (bitmap == null) reasons.Add("bitmap为null");
                    
                    Console.WriteLine($"[{timestamp}] 无法设置预览: {string.Join(", ", reasons)}");
                    
                    if (bitmap == null)
                    {
                        UpdateStatus("摄像头无效帧");
                    }
                    else if (_previewImage == null)
                    {
                        UpdateStatus("预览控件初始化失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新预览图像异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                UpdateStatus($"预览更新失败: {ex.Message}");
            }
        }

        private void OnCameraError(object? sender, string error)
        {
            // 确保在UI线程上更新
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCameraError(sender, error));
                return;
            }
            
            UpdateStatus($"摄像头错误: {error}");
            ErrorOccurred?.Invoke(this, error);
        }

        private async void OnCaptureClick(object? sender, RoutedEventArgs e)
        {
            await CaptureImageAsync();
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && IsPreviewActive)
            {
                e.Handled = true;
                await CaptureImageAsync();
            }
        }

        private void UpdateStatus(string status)
        {
            // 确保在UI线程上更新状态
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => UpdateStatus(status));
                return;
            }
            
            StatusText = status;
            Console.WriteLine($"摄像头状态: {status}");
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            // 确保控件可以接收键盘事件
            this.Focus();
        }

        private bool SetAndRaise<T>(DirectProperty<CameraPreviewControl, T> property, ref T field, T value)
        {
            var changed = !Equals(field, value);
            if (changed)
            {
                var oldValue = field;
                field = value;
                RaisePropertyChanged(property, oldValue, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
            }
            return changed;
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
    }
}