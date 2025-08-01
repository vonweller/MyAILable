using System;
using AIlable.Models;

namespace AIlable.Services;

/// <summary>
/// 关键点姿态标注工具
/// 支持人体姿态的17个关键点标注
/// </summary>
public class KeypointTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Keypoint;
    public override string Name => "姿态标注工具";
    public override string Description => "绘制人体姿态关键点标注";

    private KeypointAnnotation? _currentKeypointAnnotation;
    private Keypoint? _selectedKeypoint;
    private bool _isDraggingKeypoint = false;

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        _currentKeypointAnnotation = new KeypointAnnotation(startPoint)
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
        return _currentKeypointAnnotation;
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        // 姿态标注的创建逻辑在CreateAnnotation中完成
        // 这里主要用于实时预览或拖拽操作
        if (annotation is KeypointAnnotation keypoint && _isDraggingKeypoint && _selectedKeypoint != null)
        {
            _selectedKeypoint.Position = currentPoint;
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is KeypointAnnotation keypoint)
        {
            // 至少需要有一个可见的关键点
            return keypoint.Keypoints.Exists(k => k.Visibility != KeypointVisibility.NotAnnotated);
        }
        return false;
    }

    /// <summary>
    /// 处理关键点点击选择
    /// </summary>
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
    /// 切换关键点可见性状态
    /// </summary>
    public void ToggleKeypointVisibility(Keypoint keypoint)
    {
        keypoint.Visibility = keypoint.Visibility switch
        {
            KeypointVisibility.NotAnnotated => KeypointVisibility.Visible,
            KeypointVisibility.Visible => KeypointVisibility.Occluded,
            KeypointVisibility.Occluded => KeypointVisibility.NotAnnotated,
            _ => KeypointVisibility.Visible
        };
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
    /// 检查是否正在拖拽关键点
    /// </summary>
    public bool IsDraggingKeypoint()
    {
        return _isDraggingKeypoint;
    }
}