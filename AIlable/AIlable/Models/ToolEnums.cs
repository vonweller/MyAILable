namespace AIlable.Models;

public enum AnnotationTool
{
    Select,
    Rectangle,
    Polygon,
    Circle,
    Line,
    Point,
    OrientedBoundingBox,  // 有向边界框工具
    Keypoint,            // 关键点姿态工具
    Pan,
    Zoom
}

public enum DrawingState
{
    None,
    Drawing,
    Editing,
    Moving,
    Resizing
}