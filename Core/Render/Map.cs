using System.Numerics;

using CommunityToolkit.HighPerformance.Buffers;
using LibTessDotNet;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Sickle.Heart.Core;

public static partial class Render {

    private const float TextureScale = 1f;
    private const float WallHeight = 5f;

    private static readonly Lazy<Material> FloorMaterial = CreateMaterial("grid_orange.png");
    private static readonly Lazy<Material> WallMaterial = CreateMaterial("grid_darkgray.png");

    public static void Map(Map.Map map) {

        foreach (var vertices in map.Parts.Select(part => part.Vertices).Where(vertices => vertices.Count >= 3)) {

            DrawMesh(BuildFloorMesh(vertices), FloorMaterial.Value);
            DrawMesh(BuildWallMesh(vertices), WallMaterial.Value);
        }
    }

    private static void DrawMesh(Mesh mesh, Material material) {

        if (mesh.VertexCount == 0) return;

        UploadMesh(ref mesh, false);
        Raylib.DrawMesh(mesh, material, Matrix4x4.Identity);
        UnloadMesh(mesh);
    }

    private static Mesh BuildFloorMesh(List<Vector2> vertices) {

        var tess = Tessellate(vertices);
        if (tess.ElementCount == 0)
            return new Mesh();

        using var buffer = new ArrayPoolBufferWriter<VertexData>(tess.ElementCount * 3);

        for (var i = 0; i < tess.ElementCount * 3; i++) {

            var vertex = tess.Vertices[tess.Elements[i]].Position;
            WriteVertex(
                buffer,
                new Vector3(vertex.X, 0f, vertex.Y),
                new Vector2(vertex.X * TextureScale, vertex.Y * TextureScale),
                Vector3.UnitY
            );
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static Mesh BuildWallMesh(List<Vector2> vertices) {

        using var buffer = new ArrayPoolBufferWriter<VertexData>(vertices.Count * 6);
        var inward = SignedArea(vertices) > 0f ? 1f : -1f;

        for (var i = 0; i < vertices.Count; i++) {

            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count];
            var delta = end - start;

            if (delta.LengthSquared() <= float.Epsilon)
                continue;

            var u = delta.Length() * TextureScale;
            var normal2D = Vector2.Normalize(new Vector2(-delta.Y, delta.X) * inward);
            var normal = new Vector3(normal2D.X, 0f, normal2D.Y);

            var bottomStart = new Vector3(start.X, 0f, start.Y);
            var bottomEnd = new Vector3(end.X, 0f, end.Y);
            var topStart = new Vector3(start.X, WallHeight, start.Y);
            var topEnd = new Vector3(end.X, WallHeight, end.Y);

            if (inward > 0f) {
                WriteTriangle(buffer, normal, (bottomStart, new(0f, WallHeight)), (topEnd, new(u, 0f)), (topStart, Vector2.Zero));
                WriteTriangle(buffer, normal, (bottomStart, new(0f, WallHeight)), (bottomEnd, new(u, WallHeight)), (topEnd, new(u, 0f)));
                continue;
            }

            WriteTriangle(buffer, normal, (bottomStart, new(0f, WallHeight)), (topStart, Vector2.Zero), (topEnd, new(u, 0f)));
            WriteTriangle(buffer, normal, (bottomStart, new(0f, WallHeight)), (topEnd, new(u, 0f)), (bottomEnd, new(u, WallHeight)));
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

    private static Lazy<Material> CreateMaterial(string textureName) => new(() => {
        var material = LoadMaterialDefault();
        var texture = Resources.GetResource<Texture2D>("texture", textureName);

        SetTextureFilter(texture, TextureFilter.Point);
        SetTextureWrap(texture, TextureWrap.Repeat);
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

    private readonly record struct VertexData(Vector3 Position, Vector2 Uv, Vector3 Normal);
}
