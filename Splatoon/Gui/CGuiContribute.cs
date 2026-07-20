using ECommons.LanguageHelpers;

namespace Splatoon.ConfigGui;

internal class Contribute
{
    internal static void OpenGithubPresetSubmit()
    {
        var url = "https://github.com/PunishXIV/Splatoon/tree/main/Presets#adding-your-preset";
        Svc.Chat.Print("[Splatoon] 如何提交您的預設: ".Loc() + url);
        Utils.ProcessStart(url);
    }

    internal static void OpenDiscordLink()
    {
        Svc.Chat.Print("[Splatoon] 伺服器邀請連結: ".Loc() + Splatoon.DiscordURL);
        Utils.ProcessStart(Splatoon.DiscordURL);
    }

    internal static void Draw()
    {
        ImGui.PushID("contribute");
        ImGui.PushTextWrapPos();
        ImGuiEx.Text("如果您喜歡 Splatoon，可以考慮以下列任一方式做出貢獻:".Loc());
        ImGui.Separator();
        ImGuiEx.Text("- 提供新戰鬥的戰鬥資料".Loc());
        ImGuiEx.Text("每當新戰鬥推出時，取得其戰鬥資料對我幫助很大。如果您搶先體驗這些戰鬥並願意貢獻戰鬥資料，請透過 Discord 聯絡我以取得相關說明。".Loc());
        if(ImGui.Button("開啟 Discord 伺服器##2".Loc()))
        {
            OpenDiscordLink();
        }
        ImGui.Separator();
        ImGuiEx.Text("- 將你自己的預設公開分享".Loc());
        ImGuiEx.Text("Splatoon 有幫助你打過團本、解決過機制，或以任何方式改善你的遊戲體驗嗎？請考慮將您的預設公開分享，讓其他人也能受益！".Loc());
        ImGuiEx.Text("如果您有帳號，可以將其送至 Github，或傳送到我的 Discord 伺服器。".Loc());
        if(ImGui.Button("開啟 Github 頁面".Loc()))
        {
            OpenGithubPresetSubmit();
        }
        ImGui.SameLine();
        if(ImGui.Button("開啟 Discord 伺服器".Loc()))
        {
            OpenDiscordLink();
        }
        ImGui.Separator();
        ImGuiEx.Text("- 給倉庫加個星星".Loc());
        ImGuiEx.Text("沒有任何預設可以分享？您仍然可以透過為 Splatoon 及我的其他外掛倉庫加星星來提供幫助！".Loc());
        ImGuiEx.Text("若要這麼做，您只需要一個 Github 帳號。登入後，前往下方連結並點擊頁面右上角的「Star」按鈕即可。".Loc());
        if(ImGui.Button("開啟 Splatoon 倉庫".Loc()))
        {
            var url = "https://github.com/PunishXIV/Splatoon";
            Svc.Chat.Print("[Splatoon] Splatoon 倉庫: ".Loc() + url);
            Utils.ProcessStart(url);
        }
        /*ImGui.SameLine();
        if (ImGui.Button("開啟 NightmareXIV 外掛倉庫".Loc()))
        {
            var url = "https://github.com/NightmareXIV/MyDalamudPlugins";
            Svc.Chat.Print("[Splatoon] NightmareXIV 外掛倉庫: ".Loc() + url);
            ProcessStart(url);
        }*/
        ImGui.Separator();
        ImGuiEx.Text("- 財務支持".Loc());
        ImGuiEx.Text("如果您想在財務上支持我，可以使用 Patreon、Ko-Fi 或加密貨幣。財務支持能讓我投入更多時間開發外掛！".Loc());

        if(ImGui.Button("Patreon"))
        {
            ShellStart("https://subscribe.nightmarexiv.com/");
        }
        ImGui.SameLine();
        if(ImGui.Button("Ko-Fi"))
        {
            ShellStart("https://donate.nightmarexiv.com/");
        }
        ImGui.SameLine();
        if(ImGui.Button("加密貨幣"))
        {
            ShellStart("https://crypto.nightmarexiv.com/");
        }

        ImGuiEx.Text("感謝您的貢獻！".Loc());
        ImGui.PopTextWrapPos();
        ImGui.PopID();
    }
}
