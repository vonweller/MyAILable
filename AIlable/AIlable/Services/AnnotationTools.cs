using System;
using System.Collections.Generic;
using AIlable.Models;

namespace AIlable.Services;

public abstract class AnnotationTool
{
    public abstract AnnotationType AnnotationType { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }

    protected Point2D? _startPoint;
    protected Point2D? _currentPoint;
    protected bool _isDrawing;
    protected Annotation? _currentAnnotation;

    // 当前标签和颜色
    public string CurrentLabel { get; set; } = "object";
    public string CurrentColor { get; set; } = "#FF0000";

    public virtual void StartDrawing(Point2D point)
    {
        _startPoint = point;
        _currentPoint = point;
        _isDrawing = true;
        _currentAnnotation = CreateAnnotation(point);
    }

    public virtual void UpdateDrawing(Point2D point)
    {
        if (!_isDrawing || _currentAnnotation == null) return;
        
        _currentPoint = point;
        UpdateAnnotation(_currentAnnotation, point);
    }

    public virtual Annotation? FinishDrawing(Point2D point)
    {
        if (!_isDrawing || _currentAnnotation == null) return null;
        
        _currentPoint = point;
        UpdateAnnotation(_currentAnnotation, point);
        
        var result = ValidateAndFinalize(_currentAnnotation) ? _currentAnnotation : null;
        
        _isDrawing = false;
        _currentAnnotation = null;
        _startPoint = null;
        _currentPoint = null;
        
        return result;
    }

    public virtual void CancelDrawing()
    {
        _isDrawing = false;
        _currentAnnotation = null;
        _startPoint = null;
        _currentPoint = null;
    }

    public bool IsDrawing => _isDrawing;
    public Annotation? CurrentAnnotation => _currentAnnotation;

    protected abstract Annotation CreateAnnotation(Point2D startPoint);
    protected abstract void UpdateAnnotation(Annotation annotation, Point2D currentPoint);
    protected virtual bool ValidateAndFinalize(Annotation annotation) => true;
}

public class RectangleTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Rectangle;
    public override string Name => "矩形工具";
    public override string Description => "绘制矩形标注";

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        return new RectangleAnnotation(startPoint, startPoint)
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        if (annotation is RectangleAnnotation rect && _startPoint.HasValue)
        {
            var topLeft = new Point2D(
                Math.Min(_startPoint.Value.X, currentPoint.X),
                Math.Min(_startPoint.Value.Y, currentPoint.Y)
            );
            
            var bottomRight = new Point2D(
                Math.Max(_startPoint.Value.X, currentPoint.X),
                Math.Max(_startPoint.Value.Y, currentPoint.Y)
            );

            rect.TopLeft = topLeft;
            rect.BottomRight = bottomRight;
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is RectangleAnnotation rect)
        {
            return rect.Width > 5 && rect.Height > 5; // Minimum size validation
        }
        return false;
    }
}

public class CircleTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Circle;
    public override string Name => "圆形工具";
    public override string Description => "绘制圆形标注";

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        return new CircleAnnotation(startPoint, 0)
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        if (annotation is CircleAnnotation circle && _startPoint.HasValue)
        {
            var radius = _startPoint.Value.DistanceTo(currentPoint);
            circle.Radius = radius;
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is CircleAnnotation circle)
        {
            return circle.Radius > 5; // Minimum radius validation
        }
        return false;
    }
}

public class LineTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Line;
    public override string Name => "线条工具";
    public override string Description => "绘制直线标注";

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        // 创建线条时，结束点初始化为起始点，这样线条立即可见
        return new LineAnnotation(startPoint, startPoint)
        {
            Label = CurrentLabel,
            Color = CurrentColor,
            IsVisible = true  // 确保立即可见
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        if (annotation is LineAnnotation line && _startPoint.HasValue)
        {
            line.StartPoint = _startPoint.Value;
            line.EndPoint = currentPoint;
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is LineAnnotation line)
        {
            return line.Length > 5; // Minimum length validation
        }
        return false;
    }
}

public class PointTool : AnnotationTool
{
    public override AnnotationType AnnotationType => AnnotationType.Point;
    public override string Name => "点工具";
    public override string Description => "添加点标注";

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        return new PointAnnotation(startPoint)
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        // Points don't need updating during drawing
    }

    public override Annotation? FinishDrawing(Point2D point)
    {
        // For points, we create and return immediately
        var pointAnnotation = CreateAnnotation(point);
        _isDrawing = false;
        return pointAnnotation;
    }
}

public class PolygonTool : AnnotationTool
{
    private List<Point2D> _vertices = new();
    private bool _isCompleted;
    private DateTime _lastClickTime = DateTime.MinValue;
    private Point2D _lastClickPoint = Point2D.Zero;
    private const double DoubleClickThreshold = 500; // milliseconds
    private const double DoubleClickDistance = 10; // pixels

    public override AnnotationType AnnotationType => AnnotationType.Polygon;
    public override string Name => "多边形工具";
    public override string Description => "绘制多边形标注";

    public override void StartDrawing(Point2D point)
    {
        if (!_isDrawing)
        {
            // Start new polygon
            _vertices.Clear();
            _vertices.Add(point);
            _isDrawing = true;
            _isCompleted = false;
            _currentAnnotation = CreateAnnotation(point);
        }
        else
        {
            // Add vertex to existing polygon
            AddVertex(point);
        }
    }

    public void AddVertex(Point2D point)
    {
        if (_isDrawing && !_isCompleted)
        {
            _vertices.Add(point);
            if (_currentAnnotation is PolygonAnnotation polygon)
            {
                polygon.AddVertex(point);
            }
        }
    }

    public override Annotation? FinishDrawing(Point2D point)
    {
        if (!_isDrawing || _currentAnnotation == null) return null;

        var currentTime = DateTime.Now;
        var timeSinceLastClick = (currentTime - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = point.DistanceTo(_lastClickPoint);

        // Check for double-click to close polygon
        if (timeSinceLastClick < DoubleClickThreshold &&
            distanceFromLastClick < DoubleClickDistance &&
            _vertices.Count >= 3)
        {
            // Double-click detected, close the polygon
            _isCompleted = true;
            _isDrawing = false;

            var result = ValidateAndFinalize(_currentAnnotation) ? _currentAnnotation : null;

            // Reset for next polygon
            _vertices.Clear();
            _currentAnnotation = null;

            return result;
        }

        // Check if we're closing the polygon (clicking near first vertex)
        if (_vertices.Count >= 3)
        {
            var firstVertex = _vertices[0];
            var distance = point.DistanceTo(firstVertex);

            if (distance < 15) // Close polygon if within 15 pixels of start
            {
                _isCompleted = true;
                _isDrawing = false;

                var result = ValidateAndFinalize(_currentAnnotation) ? _currentAnnotation : null;

                // Reset for next polygon
                _vertices.Clear();
                _currentAnnotation = null;

                return result;
            }
        }

        // Update last click info for double-click detection
        _lastClickTime = currentTime;
        _lastClickPoint = point;

        return null; // Continue drawing
    }

    protected override Annotation CreateAnnotation(Point2D startPoint)
    {
        return new PolygonAnnotation(new[] { startPoint })
        {
            Label = CurrentLabel,
            Color = CurrentColor
        };
    }

    protected override void UpdateAnnotation(Annotation annotation, Point2D currentPoint)
    {
        // Update preview line for polygon
        if (annotation is PolygonAnnotation polygon && _isDrawing)
        {
            polygon.PreviewEndPoint = currentPoint;
        }
    }

    protected override bool ValidateAndFinalize(Annotation annotation)
    {
        if (annotation is PolygonAnnotation polygon)
        {
            return polygon.IsClosed && polygon.Vertices.Count >= 3;
        }
        return false;
    }

    public override void CancelDrawing()
    {
        base.CancelDrawing();
        _vertices.Clear();
        _isCompleted = false;
        _lastClickTime = DateTime.MinValue;
        _lastClickPoint = Point2D.Zero;
    }
}

public class ToolManager
{
    private readonly Dictionary<Models.AnnotationTool, AnnotationTool> _tools;
    private Models.AnnotationTool _activeTool;

    public ToolManager()
    {
        _tools = new Dictionary<Models.AnnotationTool, AnnotationTool>
        {
            { Models.AnnotationTool.Rectangle, new RectangleTool() },
            { Models.AnnotationTool.Circle, new CircleTool() },
            { Models.AnnotationTool.Line, new LineTool() },
            { Models.AnnotationTool.Point, new PointTool() },
            { Models.AnnotationTool.Polygon, new PolygonTool() },
            { Models.AnnotationTool.OrientedBoundingBox, new OrientedBoundingBoxTool() }
        };
        
        _activeTool = Models.AnnotationTool.Select;
    }

    public Models.AnnotationTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (_activeTool != value)
            {
                // Cancel any ongoing drawing
                if (_tools.ContainsKey(_activeTool))
                {
                    _tools[_activeTool].CancelDrawing();
                }
                
                _activeTool = value;
                ActiveToolChanged?.Invoke(_activeTool);
            }
        }
    }

    public AnnotationTool? GetActiveTool()
    {
        return _tools.ContainsKey(_activeTool) ? _tools[_activeTool] : null;
    }

    public AnnotationTool? GetTool(Models.AnnotationTool toolType)
    {
        return _tools.ContainsKey(toolType) ? _tools[toolType] : null;
    }

    public IEnumerable<Models.AnnotationTool> GetAvailableTools()
    {
        return _tools.Keys;
    }

    public event Action<Models.AnnotationTool>? ActiveToolChanged;

    /// <summary>
    /// 更新所有工具的当前标签和颜色
    /// </summary>
    /// <param name="label">当前标签</param>
    /// <param name="color">当前颜色</param>
    public void UpdateCurrentLabelAndColor(string label, string color)
    {
        foreach (var tool in _tools.Values)
        {
            tool.CurrentLabel = label;
            tool.CurrentColor = color;
        }
    }
}