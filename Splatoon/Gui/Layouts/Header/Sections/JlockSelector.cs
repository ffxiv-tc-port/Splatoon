using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.LanguageHelpers;
using ECommons.MathHelpers;
using NightmareUI;
using NightmareUI.PrimaryUI;
using Splatoon.Utility;

namespace Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader.Subcommands;

internal static class JlockSelector
{
    internal static string jobFilter = "";
    internal static void DrawJlockSelector(this Layout layout)
    {
        if(Player.Available)
        {
            if(layout.JobLockH.Count == 0 || layout.JobLockH.Contains(Player.Job))
            {
                ImGuiEx.HelpMarker("玩家職業符合此選項".Loc(), EColor.GreenBright, FontAwesomeIcon.Check.ToIconString(), false);
            }
            else
            {
                ImGuiEx.HelpMarker("玩家職業與此選項不符".Loc(), EColor.RedBright, FontAwesomeIcon.Times.ToIconString(), false);
            }
            ImGui.SameLine();
        }
        ImGuiEx.SetNextItemFullWidth();
        ImGuiEx.JobSelector("##jobSelector", layout.JobLockH, [ImGuiEx.JobSelectorOption.BulkSelectors, ImGuiEx.JobSelectorOption.IncludeBase], 7, "All Jobs");
    }
}
