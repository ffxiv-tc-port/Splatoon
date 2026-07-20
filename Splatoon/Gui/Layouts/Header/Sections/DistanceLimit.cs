using ECommons.LanguageHelpers;

namespace Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader.Subcommands;

internal static class DistanceLimit
{
    internal static void DrawDistanceLimit(this Layout layout)
    {
        if(layout.UseDistanceLimit)
        {
            ImGui.SetNextItemWidth(150f);
            ImGui.Combo("##dlimittype", ref layout.DistanceLimitType, new string[] { "與目前目標的距離".Loc(), "與元素的距離".Loc() }, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragFloat("##dlimit1", ref layout.MinDistance, 0.1f);
            if(ImGui.IsItemHovered()) ImGui.SetTooltip("包含此數值".Loc());
            ImGui.SameLine();
            ImGuiEx.Text("-");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragFloat("##dlimit2", ref layout.MaxDistance, 0.1f);
            if(ImGui.IsItemHovered()) ImGui.SetTooltip("排除此數值".Loc());
            if(layout.DistanceLimitType == 0)
            {
                ImGuiEx.TextV("碰撞箱:".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("+my##", ref layout.DistanceLimitMyHitbox);
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("將我方碰撞箱數值加入距離計算".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("+target##", ref layout.DistanceLimitTargetHitbox);
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("將目標碰撞箱數值加入距離計算".Loc());
            }
        }
    }
}
