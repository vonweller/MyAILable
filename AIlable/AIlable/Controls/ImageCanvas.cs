using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AIlable.Models;
using AIlable.Services;

namespace AIlable.Controls;

public class ImageCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> ImageProperty =
        AvaloniaProperty.Register<ImageCanvas, Bitmap?>(nameof(Image));

    public static readonly StyledProperty<ObservableCollection<Annotation>?> AnnotationsProperty =
        AvaloniaProperty.Register<ImageCanvas, ObservableCollection<Annotation>?>(nameof(Annotations));

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<ImageCanvas, double>(nameof(ZoomFactor), 1.0);

    public static readonly StyledProperty<Point> PanOffsetProperty =
        AvaloniaProperty.Register<ImageCanvas, Point>(nameof(PanOffset), new Point(0, 0));

    public static readonly StyledProperty<bool> ShowAnnotationsProperty =
        AvaloniaProperty.Register<ImageCanvas, bool>(nameof(ShowAnnotations), true);

    public static readonly StyledProperty<Annotation?> SelectedAnnotationProperty =
        AvaloniaProperty.Register<ImageCanvas, Annotation?>(nameof(SelectedAnnotation),
            coerce: OnSelectedAnnotationCoerce);

    private static Annotation? OnSelectedAnnotationCoerce(AvaloniaObject obj, Annotation? value)
    {
        if (obj is ImageCanvas canvas)
        {
            // 清除所有标注的选中状态
            if (canvas.Annotations != null)
            {
                foreach (var annotation in canvas.Annotations)
                {
                    annotation.IsSelected = false;
                }
            }
            
            // 设置新选中标注的状态
            if (value != null)
            {
                value.IsSelected = true;
            }
            
            // 触发重绘
            canvas.InvalidateVisual();
        }
        return value;
    }

    public static readonly StyledProperty<Annotation?> CurrentDrawingAnnotationProperty =
        AvaloniaProperty.Register<ImageCanvas, Annotation?>(nameof(CurrentDrawingAnnotation),
            coerce: OnCurrentDrawingAnnotationCoerce);

    private static Annotation? OnCurrentDrawingAnnotationCoerce(AvaloniaObject obj, Annotation? value)
    {
        if (obj is ImageCanvas canvas)
        {
            // Trigger invalidation when CurrentDrawingAnnotation changes
            canvas.InvalidateVisual();
        }
        return value;
    }

    private Point _lastPointerPosition;
    private bool _isPanning;
    private bool _isPointerPressed;
    private bool _isRotatingOBB = false;
    private OrientedBoundingBoxAnnotation? _rotatingOBB = null;
    private OrientedBoundingBoxTool? _obbTool = null;

    public Bitmap? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public ObservableCollection<Annotation>? Annotations
    {
        get => GetValue(AnnotationsProperty);
        set => SetValue(AnnotationsProperty, value);
    }

    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, Math.Max(0.1, Math.Min(10.0, value)));
    }

    public Point PanOffset
    {
        get => GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    public bool ShowAnnotations
    {
        get => GetValue(ShowAnnotationsProperty);
        set => SetValue(ShowAnnotationsProperty, value);
    }

    public Annotation? SelectedAnnotation
    {
        get => GetValue(SelectedAnnotationProperty);
        set => SetValue(SelectedAnnotationProperty, value);
    }

    public Annotation? CurrentDrawingAnnotation
    {
        get => GetValue(CurrentDrawingAnnotationProperty);
        set => SetValue(CurrentDrawingAnnotationProperty, value);
    }

    // Events for view interaction
    public event System.Action? FitToWindowRequested;
    public event System.Action? ResetViewRequested;
    public event EventHandler<Point2D>? PointerClickedOnImage;
    public event EventHandler<Point2D>? PointerMovedOnImage;
    public event EventHandler<Annotation>? AnnotationSelected;
    
    // 新增：获取工具管理器的事件
    public event Func<OrientedBoundingBoxTool?>? GetOBBToolRequested;

    static ImageCanvas()
    {
        AffectsRender<ImageCanvas>(ImageProperty, AnnotationsProperty, ZoomFactorProperty, 
            PanOffsetProperty, ShowAnnotationsProperty, SelectedAnnotationProperty, CurrentDrawingAnnotationProperty);
    }

    public ImageCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Image == null) return;

        // Calculate the scaled image size
        var imageSize = new Size(Image.Size.Width * ZoomFactor, Image.Size.Height * ZoomFactor);
        var imageRect = new Rect(PanOffset, imageSize);

        // Draw the image
        context.DrawImage(Image, imageRect);

        // Draw saved annotations if enabled
        if (ShowAnnotations && Annotations != null)
        {
            foreach (var annotation in Annotations)
            {
                if (annotation.IsVisible)
                {
                    DrawAnnotation(context, annotation, imageRect);
                }
            }
        }

        // Draw current drawing annotation
        if (CurrentDrawingAnnotation != null && CurrentDrawingAnnotation.IsVisible)
        {
            DrawAnnotation(context, CurrentDrawingAnnotation, imageRect, true);
        }
    }

    private void DrawAnnotation(DrawingContext context, Annotation annotation, Rect imageRect, bool isDrawing = false)
    {
        var color = ParseColor(annotation.Color);
        var strokeBrush = new SolidColorBrush(color);
        var pen = new Pen(strokeBrush, annotation.StrokeWidth);
        
        // Use different opacity for drawing annotation
        if (isDrawing)
        {
            strokeBrush = new SolidColorBrush(color) { Opacity = 0.7 };
            pen = new Pen(strokeBrush, annotation.StrokeWidth);
        }
        
        var brush = annotation.IsSelected ? 
            new SolidColorBrush(Color.FromArgb(64, color.R, color.G, color.B)) : null;

        // 绘制标注几何形状
        switch (annotation)
        {
            case RectangleAnnotation rect:
                DrawRectangle(context, rect, imageRect, pen, brush);
                break;
            case CircleAnnotation circle:
                DrawCircle(context, circle, imageRect, pen, brush);
                break;
            case LineAnnotation line:
                DrawLine(context, line, imageRect, pen);
                break;
            case PointAnnotation point:
                DrawPoint(context, point, imageRect, pen, strokeBrush);
                break;
            case PolygonAnnotation polygon:
                DrawPolygon(context, polygon, imageRect, pen, brush);
                break;
            case OrientedBoundingBoxAnnotation obb:
                DrawOrientedBoundingBox(context, obb, imageRect, pen, brush, isDrawing);
                break;
            case KeypointAnnotation keypoint:
                DrawKeypoints(context, keypoint, imageRect, pen, brush, isDrawing);
                break;
        }
        
        // 绘制标签文本
        DrawAnnotationLabel(context, annotation, imageRect);
    }
    
    private void DrawAnnotationLabel(DrawingContext context, Annotation annotation, Rect imageRect)
    {
        if (string.IsNullOrEmpty(annotation.Label)) return;
        
        // 获取标注的中心点或顶部位置
        var labelPosition = GetLabelPosition(annotation, imageRect);
        
        // 创建文本
        var typeface = new Typeface("Microsoft YaHei", FontStyle.Normal, FontWeight.Normal);
        var formattedText = new FormattedText(annotation.Label, 
            System.Globalization.CultureInfo.CurrentCulture, 
            FlowDirection.LeftToRight, 
            typeface, 
            12, 
            new SolidColorBrush(Colors.White));
        
        // 计算背景矩形 - 使用固定大小代替动态测量
        var textWidth = annotation.Label.Length * 8; // 估算文本宽度
        var textHeight = 14; // 固定文本高度
        var backgroundRect = new Rect(
            labelPosition.X - 2, 
            labelPosition.Y - 2, 
            textWidth + 4, 
            textHeight + 4);
        
        // 绘制半透明背景
        var backgroundColor = ParseColor(annotation.Color);
        var backgroundBrush = new SolidColorBrush(Color.FromArgb(180, backgroundColor.R, backgroundColor.G, backgroundColor.B));
        context.DrawRectangle(backgroundBrush, null, backgroundRect);
        
        // 绘制文本
        context.DrawText(formattedText, labelPosition);
    }
    
    private Point GetLabelPosition(Annotation annotation, Rect imageRect)
    {
        var center = ImageToScreen(annotation.GetCenter(), imageRect);
        
        // 根据标注类型调整标签位置
        switch (annotation)
        {
            case RectangleAnnotation rect:
                var topLeft = ImageToScreen(rect.TopLeft, imageRect);
                return new Point(topLeft.X, topLeft.Y - 18); // 矩形上方
            case CircleAnnotation circle:
                var radius = circle.Radius * ZoomFactor;
                return new Point(center.X - 20, center.Y - radius - 18); // 圆形上方
            case OrientedBoundingBoxAnnotation obb:
                // OBB标签显示在旋转后边界框的上方
                var obbPoints = obb.GetPoints();
                if (obbPoints.Count >= 4)
                {
                    var topY = obbPoints.Min(p => ImageToScreen(p, imageRect).Y);
                    return new Point(center.X - 20, topY - 18);
                }
                return new Point(center.X - 20, center.Y - 18);
            default:
                return new Point(center.X - 20, center.Y - 18); // 默认位置
        }
    }

    private void DrawRectangle(DrawingContext context, RectangleAnnotation rect, Rect imageRect, Pen pen, IBrush? brush)
    {
        var topLeft = ImageToScreen(rect.TopLeft, imageRect);
        var bottomRight = ImageToScreen(rect.BottomRight, imageRect);
        var drawRect = new Rect(topLeft, bottomRight);

        if (brush != null)
            context.DrawRectangle(brush, pen, drawRect);
        else
            context.DrawRectangle(null, pen, drawRect);
    }

    private void DrawCircle(DrawingContext context, CircleAnnotation circle, Rect imageRect, Pen pen, IBrush? brush)
    {
        var center = ImageToScreen(circle.Center, imageRect);
        var radius = circle.Radius * ZoomFactor;
        var ellipse = new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));

        context.DrawGeometry(brush, pen, ellipse);
    }

    private void DrawLine(DrawingContext context, LineAnnotation line, Rect imageRect, Pen pen)
    {
        var start = ImageToScreen(line.StartPoint, imageRect);
        var end = ImageToScreen(line.EndPoint, imageRect);
        
        // 如果起点和终点相同（刚开始绘制），绘制一个小点作为视觉提示
        if (Math.Abs(start.X - end.X) < 2 && Math.Abs(start.Y - end.Y) < 2)
        {
            // 绘制一个小圆点表示线条起点
            var pointRadius = 3.0;
            var pointRect = new Rect(start.X - pointRadius, start.Y - pointRadius, pointRadius * 2, pointRadius * 2);
            
            // 安全地获取画笔颜色
            var color = pen.Brush is SolidColorBrush solidBrush ? solidBrush.Color : Colors.Blue;
            var pointBrush = new SolidColorBrush(color);
            context.DrawEllipse(pointBrush, pen, pointRect);
        }
        else
        {
            context.DrawLine(pen, start, end);
        }
    }

    private void DrawPoint(DrawingContext context, PointAnnotation point, Rect imageRect, Pen pen, IBrush brush)
    {
        var center = ImageToScreen(point.Position, imageRect);
        var radius = point.Size / 2 * ZoomFactor;
        var ellipse = new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));

        context.DrawGeometry(brush, pen, ellipse);
    }

    private void DrawPolygon(DrawingContext context, PolygonAnnotation polygon, Rect imageRect, Pen pen, IBrush? brush)
    {
        if (polygon.Vertices.Count < 1) return;

        var points = new List<Point>();
        foreach (var vertex in polygon.Vertices)
        {
            points.Add(ImageToScreen(vertex, imageRect));
        }

        // Draw the polygon if we have at least 2 points
        if (points.Count >= 2)
        {
            var geometry = new PolylineGeometry(points, polygon.IsClosed);
            context.DrawGeometry(brush, pen, geometry);
        }

        // Draw preview line for polygon being drawn
        if (polygon == CurrentDrawingAnnotation && polygon.PreviewEndPoint.HasValue && points.Count > 0)
        {
            var previewEndScreen = ImageToScreen(polygon.PreviewEndPoint.Value, imageRect);
            var lastPoint = points[points.Count - 1];

            // Draw preview line from last vertex to current mouse position
            var previewPen = new Pen(pen.Brush, pen.Thickness) { DashStyle = DashStyle.Dash };
            context.DrawLine(previewPen, lastPoint, previewEndScreen);

            // Also draw line from preview point back to first vertex if we have enough points
            if (points.Count >= 2)
            {
                var firstPoint = points[0];
                var deltaX = previewEndScreen.X - firstPoint.X;
                var deltaY = previewEndScreen.Y - firstPoint.Y;
                var distanceToFirst = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // If close to first point, highlight the closing line
                if (distanceToFirst < 15) // 15 pixels threshold
                {
                    var closingPen = new Pen(new SolidColorBrush(Colors.LimeGreen), pen.Thickness + 1);
                    context.DrawLine(closingPen, previewEndScreen, firstPoint);
                }
            }
        }

        // Draw vertices as small circles for easier editing
        if (polygon.IsSelected || polygon == CurrentDrawingAnnotation)
        {
            var vertexBrush = new SolidColorBrush(Colors.White);
            var vertexPen = new Pen(new SolidColorBrush(Colors.Black), 1);

            foreach (var screenPoint in points)
            {
                var vertexRect = new Rect(screenPoint.X - 3, screenPoint.Y - 3, 6, 6);
                context.DrawEllipse(vertexBrush, vertexPen, vertexRect);
            }

            // Highlight first vertex when drawing
            if (polygon == CurrentDrawingAnnotation && points.Count > 0)
            {
                var firstVertexBrush = new SolidColorBrush(Colors.LimeGreen);
                var firstVertexRect = new Rect(points[0].X - 4, points[0].Y - 4, 8, 8);
                context.DrawEllipse(firstVertexBrush, vertexPen, firstVertexRect);
            }
        }
    }

    private void DrawOrientedBoundingBox(DrawingContext context, OrientedBoundingBoxAnnotation obb, Rect imageRect, Pen pen, IBrush? brush, bool isDrawing = false)
    {
        var obbPoints = obb.GetPoints();
        if (obbPoints.Count < 4) return;

        // Convert OBB points to screen coordinates
        var screenPoints = new List<Point>();
        foreach (var point in obbPoints)
        {
            screenPoints.Add(ImageToScreen(point, imageRect));
        }

        // Draw the rotated rectangle
        var geometry = new PolylineGeometry(screenPoints, true);
        context.DrawGeometry(brush, pen, geometry);

        // Draw corner control points and rotation handle when selected or being drawn
        if (obb.IsSelected || obb == CurrentDrawingAnnotation || isDrawing)
        {
            // Draw corner control points
            var cornerBrush = new SolidColorBrush(Colors.White);
            var cornerPen = new Pen(new SolidColorBrush(Colors.Black), 1);

            foreach (var screenPoint in screenPoints)
            {
                var cornerRect = new Rect(screenPoint.X - 4, screenPoint.Y - 4, 8, 8);
                context.DrawEllipse(cornerBrush, cornerPen, cornerRect);
            }

            // Draw rotation handle - a small circle above the center
            var center = ImageToScreen(obb.GetCenter(), imageRect);
            var rotationHandleOffset = 30; // Distance above center
            var rotationHandlePoint = new Point(center.X, center.Y - rotationHandleOffset);
            
            // Draw line from center to rotation handle
            var handleLinePen = new Pen(pen.Brush, 1) { DashStyle = DashStyle.Dash };
            context.DrawLine(handleLinePen, center, rotationHandlePoint);
            
            // Draw rotation handle circle
            var rotationHandleBrush = new SolidColorBrush(Colors.LightBlue);
            var rotationHandlePen = new Pen(new SolidColorBrush(Colors.Blue), 2);
            var rotationHandleRect = new Rect(rotationHandlePoint.X - 6, rotationHandlePoint.Y - 6, 12, 12);
            context.DrawEllipse(rotationHandleBrush, rotationHandlePen, rotationHandleRect);

            // Draw center point
            var centerBrush = new SolidColorBrush(Colors.Red);
            var centerPen = new Pen(new SolidColorBrush(Colors.DarkRed), 1);
            var centerRect = new Rect(center.X - 3, center.Y - 3, 6, 6);
            context.DrawEllipse(centerBrush, centerPen, centerRect);

            // Draw angle display text
            var angleText = $"{obb.Angle:F1}°";
            var typeface = new Typeface("Microsoft YaHei", FontStyle.Normal, FontWeight.Bold);
            var formattedAngleText = new FormattedText(
                angleText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                12,
                new SolidColorBrush(Colors.White));

            // Position angle text near the rotation handle
            var angleTextPosition = new Point(rotationHandlePoint.X + 15, rotationHandlePoint.Y - 8);
            
            // Draw semi-transparent background for angle text
            var angleTextBackground = new Rect(
                angleTextPosition.X - 2,
                angleTextPosition.Y - 2,
                angleText.Length * 7 + 4,
                16);
            var angleBgBrush = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
            context.DrawRectangle(angleBgBrush, null, angleTextBackground);
            
            // Draw angle text
            context.DrawText(formattedAngleText, angleTextPosition);
        }
    }

    private void DrawKeypoints(DrawingContext context, KeypointAnnotation keypoint, Rect imageRect, Pen pen, IBrush? brush, bool isDrawing = false)
    {
        // 绘制骨骼连接线（如果启用）
        if (keypoint.ShowSkeleton)
        {
            DrawSkeleton(context, keypoint, imageRect, pen);
        }

        // 绘制关键点
        foreach (var kp in keypoint.Keypoints)
        {
            if (kp.Visibility == KeypointVisibility.NotAnnotated) continue;

            var screenPos = ImageToScreen(kp.Position, imageRect);
            var radius = 4.0; // 关键点半径

            // 根据可见性状态选择颜色
            IBrush keypointBrush;
            Pen keypointPen;

            switch (kp.Visibility)
            {
                case KeypointVisibility.Visible:
                    keypointBrush = new SolidColorBrush(Colors.LimeGreen);
                    keypointPen = new Pen(new SolidColorBrush(Colors.DarkGreen), 2);
                    break;
                case KeypointVisibility.Occluded:
                    keypointBrush = new SolidColorBrush(Colors.Orange);
                    keypointPen = new Pen(new SolidColorBrush(Colors.DarkOrange), 2);
                    break;
                default:
                    continue;
            }

            // 绘制关键点圆圈
            var keypointRect = new Rect(screenPos.X - radius, screenPos.Y - radius, radius * 2, radius * 2);
            context.DrawEllipse(keypointBrush, keypointPen, keypointRect);

            // 在选中或绘制状态下显示关键点编号
            if (keypoint.IsSelected || keypoint == CurrentDrawingAnnotation || isDrawing)
            {
                var typeface = new Typeface("Arial", FontStyle.Normal, FontWeight.Bold);
                var text = new FormattedText(kp.Id.ToString(), 
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    new SolidColorBrush(Colors.White));

                var textPos = new Point(screenPos.X - 5, screenPos.Y - 15);
                context.DrawText(text, textPos);
            }
        }
    }

    private void DrawSkeleton(DrawingContext context, KeypointAnnotation keypoint, Rect imageRect, Pen pen)
    {
        // COCO骨骼连接定义（调整为基于0的索引）
        var skeleton = new int[,]
        {
            {15, 13}, {13, 11}, {16, 14}, {14, 12}, {11, 12},  // 腿部
            {5, 11}, {6, 12}, {5, 6},                          // 躯干
            {5, 7}, {6, 8}, {7, 9}, {8, 10},                  // 手臂
            {1, 2}, {0, 1}, {0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}  // 头部
        };

        var penBrush = pen.Brush as SolidColorBrush ?? new SolidColorBrush(Colors.Blue);
        var skeletonPen = new Pen(new SolidColorBrush(penBrush.Color) { Opacity = 0.7 }, 2);

        for (int i = 0; i < skeleton.GetLength(0); i++)
        {
            var point1Index = skeleton[i, 0];
            var point2Index = skeleton[i, 1];

            if (point1Index < keypoint.Keypoints.Count && point2Index < keypoint.Keypoints.Count)
            {
                var kp1 = keypoint.Keypoints[point1Index];
                var kp2 = keypoint.Keypoints[point2Index];

                // 只有当两个关键点都可见时才绘制连接线
                if (kp1.Visibility == KeypointVisibility.Visible && kp2.Visibility == KeypointVisibility.Visible)
                {
                    var screenPos1 = ImageToScreen(kp1.Position, imageRect);
                    var screenPos2 = ImageToScreen(kp2.Position, imageRect);

                    context.DrawLine(skeletonPen, screenPos1, screenPos2);
                }
            }
        }
    }

    private Point ImageToScreen(Point2D imagePoint, Rect imageRect)
    {
        return new Point(
            imageRect.X + imagePoint.X * ZoomFactor,
            imageRect.Y + imagePoint.Y * ZoomFactor
        );
    }

    private Point2D ScreenToImage(Point screenPoint, Rect imageRect)
    {
        return new Point2D(
            (screenPoint.X - imageRect.X) / ZoomFactor,
            (screenPoint.Y - imageRect.Y) / ZoomFactor
        );
    }

    private static Color ParseColor(string colorString)
    {
        try
        {
            return Color.Parse(colorString);
        }
        catch
        {
            return Colors.Red;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var position = e.GetPosition(this);
        _lastPointerPosition = position;
        _isPointerPressed = true;

        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }
        else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            HandleLeftClick(position);
        }

        e.Handled = true;
        Focus();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_isPointerPressed)
        {
            var delta = position - _lastPointerPosition;

            if (_isPanning)
            {
                PanOffset = new Point(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
                InvalidateVisual();
            }
            else if (_isRotatingOBB && _rotatingOBB != null && _obbTool != null)
            {
                // 处理OBB旋转
                if (Image != null)
                {
                    var imageRect = new Rect(PanOffset, new Size(Image.Size.Width * ZoomFactor, Image.Size.Height * ZoomFactor));
                    var imagePoint = ScreenToImage(position, imageRect);
                    _obbTool.UpdateRotation(_rotatingOBB, imagePoint);
                    InvalidateVisual();
                }
            }
        }

        // Always notify about mouse movement for drawing tools
        if (Image != null)
        {
            var imageRect = new Rect(PanOffset, new Size(Image.Size.Width * ZoomFactor, Image.Size.Height * ZoomFactor));
            if (imageRect.Contains(position))
            {
                var imagePoint = ScreenToImage(position, imageRect);
                PointerMovedOnImage?.Invoke(this, imagePoint);
            }
        }

        _lastPointerPosition = position;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _isPointerPressed = false;
        _isPanning = false;
        
        // 结束OBB旋转操作
        if (_isRotatingOBB && _obbTool != null)
        {
            _obbTool.EndRotation();
            _isRotatingOBB = false;
            _rotatingOBB = null;
        }
        
        Cursor = Cursor.Default;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var delta = e.Delta.Y;
        var zoomFactor = delta > 0 ? 1.1 : 0.9;
        
        var pointerPosition = e.GetPosition(this);
        ZoomAt(pointerPosition, zoomFactor);

        e.Handled = true;
    }

    private void HandleLeftClick(Point position)
    {
        if (Image == null) return;

        var imageRect = new Rect(PanOffset, new Size(Image.Size.Width * ZoomFactor, Image.Size.Height * ZoomFactor));
        
        // Check if click is within image bounds
        if (!imageRect.Contains(position)) return;

        var imagePoint = ScreenToImage(position, imageRect);
        
        // 首先检查是否点击了OBB或关键点的特殊控制点
        if (HandleSpecialInteraction(imagePoint))
        {
            return;
        }
        
        // Check for annotation selection first
        var clickedAnnotation = FindAnnotationAtPoint(imagePoint);
        if (clickedAnnotation != null)
        {
            SelectedAnnotation = clickedAnnotation;
            AnnotationSelected?.Invoke(this, clickedAnnotation);
        }
        else
        {
            SelectedAnnotation = null;
        }

        // Always notify about the click for drawing tools
        PointerClickedOnImage?.Invoke(this, imagePoint);

        InvalidateVisual();
    }

    /// <summary>
    /// 处理OBB旋转和关键点拖拽等特殊交互
    /// </summary>
    private bool HandleSpecialInteraction(Point2D imagePoint)
    {
        if (Annotations == null) return false;

        // 检查是否点击了OBB的旋转控制柄
        foreach (var annotation in Annotations)
        {
            if (annotation is OrientedBoundingBoxAnnotation obb && obb.IsSelected)
            {
                // 获取OBB工具
                _obbTool = GetOBBToolRequested?.Invoke();
                if (_obbTool != null)
                {
                    // 检查是否点击了旋转控制柄
                    if (_obbTool.IsPointOnRotationHandle(obb, imagePoint, ZoomFactor))
                    {
                        // 开始旋转操作
                        _isRotatingOBB = true;
                        _rotatingOBB = obb;
                        _obbTool.StartRotation(obb, imagePoint);
                        Cursor = new Cursor(StandardCursorType.Hand);
                        return true;
                    }
                    
                    // 检查是否点击了角点控制柄
                    var cornerIndex = _obbTool.GetCornerHandleIndex(obb, imagePoint, ZoomFactor);
                    if (cornerIndex >= 0)
                    {
                        // 这里可以添加角点拖拽调整大小的逻辑
                        // 暂时不实现，专注于旋转功能
                        return true;
                    }
                }
            }
            else if (annotation is KeypointAnnotation keypoint && keypoint.IsSelected) 
            {
                // 检查是否点击了关键点
                var nearestKeypoint = keypoint.GetNearestKeypoint(imagePoint);
                if (nearestKeypoint != null)
                {
                    // 开始拖拽关键点
                    // 这里需要设置拖拽状态
                    return true;
                }
            }
        }

        return false;
    }

    private Annotation? FindAnnotationAtPoint(Point2D point)
    {
        if (Annotations == null) return null;

        // Check in reverse order to prioritize top-most annotations
        for (int i = Annotations.Count - 1; i >= 0; i--)
        {
            var annotation = Annotations[i];
            if (annotation.IsVisible && annotation.ContainsPoint(point))
            {
                return annotation;
            }
        }

        return null;
    }

    private void ZoomAt(Point center, double factor)
    {
        var newZoom = ZoomFactor * factor;
        if (newZoom < 0.1 || newZoom > 10.0) return;

        var deltaZoom = newZoom / ZoomFactor;
        var newOffset = new Point(
            center.X - (center.X - PanOffset.X) * deltaZoom,
            center.Y - (center.Y - PanOffset.Y) * deltaZoom
        );

        ZoomFactor = newZoom;
        PanOffset = newOffset;
        InvalidateVisual();
    }

    public void FitToWindow()
    {
        if (Image == null || Bounds.Width == 0 || Bounds.Height == 0) return;

        var scale = ImageService.CalculateFitToWindowScale(Image.Size, Bounds.Size);
        ZoomFactor = scale;
        
        var scaledSize = new Size(Image.Size.Width * scale, Image.Size.Height * scale);
        PanOffset = new Point(
            (Bounds.Width - scaledSize.Width) / 2,
            (Bounds.Height - scaledSize.Height) / 2
        );

        InvalidateVisual();
    }

    public void ResetView()
    {
        ZoomFactor = 1.0;
        PanOffset = new Point(0, 0);
        InvalidateVisual();
    }
}