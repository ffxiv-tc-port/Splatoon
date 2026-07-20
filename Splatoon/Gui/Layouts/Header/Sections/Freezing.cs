using Dalamud.Interface.Components;
using ECommons.LanguageHelpers;

namespace Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader.Subcommands;

internal static class Freezing
{
    internal static void DrawFreezing(this Layout layout)
    {
        if(layout.Freezing)
        {
            ImGuiEx.Text("凍結時間:".Loc());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragFloat("##freezeTime", ref layout.FreezeFor, 0.1f, 0.1f, 99999f, $"{layout.FreezeFor:F1}");
            ImGuiEx.HelpMarker("顯示凍結元素的持續秒數。".Loc());

            ImGuiEx.Text("重新凍結間隔:".Loc());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragFloat("##freezeInt", ref layout.IntervalBetweenFreezes, 0.1f, 0.1f, 99999f, $"{layout.IntervalBetweenFreezes:F1}");
            ImGuiEx.HelpMarker("產生新凍結元素之間的秒數間隔。\n數值越低，產生的元素越多。".Loc());
            if(layout.IntervalBetweenFreezes < 0.5f)
            {
                ImGuiEx.HelpMarker("Warning: your interval between freezes is very low. Please ensure that this is intentional.", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }

            ImGuiEx.Text("顯示延遲:".Loc());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragFloat("##freezeDD", ref layout.FreezeDisplayDelay, 0.1f, 0, 99999, $"{layout.FreezeDisplayDelay:F1}");
            ImGuiEx.HelpMarker("新建立的凍結元素顯示前的延遲秒數。".Loc());

            ImGuiEx.Text("重置條件:".Loc());
            ImGui.SameLine();
            ImGui.Checkbox("戰鬥結束".Loc(), ref layout.FreezeResetCombat);
            ImGui.SameLine();
            ImGui.Checkbox("區域切換".Loc(), ref layout.FreezeResetTerr);
        }
    }
}
