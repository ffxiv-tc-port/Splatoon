using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Text;

using ECommons.DalamudServices.Legacy;

namespace SplatoonScriptsOfficial.Generic;

public class CustomResolutionSwitcher : SplatoonScript
{
    public override Metadata Metadata { get; } = new(2, "NightmareXIV");
    public override HashSet<uint>? ValidTerritories { get; } = null;

    bool PreviousState = false;

    public unsafe override void OnUpdate()
    {
        // 🔴 Framework.Instance() 宣告為 [StaticAddress(..., isPointer: true)]:產生器讀的是
        //    「指標的位址」再解參考一層,所以它**會回 null**(不帶 isPointer 的那種才保證
        //    非 null,失效時是擲 InvalidOperationException),而特徵碼失配時它同樣會擲。
        //    裸解參考 null 原生指標是 AccessViolationException,在 .NET Core 屬
        //    corrupted-state exception,try/catch 完全攔不到 ⇒ 只能事前判空。
        //    這裡是 OnUpdate(每幀),所以判空後直接 return、不寫 log、也不動
        //    PreviousState —— fail-closed:狀態不明時不切解析度,等下一幀再判。
        Framework* framework;
        try
        {
            framework = Framework.Instance();
        }
        catch
        {
            return;
        }

        if(framework == null) return;

        var newState = framework->WindowInactive;
        if(newState != PreviousState)
        {
            PreviousState = newState;
            if(newState)
            {
                On();
            }
            else
            {
                Off();
            }
        }
    }

    void On()
    {
        Svc.Commands.ProcessCommand($"/gres {C.Resolution}");
        Svc.Commands.ProcessCommand("/gres on");
    }

    void Off()
    {
        Svc.Commands.ProcessCommand("/gres off");
    }

    public override void OnDisable()
    {
        Off();
    }

    public override void OnSettingsDraw()
    {
        ImGui.SetNextItemWidth(200f);
        ImGui.InputFloat("Resolution when minimized", ref C.Resolution);
    }

    Config C => Controller.GetConfig<Config>();
    public class Config : IEzConfig
    {
        public float Resolution = 0.25f;
    }
}
