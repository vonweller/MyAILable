using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AIlable.Services
{
    /// <summary>
    /// 摄像头服务接口，提供摄像头访问和图像捕获功能
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>
        /// 获取可用的摄像头设备列表
        /// </summary>
        Task<List<CameraDevice>> GetAvailableCamerasAsync();

        /// <summary>
        /// 初始化指定的摄像头设备
        /// </summary>
        /// <param name="deviceIndex">摄像头设备索引</param>
        /// <param name="width">预览宽度</param>
        /// <param name="height">预览高度</param>
        Task<bool> InitializeCameraAsync(int deviceIndex = 0, int width = 640, int height = 480);

        /// <summary>
        /// 开始摄像头预览
        /// </summary>
        Task<bool> StartPreviewAsync();

        /// <summary>
        /// 停止摄像头预览
        /// </summary>
        Task StopPreviewAsync();

        /// <summary>
        /// 捕获当前帧作为图像
        /// </summary>
        /// <returns>捕获的图像位图</returns>
        Task<Bitmap?> CaptureFrameAsync();

        /// <summary>
        /// 保存捕获的图像到指定路径
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        Task<bool> SaveCapturedImageAsync(string outputPath);

        /// <summary>
        /// 获取当前预览帧
        /// </summary>
        Bitmap? GetCurrentFrame();

        /// <summary>
        /// 摄像头是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 摄像头是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 当前摄像头设备信息
        /// </summary>
        CameraDevice? CurrentDevice { get; }

        /// <summary>
        /// 新帧可用事件
        /// </summary>
        event EventHandler<Bitmap>? FrameAvailable;

        /// <summary>
        /// 摄像头错误事件
        /// </summary>
        event EventHandler<string>? CameraError;
    }

    /// <summary>
    /// 摄像头设备信息
    /// </summary>
    public class CameraDevice
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }
}