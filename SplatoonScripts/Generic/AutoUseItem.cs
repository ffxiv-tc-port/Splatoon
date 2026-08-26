using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class AutoUseItem : SplatoonScript
{
    private const uint PotionID = 12345;
    public override HashSet<uint>? ValidTerritories { get; } = [];
    public override Metadata Metadata => new(2, "NightmareXIV");

    public override void OnUpdate()
    {
        if(Player.Available && (float)Player.Object.CurrentHp / (float)Player.Object.MaxHp < 0.3f && InventoryManager.Instance()->GetInventoryItemCount(PotionID) + InventoryManager.Instance()->GetInventoryItemCount(PotionID, true) > 0)
        {
            if(!Player.IsAnimationLocked && ActionManager.Instance()->GetActionStatus(ActionType.Item, PotionID) == 0 && EzThrottler.Throttle("AutoUsePot"))
            {
                // 🔴 AgentInventoryContext.Instance() 由 [Agent(AgentId.InventoryContext)]
                //    產生:內部鏈 AgentModule -> UIModule -> Framework,任一層回 null 整條就
                //    回 null(登入前、切場景時是常態),底層 [StaticAddress]/[MemberFunction]
                //    特徵碼失配時改為擲 InvalidOperationException——兩種失效模式並存。
                //    裸解參考 null 原生指標是 AccessViolationException,在 .NET Core 屬
                //    corrupted-state exception,try/catch 攔不到 ⇒ 只能事前判空。
                //    fail-closed:取不到 agent 就不用道具。這裡是 OnUpdate(每幀),不寫 log。
                AgentInventoryContext* agent;
                try
                {
                    agent = AgentInventoryContext.Instance();
                }
                catch
                {
                    agent = null;
                }

                if(agent == null) return;
                agent->UseItem(PotionID);
            }
        }
    }
}
