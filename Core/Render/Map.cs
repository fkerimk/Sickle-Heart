using System.Numerics;

using CommunityToolkit.HighPerformance.Buffers;
using LibTessDotNet;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Sickle.Heart.Core;

public static partial class Render {

    private static readonly Dictionary<string, Lazy<Material>> Materials = [];

    public static void Map(Map.Map map) {

        for (var i = 0; i < map.Parts.Count; i++) {
            
            var part = map.Parts[i];
            
            var vertices = part.Vertices;
            if (vertices.Count < 3) continue;
            
            var wallDirection = GetWallDirection(map, i);

            DrawMesh(BuildFloorMesh(vertices, part.YOffset, part.Floor), GetMaterial(part.Floor), part.Floor);
            DrawMesh(BuildCeilingMesh(vertices, part.YOffset + part.Height, part.Ceil), GetMaterial(part.Ceil), part.Ceil);
            DrawMesh(BuildWallMesh(map, i, wallDirection), GetMaterial(part.Wall), part.Wall);
        }
    }

    private static void DrawMesh(Mesh mesh, Material material, Map.Surface surface) {

        if (mesh.VertexCount == 0) return;

        var texture = Resources.GetResource<Texture2D>("texture", surface.Texture);
        SetTextureWrap(texture, surface.Wrap);
        SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        UploadMesh(ref mesh, false);
        Raylib.DrawMesh(mesh, material, Matrix4x4.Identity);
        UnloadMesh(mesh);
    }

    private static Mesh BuildFloorMesh(List<Vector2> vertices, float yOffset, Map.Surface surface) {

        var tess = Tessellate(vertices);
        
        if (tess.ElementCount == 0)
            return new Mesh();

        using var buffer = new ArrayPoolBufferWriter<VertexData>(tess.ElementCount * 3);
        var bounds = GetBounds(vertices);

        for (var i = 0; i < tess.ElementCount * 3; i++) {

            var vertex = tess.Vertices[tess.Elements[i]].Position;
            
            WriteVertex(
                buffer,
                new Vector3(vertex.X, yOffset, vertex.Y),
                GetHorizontalUv(new Vector2(vertex.X, vertex.Y), bounds, surface),
                Vector3.UnitY
            );
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static Mesh BuildWallMesh(Map.Map map, int partIndex, float wallDirection) {

        var part = map.Parts[partIndex];
        var vertices = part.Vertices;
        
        using var buffer = new ArrayPoolBufferWriter<VertexData>(vertices.Count * 18);
        
        var inward = MathF.Sign(SignedArea(vertices)) * wallDirection;
        var bottom = part.YOffset;
        var top = part.YOffset + part.Height;

        for (var i = 0; i < vertices.Count; i++) {

            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count];
            var delta = end - start;

            if (delta.LengthSquared() <= float.Epsilon)
                continue;

            var normal2D = Vector2.Normalize(new Vector2(-delta.Y, delta.X) * inward);
            var normal = new Vector3(normal2D.X, 0f, normal2D.Y);

            foreach (var face in GetVisibleWallFaces(map, partIndex, start, end, bottom, top))
                WriteWallSegment(buffer, start, end, face.From, face.To, inward, normal, face.Bottom, face.Top, part.Wall);
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static void WriteWallSegment(ArrayPoolBufferWriter<VertexData> buffer, Vector2 edgeStart, Vector2 edgeEnd, float from, float to, float inward, Vector3 normal, float bottom, float top, Map.Surface surface) {

        if (to - from <= 0.0001f)
            return;

        var start = Vector2.Lerp(edgeStart, edgeEnd, from);
        var end = Vector2.Lerp(edgeStart, edgeEnd, to);
        var bottomStart = new Vector3(start.X, bottom, start.Y);
        var bottomEnd = new Vector3(end.X, bottom, end.Y);
        var topStart = new Vector3(start.X, top, start.Y);
        var topEnd = new Vector3(end.X, top, end.Y);
        var uvBottomStart = GetWallUv(start, end, bottom, top, bottom, surface);
        var uvBottomEnd = GetWallUv(start, end, bottom, top, bottom, surface, true);
        var uvTopStart = GetWallUv(start, end, bottom, top, top, surface);
        var uvTopEnd = GetWallUv(start, end, bottom, top, top, surface, true);

        if (inward > 0f) {
            WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topEnd, uvTopEnd), (topStart, uvTopStart));
            WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (bottomEnd, uvBottomEnd), (topEnd, uvTopEnd));
            return;
        }

        WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topStart, uvTopStart), (topEnd, uvTopEnd));
        WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topEnd, uvTopEnd), (bottomEnd, uvBottomEnd));
    }

    private static List<WallFaceSegment> GetVisibleWallFaces(Map.Map map, int currentPartIndex, Vector2 edgeStart, Vector2 edgeEnd, float currentBottom, float currentTop) {

        var splitPoints = new List<float> { 0f, 1f };
        var overlaps = new List<EdgeOverlap>();
        var delta = edgeEnd - edgeStart;
        var lengthSquared = delta.LengthSquared();

        if (lengthSquared <= float.Epsilon)
            return [];

        for (var partIndex = 0; partIndex < map.Parts.Count; partIndex++) {
            
            var part = map.Parts[partIndex];
            var vertices = part.Vertices;

            for (var vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {

                if (!Util.TryGetNextVertexIndex(vertices, vertexIndex, out var nextIndex))
                    continue;

                if (partIndex == currentPartIndex && vertices[vertexIndex] == edgeStart && vertices[nextIndex] == edgeEnd)
                    continue;

                if (!TryGetOverlap(edgeStart, edgeEnd, vertices[vertexIndex], vertices[nextIndex], lengthSquared, out var overlap))
                    continue;

                splitPoints.Add(overlap.from);
                splitPoints.Add(overlap.to);
                overlaps.Add(new EdgeOverlap(
                    overlap.from,
                    overlap.to,
                    part.YOffset,
                    part.YOffset + part.Height
                ));
            }
        }

        splitPoints.Sort();

        var faces = new List<WallFaceSegment>();

        for (var i = 0; i < splitPoints.Count - 1; i++) {
            
            var from = splitPoints[i];
            var to = splitPoints[i + 1];

            if (to - from <= 0.0001f)
                continue;

            var sample = (from + to) * 0.5f;
            var occluders = overlaps
                .Where(overlap => overlap.From <= sample && sample <= overlap.To)
                .Select(overlap => new VerticalRange(
                    MathF.Max(currentBottom, overlap.Bottom),
                    MathF.Min(currentTop, overlap.Top)
                ))
                .Where(range => range.Top - range.Bottom > 0.0001f)
                .OrderBy(range => range.Bottom)
                .ToList();

            faces.AddRange(SubtractVerticalRanges(currentBottom, currentTop, occluders).Select(verticalRange => new WallFaceSegment(from, to, verticalRange.Bottom, verticalRange.Top)));
        }

        return faces;
    }

    private static List<VerticalRange> SubtractVerticalRanges(float bottom, float top, List<VerticalRange> cuts) {

        var remaining = new List<VerticalRange> { new(bottom, top) };

        foreach (var cut in cuts) {
            
            for (var i = remaining.Count - 1; i >= 0; i--) {
                
                var range = remaining[i];
                var overlapBottom = MathF.Max(range.Bottom, cut.Bottom);
                var overlapTop = MathF.Min(range.Top, cut.Top);

                if (overlapTop - overlapBottom <= 0.0001f)
                    continue;

                remaining.RemoveAt(i);

                if (range.Bottom < overlapBottom - 0.0001f)
                    remaining.Insert(i, new VerticalRange(range.Bottom, overlapBottom));

                if (overlapTop < range.Top - 0.0001f)
                    remaining.Insert(i + (range.Bottom < overlapBottom - 0.0001f ? 1 : 0), new VerticalRange(overlapTop, range.Top));
            }
        }

        return remaining;
    }

    private static bool TryGetOverlap(Vector2 edgeStart, Vector2 edgeEnd, Vector2 otherStart, Vector2 otherEnd, float lengthSquared, out (float from, float to) overlap) {

        overlap = default;

        const float epsilon = 0.0001f;

        var delta = edgeEnd - edgeStart;
        var otherDelta = otherEnd - otherStart;

        if (MathF.Abs(Cross(delta, otherDelta)) > epsilon)
            return false;

        if (MathF.Abs(Cross(delta, otherStart - edgeStart)) > epsilon || MathF.Abs(Cross(delta, otherEnd - edgeStart)) > epsilon)
            return false;

        var t0 = Vector2.Dot(otherStart - edgeStart, delta) / lengthSquared;
        var t1 = Vector2.Dot(otherEnd - edgeStart, delta) / lengthSquared;
        var from = MathF.Max(0f, MathF.Min(t0, t1));
        var to = MathF.Min(1f, MathF.Max(t0, t1));

        if (to - from <= epsilon)
            return false;

        overlap = (from, to);
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static float GetWallDirection(Map.Map map, int partIndex) {

        var sample = map.Parts[partIndex].Vertices[0];
        var depth = map.Parts.Where((_, i) => i != partIndex).Count(t => Util.IsPointInPolygon(sample, t.Vertices));

        return depth % 2 == 0 ? 1f : -1f;
    }

    private static Mesh BuildCeilingMesh(List<Vector2> vertices, float height, Map.Surface surface) {

        var tess = Tessellate(vertices);
        
        if (tess.ElementCount == 0)
            return new Mesh();

        using var buffer = new ArrayPoolBufferWriter<VertexData>(tess.ElementCount * 3);
        
        var bounds = GetBounds(vertices);

        for (var i = 0; i < tess.ElementCount; i++) {

            var a = tess.Vertices[tess.Elements[i * 3]].Position;
            var b = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var c = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

            WriteTriangle(
                buffer,
                -Vector3.UnitY,
                (new Vector3(a.X, height, a.Y), GetHorizontalUv(new Vector2(a.X, a.Y), bounds, surface)),
                (new Vector3(c.X, height, c.Y), GetHorizontalUv(new Vector2(c.X, c.Y), bounds, surface)),
                (new Vector3(b.X, height, b.Y), GetHorizontalUv(new Vector2(b.X, b.Y), bounds, surface))
            );
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static Tess Tessellate(List<Vector2> vertices) {

        var contour = vertices
            .Select(vertex => new ContourVertex { Position = new Vec3(vertex.X, vertex.Y, 0f) })
            .ToArray();

        var tess = new Tess();
        tess.AddContour(contour);
        tess.Tessellate();

        return tess;
    }

    private static float SignedArea(List<Vector2> vertices) {

        var area = 0f;

        for (var i = 0; i < vertices.Count; i++) {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return area * 0.5f;
    }

    private static (Vector2 min, Vector2 max) GetBounds(List<Vector2> vertices) {
        
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);

        foreach (var vertex in vertices) {
            
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        return (min, max);
    }

    private static Vector2 GetHorizontalUv(Vector2 position, (Vector2 min, Vector2 max) bounds, Map.Surface surface) {

        var uv = surface.Mode switch {
            
            Heart.Map.TileMode.Local => new Vector2(
                
                NormalizeToBounds(position.X, bounds.min.X, bounds.max.X),
                NormalizeToBounds(position.Y, bounds.min.Y, bounds.max.Y)
            ),
            
            _ => position,
        };

        return ApplySurfaceTransform(uv, surface);
    }

    private static Vector2 GetWallUv(Vector2 segmentStart, Vector2 segmentEnd, float bottom, float top, float verticalPosition, Map.Surface surface, bool useEnd = false) {

        var horizontalDistance = useEnd ? Vector2.Distance(segmentStart, segmentEnd) : 0f;
        var verticalDistance = top - verticalPosition;

        var uv = surface.Mode switch {
            
            Heart.Map.TileMode.Local => new Vector2(
                
                NormalizeToBounds(horizontalDistance, 0f, Vector2.Distance(segmentStart, segmentEnd)),
                NormalizeToBounds(verticalDistance, 0f, top - bottom)
            ),
            
            _ => new Vector2(horizontalDistance, verticalDistance),
        };

        return ApplySurfaceTransform(uv, surface);
    }

    private static float NormalizeToBounds(float value, float min, float max) {

        var size = max - min;

        if (MathF.Abs(size) <= float.Epsilon)
            return 0f;

        return (value - min) / size;
    }

    private static Vector2 ApplySurfaceTransform(Vector2 uv, Map.Surface surface) {

        var tiling = surface.Mode switch {
            
            Heart.Map.TileMode.Local => new Vector2(
                
                uv.X * surface.Tiling.X,
                uv.Y * surface.Tiling.Y
            ),
            
            _ => new Vector2(
                
                DivideByTileSize(uv.X, surface.Tiling.X),
                DivideByTileSize(uv.Y, surface.Tiling.Y)
            ),
        };

        return tiling + surface.Offset;
    }

    private static float DivideByTileSize(float value, float tileSize) {

        if (MathF.Abs(tileSize) <= float.Epsilon)
            return 0f;

        return value / tileSize;
    }

    private static Material GetMaterial(Map.Surface surface) =>
        Materials.GetValueOrDefault(surface.Texture)?.Value ?? (Materials[surface.Texture] = CreateMaterial(surface.Texture)).Value;

    private static Lazy<Material> CreateMaterial(string textureName) => new(() => {
        
        var material = LoadMaterialDefault();
        var texture = Resources.GetResource<Texture2D>("texture", textureName);

        SetTextureFilter(texture, TextureFilter.Point);
        SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);

        return material;
    });

    private static Mesh BuildMesh(ReadOnlySpan<VertexData> vertices) {

        if (vertices.IsEmpty)
            return new Mesh();

        var mesh = new Mesh(vertices.Length, vertices.Length / 3);
        mesh.AllocVertices();
        mesh.AllocTexCoords();
        mesh.AllocNormals();

        var positions = mesh.VerticesAs<float>();
        var texCoords = mesh.TexCoordsAs<float>();
        var normals = mesh.NormalsAs<float>();

        for (var i = 0; i < vertices.Length; i++) {
            var vertex = vertices[i];

            positions[i * 3] = vertex.Position.X;
            positions[i * 3 + 1] = vertex.Position.Y;
            positions[i * 3 + 2] = vertex.Position.Z;

            texCoords[i * 2] = vertex.Uv.X;
            texCoords[i * 2 + 1] = vertex.Uv.Y;

            normals[i * 3] = vertex.Normal.X;
            normals[i * 3 + 1] = vertex.Normal.Y;
            normals[i * 3 + 2] = vertex.Normal.Z;
        }

        return mesh;
    }

    private static void WriteTriangle(ArrayPoolBufferWriter<VertexData> buffer, Vector3 normal, params (Vector3 position, Vector2 uv)[] vertices) {

        var span = buffer.GetSpan(vertices.Length);

        for (var i = 0; i < vertices.Length; i++)
            span[i] = new VertexData(vertices[i].position, vertices[i].uv, normal);

        buffer.Advance(vertices.Length);
    }

    private static void WriteVertex(ArrayPoolBufferWriter<VertexData> buffer, Vector3 position, Vector2 uv, Vector3 normal) {

        buffer.GetSpan(1)[0] = new VertexData(position, uv, normal);
        buffer.Advance(1);
    }

    private readonly record struct EdgeOverlap(float From, float To, float Bottom, float Top);
    private readonly record struct VerticalRange(float Bottom, float Top);
    private readonly record struct WallFaceSegment(float From, float To, float Bottom, float Top);
    private readonly record struct VertexData(Vector3 Position, Vector2 Uv, Vector3 Normal);
}
