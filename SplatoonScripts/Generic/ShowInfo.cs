using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class ShowInfo : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [0];
    public override Metadata Metadata => new(2, "NightmareXIV");
    public override void OnSettingsDraw()
    {
        // 🔴 LayoutWorld 是 [StaticAddress(..., isPointer: true)],Instance() 回傳靜態槽「裡面的
        //    值」,可以合法為 null(區域切換、讀取畫面期間)。直接 -> 解參考產生的
        //    AccessViolationException 在 .NET Core 是 corrupted-state exception,try/catch 攔不到。
        //    取不到就什麼都不顯示(fail-closed)。
        var layoutWorld = LayoutWorld.Instance();
        if(layoutWorld == null)
        {
            return;
        }

        var a = layoutWorld->ActiveLayout;
        if(a != null)
        {
            foreach(var f in a->ActiveFestivals)
            {
                ImGuiEx.Text($"{f.Id}/{f.Phase}");
            }
        }
    }
}
