using static Raylib_cs.Raylib;
using static Raylib_cs.Color;

namespace Sickle.Heart;

public static class Render {

    public static void Start() {
        
        BeginDrawing();
        ClearBackground(DarkGray);
    }

    public static void Stop() {
        
        EndDrawing();
    }
}