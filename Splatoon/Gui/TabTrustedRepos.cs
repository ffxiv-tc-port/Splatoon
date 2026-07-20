using ECommons.LanguageHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.Gui;
public static class TabTrustedRepos
{
    public static void Draw()
    {
        ref var pass = ref Ref<bool>.Get();
        var display = pass || P.Config.ExtraTrustedRepos != "" || P.Config.ExtraUpdateLinks != "";
        if(!display)
        {
            ImGuiEx.TextWrapped(EColor.RedBright, "您即將存取「極度危險的選項」。正常情況下，只有開發者才會需要使用它。".Loc());
            ImGui.Checkbox($"我了解不當使用這些功能可能導致無法復原的損害。", ref pass);
        }
        if(!pass) return;
        pass = true;
        ImGuiEx.Text($"額外信任來源");
        ImGui.Indent();
        ImGuiEx.TextWrapped($"新增您想從中匯入腳本的額外信任來源，每行一項。任何以您新增的字串開頭的網址都會被視為可信任。請謹慎選擇。若您不當使用此功能，Splatoon 開發者與發布者將不對您的遊戲、角色、個人資料、作業系統與電腦所發生的任何可能損害負責。".Loc());
        ImGui.Unindent();
        ImGuiEx.InputTextMultilineExpanding("trustSource", ref P.Config.ExtraTrustedRepos, 2000, 5);
        ImGui.Separator();
        ImGuiEx.Text($"額外更新來源");
        ImGui.Indent();
        ImGuiEx.TextWrapped(EColor.RedBright, $"除官方 Splatoon 倉庫外，Splatoon 還會從以下清單檢查腳本更新，每行一項。警告：在此新增額外清單，即代表您允許該清單的維護者在您的電腦上「不受任何限制地執行任意程式碼」。若您不當使用此功能，Splatoon 開發者與發布者將不對您的遊戲、角色、個人資料、作業系統與電腦所發生的任何可能損害負責。".Loc());
        ImGui.Unindent();
        ImGuiEx.InputTextMultilineExpanding("trustRepo", ref P.Config.ExtraUpdateLinks, 2000, 5);
    }
}
