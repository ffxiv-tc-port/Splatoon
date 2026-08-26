using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices.Legacy;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ECommons.Interop;

namespace SplatoonScriptsOfficial.Generic
{
    public unsafe class ShowTooltipOnKey : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => [];
        public override Metadata? Metadata => new(4, "NightmareXIV");

        private bool keyState = false;
        private Config Conf = null!;

        private delegate long AddonItemDetail_Show(long a1, byte a2, uint a3);
        [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 41 8B F8 0F B6 F2 48 8B D9 E8 ?? ?? ?? ?? F6 80 ?? ?? ?? ?? ?? 74 0F 44 8B C7 40 0F B6 D6 48 8B CB E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 20 5F C3 CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24", DetourName = nameof(AddonItemDetail_ShowDetour), Fallibility = Fallibility.Fallible)]
        private Hook<AddonItemDetail_Show> AddonItemDetail_ShowHook = null!;

        private long AddonItemDetail_ShowDetour(long a1, byte a2, uint a3)
        {
            //DuoLog.Information($"{a1:X16}");
            // 🔴 OnDisable() 會 Dispose 這個 hook,但 detour 仍可能在飛:自己的 in-flight 呼叫,
            //    或別的外掛把 hook 疊在同一位址上、經由它的 trampoline 轉進來(Dalamud 的
            //    MultiHookTracker 就是為此存在)。裸讀 .Original 在 Dispose 之後會從
            //    CheckDisposed() 擲 ObjectDisposedException,而例外從 detour 擲回原生呼叫端
            //    是未定義行為;若後端是 MinHook(DalamudForceMinHook),Dispose 真的釋放了
            //    trampoline,那就是 use-after-free —— AVE 在 .NET Core 是 corrupted-state
            //    exception,try/catch 完全攔不到。
            //    .OriginalDisposeSafe 在 IsDisposed 時改用「函式位址本身」重建委派,兩種後端都安全。
            // ⚠️ 這個 hook 由 [Signature] → Hook<T>.FromAddress 建立(Reloaded/MinHook 後端),
            //    Hook.address ＝目標函式位址本身,所以 OriginalDisposeSafe 成立。
            //    FromImport／FromFunctionPointerVariable 型的 hook(address ＝指標變數位址)
            //    絕不可照抄這一招 —— 那會把資料當程式碼跳過去。
            var hook = AddonItemDetail_ShowHook;
            if(hook == null)
            {
                // 特徵碼標成 Fallibility.Fallible,失配時這個欄位會真的是 null —— 不過那種情況下
                // hook 根本沒掛上,detour 也不會被呼叫。真的走到這裡就要看得見。
                PluginLog.Information("[ShowTooltipOnKey] hook 在呼叫途中消失,本次跳過原函式呼叫。");
                return 0;
            }
            var ret = hook.OriginalDisposeSafe(a1, a2, a3);
            try
            {
                if(!Bitmask.IsBitSet(TerraFX.Interop.Windows.Windows.GetKeyState((int)Conf.Key), 15))
                {
                    ((AtkUnitBase*)a1)->IsVisible = false;
                }
            }
            catch(Exception e)
            {
                e.Log();
            }
            return ret;
        }

        public override void OnEnable()
        {
            SignatureHelper.Initialise(this);
            AddonItemDetail_ShowHook?.Enable();
        }

        public override void OnDisable()
        {
            // 🔴 刻意不把 AddonItemDetail_ShowHook 設成 null:仍在飛的 detour 讀到 null 會把
            //    NullReferenceException 擲回原生呼叫端,比留著已 Dispose 的物件更糟。
            AddonItemDetail_ShowHook?.Disable();
            AddonItemDetail_ShowHook?.Dispose();
        }

        public override void OnSetup()
        {
            Conf = Controller.GetConfig<Config>();
        }

        public override void OnSettingsDraw()
        {
            ImGui.SetNextItemWidth(200f);
            if(ImGui.BeginCombo("##inputKey", $"{Conf.Key}"))
            {
                var block = false;
                if(ImGui.Selectable("Cancel"))
                {
                }
                if(ImGui.IsItemHovered()) block = true;
                if(ImGui.Selectable("Clear"))
                {
                    Conf.Key = Keys.None;
                }
                if(ImGui.IsItemHovered()) block = true;
                if(!block)
                {
                    ImGuiEx.Text(GradientColor.Get(ImGuiColors.ParsedGreen, ImGuiColors.DalamudRed), "Now press new key...");
                    foreach(var x in Enum.GetValues<Keys>())
                    {
                        if(Bitmask.IsBitSet(TerraFX.Interop.Windows.Windows.GetKeyState((int)x), 15))
                        {
                            ImGui.CloseCurrentPopup();
                            Conf.Key = x;
                            break;
                        }
                    }
                }
                ImGui.EndCombo();
            }
            if(Conf.Key != Keys.None)
            {
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                {
                    Conf.Key = Keys.None;
                }
            }
        }

        private class Config : IEzConfig
        {
            public Keys Key = Keys.ControlKey;
        }
    }
}
