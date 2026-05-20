using System.Numerics;
using Sickle.Heart.Core;

namespace Sickle.Heart.Map;

public class Map {

    public readonly List<Part> Parts = [];
    
    public void DeleteVertex(int part, int vertex) {
        
        var vertices = Parts[part].Vertices;

        if (vertices.Count <= 3)
            return;

        vertices.RemoveAt(vertex);
    }
    
    public void DeleteVertex((int part, int vertex) tuple) => DeleteVertex(tuple.part, tuple.vertex);

    public void InsertVertex(int part, int index, Vector2 point) {
        
        Parts[part].Vertices.Insert(index, point);
    }
    
    public bool TryFindPointOnLine(out Vector2 point, out int partIndex, out int insertIndex) {
        
        const float LineSelectDistance = 0.15f;
        
        point = Vector2.Zero;
        partIndex = -1;
        insertIndex = -1;

        for (var i = 0; i <  Parts.Count; i++) {

            var vertices = Parts[i].Vertices;

            if (vertices.Count < 2)
                continue;

            for (var j = 0; j < vertices.Count; j++) {

                if (!Util.TryGetNextVertexIndex(vertices, j, out var next))
                    continue;

                if (!Util.TryFindPointOnLine(Input.MouseWorldPos, vertices[j], vertices[next], LineSelectDistance, out point))
                    continue;

                partIndex = i;
                insertIndex = j + 1;

                return true;
            }
        }

        return false;
    }
}