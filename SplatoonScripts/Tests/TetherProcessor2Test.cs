using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Tests
{
    public unsafe class TetherProcessor2Test : SplatoonScript
    {
        public override Metadata Metadata => new(2, "NightmareXIV");
        //__int64 __fastcall sub_14072F1E0(__int64 a1, unsigned __int8 a2, char a3, char a4, char a5)
        private delegate long ProcessActorControlPacket(GameObject* a1, byte a2, byte a3, byte a4, byte a5);
        [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 57 48 83 EC 20 41 0F B6 E9 0F B6 DA", DetourName = nameof(ProcessActorControlPacketDetour))]
        private Hook<ProcessActorControlPacket> ProcessActorControlPacketHook;


        public override HashSet<uint> ValidTerritories => [];

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

        private long ProcessActorControlPacketDetour(GameObject* a1, byte a2, byte a3, byte a4, byte a5)
        {
            // 🔴 OnDisable() 會 Dispose 這個 hook,但 detour 仍可能在飛:自己的 in-flight 呼叫,
            //    或別的外掛把 hook 疊在同一位址上、經由它的 trampoline 轉進來(Dalamud 的
            //    MultiHookTracker 就是為此存在)。裸讀 .Original 在 Dispose 之後會從
            //    CheckDisposed() 擲 ObjectDisposedException,而例外從 detour 擲回原生呼叫端
            //    是未定義行為;若後端是 MinHook(DalamudForceMinHook),Dispose 真的釋放了
            //    trampoline,那就是 use-after-free —— AVE 在 .NET Core 是 corrupted-state
            //    exception,try/catch 完全攔不到(這個 detour 根本沒有 try/catch)。
            //    .OriginalDisposeSafe 在 IsDisposed 時改用「函式位址本身」重建委派,兩種後端都安全。
            // ⚠️ 這個 hook 由 [Signature] → Hook<T>.FromAddress 建立,Hook.address ＝目標函式
            //    位址本身,所以 OriginalDisposeSafe 成立。FromImport／FromFunctionPointerVariable
            //    型的 hook(address ＝指標變數位址)絕不可照抄這一招 —— 那會把資料當程式碼跳過去。
            var hook = ProcessActorControlPacketHook;
            PluginLog.Information($"Tether lost: {GenericHelpers.Read(a1->Name)}, {a2:X8}, {a3:X8}, {a4:X8}, {a5:X8}");
            if(hook == null)
            {
                // 理論上到不了:hook 建不起來時 detour 根本不會被掛上。真的發生就要看得見。
                PluginLog.Information("[TetherProcessor2Test] hook 在呼叫途中消失,本次跳過原函式呼叫。");
                return 0;
            }
            return hook.OriginalDisposeSafe(a1, a2, a3, a4, a5);
        }
    }
}
