using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VendorProfit;

internal static class MarketTheme
{
    internal static readonly Vector4 Background = new(.045f,.060f,.085f,1);
    internal static readonly Vector4 Panel = new(.070f,.092f,.125f,1);
    internal static readonly Vector4 Raised = new(.092f,.122f,.160f,1);
    internal static readonly Vector4 Text = new(.89f,.93f,.97f,1);
    internal static readonly Vector4 Muted = new(.53f,.62f,.72f,1);
    internal static readonly Vector4 Accent = new(.33f,.84f,.72f,1);
    internal static readonly Vector4 Gold = new(.96f,.80f,.45f,1);
    internal static void Push(float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(16,16)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,6)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,8)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,new Vector2(10,9)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,12*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,10*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,6*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding,8*scale);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,Background);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,Panel);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,Background);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,Background);
        ImGui.PushStyleColor(ImGuiCol.Text,Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,Muted);
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.15f,.20f,.26f,1));
        ImGui.PushStyleColor(ImGuiCol.Separator,new Vector4(.15f,.20f,.26f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,Raised);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,new Vector4(.13f,.20f,.25f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,new Vector4(.15f,.25f,.29f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.11f,.20f,.23f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.14f,.29f,.30f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.17f,.37f,.35f,1));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.11f,.21f,.24f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,new Vector4(.13f,.27f,.29f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,new Vector4(.16f,.34f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,Accent);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram,Accent);
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg,Raised);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg,new Vector4(0,0,0,0));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt,new Vector4(1,1,1,.022f));
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight,new Vector4(.12f,.16f,.21f,1));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,new Vector4(0,0,0,0));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,new Vector4(.20f,.29f,.36f,1));
    }
    internal static void Pop(){ImGui.PopStyleColor(25);ImGui.PopStyleVar(8);}
}
