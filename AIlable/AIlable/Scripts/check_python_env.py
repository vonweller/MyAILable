#!/usr/bin/env python3
"""
Python环境检测脚本
用于检查AI模型转换所需的Python环境
"""

import sys
import subprocess
import importlib.util

def check_python_version():
    """检查Python版本"""
    print(f"🐍 Python版本: {sys.version}")
    
    version_info = sys.version_info
    if version_info.major < 3 or (version_info.major == 3 and version_info.minor < 8):
        print("❌ Python版本过低，建议使用Python 3.8+")
        return False
    else:
        print("✅ Python版本符合要求")
        return True

def check_package(package_name):
    """检查包是否已安装"""
    try:
        spec = importlib.util.find_spec(package_name)
        if spec is not None:
            module = importlib.import_module(package_name)
            version = getattr(module, '__version__', '未知版本')
            print(f"✅ {package_name} 已安装 (版本: {version})")
            return True
        else:
            print(f"❌ {package_name} 未安装")
            return False
    except ImportError:
        print(f"❌ {package_name} 未安装")
        return False

def install_package(package_name):
    """尝试安装包"""
    try:
        print(f"🔄 正在安装 {package_name}...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", package_name])
        print(f"✅ {package_name} 安装成功")
        return True
    except subprocess.CalledProcessError:
        print(f"❌ {package_name} 安装失败")
        return False

def main():
    """主函数"""
    print("=" * 50)
    print("🤖 AIlable Python环境检测工具")
    print("=" * 50)
    
    # 检查Python版本
    if not check_python_version():
        print("\n请升级Python版本后重试")
        return False
    
    print("\n" + "-" * 30)
    print("📦 检查必需的包...")
    print("-" * 30)
    
    # 检查必需的包
    required_packages = [
        "torch",
        "ultralytics", 
        "onnx"
    ]
    
    missing_packages = []
    
    for package in required_packages:
        if not check_package(package):
            missing_packages.append(package)
    
    if missing_packages:
        print(f"\n❌ 缺少以下包: {', '.join(missing_packages)}")
        print("\n🔧 安装建议:")
        print("pip install ultralytics")
        print("或使用国内镜像:")
        print("pip install ultralytics -i https://pypi.tuna.tsinghua.edu.cn/simple/")
        
        # 询问是否自动安装
        try:
            choice = input("\n是否尝试自动安装缺少的包? (y/n): ").lower().strip()
            if choice == 'y' or choice == 'yes':
                for package in missing_packages:
                    install_package(package)
        except KeyboardInterrupt:
            print("\n用户取消安装")
        
        return False
    else:
        print("\n✅ 所有必需的包都已安装！")
        print("🎉 Python环境配置完成，可以进行模型转换了")
        return True

if __name__ == "__main__":
    try:
        success = main()
        if success:
            sys.exit(0)
        else:
            sys.exit(1)
    except KeyboardInterrupt:
        print("\n\n用户中断检测")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ 检测过程中出现错误: {e}")
        sys.exit(1)
