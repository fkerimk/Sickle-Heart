using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Sickle.Heart.Core;

public static class Util {

    public static Vector2 ScreenToWorld(Vector2 pos, Camera2D cam) => GetScreenToWorld2D(pos, cam);
    
    public static float Distance(Vector2 a, Vector2 b) => Raymath.Vector2Distance(a, b);
    
    public static float Dot(Vector2 a, Vector2 b) => Raymath.Vector2DotProduct(a, b);
    
    public static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b, out Vector2 closestPoint) {
        
        var ab = b - a;
        var lengthSquared = Dot(ab, ab);

        if (lengthSquared <= float.Epsilon) {
            
            closestPoint = a;
            return Distance(point, a);
        }

        var t = Dot(point - a, ab) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        closestPoint = a + ab * t;

        return Distance(point, closestPoint);
    }
    
    public static bool TryFindPointOnLine(Vector2 targetPoint, Vector2 lineStart, Vector2 lineEnd, float selectDistance, out Vector2 point ) {
        
        point = Vector2.Zero;

        var distance = DistancePointToSegment(targetPoint, lineStart, lineEnd, out var closestPoint );

        if (distance > selectDistance) return false;

        point = closestPoint;
        
        return true;
    }

    public static bool TryGetNextVertexIndex(List<Vector2> vertices, int index, out int next) {
        
        next = index + 1;
        if (next < vertices.Count || vertices.Count <= 2) return true;
        next = 0;
        
        return true;
    }
}