using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace AIlable.Services
{
    /// <summary>
    /// 摄像头服务实现，基于OpenCV
    /// </summary>
    public class CameraService : ICameraService
    {
        private VideoCapture? _capture;
        private Mat? _frame;
        private bool _isRunning;
        private bool _isInitialized;
        private CameraDevice? _currentDevice;
        private System.Threading.Timer? _captureTimer;
        private readonly object _lock = new object();

        public bool IsRunning => _isRunning;
        public bool IsInitialized => _isInitialized;
        public CameraDevice? CurrentDevice => _currentDevice;

        public event EventHandler<Bitmap>? FrameAvailable;
        public event EventHandler<string>? CameraError;

        public async Task<List<CameraDevice>> GetAvailableCamerasAsync()
        {
            return await Task.Run(() =>
            {
                var cameras = new List<CameraDevice>();
                
                // 尝试检测前几个摄像头设备
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using var testCapture = new VideoCapture(i);
                        if (testCapture.IsOpened())
                        {
                            cameras.Add(new CameraDevice
                            {
                                Index = i,
                                Name = $"Camera {i}",
                                Description = $"摄像头设备 {i}",
                                IsAvailable = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"测试摄像头 {i} 时出错: {ex.Message}");
                    }
                }

                return cameras;
            });
        }

        public async Task<bool> InitializeCameraAsync(int deviceIndex = 0, int width = 640, int height = 480)
        {
            try
            {
                await StopPreviewAsync();

                // 完全释放之前的摄像头资源
                if (_capture != null)
                {
                    _capture.Release();
                    _capture.Dispose();
                    _capture = null;
                }

                Console.WriteLine($"尝试初始化摄像头设备 {deviceIndex}...");

                // 使用DirectShow后端（Windows平台）以避免系统设置影响
                _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
                
                if (!_capture.IsOpened())
                {
                    Console.WriteLine($"DirectShow设备 {deviceIndex} 无法打开，尝试默认后端...");
                    _capture?.Release();
                    _capture?.Dispose();
                    
                    // 使用默认后端
                    _capture = new VideoCapture(deviceIndex);
                    
                    if (!_capture.IsOpened())
                    {
                        Console.WriteLine($"设备 {deviceIndex} 无法打开，尝试其他设备...");
                        
                        // 尝试其他常用的摄像头索引
                        for (int i = 0; i < 3; i++)
                        {
                            if (i == deviceIndex) continue;
                            
                            _capture?.Release();
                            _capture?.Dispose();
                            _capture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                            
                            if (_capture.IsOpened())
                            {
                                Console.WriteLine($"成功打开摄像头设备 {i} (DirectShow)");
                                deviceIndex = i;
                                break;
                            }
                        }
                        
                        if (!_capture.IsOpened())
                        {
                            CameraError?.Invoke(this, $"无法打开任何摄像头设备，请检查摄像头连接和权限");
                            return false;
                        }
                    }
                }

                // 只设置基本参数，不修改任何可能影响系统的颜色设置
                _capture.Set(VideoCaptureProperties.FrameWidth, width);
                _capture.Set(VideoCaptureProperties.FrameHeight, height);
                _capture.Set(VideoCaptureProperties.Fps, 30);
                
                // 确保使用RGB格式输出
                _capture.Set(VideoCaptureProperties.ConvertRgb, 1);
                
                // 绝对不设置任何颜色相关参数，以免影响系统全局设置
                // 这些参数可能会永久改变摄像头的系统设置！
                
                // 预读取几帧来"热身"摄像头
                var warmupFrame = new Mat();
                for (int i = 0; i < 5; i++)
                {
                    if (_capture.Read(warmupFrame))
                    {
                        Console.WriteLine($"热身帧 {i + 1}: {warmupFrame.Width}x{warmupFrame.Height}");
                        if (i == 4 && !warmupFrame.Empty())
                        {
                            Console.WriteLine("摄像头热身完成，获取到有效帧");
                        }
                    }
                    await Task.Delay(100); // 等待100ms
                }
                warmupFrame.Dispose();
                
                // 验证设置是否成功
                var actualWidth = _capture.Get(VideoCaptureProperties.FrameWidth);
                var actualHeight = _capture.Get(VideoCaptureProperties.FrameHeight);
                
                Console.WriteLine($"摄像头参数: {actualWidth}x{actualHeight}");
                
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    CameraError?.Invoke(this, "摄像头初始化失败：无法获取有效的视频帧尺寸");
                    _capture?.Release();
                    _capture?.Dispose();
                    return false;
                }

                _frame = new Mat();
                _currentDevice = new CameraDevice
                {
                    Index = deviceIndex,
                    Name = $"Camera {deviceIndex}",
                    Description = $"摄像头设备 {deviceIndex} ({actualWidth}x{actualHeight})",
                    IsAvailable = true
                };

                _isInitialized = true;
                Console.WriteLine("摄像头初始化成功！");
                return true;
            }
            catch (Exception ex)
            {
                CameraError?.Invoke(this, $"初始化摄像头失败: {ex.Message}");
                Console.WriteLine($"摄像头初始化异常: {ex}");
                return false;
            }
        }

        public async Task<bool> StartPreviewAsync()
        {
            if (!_isInitialized || _capture == null || _frame == null)
            {
                CameraError?.Invoke(this, "摄像头未初始化");
                return false;
            }

            try
            {
                _isRunning = true;
                
                // 启动定时器来捕获帧
                _captureTimer = new System.Threading.Timer(CaptureFrameCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(33)); // ~30 FPS

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                CameraError?.Invoke(this, $"启动摄像头预览失败: {ex.Message}");
                return false;
            }
        }

        public async Task StopPreviewAsync()
        {
            _isRunning = false;
            
            _captureTimer?.Dispose();
            _captureTimer = null;

            await Task.CompletedTask;
        }

        public async Task<Bitmap?> CaptureFrameAsync()
        {
            if (!_isInitialized || _capture == null || _frame == null)
                return null;

            try
            {
                return await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        if (_capture.Read(_frame) && !_frame.Empty())
                        {
                            return MatToBitmap(_frame);
                        }
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                CameraError?.Invoke(this, $"捕获帧失败: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveCapturedImageAsync(string outputPath)
        {
            try
            {
                // 使用异步操作，避免阻塞线程
                return await Task.Run(() =>
                {
                    // 获取当前帧（快速操作）
                    var bitmap = GetCurrentFrame();
                    if (bitmap == null)
                        return false;

                    // 确保输出目录存在
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // 使用高质量JPEG编码，平衡质量和性能
                    bitmap.Save(outputPath);
                    Console.WriteLine($"图像已异步保存: {outputPath}");
                    return true;
                });
            }
            catch (Exception ex)
            {
                CameraError?.Invoke(this, $"保存图像失败: {ex.Message}");
                return false;
            }
        }

        public Bitmap? GetCurrentFrame()
        {
            if (!_isInitialized || _capture == null || _frame == null)
                return null;

            try
            {
                lock (_lock)
                {
                    if (_capture.Read(_frame) && !_frame.Empty())
                    {
                        return MatToBitmap(_frame);
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                CameraError?.Invoke(this, $"获取当前帧失败: {ex.Message}");
                return null;
            }
        }

        private void CaptureFrameCallback(object? state)
        {
            if (!_isRunning || _capture == null || _frame == null)
                return;

            try
            {
                lock (_lock)
                {
                    // 读取帧
                    var success = _capture.Read(_frame);
                    if (success && !_frame.Empty())
                    {
                        // 添加调试信息
                        if (_frame.Width > 0 && _frame.Height > 0)
                        {
                            var bitmap = MatToBitmap(_frame);
                            if (bitmap != null)
                            {
                                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                                Console.WriteLine($"[{timestamp}] CameraService: 准备触发FrameAvailable事件, 图像尺寸: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                                
                                // 检查是否有事件订阅者
                                var eventHandler = FrameAvailable;
                                if (eventHandler != null)
                                {
                                    var invocationList = eventHandler.GetInvocationList();
                                    Console.WriteLine($"[{timestamp}] FrameAvailable事件有 {invocationList.Length} 个订阅者");
                                    
                                    eventHandler.Invoke(this, bitmap);
                                    Console.WriteLine($"[{timestamp}] FrameAvailable事件已触发");
                                }
                                else
                                {
                                    Console.WriteLine($"[{timestamp}] 警告: FrameAvailable事件没有订阅者！");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Mat转Bitmap失败");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"获取到无效帧: {_frame.Width}x{_frame.Height}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("读取帧失败或帧为空");
                        // 如果连续读取失败，尝试重新初始化
                        Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            if (_isRunning)
                            {
                                Console.WriteLine("尝试重新初始化摄像头...");
                                await InitializeCameraAsync(_currentDevice?.Index ?? 0);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"捕获帧回调异常: {ex.Message}");
                CameraError?.Invoke(this, $"捕获帧回调失败: {ex.Message}");
            }
        }

        private static Bitmap? MatToBitmap(Mat mat)
        {
            try
            {
                if (mat.Empty() || mat.Width <= 0 || mat.Height <= 0)
                {
                    Console.WriteLine($"Mat无效: Empty={mat.Empty()}, Size={mat.Width}x{mat.Height}");
                    return null;
                }

                Console.WriteLine($"原始Mat信息: 尺寸={mat.Width}x{mat.Height}, 通道数={mat.Channels()}, 深度={mat.Depth()}");

                Mat rgbMat;
                
                // 正确处理颜色空间转换 - OpenCV默认是BGR，需要转换为RGB
                if (mat.Channels() == 3)
                {
                    Console.WriteLine("检测到3通道BGR图像，转换为RGB");
                    rgbMat = new Mat();
                    Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);
                }
                else if (mat.Channels() == 1)
                {
                    Console.WriteLine("检测到灰度图，转换为RGB");
                    rgbMat = new Mat();
                    Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.GRAY2RGB);
                }
                else if (mat.Channels() == 4)
                {
                    Console.WriteLine("检测到4通道图像，转换为RGB");
                    rgbMat = new Mat();
                    Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGRA2RGB);
                }
                else
                {
                    Console.WriteLine($"不支持的通道数: {mat.Channels()}，尝试直接使用");
                    rgbMat = mat.Clone();
                }

                // 使用PNG格式编码以保持完整的颜色信息
                var encodeParams = new int[] { (int)ImwriteFlags.PngCompression, 1 };
                var success = Cv2.ImEncode(".png", rgbMat, out byte[] bytes, encodeParams);
                
                // 清理临时Mat
                if (rgbMat != mat)
                    rgbMat.Dispose();
                
                if (!success || bytes.Length == 0)
                {
                    Console.WriteLine("编码PNG失败");
                    return null;
                }
                
                Console.WriteLine($"成功编码RGB图像，字节数: {bytes.Length}");
                
                // 创建内存流
                using var stream = new MemoryStream(bytes);
                
                // 创建Avalonia Bitmap
                var bitmap = new Bitmap(stream);
                Console.WriteLine($"成功转换Bitmap: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mat转Bitmap异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                _isRunning = false;
                _isInitialized = false;
                
                // 停止预览
                StopPreviewAsync().Wait(1000); // 最多等待1秒
                
                // 释放定时器
                _captureTimer?.Dispose();
                _captureTimer = null;
                
                // 确保摄像头资源完全释放，避免影响系统
                if (_capture != null)
                {
                    Console.WriteLine("正在释放摄像头资源...");
                    _capture.Release();
                    _capture.Dispose();
                    _capture = null;
                    Console.WriteLine("摄像头资源已释放");
                }
                
                // 释放Mat资源
                _frame?.Dispose();
                _frame = null;
                
                // 强制垃圾回收，确保资源完全释放
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"释放摄像头资源时出错: {ex.Message}");
            }
        }
    }
}