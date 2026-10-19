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
        // 🔴 Framework.Instance() 宣告為 [StaticAddress(..., isPointer: true)],它**會回 null**;
        //    特徵碼失配時則是擲 InvalidOperationException。裸解參考 null 原生指標是
        //    AccessViolationException,try/catch 完全攔不到 ⇒ 只能事前判空。
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
