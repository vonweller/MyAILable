using System;
using System.Numerics;

namespace AIlable.Models;

public struct Point2D : IEquatable<Point2D>
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Point2D Zero => new(0, 0);

    public static Point2D operator +(Point2D left, Point2D right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Point2D operator -(Point2D left, Point2D right)
        => new(left.X - right.X, left.Y - right.Y);

    public static Point2D operator *(Point2D point, double scalar)
        => new(point.X * scalar, point.Y * scalar);

    public double DistanceTo(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public bool Equals(Point2D other)
        => Math.Abs(X - other.X) < double.Epsilon && Math.Abs(Y - other.Y) < double.Epsilon;

    public override bool Equals(object? obj)
        => obj is Point2D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y);

    public override string ToString()
        => $"({X:F2}, {Y:F2})";
}