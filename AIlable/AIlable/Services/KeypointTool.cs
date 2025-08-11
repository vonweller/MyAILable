using System;
using System.Collections.Generic;
using System.Linq;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 关键点姿态标注工具
/// 支持灵活的关键点标注：先框选人体，再逐个标记关键点并选择标签
/// 用户可以只标记需要的关键点（如只标3个点）
/// </summary>
public class KeypointTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Keypoint;
    public override string Name => "姿态标注工具";
    public override string Description => "先框选人体区域，再为每个关键点选择标签并标记位置";

    // 标注状态
    public enum KeypointAnnotationState
    {
        DrawingBoundingBox,  // 正在绘制边界框
        PlacingKeypoints     // 正在放置关键点
    }

    private KeypointAnnotation? _currentKeypointAnnotation;
    private Keypoint? _selectedKeypoint;
    private bool _isDraggingKeypoint = false;
    private KeypointAnnotationState _currentState = KeypointAnnotationState.DrawingBoundingBox;
    private Point2D _boundingBoxStart;
    private Point2D _boundingBoxEnd;
    
    // 当前选择的关键点标签（从标签下拉框获取）
    private string _currentKeypointLabel = "";

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        if (_currentState == KeypointAnnotationState.DrawingBoundingBox)
        {
            // 第一阶段：开始绘制边界框
            _boundingBoxStart = startPoint;
            _boundingBoxEnd = startPoint;
            
            // 创建姿态标注，边界框标签使用当前选择的标签
            _currentKeypointAnnotation = new KeypointAnnotation(startPoint)
            {
                Label = CurrentLabel,  // 人体边界框的标签（如"person"）
                Color = CurrentColor
            };
            
            // 立即设置边界框信息，确保HasBoundingBox为true
            _currentKeypointAnnotation.SetBoundingBox(_boundingBoxStart, _boundingBoxEnd);
            
            return _currentKeypointAnnotation;
        }
        
        return _currentKeypointAnnotation!; // 在关键点标记阶段返回现有标注
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        if (annotation is KeypointAnnotation keypoint)
        {
            if (_currentState == KeypointAnnotationState.DrawingBoundingBox)
            {
                // 更新边界框终点
                _boundingBoxEnd = currentPoint;
                keypoint.SetBoundingBox(_boundingBoxStart, currentPoint);
            }
            else if (_isDraggingKeypoint && _selectedKeypoint != null)
            {
                // 拖拽已有关键点
                _selectedKeypoint.Position = currentPoint;
            }
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is KeypointAnnotation keypoint)
        {
            if (_currentState == KeypointAnnotationState.DrawingBoundingBox)
            {
                // 完成边界框绘制，切换到关键点标记状态
                _currentState = KeypointAnnotationState.PlacingKeypoints;
                return false; // 返回false表示标注还未完成
            }
            else if (_currentState == KeypointAnnotationState.PlacingKeypoints)
            {
                // 关键点标记阶段，用户可以随时完成标注（按Enter或右键等）
                if (keypoint.Keypoints.Count > 0)
                {
                    // 确保边界框信息被正确保存到最终标注中
                    if (!keypoint.HasBoundingBox && (_boundingBoxStart.X != 0 || _boundingBoxStart.Y != 0 || _boundingBoxEnd.X != 0 || _boundingBoxEnd.Y != 0))
                    {
                        keypoint.SetBoundingBox(_boundingBoxStart, _boundingBoxEnd);
                    }
                    
                    // 重置状态为下次标注准备
                    _currentState = KeypointAnnotationState.DrawingBoundingBox;
                    return true; // 标注完成
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 在关键点标记阶段处理点击
    /// 使用当前选择的标签标记关键点
    /// </summary>
    public bool HandleKeypointPlacement(Point2D point, string keypointLabel)
    {
        if (_currentState == KeypointAnnotationState.PlacingKeypoints && 
            _currentKeypointAnnotation != null)
        {
            // 检查点击位置是否在边界框内
            if (IsPointInBoundingBox(point))
            {
                // 添加或更新关键点
                _currentKeypointAnnotation.AddKeypoint(point, keypointLabel);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除指定标签的关键点
    /// </summary>
    public bool RemoveKeypoint(string keypointLabel)
    {
        if (_currentKeypointAnnotation != null)
        {
            return _currentKeypointAnnotation.RemoveKeypoint(keypointLabel);
        }
        return false;
    }

    /// <summary>
    /// 检查点是否在边界框内
    /// </summary>
    private bool IsPointInBoundingBox(Point2D point)
    {
        var minX = Math.Min(_boundingBoxStart.X, _boundingBoxEnd.X);
        var maxX = Math.Max(_boundingBoxStart.X, _boundingBoxEnd.X);
        var minY = Math.Min(_boundingBoxStart.Y, _boundingBoxEnd.Y);
        var maxY = Math.Max(_boundingBoxStart.Y, _boundingBoxEnd.Y);
        
        return point.X >= minX && point.X <= maxX && 
               point.Y >= minY && point.Y <= maxY;
    }

    /// <summary>
    /// 完成当前关键点标注（确保数据完整性）
    /// </summary>
    public bool FinishCurrentAnnotation()
    {
        if (_currentState == KeypointAnnotationState.PlacingKeypoints && 
            _currentKeypointAnnotation != null)
        {
            if (_currentKeypointAnnotation.Keypoints.Count > 0)
            {
                // 确保边界框信息被正确保存
                if (!_currentKeypointAnnotation.HasBoundingBox)
                {
                    _currentKeypointAnnotation.SetBoundingBox(_boundingBoxStart, _boundingBoxEnd);
                }
                
                // 重置状态
                _currentState = KeypointAnnotationState.DrawingBoundingBox;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 取消当前标注
    /// </summary>
    public void CancelCurrentAnnotation()
    {
        _currentState = KeypointAnnotationState.DrawingBoundingBox;
        _currentKeypointAnnotation = null;
        _selectedKeypoint = null;
        _isDraggingKeypoint = false;
    }

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public KeypointAnnotationState GetCurrentState()
    {
        return _currentState;
    }

    /// <summary>
    /// 获取当前边界框（用于绘制）
    /// </summary>
    public (Point2D start, Point2D end) GetCurrentBoundingBox()
    {
        return (_boundingBoxStart, _boundingBoxEnd);
    }

    /// <summary>
    /// 获取已标记的关键点标签列表
    /// </summary>
    public List<string> GetMarkedKeypointLabels()
    {
        if (_currentKeypointAnnotation != null)
        {
            return _currentKeypointAnnotation.Keypoints.Select(k => k.Label).ToList();
        }
        return new List<string>();
    }
    public bool HandleKeypointClick(KeypointAnnotation annotation, Point2D point)
    {
        var nearestKeypoint = annotation.GetNearestKeypoint(point);
        if (nearestKeypoint != null)
        {
            _selectedKeypoint = nearestKeypoint;
            _currentKeypointAnnotation = annotation;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 开始拖拽关键点
    /// </summary>
    public void StartDraggingKeypoint(KeypointAnnotation annotation, Keypoint keypoint)
    {
        _currentKeypointAnnotation = annotation;
        _selectedKeypoint = keypoint;
        _isDraggingKeypoint = true;
    }

    /// <summary>
    /// 更新拖拽中的关键点位置
    /// </summary>
    public void UpdateDraggingKeypoint(Point2D newPosition)
    {
        if (_isDraggingKeypoint && _selectedKeypoint != null)
        {
            _selectedKeypoint.Position = newPosition;
        }
    }

    /// <summary>
    /// 结束拖拽关键点
    /// </summary>
    public void EndDraggingKeypoint()
    {
        _isDraggingKeypoint = false;
        _selectedKeypoint = null;
        _currentKeypointAnnotation = null;
    }

    /// <summary>
    /// 切换关键点可见性状态（学习自X-AnyLabeling的优秀实践）
    /// 右键点击关键点时调用：可见 -> 遮挡 -> 不标注 -> 可见
    /// </summary>
    public void ToggleKeypointVisibility(Keypoint keypoint)
    {
        if (_currentKeypointAnnotation != null)
        {
            _currentKeypointAnnotation.CycleKeypointVisibility(keypoint);
        }
        else
        {
            // 兼容其他情况的直接切换
            keypoint.Visibility = keypoint.Visibility switch
            {
                KeypointVisibility.NotAnnotated => KeypointVisibility.Visible,
                KeypointVisibility.Visible => KeypointVisibility.Occluded,
                KeypointVisibility.Occluded => KeypointVisibility.NotAnnotated,
                _ => KeypointVisibility.Visible
            };
        }
    }

    /// <summary>
    /// 重置关键点到初始位置
    /// </summary>
    public void ResetKeypointsToInitial(KeypointAnnotation annotation, Point2D centerPosition)
    {
        // 重新创建一个新的姿态标注以获取初始位置
        var tempAnnotation = new KeypointAnnotation(centerPosition);
        
        // 将初始位置复制到当前标注
        for (int i = 0; i < Math.Min(annotation.Keypoints.Count, tempAnnotation.Keypoints.Count); i++)
        {
            annotation.Keypoints[i].Position = tempAnnotation.Keypoints[i].Position;
            // 保持原有的可见性状态
        }
    }

    /// <summary>
    /// 自动连接关键点（基于COCO骨骼定义）
    /// </summary>
    public void AutoConnectKeypoints(KeypointAnnotation annotation)
    {
        // 这个方法可以用于自动调整关键点位置以形成合理的人体姿态
        // 实现可以包括：
        // 1. 检查对称性（左右肢体）
        // 2. 检查解剖学合理性（关节角度、肢体长度比例）
        // 3. 自动推断遮挡的关键点位置
        
        // 简单的对称性检查示例
        var center = annotation.GetCenter();
        
        // 如果一侧的关键点可见而另一侧不可见，可以根据对称性推断
        var keypointPairs = new (int left, int right)[]
        {
            (1, 2),   // 左眼, 右眼
            (3, 4),   // 左耳, 右耳
            (5, 6),   // 左肩, 右肩
            (7, 8),   // 左肘, 右肘
            (9, 10),  // 左腕, 右腕
            (11, 12), // 左髋, 右髋
            (13, 14), // 左膝, 右膝
            (15, 16)  // 左踝, 右踝
        };

        foreach (var (left, right) in keypointPairs)
        {
            if (left < annotation.Keypoints.Count && right < annotation.Keypoints.Count)
            {
                var leftKeypoint = annotation.Keypoints[left];
                var rightKeypoint = annotation.Keypoints[right];
                
                // 如果左侧可见而右侧不可见，根据对称性推断右侧位置
                if (leftKeypoint.Visibility == KeypointVisibility.Visible && 
                    rightKeypoint.Visibility == KeypointVisibility.NotAnnotated)
                {
                    var deltaX = leftKeypoint.Position.X - center.X;
                    rightKeypoint.Position = new Point2D(center.X - deltaX, leftKeypoint.Position.Y);
                    rightKeypoint.Visibility = KeypointVisibility.Visible;
                }
                // 反之亦然
                else if (rightKeypoint.Visibility == KeypointVisibility.Visible && 
                         leftKeypoint.Visibility == KeypointVisibility.NotAnnotated)
                {
                    var deltaX = rightKeypoint.Position.X - center.X;
                    leftKeypoint.Position = new Point2D(center.X - deltaX, rightKeypoint.Position.Y);
                    leftKeypoint.Visibility = KeypointVisibility.Visible;
                }
            }
        }
    }

    /// <summary>
    /// 获取当前选中的关键点
    /// </summary>
    public Keypoint? GetSelectedKeypoint()
    {
        return _selectedKeypoint;
    }

    /// <summary>
    /// 获取当前正在编辑的关键点标注
    /// </summary>
    public KeypointAnnotation? GetCurrentAnnotation()
    {
        return _currentKeypointAnnotation;
    }
    
    /// <summary>
    /// 检查是否正在拖拽关键点
    /// </summary>
    public bool IsDraggingKeypoint()
    {
        return _isDraggingKeypoint;
    }
    
    /// <summary>
    /// 创建标准COCO 17关键点预设（学习自X-AnyLabeling的标准化实践）
    /// </summary>
    public KeypointAnnotation CreateCocoPreset(Point2D boundingBoxStart, Point2D boundingBoxEnd, string label = "person")
    {
        var annotation = new KeypointAnnotation(boundingBoxStart)
        {
            Label = label,
            Color = CurrentColor
        };
        annotation.SetBoundingBox(boundingBoxStart, boundingBoxEnd);
        
        // 创建标准的COCO 17个关键点，初始状态为NotAnnotated
        var keypoints = new List<Keypoint>();
        for (int i = 0; i < KeypointAnnotation.CocoKeypointNames.Length; i++)
        {
            keypoints.Add(new Keypoint
            {
                Id = i,
                Label = KeypointAnnotation.CocoKeypointNames[i],
                Name = KeypointAnnotation.CocoKeypointNames[i],
                Position = new Point2D(0, 0), // 初始位置，用户可以后续放置
                Visibility = KeypointVisibility.NotAnnotated
            });
        }
        
        annotation.Keypoints.Clear();
        annotation.Keypoints.AddRange(keypoints);
        
        return annotation;
    }
    
    /// <summary>
    /// 获取当前可用的关键点标签列表（COCO标准）
    /// </summary>
    public List<string> GetAvailableKeypointLabels()
    {
        return KeypointAnnotation.CocoKeypointNames.ToList();
    }
    
    /// <summary>
    /// 导出COCO格式的关键点数据（兼容X-AnyLabeling格式）
    /// </summary>
    public Dictionary<string, object> ExportToCoco(KeypointAnnotation annotation)
    {
        var cocoData = new Dictionary<string, object>
        {
            ["keypoints"] = annotation.GetCocoKeypoints(),
            ["num_keypoints"] = annotation.Keypoints.Count(k => k.Visibility != KeypointVisibility.NotAnnotated),
            ["bbox"] = new double[] 
            { 
                Math.Min(annotation.BoundingBoxStart.X, annotation.BoundingBoxEnd.X),
                Math.Min(annotation.BoundingBoxStart.Y, annotation.BoundingBoxEnd.Y),
                Math.Abs(annotation.BoundingBoxEnd.X - annotation.BoundingBoxStart.X),
                Math.Abs(annotation.BoundingBoxEnd.Y - annotation.BoundingBoxStart.Y)
            },
            ["area"] = Math.Abs((annotation.BoundingBoxEnd.X - annotation.BoundingBoxStart.X) * 
                               (annotation.BoundingBoxEnd.Y - annotation.BoundingBoxStart.Y)),
            ["category_id"] = 1, // 人体类别
            ["iscrowd"] = 0
        };
        
        return cocoData;
    }
}