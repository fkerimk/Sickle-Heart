namespace Sickle.Heart.Core;

public static class Time {

    public static float Total => (float)Raylib_cs.Raylib.GetTime();
    public static float Delta => Raylib_cs.Raylib.GetFrameTime();
}