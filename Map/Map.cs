using System.Numerics;
using Sickle.Heart.Core;

namespace Sickle.Heart.Map;

public class Map {

    public readonly List<Part> Parts = [];

    public void DeleteVertex(int part, int vertex) {
        
        if (Parts[part].Vertices.Count <= 3) {
            Parts.RemoveAt(part);
            return;
        }

        var vertices = Parts[part].Vertices;

        vertices.RemoveAt(vertex);
    }
    
    public void DeleteVertex((int part, int vertex) tuple) => DeleteVertex(tuple.part, tuple.vertex);

    public void InsertVertex(int part, int index, Vector2 point) {
        
        Parts[part].Vertices.Insert(index, point);
    }
    
    public bool TryFindPointOnLine(float selectDistance, out Vector2 point, out int partIndex, out int insertIndex) {

        if (!TryFindLine(selectDistance, out point, out partIndex, out var startIndex, out _)) {
            insertIndex = -1;
            return false;
        }

        insertIndex = startIndex + 1;

        return true;
    }

    public bool TryFindPointOnLine(out Vector2 point, out int partIndex, out int insertIndex) =>
        TryFindPointOnLine(float.PositiveInfinity, out point, out partIndex, out insertIndex);

    public bool TryFindLine(float selectDistance, out Vector2 point, out int partIndex, out int startIndex, out int endIndex) {

        point = Vector2.Zero;
        partIndex = -1;
        startIndex = -1;
        endIndex = -1;
        var bestDistance = float.PositiveInfinity;

        for (var i = 0; i < Parts.Count; i++) {

            var vertices = Parts[i].Vertices;

            if (vertices.Count < 2)
                continue;

            for (var j = 0; j < vertices.Count; j++) {

                if (!Util.TryGetNextVertexIndex(vertices, j, out var next))
                    continue;

                var distance = Util.DistancePointToSegment(Input.MouseWorldPos, vertices[j], vertices[next], out var closestPoint);

                if (distance > selectDistance || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                point = closestPoint;
                partIndex = i;
                startIndex = j;
                endIndex = next;
            }
        }

        return partIndex != -1;
    }

    public bool TryFindLine(out Vector2 point, out int partIndex, out int startIndex, out int endIndex) =>
        TryFindLine(float.PositiveInfinity, out point, out partIndex, out startIndex, out endIndex);

    public int FindPartContaining(Vector2 point, int ignorePart = -1) {

        for (var i = Parts.Count - 1; i >= 0; i--) {

            if (i == ignorePart)
                continue;

            if (Util.IsPointInPolygon(point, Parts[i].Vertices))
                return i;
        }

        return -1;
    }
}
