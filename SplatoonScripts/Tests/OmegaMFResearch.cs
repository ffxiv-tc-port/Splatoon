using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using ECommons.Logging;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Tests
{
    public class OmegaMFResearch : SplatoonScript
    {
        public override Metadata Metadata => new(2, "NightmareXIV");
        private delegate void ProcessActorControlPacket(uint a1, uint a2, uint a3, uint a4, uint a5, uint a6, int a7, uint a8, long a9, byte a10);
        [Signature("40 55 53 41 55 41 56 41 57 48 8D AC 24", DetourName = nameof(ProcessActorControlPacketDetour))]
        private Hook<ProcessActorControlPacket> ProcessActorControlPacketHook;


        public override HashSet<uint> ValidTerritories => [];
        private Dictionary<uint, List<string>> Values = [];
        private bool Mechanic = false;

        public override void OnEnable()
        {
            SignatureHelper.Initialise(this);
            ProcessActorControlPacketHook.Enable();
            Svc.Chat.Print($"ProcessActorControlPacketHook.Address: {ProcessActorControlPacketHook.Address:X16}");
        }

        public override void OnDisable()
        {
            // 先 Disable() 把函式序言還原,擋掉「新的」呼叫進入 detour,再 Dispose()。
            // 🔴 刻意不把欄位設成 null:仍在飛的 detour 讀到 null 會把 NullReferenceException
            //    擲回原生呼叫端,比留著已 Dispose 的物件更糟(同 Tests/EffectResultTest.cs 的先例)。
            //    ⚠️ 補上 ?. 是因為特徵碼失配時 SignatureHelper 會讓欄位留在 null,
            //    原本的裸呼叫會在停用腳本時擲 NullReferenceException。
            ProcessActorControlPacketHook?.Disable();
            ProcessActorControlPacketHook?.Dispose();
        }

        public override void OnMessage(string Message)
        {
            if(Message.Contains("(7635>31550)"))
            {
                DuoLog.Information($"Starts casting {Environment.TickCount64}");
                Values.Clear();
                DuoLog.Information($"Init");
                Mechanic = true;
            }
        }

        public override void OnUpdate()
        {
            if(Mechanic)
            {
                var casters = Svc.Objects.Where(x => x is IBattleNpc b && !b.IsTargetable() && b.IsCharacterVisible() && b.IsCasting).Cast<IBattleNpc>();
                foreach(var x in casters)
                {
                    DuoLog.Information($"Cast {x} - {Environment.TickCount64}");
                    if(Values.TryGetValue(x.EntityId, out var coll))
                    {
                        foreach(var z in coll)
                        {
                            DuoLog.Information(z);
                        }
                    }
                    Mechanic = false;
                }

            }
        }

        private void ProcessActorControlPacketDetour(uint a1, uint a2, uint a3, uint a4, uint a5, uint a6, int a7, uint a8, long a9, byte a10)
        {
            // 🔴 OnDisable() 會 Dispose 這個 hook,但 detour 仍可能在飛:自己的 in-flight 呼叫,
            //    或別的外掛把 hook 疊在同一位址上、經由它的 trampoline 轉進來(Dalamud 的
            //    MultiHookTracker 就是為此存在)。裸讀 .Original 在 Dispose 之後會從
            //    CheckDisposed() 擲 ObjectDisposedException,而例外從 detour 擲回原生呼叫端
            //    是未定義行為;若後端是 MinHook(DalamudForceMinHook),Dispose 真的釋放了
            //    trampoline,那就是 use-after-free —— AVE 在 .NET Core 是 corrupted-state
            //    exception,try/catch 完全攔不到(原本那行也剛好在 try 之外)。
            //    .OriginalDisposeSafe 在 IsDisposed 時改用「函式位址本身」重建委派,兩種後端都安全。
            // ⚠️ 這個 hook 由 [Signature] → Hook<T>.FromAddress 建立,Hook.address ＝目標函式
            //    位址本身,所以 OriginalDisposeSafe 成立。FromImport／FromFunctionPointerVariable
            //    型的 hook(address ＝指標變數位址)絕不可照抄這一招 —— 那會把資料當程式碼跳過去。
            var hook = ProcessActorControlPacketHook;
            try
            {
                if(a2 == 0x3F)
                {
                    DuoLog.Information($"Decided: {Environment.TickCount64}");
                }
                PluginLog.Information($"ActorControlPacket: {a1:X8}, {a2:X8}, {a3:X8}, {a4:X8}, {a5:X8}, {a6:X8}, {a7:X8}, {a8:X8}, {a9:X16}, {a10:X2}");
                if(!Values.ContainsKey(a1))
                {
                    Values[a1] = [];
                }
                Values[a1].Add($"{Environment.TickCount64} - {a1:X8}, {a2:X8}, {a3:X8}, {a4:X8}, {a5:X8}, {a6:X8}, {a7:X8}, {a8:X8}, {a9:X16}, {a10:X2}");
            }
            catch(Exception e) { e.Log(); }
            if(hook == null)
            {
                // 理論上到不了:hook 建不起來時 detour 根本不會被掛上。真的發生就要看得見。
                PluginLog.Information("[OmegaMFResearch] hook 在呼叫途中消失,本次跳過原函式呼叫。");
                return;
            }
            hook.OriginalDisposeSafe(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10);
        }
    }
}
