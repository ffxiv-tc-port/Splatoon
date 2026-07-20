using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon;
internal unsafe partial class CGui
{
    internal void DrawArchive()
    {
        ImGuiEx.TextWrapped($"""
            您可以封存不再使用的布局。已封存的布局：
            - 不會被處理，也不會消耗任何資源；
            - 會被包含在備份中；
            - 無法編輯、檢視或重新排序；
            - 可隨時匯出至剪貼簿或還原。
            """);
        var groups = P.Archive.LayoutsL.Select(x => x.Group).Distinct().Order();

        foreach(var group in groups)
        {
            if(group == "") continue;
            if(ImGuiEx.TreeNode(group))
            {
                var grp = P.Archive.LayoutsL.Where(x => x.Group == group);
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Copy, "複製群組"))
                {
                    Copy(grp.Select(x => EzConfig.DefaultSerializationFactory.Serialize(x, false)).Join("\n"));
                }
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.ArrowCircleLeft, "還原群組"))
                {
                    foreach(var x in grp)
                    {
                        P.Config.LayoutsL.Add(x.JSONClone());
                        new TickScheduler(() => P.Archive.LayoutsL.Remove(x));
                    }
                }
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "刪除群組", ImGuiEx.Ctrl))
                {
                    foreach(var x in grp)
                    {
                        new TickScheduler(() => P.Archive.LayoutsL.Remove(x));
                    }
                }
                ImGui.PushID(group);
                DrawArchiveEntries(grp);
                ImGui.PopID();
                ImGui.TreePop();
            }
        }
        var nogrp = P.Archive.LayoutsL.Where(x => x.Group == "");
        if(nogrp.Any()) DrawArchiveEntries(nogrp);
    }

    private void DrawArchiveEntries(IEnumerable<Layout> layouts)
    {
        if(ImGui.BeginTable("EntryArchive", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("名稱", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("資訊");
            ImGui.TableSetupColumn("控制");

            foreach(var x in layouts)
            {
                ImGui.PushID(x.GUID);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGuiEx.TextV($"{x.Name}");

                ImGui.TableNextColumn();

                ImGuiEx.TextV($"{x.ElementsL.Count} 個元素");

                ImGui.TableNextColumn();

                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Copy, "複製"))
                {
                    Copy(EzConfig.DefaultSerializationFactory.Serialize(x, false));
                }
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.ArrowCircleLeft, "還原"))
                {
                    P.Config.LayoutsL.Add(x.JSONClone());
                    new TickScheduler(() => P.Archive.LayoutsL.Remove(x));
                }
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "刪除", ImGuiEx.Ctrl))
                {
                    new TickScheduler(() => P.Archive.LayoutsL.Remove(x));
                }
                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }
}
