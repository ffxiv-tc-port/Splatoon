using ECommons.LanguageHelpers;
using Splatoon.Utility;

namespace Splatoon;

internal partial class CGui
{
    internal static void DrawRounding(ref float x, ref float y, ref float z)
    {
        if(ImGuiEx.IconButton(FontAwesomeIcon.Hashtag))
        {
            x = MathF.Round(x);
            y = MathF.Round(y);
            z = MathF.Round(z);
        }
        ImGuiEx.Tooltip("Round coordinates to an integer");
    }

    internal static bool AddEmptyLayout(out Layout l)
    {
        if(NewLayoytName.Contains("~"))
        {
            Notify.Error("名稱不可包含保留字元: ~".Loc());
        }
        else if(NewLayoytName.Contains(","))
        {
            Notify.Error("名稱不可包含保留字元: ,".Loc());
        }
        else
        {
            l = new Layout()
            {
                Name = CGui.NewLayoytName
            };
            if(Svc.ClientState != null) l.ZoneLockH.Add(Svc.ClientState.TerritoryType);
            P.Config.LayoutsL.Add(l);
            CGui.NewLayoytName = "";
            return true;
        }
        l = default;
        return false;
    }

    private static void DrawRotationSelector(Element el)
    {
        ImGui.SameLine();
        ImGuiEx.Text("加入角度:".Loc());
        ImGui.SameLine();
        var angleDegrees = el.AdditionalRotation.RadiansToDegrees();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50f);
        ImGui.DragFloat("##ExtraAngle", ref angleDegrees, 0.1f, 0f, 360f);
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("按住 shift 加快調整速度；\n雙擊以手動輸入。".Loc());
        if(angleDegrees < 0f || angleDegrees > 360f) angleDegrees = 0f;
        el.AdditionalRotation = angleDegrees.DegreesToRadians();
        if(el.type != 1)
        {
            ImGui.SameLine();
            ImGui.Checkbox("Face##", ref el.FaceMe);
            if(el.FaceMe)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                string[] faceOptions = { "<1>", "<2>", "<3>", "<4>", "<5>", "<6>", "<7>", "<8>", "<t1>", "<t2>", "<h1>", "<h2>", "<d1>", "<d2>", "<d3>", "<d4>" };

                if(ImGui.BeginCombo("Face chara##", el.faceplayer))
                {
                    foreach(var option in faceOptions)
                    {
                        if(ImGui.Selectable(option))
                        {
                            el.faceplayer = option;
                        }
                    }
                    ImGui.EndCombo();
                }
            }
        }
    }
}
