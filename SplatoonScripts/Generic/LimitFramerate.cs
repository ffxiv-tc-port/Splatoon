using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class LimitFramerate : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = null;
    private long LastFrame;
    public override Metadata Metadata => new(2, "NightmareXIV");

    public override void OnUpdate()
    {
        // 🔴 Framework.Instance() 宣告為 [StaticAddress(..., isPointer: true)]:產生器讀的是
        //    「指標的位址」再解參考一層,所以它**會回 null**(不帶 isPointer 的那種才保證
        //    非 null,失效時是擲 InvalidOperationException),而特徵碼失配時它同樣會擲。
        //    裸解參考 null 原生指標是 AccessViolationException,在 .NET Core 屬
        //    corrupted-state exception,try/catch 完全攔不到 ⇒ 只能事前判空。
        //    這裡是 OnUpdate(每幀),判空後直接 return、不寫 log ——
        //    fail-closed:狀態不明時不做限幀睡眠(不睡覺比崩潰安全)。
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

        if(framework->WindowInactive
            && !Svc.Condition[ConditionFlag.OccupiedInQuestEvent])
        {
            var diff = Environment.TickCount64 - LastFrame;
            if(diff >= 0 && diff < 16)
            {
                Thread.Sleep((int)(16 - diff));
            }
            LastFrame = Environment.TickCount64;
        }
    }
}
