using System.Numerics;
using ImGuiNET;
using rlImGui_cs;
using static rlImGui_cs.rlImGui;

namespace Sickle.Heart.Core;

public static unsafe class Gui {
    
    private static ImGuiIOPtr _io;

    private const float DragSpeed = .05f;
    
    public static bool WantCaptureMouse => _io.WantCaptureMouse;

    public static void Setup() {

        rlImGui.Setup();

        _io = ImGui.GetIO();
        _io.NativePtr->IniFilename = null;

        SetupFonts();
    }

    private static void SetupFonts() {
        
        _io.Fonts.Clear();
        
        var montserratRegular = Resources.FindResourceFile("font", "Montserrat-Regular.ttf");
        _io.Fonts.AddFontFromFileTTF(montserratRegular, 18f);
        
        ReloadFonts();
    }

    public static void Shutdown() => rlImGui.Shutdown();

    public static void Start() => rlImGui.Begin();
    
    public static bool Begin(string name) => ImGui.Begin(name, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
    public static void End () => ImGui.End();
    
    public static void Stop() => rlImGui.End();

    public static void SetNextWindowPos(Vector2 pos) => ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
    public static void SetNextWindowSize(Vector2 size) => ImGui.SetNextWindowSize(size, ImGuiCond.Always);

    public static void SetNextWindow(Vector2 pos, Vector2 size) {
        
        SetNextWindowPos(pos);
        SetNextWindowSize(size);
    }

    public static void Text(string label) => ImGui.Text(label);

    public static bool InputText(string label, ref string value, uint maxLenght) => ImGui.InputText(label, ref value, maxLenght);
    
    public static void Separator() => ImGui.Separator();
    
    public static bool DragFloat(string label, ref float value, float? min = null, float? max = null) => ImGui.DragFloat(label, ref value, DragSpeed, min ?? float.MinValue, max ?? float.MaxValue);
    public static bool DragVector2(string label, ref Vector2 value, float? min = null, float? max = null) => ImGui.DragFloat2(label, ref value, DragSpeed, min ?? float.MinValue, max ?? float.MaxValue);

    public static bool CollapsingHeader(string label) => ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);

    public static bool Combo(string label, string[] items, ref int currentItem) => ImGui.Combo(label, ref currentItem, items, items.Length);
}
