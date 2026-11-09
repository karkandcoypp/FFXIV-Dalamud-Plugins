using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

internal static class HuntTheme {
    internal static readonly Vector4 Accent=new(.37f,.85f,.70f,1);
    internal static readonly Vector4 Brand=new(.48f,.78f,.72f,1);
    internal static readonly Vector4 Muted=new(.64f,.68f,.72f,1);
    internal static readonly Vector4 Text=new(.93f,.95f,.96f,1);
    internal static readonly Vector4 Surface=new(.085f,.105f,.12f,1);
    internal static readonly Vector4 Selected=new(.17f,.23f,.25f,1);
    internal static readonly Vector4 Gold=new(.95f,.77f,.46f,1);
    private static readonly (ImGuiCol Slot,Vector4 Color)[] Colors=[
        (ImGuiCol.ChildBg,Surface),
        (ImGuiCol.Text,new(.91f,.94f,.99f,1)),(ImGuiCol.TextDisabled,Muted),
        (ImGuiCol.Border,new(.16f,.19f,.21f,1)),(ImGuiCol.Separator,new(.16f,.19f,.21f,1)),
        (ImGuiCol.Button,new(.13f,.17f,.19f,1)),(ImGuiCol.ButtonHovered,new(.20f,.26f,.28f,1)),(ImGuiCol.ButtonActive,new(.24f,.32f,.34f,1)),
        (ImGuiCol.Header,Surface),(ImGuiCol.HeaderHovered,Selected),(ImGuiCol.HeaderActive,new(.21f,.29f,.30f,1)),
        (ImGuiCol.Tab,new(.09f,.11f,.17f,1)),(ImGuiCol.TabActive,new(.21f,.26f,.41f,1)),(ImGuiCol.TabHovered,new(.26f,.32f,.49f,1)),
        (ImGuiCol.FrameBg,new(.105f,.13f,.19f,1)),(ImGuiCol.CheckMark,Accent),(ImGuiCol.PlotHistogram,Brand),
        (ImGuiCol.TitleBg,new(.055f,.065f,.095f,1)),(ImGuiCol.TitleBgActive,new(.095f,.115f,.18f,1)),(ImGuiCol.TitleBgCollapsed,new(.055f,.065f,.095f,1)),
        (ImGuiCol.ScrollbarBg,new(.055f,.065f,.095f,1)),(ImGuiCol.ScrollbarGrab,new(.22f,.27f,.38f,1)),(ImGuiCol.ScrollbarGrabHovered,new(.30f,.37f,.51f,1)),
        (ImGuiCol.TableRowBg,new(.08f,.095f,.135f,1)),(ImGuiCol.TableRowBgAlt,new(.10f,.12f,.17f,1))
    ];
    internal static void Push() {
        foreach(var (slot,color) in Colors) ImGui.PushStyleColor(slot,color);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(16,14));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(9,5));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(8,8));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,12);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,7);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,12);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,new Vector2(7,6));
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding,6);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding,8);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize,10);
    }
    internal static void Pop() { ImGui.PopStyleVar(10);ImGui.PopStyleColor(Colors.Length); }
    internal static bool BeginCard(string id){
        float width=ImGui.GetContentRegionAvail().X;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,new Vector2(12,12));ImGui.PushStyleColor(ImGuiCol.TableRowBg,Surface);
        if(!ImGui.BeginTable(id,1,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingFixedFit|ImGuiTableFlags.PadOuterX|ImGuiTableFlags.NoSavedSettings,new Vector2(width,0))){ImGui.PopStyleColor();ImGui.PopStyleVar();return false;}
        ImGui.TableSetupColumn("##body",ImGuiTableColumnFlags.WidthFixed,System.Math.Max(1,width-24));
        ImGui.TableNextRow();ImGui.TableNextColumn();return true;
    }
    internal static void EndCard(){ImGui.EndTable();ImGui.PopStyleColor();ImGui.PopStyleVar();}
    internal static void Heading(string text)=>ImGui.TextWrapped(text);
    internal static bool Primary(string label,Vector2 size){
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.29f,.72f,.62f,1));ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.38f,.82f,.72f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.23f,.62f,.53f,1));ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(.025f,.085f,.075f,1));
        bool clicked=ImGui.Button(label,size);ImGui.PopStyleColor(4);return clicked;
    }
    internal static void Hint(string text) { if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(text); }
}
