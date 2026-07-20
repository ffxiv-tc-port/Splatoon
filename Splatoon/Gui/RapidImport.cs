using ECommons.LanguageHelpers;
using ECommons.Reflection;
using Splatoon.SplatoonScripting;

namespace Splatoon.Gui;

internal static class RapidImport
{
    internal static bool RapidImportEnabled = false;
    internal static void Draw()
    {
        if(ImGui.Checkbox("啟用快速匯入", ref RapidImportEnabled))
        {
            ImGui.SetClipboardText("");
        }
        ImGuiEx.TextWrapped("只需複製即可輕鬆匯入多個預設。Splatoon 會讀取您的剪貼簿並嘗試匯入您複製的內容。啟用後您的剪貼簿內容會被清空。".Loc());
        if(RapidImportEnabled)
        {
            try
            {
                var text = ImGui.GetClipboardText();
                if(text != "")
                {
                    if(ScriptingProcessor.IsUrlTrusted(text))
                    {
                        TryNotify("正在從可信任網址下載腳本".Loc());
                        ScriptingProcessor.DownloadScript(text, false);
                    }
                    else
                    {
                        if(CGui.ImportFromClipboard())
                        {
                            TryNotify("匯入成功".Loc());
                        }
                        else
                        {
                            TryNotify("匯入失敗".Loc());
                        }
                    }
                    ImGui.SetClipboardText("");
                }
            }
            catch(Exception e)
            {
                //
            }
        }
    }

    private static void TryNotify(string s)
    {
        P.NotificationMasterApi.DisplayTrayNotification("Splatoon", s);
    }
}
