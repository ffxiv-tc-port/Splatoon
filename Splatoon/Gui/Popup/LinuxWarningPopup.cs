using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using ECommons.ChatMethods;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using ECommons.LanguageHelpers;
using ECommons.PartyFunctions;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Splatoon.Gui.Priority;
#nullable enable
public class LinuxWarningPopup : Window
{
    public LinuxWarningPopup() : base("Splatoon - Linux/Mac OS detected - Warning", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.SetSizeConstraints(new(500, 100), new(500, float.MaxValue));
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        IsOpen = Utils.IsLinux() && !P.Config.DX11EnabledOnMacLinux && !P.Config.DX11MacLinuxWarningHidden;
    }

    public override void Draw()
    {
        ImGuiEx.TextWrapped($"""
            偵測到 Linux 或 Mac OS 環境，因崩潰問題，DirectX11 渲染器已預設停用。
            如果您願意，可以開啟 Splatoon 設定，前往「渲染」分頁進行測試，若測試成功則可重新啟用。
            若對您無效，請直接隱藏此視窗並使用舊版渲染器。
            """);
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Cog, "開啟設定"))
        {
            P.ConfigGui.Open = true;
        }
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.EyeSlash, "永久隱藏此視窗"))
        {
            P.Config.DX11MacLinuxWarningHidden = true;
            IsOpen = false;
        }
    }
}
