namespace Splatoon.Gui;

internal static class TabFind
{
    internal static void Draw()
    {
        ImGui.Checkbox($"區域切換時不自動重置尋找", ref P.Config.NoFindReset);
        ImGuiEx.Text($"/sf <名稱> - 尋找單一可鎖定物件；\n" +
            $"- 可用逗號分隔搜尋多個名稱；\n" +
            $"- 在名稱前加上 !! 以包含不可鎖定物件；\n" +
            $"- 在名稱清單前加上 + 以新增物件而不清空清單；\n" +
            $"- 以 * 取代名稱以包含所有物件。");
        ImGuiEx.Text("目前搜尋物件清單如下:");
        var toRem = -1;
        for(var i = 0; i < P.SFind.Count; i++)
        {
            var e = P.SFind[i];
            ImGui.PushID($"sfind{i}");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
            ImGui.InputText($"##edit", ref e.Name, 50);
            ImGui.SameLine();
            ImGui.Checkbox($"包含不可鎖定物件", ref e.IncludeUntargetable);
            ImGui.SameLine();
            if(ImGui.Button("移除"))
            {
                toRem = i;
            }
            ImGui.PopID();
        }
        if(toRem > -1)
        {
            P.SFind.RemoveAt(toRem);
        }
        if(ImGui.Button("新增物件"))
        {
            P.SFind.Add(new());
        }
        ImGui.SameLine();
        if(ImGui.Button("全部清除"))
        {
            P.SFind.Clear();
        }
    }
}
