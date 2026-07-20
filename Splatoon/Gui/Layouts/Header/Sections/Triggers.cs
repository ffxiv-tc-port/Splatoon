using Dalamud.Interface.Colors;
using ECommons.LanguageHelpers;
using Newtonsoft.Json;
using Splatoon.Utility;

namespace Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader.Subcommands;

internal static class Triggers
{
    internal static void DrawTriggers(this Layout layout)
    {
        if(layout.UseTriggers)
        {
            for(var n = 0; n < layout.Triggers.Count; n++)
            {
                var trigger = layout.Triggers[n];
                ImGui.PushID(trigger.GUID);
                if(ImGuiEx.IconButton(FontAwesomeIcon.Trash, enabled: ImGuiEx.Ctrl))
                {
                    new TickScheduler(() => layout.Triggers.Remove(trigger));
                }
                ImGuiEx.Tooltip("按住 CTRL 並左鍵點擊以刪除".Loc());
                ImGui.SameLine(0, 1);
                if(ImGuiEx.IconButton(FontAwesomeIcon.Copy))
                {
                    Copy(JsonConvert.SerializeObject((Trigger[])[trigger]));
                }
                ImGuiEx.Tooltip("複製到剪貼簿");
                ImGui.SameLine();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.Combo("##trigger", ref trigger.Type, Trigger.Types, Trigger.Types.Length);

                ImGuiEx.TextV("重置條件:".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("退出戰鬥".Loc(), ref trigger.ResetOnCombatExit);
                ImGui.SameLine();
                ImGui.Checkbox("區域切換".Loc(), ref trigger.ResetOnTChange);
                ImGui.SameLine();
                ImGuiEx.Text("狀態: ".Loc() + trigger.FiredState);
                if(trigger.Disabled)
                {
                    ImGui.SameLine();
                    ImGuiEx.Text(ImGuiColors.DalamudRed, $"已停用，直到重置為止");
                }
                if(trigger.Type == 0 || trigger.Type == 1)
                {
                    ImGuiEx.TextV("時間: ".Loc());
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##triggertime1", ref trigger.TimeBegin, 0.1f, 0, 3599, "%.1f");
                    ImGui.SameLine();
                    ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds((long)(trigger.TimeBegin * 1000)).ToString("mm:ss.f"));
                }
                else
                {
                    ImGuiEx.InputWithRightButtonsArea($"trigger{trigger.GUID}", delegate
                    {
                        trigger.MatchIntl.ImGuiEdit(ref trigger.Match, "不分大小寫（部分符合）訊息");
                    }, delegate
                    {
                        var col = trigger.IsRegex;
                        if(col) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                        ImGui.Checkbox("Regex", ref trigger.IsRegex);
                        if(col) ImGui.PopStyleColor();
                    });
                    //ImGui.InputTextWithHint("##textinput1", "Case-insensitive message", ref trigger.Match, 1000);

                    //ImGui.SameLine();
                    ImGui.Checkbox($"僅觸發一次，直到重置為止", ref trigger.FireOnce);
                    ImGuiEx.TextV("延遲: ".Loc());
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##triggertime1", ref trigger.MatchDelay, 0.1f, 0, 3599, "%.1f");
                    ImGui.SameLine();
                    ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds((long)(trigger.MatchDelay * 1000)).ToString("mm:ss.f"));
                    trigger.Match = trigger.Match.RemoveSymbols(InvalidSymbols);
                    trigger.MatchIntl.RemoveSymbols(InvalidSymbols);
                }
                ImGui.SameLine();
                ImGuiEx.TextV("持續時間: ".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##triggertime2", ref trigger.Duration, 0.1f, 0, 3599, "%.1f");
                ImGui.SameLine();
                ImGuiEx.Text(trigger.Duration == 0 ? "無限".Loc() : DateTimeOffset.FromUnixTimeMilliseconds((long)(trigger.Duration * 1000)).ToString("mm:ss.f"));
                ImGui.Separator();
                ImGui.PopID();
            }
        }
    }
}
