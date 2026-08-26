using ECommons.LanguageHelpers;

namespace Splatoon.Gui;

internal static class TabFind
{
    internal static void Draw()
    {
        ImGui.Checkbox("Don't auto-reset find on zone change".Loc(), ref P.Config.NoFindReset);
        ImGuiEx.Text(("/sf <name> - find a single targetable object;\n" +
            "- You can search for multiple names separated by comma;\n" +
            "- Prefix name with !! to include untargetable objects;\n" +
            "- Prefix name list with + to add new objects without clearing list;\n" +
            "- Substitute name with * to include all objects.").Loc());
        ImGuiEx.Text("Here is current list of searched objects:".Loc());
        var toRem = -1;
        for(var i = 0; i < P.SFind.Count; i++)
        {
            var e = P.SFind[i];
            ImGui.PushID($"sfind{i}");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
            ImGui.InputText($"##edit", ref e.Name, 50);
            ImGui.SameLine();
            ImGui.Checkbox("Include untargetable".Loc(), ref e.IncludeUntargetable);
            ImGui.SameLine();
            if(ImGui.Button("Remove".Loc()))
            {
                toRem = i;
            }
            ImGui.PopID();
        }
        if(toRem > -1)
        {
            P.SFind.RemoveAt(toRem);
        }
        if(ImGui.Button("Add object".Loc()))
        {
            P.SFind.Add(new());
        }
        ImGui.SameLine();
        if(ImGui.Button("Clear all".Loc()))
        {
            P.SFind.Clear();
        }
    }
}
