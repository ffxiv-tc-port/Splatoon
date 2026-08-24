using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Tests
{
    public unsafe class GenericTest : SplatoonScript
    {
        public override Metadata Metadata => new(2, "NightmareXIV");
        public override HashSet<uint> ValidTerritories => [];
        //bool __fastcall sub_1400AA130(__int16 a1)
        //NumberArrayData_SetValueIfDifferentAndNotify(__int64 a1, int a2, int a3)
        private delegate void Func(nint a1, int a2, int a3);
        [Signature("3B 51 08 7D 15 48 8B 41 20 48 63 D2 44 39 04 90")]
        private Hook<Func> Hook;

        //char __fastcall sub_1409EFD60(__int64 a1, unsigned int a2)
        private delegate byte AddonRetainerTaskAsk_OnRequestedUpdate(nint a1, uint a2);
        [Signature("48 89 5C 24 ?? 55 48 83 EC 20 48 8B E9 8B DA 8B CA")]
        private Hook<AddonRetainerTaskAsk_OnRequestedUpdate> Hook2;

        public override void OnEnable()
        {
            SignatureHelper.Initialise(this);
            Hook.Enable();
            Hook2?.Enable();
            //DuoLog.Warning($"{Svc.SigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 0F B7 FA"):X16}");
            base.OnEnable();
            Svc.GameNetwork.NetworkMessage += GameNetwork_NetworkMessage;
        }

        private void GameNetwork_NetworkMessage(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, Dalamud.Game.Network.NetworkMessageDirection direction)
        {
            try
            {
                if(direction == Dalamud.Game.Network.NetworkMessageDirection.ZoneDown)
                {
                    //DuoLog.Information($"{opCode}");
                }
            }
            catch(Exception e)
            {

            }
        }

        public override void OnDisable()
        {
            // 先 Disable() 把函式序言還原,擋掉「新的」呼叫進入 detour,再 Dispose()。
            // 🔴 刻意不把欄位設成 null:仍在飛的 detour 讀到 null 會把 NullReferenceException
            //    擲回原生呼叫端,比留著已 Dispose 的物件更糟(同 Tests/EffectResultTest.cs 的先例)。
            Hook?.Disable();
            Hook?.Dispose();
            Hook2?.Disable();
            Hook2?.Dispose();
            Svc.GameNetwork.NetworkMessage -= GameNetwork_NetworkMessage;
            base.OnDisable();
        }

        private byte Detour2(nint a1, uint a2)
        {
            // 🔴 OnDisable() 會 Dispose 這個 hook,但 detour 仍可能在飛:自己的 in-flight 呼叫,
            //    或別的外掛把 hook 疊在同一位址上、經由它的 trampoline 轉進來(Dalamud 的
            //    MultiHookTracker 就是為此存在)。裸讀 .Original 在 Dispose 之後會從
            //    CheckDisposed() 擲 ObjectDisposedException,而例外從 detour 擲回原生呼叫端
            //    是未定義行為;若後端是 MinHook(DalamudForceMinHook),Dispose 真的釋放了
            //    trampoline,那就是 use-after-free —— AVE 在 .NET Core 是 corrupted-state
            //    exception,try/catch 完全攔不到。
            //    .OriginalDisposeSafe 在 IsDisposed 時改用「函式位址本身」重建委派,兩種後端都安全。
            // ⚠️ 這個 hook 由 [Signature] → Hook<T>.FromAddress 建立,Hook.address ＝目標函式
            //    位址本身,所以 OriginalDisposeSafe 成立。FromImport／FromFunctionPointerVariable
            //    型的 hook(address ＝指標變數位址)絕不可照抄這一招 —— 那會把資料當程式碼跳過去。
            var hook = Hook2;
            if(hook == null)
            {
                // 理論上到不了:hook 建不起來時 detour 根本不會被掛上。真的發生就要看得見。
                PluginLog.Information("[GenericTest] Hook2 在呼叫途中消失,本次跳過原函式呼叫。");
                return 0;
            }
            var ret = hook.OriginalDisposeSafe(a1, a2);
            try
            {
                //if (Debugger.IsAttached) Debugger.Break();
                /*var v3 = *(nint*)(a2 + 840);
                var p1 = *(nint*)(v3 + 32);
                var v6 = (uint*)(*(nint*)(v3 + 32) + 1160);
                var ptr2 = *(nint*)(a1 + 560);
                DuoLog.Information($"v3:{v3:X16}, p1:{p1:X16}, v6:{(nint)v6:X16}/{*v6:X16}, ptr2:{ptr2:X16}");*/
                PluginLog.Information($"{a1:X16}, {a2}, {ret:X2}");
            }
            catch(Exception e)
            {
                e.Log();
            }
            return ret;
        }

        private void Detour(nint a1, int a2, int a3)
        {
            // 🔴 同 Detour2:欄位先快照成區域變數,呼叫改用 .OriginalDisposeSafe。
            //    這裡的 try/catch 只包住診斷輸出,原本最後那行裸的 Hook.Original 在
            //    try 之外,Dispose 之後擲出的 ObjectDisposedException 連攔都攔不到。
            var hook = Hook;
            try
            {

                var v3 = *(nint*)(a1 + 32);
                var r = (int*)(v3 + 4 * a2);
                if(a1 > 0x0000020F4BD8ADA8 - 0x50 && a1 < 0x0000020F4BD8ADA8 + 0x50)
                {
                    PluginLog.Information($"{a1}, {a2}, {a3}");
                }
                if((nint)r == 0x0000020F4BD8ADA8)
                {
                    DuoLog.Information($"{a2}, {a3}");
                    //if (Debugger.IsAttached) Debugger.Break();
                }
            }
            catch(Exception e)
            {
                e.Log();
            }
            if(hook == null)
            {
                PluginLog.Information("[GenericTest] Hook 在呼叫途中消失,本次跳過原函式呼叫。");
                return;
            }
            hook.OriginalDisposeSafe(a1, a2, a3);
        }

        public override void OnSettingsDraw()
        {
            if(GenericHelpers.TryGetAddonByName<AtkUnitBase>("GuildLeve", out var addon) && GenericHelpers.IsAddonReady(addon) && ImGui.Button("Click"))
            {
                //DuoLog.Information($"{(nint)(addon->AtkEventListener.vfunc[2]):X16}");
                var list = addon->UldManager.NodeList[11];
            }
        }
    }
}
