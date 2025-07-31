using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIlable.Services;

namespace AIlable.ViewModels;

public partial class AIModelConfigDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _modelStatus = "未加载";
    [ObservableProperty] private string _currentModelInfo = "无";
    [ObservableProperty] private string _currentModelType = "无";
    [ObservableProperty] private IBrush _modelStatusColor = Brushes.Gray;
    [ObservableProperty] private string _modelFilePath = "";
    [ObservableProperty] private AIModelType _selectedModelType = AIModelType.YOLO;
    [ObservableProperty] private bool _hasCurrentModel = false;
    [ObservableProperty] private bool _canLoadModel = false;
    [ObservableProperty] private string _modelDescription = "";
    [ObservableProperty] private bool _isLoadingModel = false;
    [ObservableProperty] private double _loadingProgress = 0;
    [ObservableProperty] private string _loadingDebugMessages = "";

    private readonly AIModelManager _aiModelManager;
    private readonly IFileDialogService _fileDialogService;

    public AIModelConfigDialogViewModel(AIModelManager aiModelManager, IFileDialogService fileDialogService)
    {
        _aiModelManager = aiModelManager;
        _fileDialogService = fileDialogService;

        AvailableModelTypes = Enum.GetValues<AIModelType>().ToList();

        LoadModelCommand = new AsyncRelayCommand(LoadModelAsync, () => CanLoadModel);
        UnloadModelCommand = new RelayCommand(UnloadModel, () => HasCurrentModel);
        BrowseModelCommand = new AsyncRelayCommand(BrowseModelAsync);
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);

        UpdateModelStatus();
        UpdateModelDescription();
    }

    public List<AIModelType> AvailableModelTypes { get; }
    
    public ICommand LoadModelCommand { get; }
    public ICommand UnloadModelCommand { get; }
    public ICommand BrowseModelCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public bool DialogResult { get; private set; }

    private void UpdateModelStatus()
    {
        HasCurrentModel = _aiModelManager.HasActiveModel;

        if (HasCurrentModel)
        {
            ModelStatus = "✅ 已加载";
            ModelStatusColor = Brushes.Green;
            CurrentModelInfo = _aiModelManager.ActiveModel?.ModelName ?? "未知";
            CurrentModelType = _aiModelManager.ActiveModel?.ModelType.ToString() ?? "未知";
        }
        else
        {
            ModelStatus = "未加载";
            ModelStatusColor = Brushes.Gray;
            CurrentModelInfo = "无";
            CurrentModelType = "无";
        }

        // 更新命令可用性
        ((RelayCommand)UnloadModelCommand).NotifyCanExecuteChanged();
    }

    private void UpdateModelDescription()
    {
        ModelDescription = SelectedModelType switch
        {
            AIModelType.YOLO => 
                "YOLO (You Only Look Once) 是一种实时目标检测算法。\n\n" +
                "支持的功能：\n" +
                "• 实时目标检测\n" +
                "• 多类别识别\n" +
                "• 边界框回归\n\n" +
                "文件要求：\n" +
                "• ONNX格式模型文件 (.onnx)\n" +
                "• 可选：类别名称文件 (.names或.txt)\n\n" +
                "使用说明：\n" +
                "1. 选择经过训练的YOLO ONNX模型文件\n" +
                "2. 确保模型输入尺寸为640x640（标准配置）\n" +
                "3. 推理时可调整置信度阈值",
            
            AIModelType.SegmentAnything => 
                "Segment Anything Model (SAM) 是Meta开发的通用图像分割模型。\n\n" +
                "支持的功能：\n" +
                "• 自动分割\n" +
                "• 点击分割\n" +
                "• 边界框分割\n\n" +
                "注意：SAM模型支持正在开发中。",
            
            AIModelType.Custom => 
                "自定义模型支持用户自己训练的模型。\n\n" +
                "要求：\n" +
                "• ONNX格式\n" +
                "• 兼容的输入输出格式\n\n" +
                "注意：需要用户确保模型兼容性。",
            
            _ => "请选择模型类型以查看说明。"
        };
    }

    partial void OnSelectedModelTypeChanged(AIModelType value)
    {
        UpdateModelDescription();
        UpdateCanLoadModel();
    }

    partial void OnModelFilePathChanged(string value)
    {
        UpdateCanLoadModel();
    }

    private void UpdateCanLoadModel()
    {
        CanLoadModel = !string.IsNullOrEmpty(ModelFilePath) && File.Exists(ModelFilePath);
        ((AsyncRelayCommand)LoadModelCommand).NotifyCanExecuteChanged();
    }

    private async Task BrowseModelAsync()
    {
        try
        {
            var onnxFiles = new FilePickerFileType("ONNX模型文件")
            {
                Patterns = new[] { "*.onnx" }
            };

            var yoloFiles = new FilePickerFileType("YOLO模型文件")
            {
                Patterns = new[] { "*.pt", "*.pth" }
            };

            var filePath = await _fileDialogService.ShowOpenFileDialogAsync(
                "选择AI模型文件",
                new[] { onnxFiles, yoloFiles, FileDialogService.AllFiles });

            if (!string.IsNullOrEmpty(filePath))
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                // 检查是否需要转换
                if (extension == ".pt" || extension == ".pth")
                {
                    ModelStatus = "🔄 正在转换模型...";
                    ModelStatusColor = Brushes.Orange;
                    Console.WriteLine($"🔄 开始转换模型: {filePath}");

                    try
                    {
                        // 自动转换为ONNX格式
                        var convertedPath = await ConvertToOnnxAsync(filePath);
                        if (!string.IsNullOrEmpty(convertedPath))
                        {
                            ModelFilePath = convertedPath;
                            ModelStatus = "✅ 模型转换完成";
                            ModelStatusColor = Brushes.Green;
                            Console.WriteLine($"✅ 转换成功: {convertedPath}");
                        }
                        else
                        {
                            ModelStatus = "❌ 模型转换失败";
                            ModelStatusColor = Brushes.Red;
                            Console.WriteLine("❌ 转换失败：未生成有效文件");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelStatus = $"❌ 转换异常: {ex.Message}";
                        ModelStatusColor = Brushes.Red;
                        Console.WriteLine($"❌ 转换异常: {ex}");
                    }
                }
                else
                {
                    ModelFilePath = filePath;
                    Console.WriteLine($"📁 直接使用文件: {filePath}");
                }
            }
        }
        catch (Exception ex)
        {
            ModelStatus = $"浏览模型文件失败: {ex.Message}";
            ModelStatusColor = Brushes.Red;
            Console.WriteLine($"浏览模型文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将YOLO模型转换为ONNX格式
    /// </summary>
    private async Task<string?> ConvertToOnnxAsync(string modelPath)
    {
        try
        {
            Console.WriteLine($"🔄 开始转换流程: {modelPath}");
            var outputPath = Path.ChangeExtension(modelPath, ".onnx");
            Console.WriteLine($"📁 目标输出路径: {outputPath}");

            // 检查是否已经存在转换后的文件
            if (File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                Console.WriteLine($"📄 发现已存在文件，大小: {fileInfo.Length} bytes");
                // 检查文件大小，如果太小可能是之前转换失败的残留文件
                if (fileInfo.Length > 1024) // 大于1KB
                {
                    ModelStatus = "✅ 发现已转换的ONNX文件";
                    Console.WriteLine("✅ 使用已存在的有效ONNX文件");
                    return outputPath;
                }
                else
                {
                    // 删除无效的小文件
                    Console.WriteLine("🗑️ 删除无效的小文件");
                    File.Delete(outputPath);
                }
            }

            ModelStatus = "🔄 正在转换模型，请稍候...";
            Console.WriteLine("🐍 开始Python转换流程");

            // 尝试使用Python和ultralytics进行转换
            var success = await ConvertUsingPythonAsync(modelPath, outputPath);

            if (success && File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                if (fileInfo.Length > 1024) // 检查转换后的文件大小
                {
                    ModelStatus = $"✅ 转换完成 ({fileInfo.Length / 1024 / 1024:F1} MB)";
                    return outputPath;
                }
                else
                {
                    ModelStatus = "❌ 转换失败：生成的文件无效";
                    File.Delete(outputPath); // 删除无效文件
                }
            }

            // 如果Python转换失败，尝试备用方案
            ModelStatus = "🔄 Python转换失败，尝试备用方案...";
            success = await TryAlternativeConversionAsync(modelPath, outputPath);

            if (success && File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                if (fileInfo.Length > 1024)
                {
                    ModelStatus = $"✅ 备用方案转换完成 ({fileInfo.Length / 1024 / 1024:F1} MB)";
                    return outputPath;
                }
            }

            ModelStatus = "❌ 所有转换方案都失败了";
            return null;
        }
        catch (Exception ex)
        {
            ModelStatus = $"❌ 转换异常: {ex.Message}";
            Console.WriteLine($"模型转换失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 使用Python和ultralytics进行模型转换
    /// </summary>
    private async Task<bool> ConvertUsingPythonAsync(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine("🔍 开始Python转换流程");
            ModelStatus = "🔍 检查Python环境...";

            // 首先测试Python是否可用
            var testSuccess = await TestPythonAsync();
            if (!testSuccess)
            {
                Console.WriteLine("❌ Python测试失败");
                return false;
            }

            Console.WriteLine("✅ Python测试通过，开始转换");
            ModelStatus = "🔄 正在转换模型...";

            // 创建简化的Python转换脚本
            var scriptContent = $@"
import sys
import os

print('🐍 Python脚本开始执行')
print(f'输入文件: {inputPath}')
print(f'输出文件: {outputPath}')

try:
    print('📦 检查ultralytics库...')
    from ultralytics import YOLO
    print('✅ ultralytics库导入成功')

    print('📥 正在加载模型...')
    model = YOLO(r'{inputPath}')
    print('✅ 模型加载成功')

    print('🔄 开始转换为ONNX格式...')
    model.export(format='onnx', imgsz=640)
    print('✅ 转换命令执行完成')

    # 检查输出文件
    if os.path.exists(r'{outputPath}'):
        file_size = os.path.getsize(r'{outputPath}')
        print(f'✅ 转换完成！文件大小: {{file_size / 1024 / 1024:.1f}} MB')
        sys.exit(0)
    else:
        print('❌ 转换失败：未找到输出文件')
        sys.exit(1)

except ImportError as e:
    print(f'❌ 导入错误: {{e}}')
    print('请安装ultralytics: pip install ultralytics')
    sys.exit(1)
except Exception as e:
    print(f'❌ 转换异常: {{e}}')
    import traceback
    traceback.print_exc()
    sys.exit(1)
";

            var scriptPath = Path.GetTempFileName() + ".py";
            await File.WriteAllTextAsync(scriptPath, scriptContent);

            try
            {
                // 首先检查Python环境
                if (!await CheckPythonEnvironmentAsync())
                {
                    return false;
                }

                ModelStatus = "🐍 正在执行Python转换脚本...";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    ModelStatus = "❌ 无法启动Python进程";
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                Console.WriteLine($"Python输出: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Python错误: {error}");
                }

                if (process.ExitCode == 0)
                {
                    ModelStatus = "✅ Python转换成功";
                    return true;
                }
                else
                {
                    // 显示详细的错误信息
                    var errorMsg = !string.IsNullOrEmpty(error) ? error : output;
                    if (errorMsg.Contains("ultralytics"))
                    {
                        ModelStatus = "❌ 缺少ultralytics库，请安装: pip install ultralytics";
                    }
                    else if (errorMsg.Contains("python"))
                    {
                        ModelStatus = "❌ 未找到Python环境，请安装Python";
                    }
                    else
                    {
                        ModelStatus = $"❌ 转换失败: {errorMsg.Split('\n').FirstOrDefault() ?? "未知错误"}";
                    }
                    return false;
                }
            }
            finally
            {
                // 清理临时脚本文件
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ModelStatus = $"❌ 转换异常: {ex.Message}";
            Console.WriteLine($"Python转换异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 测试Python是否可用
    /// </summary>
    private async Task<bool> TestPythonAsync()
    {
        try
        {
            Console.WriteLine("🧪 测试Python环境");

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                Console.WriteLine("❌ 无法启动Python进程");
                ModelStatus = "❌ 未找到Python，请安装Python";
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Console.WriteLine($"Python版本输出: {output}");
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Python版本错误: {error}");
            }

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✅ Python环境可用");
                return true;
            }
            else
            {
                Console.WriteLine("❌ Python环境测试失败");
                ModelStatus = "❌ Python环境异常";
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Python测试异常: {ex.Message}");
            if (ex.Message.Contains("找不到指定的文件") || ex.Message.Contains("cannot find") ||
                ex.Message.Contains("系统找不到指定的文件"))
            {
                ModelStatus = "❌ 未安装Python环境";
                ShowPythonInstallationGuide();
            }
            else
            {
                ModelStatus = $"❌ Python测试异常: {ex.Message}";
            }
            return false;
        }
    }

    /// <summary>
    /// 检查Python环境和ultralytics库
    /// </summary>
    private async Task<bool> CheckPythonEnvironmentAsync()
    {
        try
        {
            ModelStatus = "🔍 检查Python环境...";

            // 检查Python是否可用
            var pythonCheckScript = @"
import sys
print(f'Python版本: {sys.version}')
try:
    import ultralytics
    print('✅ ultralytics库已安装')
    print(f'ultralytics版本: {ultralytics.__version__}')
except ImportError:
    print('❌ 未安装ultralytics库')
    print('请运行: pip install ultralytics')
    sys.exit(1)
";

            var scriptPath = Path.GetTempFileName() + ".py";
            await File.WriteAllTextAsync(scriptPath, pythonCheckScript);

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    ModelStatus = "❌ 无法启动Python，请确保已安装Python并添加到PATH";
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                Console.WriteLine($"Python环境检查输出: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Python环境检查错误: {error}");
                }

                if (process.ExitCode == 0)
                {
                    ModelStatus = "✅ Python环境检查通过";
                    return true;
                }
                else
                {
                    if (output.Contains("未安装ultralytics库") || error.Contains("ultralytics"))
                    {
                        ModelStatus = "❌ 缺少ultralytics库";
                        ShowInstallationGuide();
                    }
                    else
                    {
                        ModelStatus = "❌ Python环境检查失败";
                    }
                    return false;
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("找不到指定的文件") || ex.Message.Contains("cannot find"))
            {
                ModelStatus = "❌ 未找到Python，请安装Python并添加到系统PATH";
            }
            else
            {
                ModelStatus = $"❌ Python环境检查异常: {ex.Message}";
            }
            Console.WriteLine($"Python环境检查异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 显示Python安装指导
    /// </summary>
    private void ShowPythonInstallationGuide()
    {
        var guide = @"
� 需要安装Python环境才能进行模型转换

📋 安装步骤：

1. 📥 下载Python:
   - 访问 https://python.org
   - 下载Python 3.8或更高版本

2. 🔧 安装Python:
   - ⚠️ 重要：安装时务必勾选 'Add Python to PATH'
   - 选择 'Install for all users'

3. 📦 安装AI库:
   打开命令提示符(CMD)，运行：
   pip install ultralytics

4. ✅ 验证安装:
   python --version
   python -c ""import ultralytics; print('安装成功')""

5. 🌐 如果网络慢，可使用国内镜像:
   pip install ultralytics -i https://pypi.tuna.tsinghua.edu.cn/simple/

完成后重新尝试转换。

💡 提示：您也可以直接使用已转换好的.onnx文件
";

        Console.WriteLine(guide);
        // 这里可以显示一个更友好的对话框
    }

    /// <summary>
    /// 显示ultralytics安装指导
    /// </summary>
    private void ShowInstallationGuide()
    {
        var guide = @"
🐍 缺少ultralytics库

📦 安装命令：
pip install ultralytics

🌐 如果网络问题，使用国内镜像：
pip install ultralytics -i https://pypi.tuna.tsinghua.edu.cn/simple/

✅ 验证安装：
python -c ""import ultralytics; print('安装成功')""
";

        Console.WriteLine(guide);
    }

    /// <summary>
    /// 备用转换方案
    /// </summary>
    private async Task<bool> TryAlternativeConversionAsync(string inputPath, string outputPath)
    {
        try
        {
            ModelStatus = "🔄 尝试使用torch.onnx进行转换...";

            // 使用更基础的torch.onnx转换脚本
            var alternativeScript = $@"
import sys
import os
import torch

try:
    print('📥 正在加载PyTorch模型...')

    # 尝试直接加载.pt文件
    model = torch.load(r'{inputPath}', map_location='cpu')

    # 如果模型是字典格式，提取模型部分
    if isinstance(model, dict):
        if 'model' in model:
            model = model['model']
        elif 'state_dict' in model:
            # 这种情况需要模型架构，比较复杂
            print('❌ 模型格式需要架构定义，建议使用ultralytics')
            sys.exit(1)

    # 设置为评估模式
    model.eval()

    # 创建示例输入
    dummy_input = torch.randn(1, 3, 640, 640)

    print('🔄 正在导出ONNX...')
    torch.onnx.export(
        model,
        dummy_input,
        r'{outputPath}',
        export_params=True,
        opset_version=11,
        do_constant_folding=True,
        input_names=['input'],
        output_names=['output'],
        dynamic_axes={{'input': {{0: 'batch_size'}}, 'output': {{0: 'batch_size'}}}}
    )

    if os.path.exists(r'{outputPath}'):
        file_size = os.path.getsize(r'{outputPath}')
        print(f'✅ 备用转换完成，文件大小: {{file_size / 1024 / 1024:.1f}} MB')
        sys.exit(0)
    else:
        print('❌ 备用转换失败：未生成输出文件')
        sys.exit(1)

except Exception as e:
    print(f'❌ 备用转换失败: {{e}}')
    sys.exit(1)
";

            var scriptPath = Path.GetTempFileName() + ".py";
            await File.WriteAllTextAsync(scriptPath, alternativeScript);

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    ModelStatus = "❌ 无法启动Python进程";
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                Console.WriteLine($"备用转换输出: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"备用转换错误: {error}");
                }

                return process.ExitCode == 0;
            }
            finally
            {
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ModelStatus = $"❌ 备用转换异常: {ex.Message}";
            Console.WriteLine($"备用转换异常: {ex.Message}");
            return false;
        }
    }

    private async Task LoadModelAsync()
    {
        if (string.IsNullOrEmpty(ModelFilePath) || !File.Exists(ModelFilePath))
        {
            ModelStatus = "请先选择模型文件";
            ModelStatusColor = Brushes.Orange;
            return;
        }

        try
        {
            // 开始加载，显示进度界面
            IsLoadingModel = true;
            LoadingProgress = 0;
            LoadingDebugMessages = "";

            // 显示加载状态
            ModelStatus = "正在加载模型...";
            ModelStatusColor = Brushes.Orange;

            // 模拟加载进度和调试消息
            await UpdateLoadingProgress(10, "🔍 检查模型文件...");
            await Task.Delay(200);

            await UpdateLoadingProgress(25, $"📁 文件路径: {ModelFilePath}");
            await UpdateLoadingProgress(30, $"📊 模型类型: {SelectedModelType}");
            await Task.Delay(300);

            await UpdateLoadingProgress(40, "🧠 初始化AI引擎...");
            await Task.Delay(400);

            await UpdateLoadingProgress(60, "⚙️ 配置模型参数...");
            await Task.Delay(300);

            await UpdateLoadingProgress(80, "🔄 加载模型权重...");

            var success = await _aiModelManager.LoadModelAsync(SelectedModelType, ModelFilePath);

            if (success)
            {
                await UpdateLoadingProgress(100, "✅ 模型加载完成！");
                await Task.Delay(500);

                // 显示成功状态
                ModelStatus = "✅ 模型加载成功！";
                ModelStatusColor = Brushes.Green;
                CurrentModelInfo = Path.GetFileNameWithoutExtension(ModelFilePath);
                CurrentModelType = SelectedModelType.ToString();
                HasCurrentModel = true;

                // 可以添加成功音效或动画
                Console.WriteLine($"✅ 模型加载成功: {CurrentModelInfo}");
            }
            else
            {
                await UpdateLoadingProgress(100, "❌ 模型加载失败");
                await Task.Delay(500);

                ModelStatus = "❌ 模型加载失败";
                ModelStatusColor = Brushes.Red;
                Console.WriteLine("❌ 模型加载失败");
            }

            UpdateModelStatus();
        }
        catch (Exception ex)
        {
            await UpdateLoadingProgress(100, $"❌ 加载异常: {ex.Message}");
            await Task.Delay(500);

            ModelStatus = $"❌ 加载失败: {ex.Message}";
            ModelStatusColor = Brushes.Red;
            Console.WriteLine($"加载模型时出错: {ex.Message}");
        }
        finally
        {
            // 隐藏进度界面
            IsLoadingModel = false;
        }
    }

    private async Task UpdateLoadingProgress(double progress, string message)
    {
        LoadingProgress = progress;
        LoadingDebugMessages += $"[{DateTime.Now:HH:mm:ss}] {message}\n";

        // 确保UI更新
        await Task.Delay(50);
    }

    private void UnloadModel()
    {
        _aiModelManager.UnloadCurrentModel();
        UpdateModelStatus();
    }

    private void Confirm()
    {
        DialogResult = true;
        CloseDialog();
    }

    private void Cancel()
    {
        DialogResult = false;
        CloseDialog();
    }

    private void CloseDialog()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.OfType<Views.AIModelConfigDialog>()
                .FirstOrDefault(w => ReferenceEquals(w.DataContext, this));
            window?.Close(DialogResult);
        }
    }
}