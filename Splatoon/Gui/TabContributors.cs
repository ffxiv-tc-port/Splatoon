using Dalamud.Interface.Colors;
using ECommons.LanguageHelpers;

namespace Splatoon.Gui;

internal static class TabContributors
{
    internal static void Draw()
    {
        ImGuiEx.TextWrapped("感謝所有為 Splatoon 做出貢獻的人！以下是希望被提及的貢獻者名單。找不到自己或網站連結有誤？請在 GitHub 或 Discord 上開 issue，我會將您加入。您也可以指定您的名字以及您的網站/社群帳號（如果您願意的話）。".Loc());
        ImGuiEx.Text(ImGuiColors.DalamudRed, "警告，此名單仍在建置中，仍有許多人未列入。");
        ImGui.Separator();

        ImGuiEx.Text("玖祁 - 中文翻譯");
        ImGuiEx.Text("jojo - 預設檔與預設檔翻譯");
        ImGuiEx.Text("FrostEffects - 預設檔"); Link("Carrd", "https://frostffxiv.carrd.co/");
        ImGuiEx.Text("莫灵喵 - 預設檔");
        ImGuiEx.Text("LAMMY - 預設檔"); Link("Github", "https://github.com/LAMMY-33");
        ImGuiEx.Text($"Ry - 色盲輔助、戰鬥資料");
        ImGuiEx.Text($"Errer - 預設檔"); Link("Github", "https://github.com/Errerer/");
        ImGuiEx.Text($"Ouyk - 預設檔");
        ImGuiEx.Text($"Exnter - 預設檔"); Link("Github", "https://github.com/Exnter/");
    }

    private static void Link(string preview, string Url)
    {
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, preview ?? Url);
        if(ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if(ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ShellStart(Url);
            }
        }
    }
}
