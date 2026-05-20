using static Raylib_cs.Raylib;
using static Raylib_cs.TraceLogLevel;
using static Raylib_cs.ConfigFlags;

namespace Sickle.Heart.Core;

public static class Window {

    public static void Open(int width = 1280, int height = 720, string title = "Sickle") {
        
        SetTraceLogLevel(Error);
        SetConfigFlags(ResizableWindow);
        InitWindow(width, height, title);
        SetWindowMonitor(0);
    }

    public static bool IsAlive() {

        SetExitKey(0);
        
        return !WindowShouldClose();
    }

    public static void Close() {
        
        CloseWindow();
    }
}