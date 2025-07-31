#!/usr/bin/env python3
"""
测试YOLO模型转换功能
"""

import sys
import os
import traceback

def test_python_environment():
    """测试Python环境"""
    print("=" * 50)
    print("🐍 Python环境测试")
    print("=" * 50)
    
    print(f"Python版本: {sys.version}")
    print(f"Python路径: {sys.executable}")
    
    # 测试必需的库
    required_packages = ['torch', 'ultralytics', 'onnx']
    
    for package in required_packages:
        try:
            module = __import__(package)
            version = getattr(module, '__version__', '未知版本')
            print(f"✅ {package}: {version}")
        except ImportError:
            print(f"❌ {package}: 未安装")
            return False
    
    return True

def test_yolo_conversion(model_path=None):
    """测试YOLO转换"""
    print("\n" + "=" * 50)
    print("🤖 YOLO转换测试")
    print("=" * 50)
    
    try:
        from ultralytics import YOLO
        
        if model_path and os.path.exists(model_path):
            print(f"📁 使用指定模型: {model_path}")
            model = YOLO(model_path)
        else:
            print("📥 下载测试模型 yolov8n.pt...")
            model = YOLO('yolov8n.pt')  # 这会自动下载
        
        print("🔄 开始转换为ONNX...")
        output_path = model.export(format='onnx', imgsz=640)
        
        if os.path.exists(output_path):
            file_size = os.path.getsize(output_path)
            print(f"✅ 转换成功！")
            print(f"📄 输出文件: {output_path}")
            print(f"📏 文件大小: {file_size / 1024 / 1024:.1f} MB")
            return True
        else:
            print("❌ 转换失败：未生成输出文件")
            return False
            
    except Exception as e:
        print(f"❌ 转换异常: {e}")
        traceback.print_exc()
        return False

def main():
    """主函数"""
    print("🧪 AIlable 转换功能测试工具")
    
    # 测试Python环境
    if not test_python_environment():
        print("\n❌ Python环境测试失败")
        print("请安装缺少的包后重试")
        return False
    
    # 测试转换功能
    model_path = None
    if len(sys.argv) > 1:
        model_path = sys.argv[1]
        if not os.path.exists(model_path):
            print(f"❌ 指定的模型文件不存在: {model_path}")
            return False
    
    success = test_yolo_conversion(model_path)
    
    if success:
        print("\n🎉 所有测试通过！转换功能正常")
        return True
    else:
        print("\n❌ 转换测试失败")
        return False

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n\n用户中断测试")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ 测试过程中出现异常: {e}")
        traceback.print_exc()
        sys.exit(1)
