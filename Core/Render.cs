using System.Numerics;

using Raylib_cs;
using static Raylib_cs.Raylib;

using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Triangulate.Polygon;

namespace Sickle.Heart.Core;

public static class Render {

    private const float MapTextureScale = 1f;
    private static Texture2D? _mapTexture;
    private static Material? _mapMaterial;

    public static Camera2D Cam2D = new() {
        
        Offset = Vector2.Zero,
        Target = Vector2.Zero,
        Rotation = 0,
        Zoom = 60
    };

    public static Camera3D Cam3D = new() {
        
        Position = Vector3.Zero,
        Target = Vector3.Zero,
        Up = Vector3.UnitY,
        FovY = 90,
        Projection = CameraProjection.Perspective
    };

    public static void Start() {
        
        BeginDrawing();
        ClearBackground(Colors.Background);
    }

    public static void Stop() {
        
        EndDrawing();
    }

    public static void Begin2D() {

        Cam2D.Offset = new Vector2(GetScreenWidth() * 0.5f, GetScreenHeight() * 0.5f);
        BeginMode2D(Cam2D);
    }

    public static void End2D() {
        
        EndMode2D();
    }
    
    public static void Begin3D() {

        BeginMode3D(Cam3D);
    }
    
    public static void End3D() {
        
        EndMode3D();
    }

    public static void Line(Vector2 start, Vector2 end, Color color) => DrawLineV(start, end, color);

    public static void Rectangle(Color color, Vector2 pos, Vector2 size, float roundness = 0) {

        var rect = new Rectangle(pos, size);
        
        if (roundness == 0)
             DrawRectangleRec(rect, color);
        else DrawRectangleRounded(rect, roundness, 0, color);
    }
    
    public static void Square(Color color, Vector2 pos, float size, float roundness = 0) => Rectangle(color, new Vector2(pos.X - size * .5f, pos.Y - size * .5f), new Vector2(size, size), roundness);
    
    public static void Shape(List<Vector2> vertices, Color defaultColor, Color failColor) {
        
        if (vertices.Count < 3) return;

        var gf = NtsGeometryServices.Instance.CreateGeometryFactory();

        var coords = vertices.Select(v => new Coordinate(v.X, v.Y)).Append(new Coordinate(vertices[0].X, vertices[0].Y)).ToArray();

        var poly = gf.CreatePolygon(coords);

        var fail = !poly.IsValid;
        var geom = fail ? GeometryFixer.Fix(poly) : poly;
        var color = fail ? failColor : defaultColor;

        if (geom.IsEmpty) return;

        var tris = PolygonTriangulator.Triangulate(geom);

        for (var i = 0; i < tris.NumGeometries; i++) {
            
            var c = tris.GetGeometryN(i).Coordinates;
            if (c.Length < 3) continue;

            DrawTriangle(new Vector2((float)c[0].X, (float)c[0].Y), new Vector2((float)c[1].X, (float)c[1].Y), new Vector2((float)c[2].X, (float)c[2].Y), color);
        }
    }
    
    public static void Shape (List<Vector2> vertices, Color color) => Shape(vertices, color, Colors.Red);
    
    public static void Grid(float spacing, Color color) {
        
        var topLeft = GetScreenToWorld2D(Vector2.Zero, Cam2D);
        var bottomRight = GetScreenToWorld2D(new Vector2(GetScreenWidth(), GetScreenHeight()), Cam2D);

        var startX = MathF.Floor(topLeft.X / spacing) * spacing;
        var startY = MathF.Floor(topLeft.Y / spacing) * spacing;
        
        var endX = MathF.Ceiling(bottomRight.X / spacing) * spacing;
        var endY = MathF.Ceiling(bottomRight.Y / spacing) * spacing;

        for (var x = startX; x <= endX; x += spacing)
            DrawLineV(topLeft with { X = x }, bottomRight with { X = x }, color);

        for (var y = startY; y <= endY; y += spacing)
            DrawLineV(topLeft with { Y = y }, bottomRight with { Y = y }, color);
    }

    public static void Map(Map.Map map) {
        
        GetMapTexture();

        foreach (var vertices in map.Parts.Select(part => part.Vertices).Where(vertices => vertices.Count >= 3)) {
            
            DrawTexturedShape(vertices);
        }
    }

    private static Texture2D GetMapTexture() {

        if (_mapTexture.HasValue)
            return _mapTexture.Value;

        var texture = Resources.GetResource<Texture2D>("texture", "grid_checker.png");

        SetTextureFilter(texture, TextureFilter.Point);
        SetTextureWrap(texture, TextureWrap.Repeat);

        _mapTexture = texture;

        return texture;
    }

    private static Material GetMapMaterial() {

        if (_mapMaterial.HasValue)
            return _mapMaterial.Value;

        var material = LoadMaterialDefault();
        
        SetMaterialTexture(ref material, MaterialMapIndex.Albedo, GetMapTexture());
        
        _mapMaterial = material;

        return material;
    }

    private static void DrawTexturedShape(List<Vector2> vertices) {

        var geometry = Triangulate(vertices);
        if (geometry is null || geometry.IsEmpty) return;

        var mesh = BuildTexturedMesh(geometry);
        UploadMesh(ref mesh, false);
        DrawMesh(mesh, GetMapMaterial(), Matrix4x4.Identity);
        UnloadMesh(mesh);
    }

    private static Mesh BuildTexturedMesh(Geometry geometry) {

        var vertexCount = geometry.NumGeometries * 3;
        var mesh = new Mesh(vertexCount, geometry.NumGeometries);

        mesh.AllocVertices();
        mesh.AllocTexCoords();
        mesh.AllocNormals();

        var positions = mesh.VerticesAs<float>();
        var texCoords = mesh.TexCoordsAs<float>();
        var normals = mesh.NormalsAs<float>();

        var vertexIndex = 0;

        for (var i = 0; i < geometry.NumGeometries; i++) {

            var coords = geometry.GetGeometryN(i).Coordinates;
            if (coords.Length < 3) continue;

            for (var j = 0; j < 3; j++) {

                var x = (float)coords[j].X;
                var z = (float)coords[j].Y;

                positions[vertexIndex * 3 + 0] = x;
                positions[vertexIndex * 3 + 1] = 0f;
                positions[vertexIndex * 3 + 2] = z;

                texCoords[vertexIndex * 2 + 0] = x * MapTextureScale;
                texCoords[vertexIndex * 2 + 1] = z * MapTextureScale;

                normals[vertexIndex * 3 + 0] = 0f;
                normals[vertexIndex * 3 + 1] = 1f;
                normals[vertexIndex * 3 + 2] = 0f;

                vertexIndex++;
            }
        }

        return mesh;
    }

    private static Geometry? Triangulate(List<Vector2> vertices) {

        if (vertices.Count < 3) return null;

        var gf = NtsGeometryServices.Instance.CreateGeometryFactory();
        var coords = vertices
            .Select(v => new Coordinate(v.X, v.Y))
            .Append(new Coordinate(vertices[0].X, vertices[0].Y))
            .ToArray();

        var poly = gf.CreatePolygon(coords);
        var geom = poly.IsValid ? poly : GeometryFixer.Fix(poly);

        return geom.IsEmpty ? null : PolygonTriangulator.Triangulate(geom);
    }
}
