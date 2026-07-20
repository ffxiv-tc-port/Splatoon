using Dalamud.Interface.Components;
using ECommons.LanguageHelpers;
using Newtonsoft.Json;
using Splatoon.ConfigGui;
using Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader.Subcommands;
using Splatoon.Gui.Layouts.Header.Sections;
using Splatoon.Utility;

namespace Splatoon;

internal partial class CGui
{
    private string NewGroupName = "";

    private void LayoutDrawHeader(Layout layout)
    {
        if(ImGui.BeginTable("SingleLayoutEdit", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("##LayoutEdit1", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##LayoutEdit2", ImGuiTableColumnFlags.WidthStretch);

            //ImGui.TableHeadersRow();
            ImGui.TableNextColumn();
            var groupCol = P.Config.DisabledGroups.Contains(layout.Group);
            if(groupCol) ImGui.PushStyleColor(ImGuiCol.Text, EColor.RedBright);
            ImGuiEx.TextV("群組:".Loc());
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            if(ImGui.BeginCombo("##group", $"{(layout.Group == "" ? "- 無群組 -".Loc() : $"{layout.Group}")}"))
            {
                if(groupCol) ImGui.PopStyleColor();
                if(ImGui.Selectable("- 無群組 -".Loc()))
                {
                    layout.Group = "";
                }
                foreach(var x in P.Config.GroupOrder)
                {
                    if(ImGui.Selectable(x))
                    {
                        layout.Group = x;
                    }
                }
                void Add()
                {
                    layout.Group = NewGroupName;
                    NewGroupName = "";
                    ImGui.CloseCurrentPopup();
                }
                ImGuiEx.InputWithRightButtonsArea("SelectGroup", delegate
                {
                    if(ImGui.InputTextWithHint("##NewGroupName", "新增群組...".Loc(), ref NewGroupName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        Add();
                    }
                    NewGroupName = NewGroupName.SanitizeName();
                }, delegate
                {
                    if(ImGui.Button("新增".Loc()))
                    {
                        Add();
                    }
                });
                ImGui.EndCombo();
            }
            else
            {
                if(groupCol) ImGui.PopStyleColor();
            }


            ImGui.TableNextColumn();
            ImGuiEx.TextV("匯出:".Loc());
            ImGui.TableNextColumn();
            if(ImGui.Button("複製到剪貼簿".Loc()))
            {
                layout.ExportToClipboard();
            }
            ImGui.SameLine();
            ImGuiEx.TextV("分享:".Loc());
            ImGui.SameLine();
            if(ImGui.Button("GitHub".Loc()))
            {
                layout.ExportToClipboard();
                Contribute.OpenGithubPresetSubmit();
            }
            ImGui.SameLine(0, 1);
            if(ImGui.Button("Discord".Loc()))
            {
                layout.ExportToClipboard();
                Contribute.OpenDiscordLink();
            }
            ImGui.SameLine();
            if(ImGui.Button("複製給 Web API 使用".Loc()))
            {
                HTTPExportToClipboard(layout);
            }
            ImGuiEx.Tooltip("按住 ALT 複製原始 JSON（用於 POST body，否則需自行進行 URL 編碼）\n按住 CTRL 並點擊以複製已進行 URL 編碼的原始資料".Loc());


            ImGui.TableNextColumn();
            ImGui.Checkbox("啟用".Loc(), ref layout.Enabled);

            if(layout.IsVisible())
            {
                ImGuiEx.HelpMarker("此布局目前正在渲染中".Loc(), EColor.GreenBright, FontAwesomeIcon.Eye.ToIconString());
            }
            else
            {
                ImGuiEx.HelpMarker("此布局目前未在渲染中".Loc(), EColor.White, FontAwesomeIcon.EyeSlash.ToIconString());
            }
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(150f.Scale());
            if(ImGui.BeginCombo("##phaseSelectorL", $"{(layout.Phase == 0 ? "任意階段".Loc() : $"Phase ??".Loc(layout.Phase))}"))
            {
                if(ImGui.Selectable("任意階段".Loc())) layout.Phase = 0;
                if(ImGui.Selectable("第一階段（門王）".Loc())) layout.Phase = 1;
                if(ImGui.Selectable("第二階段（門王之後）".Loc())) layout.Phase = 2;
                ImGuiEx.Text("手動選擇階段:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(30f.Scale());
                ImGui.DragInt("##mPSel", ref layout.Phase, 0.1f, 0, 9);
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.Checkbox("在副本中停用".Loc(), ref layout.DisableInDuty);

            ImGui.TableNextColumn();
            ImGuiEx.TextV("名稱:".Loc());
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            if(ImGui.InputText("##name", ref layout.Name, 100))
            {
                layout.Name = layout.Name.SanitizeName();
            }

            ImGui.TableNextColumn();
            ImGuiEx.TextV("國際化名稱:".Loc());
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            layout.InternationalName.ImGuiEdit(ref layout.Name);

            ImGui.TableNextColumn();
            ImGuiEx.TextV("顯示條件:".Loc());
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            ImGui.Combo("##dcn", ref layout.DCond, Layout.DisplayConditions, Layout.DisplayConditions.Length);

            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            if(ImGui.BeginCombo("##zlock", layout.IsZoneBlacklist ? "區域黑名單".Loc() : "區域白名單".Loc()))
            {
                if(ImGui.Selectable("白名單模式".Loc()))
                {
                    layout.IsZoneBlacklist = false;
                }
                if(ImGui.Selectable("黑名單模式".Loc()))
                {
                    layout.IsZoneBlacklist = true;
                }
                ImGui.EndCombo();
            }
            ImGui.TableNextColumn();
            layout.DrawZlockSelector();

            ImGui.TableNextColumn();

            ImGuiEx.TextV("場景");
            ImGui.TableNextColumn();
            layout.DrawSceneSelector();

            ImGui.TableNextColumn();
            ImGuiEx.TextV("職業鎖定".Loc());
            ImGui.TableNextColumn();
            layout.DrawJlockSelector();

            var selectedConf = layout.Subconfigurations.FirstOrDefault(x => x.Guid == layout.SelectedSubconfigurationID);
            ImGui.TableNextColumn();
            ImGuiEx.TextV(selectedConf == null ? EColor.GreenBright : EColor.YellowBright, "設定檔".Loc());
            ImGui.TableNextColumn();
            layout.DrawLayoutConfigurations();

            if(layout.Subconfigurations.Count > 0)
            {
                ImGui.TableNextColumn();
                ImGuiEx.TextV(selectedConf == null ? EColor.GreenBright : EColor.YellowBright, "設定檔名稱".Loc());
                ImGui.TableNextColumn();
                layout.DrawLayoutConfigurationName();
            }

            ImGui.TableNextColumn();
            ImGui.Checkbox("距離限制".Loc(), ref layout.UseDistanceLimit);
            ImGui.TableNextColumn();
            layout.DrawDistanceLimit();

            ImGui.TableNextColumn();
            ImGuiEx.TextV("多重條件".Loc());
            ImGui.TableNextColumn();
            ImGuiEx.RadioButtonBool("AND##mcc", "OR##mcc", ref layout.ConditionalAnd, true);

            ImGui.TableNextColumn();
            ImGui.Checkbox("凍結".Loc(), ref layout.Freezing);
            ImGuiComponents.HelpMarker(
@"Freeze is an advanced setting that can have negative side effects.
When the requirements to display an element are met,
a new element is created and frozen in place and displayed for a duration.
New frozen elements are created every refreeze interval.".Loc());
            ImGui.TableNextColumn();
            layout.DrawFreezing();

            ImGui.TableNextColumn();
            ImGui.Checkbox("啟用觸發器".Loc(), ref layout.UseTriggers);
            if(layout.UseTriggers)
            {
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "新增".Loc()))
                {
                    layout.Triggers.Add(new Trigger());
                }
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Copy, "複製".Loc()))
                {
                    ImGui.SetClipboardText(JsonConvert.SerializeObject(layout.Triggers));
                }
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "貼上並取代".Loc(), ImGui.GetIO().KeyCtrl || layout.Triggers.Count == 0))
                {
                    try
                    {
                        layout.Triggers = JsonConvert.DeserializeObject<List<Trigger>>(ImGui.GetClipboardText());
                    }
                    catch(Exception e)
                    {
                        Notify.Error(e.Message);
                    }
                }
                if(layout.Triggers.Count != 0)
                {
                    ImGuiEx.Tooltip("按住 CTRL 並點擊。現有的觸發器將會被覆寫。".Loc());
                }
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "貼上並新增".Loc()))
                {
                    try
                    {
                        var newTriggers = JsonConvert.DeserializeObject<List<Trigger>>(ImGui.GetClipboardText());
                        foreach(var t in newTriggers)
                        {
                            layout.Triggers.Add(t);
                        }
                    }
                    catch(Exception e)
                    {
                        Notify.Error(e.Message);
                    }
                }
            }
            ImGui.TableNextColumn();
            layout.DrawTriggers();

            ImGui.EndTable();
        }


        var i = layout.Name;
        var topCursorPos = ImGui.GetCursorPos();
    }
}
