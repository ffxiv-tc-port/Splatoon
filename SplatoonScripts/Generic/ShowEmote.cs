using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
#nullable disable

namespace SplatoonScriptsOfficial.Generic;

public unsafe class ShowEmote : SplatoonScript
{
    public override HashSet<uint> ValidTerritories => null;
    public override Metadata Metadata => new(7, "NightmareXIV");

    private delegate long OnEmoteFuncDelegate(IntPtr a1, GameObject* source, ushort emoteId, GameObjectId targetId, long a5);
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 30 4C 8B 74 24 ?? 48 8B D9", DetourName = nameof(OnEmoteFuncDetour))]
    private Hook<OnEmoteFuncDelegate> OnEmoteFuncHook;

    public override void OnEnable()
    {
        SignatureHelper.Initialise(this);
        OnEmoteFuncHook?.Enable();
    }

    public override void OnDisable()
    {
        Svc.Commands.RemoveHandler("/playemote");
        // 先 Disable() 把函式序言還原,擋掉「新的」呼叫進入 detour,再 Dispose()。
        // (Dalamud 的 Dispose() 內部也會 Disable(),這裡寫明是為了讓卸載時序看得見。)
        // 🔴 刻意不把 OnEmoteFuncHook 設成 null:仍在飛的 detour 讀到 null 會把
        //    NullReferenceException 擲回原生呼叫端,比留著已 Dispose 的物件更糟。
        OnEmoteFuncHook?.Disable();
        OnEmoteFuncHook?.Dispose();
    }

    public override void OnSettingsDraw()
    {
        ImGui.Checkbox("Display emotes on all targets", ref Controller.GetConfig<Config>().ShowOnOthers);
    }

    private long OnEmoteFuncDetour(IntPtr a1, GameObject* source, ushort emoteId, GameObjectId targetId, long a5)
    {
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
        var hook = OnEmoteFuncHook;
        try
        {
            if(targetId == Svc.Objects.LocalPlayer?.EntityId)
            {
                var emoteName = Svc.Data.GetExcelSheet<Emote>()?.GetRowOrDefault(emoteId)?.Name;
                Svc.Chat.Print($">> {GenericHelpers.Read(source->Name)} uses {emoteName} on you.");
            }
            else if(Controller.GetConfig<Config>().ShowOnOthers)
            {
                var emoteName = Svc.Data.GetExcelSheet<Emote>()?.GetRowOrDefault(emoteId)?.Name;
                var target = Svc.Objects.FirstOrDefault(x => (ulong)x.Struct()->GetGameObjectId() == (ulong)targetId);
                Svc.Chat.Print($">> {GenericHelpers.Read(source->Name)} uses {emoteName}" + (target != null ? $" on {target.Name}" : ""));
            }
        }
        catch(Exception e)
        {
            Svc.Chat.Print($"{e.Message}\n{e.StackTrace}");
        }
        if(hook == null)
        {
            // 理論上到不了:hook 建不起來時 detour 根本不會被掛上。真的發生就要看得見。
            PluginLog.Information("[ShowEmote] hook 在呼叫途中消失,本次跳過原函式呼叫。");
            return 0;
        }
        return hook.OriginalDisposeSafe(a1, source, emoteId, targetId, a5);
    }

    public class Config : IEzConfig
    {
        public bool ShowOnOthers = false;
    }
}
