using ECommons.LanguageHelpers;
using Splatoon.Gui.Windows;
using Splatoon.Modules.TranslationWorkspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.Gui.Tabs;
public unsafe static class TabTranslator
{
    public static void Draw()
    {
        ImGuiEx.TextWrapped($"""
            Beta 功能 - 可能存在問題。請謹慎操作，並以小批次進行翻譯與提交 PR。
            要開始使用翻譯工具，請複製整份 GitHub .md 檔案的文字並按下「從剪貼簿匯入頁面」按鈕，或選擇先前匯入的頁面。
            翻譯完成後，請使用「複製結果到剪貼簿」按鈕複製文字，並向原始倉庫提交拉取請求。
            """);
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "從剪貼簿匯入頁面".Loc()))
        {
            try
            {
                var page = new Page(Paste());
                if(page != null)
                {
                    P.Config.TranslatorPages.Add(page);
                    new TranslationWorkspaceWindow($"翻譯工作區".Loc() + $"##{page.ID}", page);
                }
            }
            catch(Exception e)
            {
                e.Log();
                Notify.Error(e.Message);
            }
        }
        foreach(var page in P.Config.TranslatorPages)
        {
            if(ImGui.Selectable($"{page.Name}##{page.ID}"))
            {
                try
                {
                    new TranslationWorkspaceWindow($"翻譯工作區".Loc() + $"##{page.ID}", page);
                }
                catch(Exception e)
                {
                    e.Log();
                    Notify.Error(e.Message);
                }
            }
        }
    }
}