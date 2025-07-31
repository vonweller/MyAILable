namespace AIlable.Models;

public enum AnnotationType
{
    Rectangle,
    Polygon,
    Circle,
    Line,
    Point,
    Polyline,
    Ellipse,
    OrientedBoundingBox  // 有向边界框 (OBB)
}

public enum AnnotationState
{
    Normal,
    Selected,
    Editing,
    Creating
}

public enum ExportFormat
{
    COCO,
    VOC,
    YOLO,
    DOTA,
    MOT,
    MASK,
    PPOCR,
    JSON,
    TXT
}