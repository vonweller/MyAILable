#!/usr/bin/env python3
"""
OBB (Oriented Bounding Box) 模型转换脚本
支持YOLO OBB模型的转换和导出
"""

import sys
import os
import traceback
from pathlib import Path

def convert_obb_model(input_path, output_path=None, export_format='onnx', imgsz=640):
    """
    转换OBB模型
    
    Args:
        input_path: 输入模型路径 (.pt文件)
        output_path: 输出路径 (可选)
        export_format: 导出格式 ('onnx', 'torchscript', 'coreml', 'tflite', 'pb')
        imgsz: 输入图像尺寸
    """
    try:
        print(f"🔄 开始转换OBB模型: {input_path}")
        print(f"📋 导出格式: {export_format}")
        print(f"📏 图像尺寸: {imgsz}")
        
        # 导入ultralytics
        try:
            from ultralytics import YOLO
            print("✅ ultralytics库导入成功")
        except ImportError as e:
            print(f"❌ 无法导入ultralytics: {e}")
            print("请安装: pip install ultralytics")
            return False
        
        # 检查输入文件
        if not os.path.exists(input_path):
            print(f"❌ 输入文件不存在: {input_path}")
            return False
        
        # 加载模型
        print("📥 正在加载OBB模型...")
        model = YOLO(input_path)
        print("✅ 模型加载成功")
        
        # 检查模型类型
        if hasattr(model, 'task') and model.task != 'obb':
            print(f"⚠️ 警告: 模型任务类型为 '{model.task}'，不是OBB模型")
            print("继续转换，但可能不是预期的OBB格式")
        
        # 设置输出路径
        if output_path is None:
            input_path_obj = Path(input_path)
            if export_format == 'onnx':
                output_path = str(input_path_obj.with_suffix('.onnx'))
            elif export_format == 'torchscript':
                output_path = str(input_path_obj.with_suffix('.torchscript'))
            elif export_format == 'coreml':
                output_path = str(input_path_obj.with_suffix('.mlmodel'))
            elif export_format == 'tflite':
                output_path = str(input_path_obj.with_suffix('.tflite'))
            elif export_format == 'pb':
                output_path = str(input_path_obj.with_suffix('.pb'))
            else:
                output_path = str(input_path_obj.with_suffix(f'.{export_format}'))
        
        print(f"📁 输出路径: {output_path}")
        
        # 执行转换
        print("🔄 开始转换...")
        try:
            # 根据不同格式设置参数
            export_args = {
                'format': export_format,
                'imgsz': imgsz,
                'optimize': True,  # 优化模型
                'half': False,     # 不使用半精度，保持兼容性
            }
            
            # 对于ONNX格式，添加额外参数
            if export_format == 'onnx':
                export_args.update({
                    'dynamic': False,  # 固定输入尺寸
                    'simplify': True,  # 简化ONNX图
                    'opset': 11,       # ONNX操作集版本
                })
            
            # 执行导出
            exported_path = model.export(**export_args)
            print(f"✅ 转换完成: {exported_path}")
            
            # 验证输出文件
            if os.path.exists(exported_path):
                file_size = os.path.getsize(exported_path)
                print(f"📄 文件大小: {file_size / 1024 / 1024:.1f} MB")
                
                # 检查文件是否有效
                if file_size < 1024:  # 小于1KB可能是无效文件
                    print("⚠️ 警告: 输出文件很小，可能转换失败")
                    return False
                
                return exported_path
            else:
                print("❌ 转换失败: 未找到输出文件")
                return False
                
        except Exception as e:
            print(f"❌ 转换过程中出错: {e}")
            traceback.print_exc()
            return False
            
    except Exception as e:
        print(f"❌ 转换失败: {e}")
        traceback.print_exc()
        return False

def validate_obb_model(model_path):
    """
    验证OBB模型
    """
    try:
        print(f"🔍 验证OBB模型: {model_path}")
        
        from ultralytics import YOLO
        model = YOLO(model_path)
        
        # 检查模型信息
        print(f"📋 模型任务: {getattr(model, 'task', '未知')}")
        print(f"📋 模型名称: {getattr(model, 'model_name', '未知')}")
        
        # 尝试获取类别信息
        if hasattr(model, 'names'):
            print(f"📋 类别数量: {len(model.names)}")
            print(f"📋 类别列表: {list(model.names.values())[:10]}...")  # 显示前10个类别
        
        print("✅ 模型验证通过")
        return True
        
    except Exception as e:
        print(f"❌ 模型验证失败: {e}")
        return False

def main():
    """主函数"""
    if len(sys.argv) < 2:
        print("用法: python convert_obb_model.py <input_model.pt> [output_path] [format] [imgsz]")
        print("示例: python convert_obb_model.py yolov8n-obb.pt model.onnx onnx 640")
        print("支持的格式: onnx, torchscript, coreml, tflite, pb")
        return False
    
    input_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) > 2 else None
    export_format = sys.argv[3] if len(sys.argv) > 3 else 'onnx'
    imgsz = int(sys.argv[4]) if len(sys.argv) > 4 else 640
    
    print("🤖 OBB模型转换工具")
    print("=" * 50)
    
    # 验证模型
    if not validate_obb_model(input_path):
        return False
    
    # 转换模型
    result = convert_obb_model(input_path, output_path, export_format, imgsz)
    
    if result:
        print("\n🎉 转换成功完成！")
        print(f"📄 输出文件: {result}")
        return True
    else:
        print("\n❌ 转换失败")
        return False

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n\n用户中断转换")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ 转换过程中出现异常: {e}")
        traceback.print_exc()
        sys.exit(1)
