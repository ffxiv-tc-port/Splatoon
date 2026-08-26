using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices.Legacy;
using Splatoon.Modules;
using Splatoon.SplatoonScripting;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Splatoon.Memory;

internal unsafe class ObjectEffectProcessor
{
    internal delegate long ProcessObjectEffect(GameObject* a1, ushort a2, ushort a3, long a4);
    [Signature("4C 8B DC 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 6B F0 48 8B D9 49 89 7B E0", DetourName = nameof(ProcessObjectEffectDetour), Fallibility = Fallibility.Fallible)]
    internal Hook<ProcessObjectEffect> ProcessObjectEffectHook = null;
    internal long ProcessObjectEffectDetour(GameObject* a1, ushort a2, ushort a3, long a4)
    {
        try
        {
            // 🔴 a1 是遊戲直接傳進 detour 的原生指標，這裡對它解參考四次
            //（Name／EntityId／BaseId／ObjectKind）都沒判空。
            // 漏判的代價是在 detour 裡吃 AccessViolation —— corrupted-state exception，
            // 下面那個 catch 完全攔不到。
            // 另外 (nint)a1 為 0 時還會在 ObjectEffectInfos 裡註冊一筆 key=0 的假紀錄。
            // a1 為 null 就整段跳過（不記錄、不派事件），原函式照樣呼叫，不改變遊戲行為。
            if(a1 != null)
            {
                if(P.Config.Logging)
                {
                    var text = $"ObjectEffect: on {a1->Name.Read()} {a1->EntityId.Format()}/{a1->BaseId.Format()} data {a2}, {a3}";
                    Logger.Log(text);
                    if(a1->ObjectKind != FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc) P.LogWindow.Log(text);
                }
                var ptr = (nint)a1;
                if(!AttachedInfo.ObjectEffectInfos.ContainsKey(ptr))
                {
                    AttachedInfo.ObjectEffectInfos[ptr] = [];
                }
                AttachedInfo.ObjectEffectInfos[ptr].Add(new()
                {
                    StartTime = Environment.TickCount64,
                    data1 = a2,
                    data2 = a3
                });
                ScriptingProcessor.OnObjectEffect(a1->EntityId, a2, a3);
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return ProcessObjectEffectHook.OriginalDisposeSafe(a1, a2, a3, a4);
    }

    internal ObjectEffectProcessor()
    {
        try
        {
            SignatureHelper.Initialise(this);
            Enable();
        }
        catch(Exception e)
        {
            e.LogWarning();
        }
    }

    internal void Enable()
    {
        try
        {
            if(!ProcessObjectEffectHook.IsEnabled) ProcessObjectEffectHook.Enable();
        }

        catch(Exception e)
        {
            e.LogWarning();
        }
    }

    internal void Disable()
    {
        try
        {
            if(ProcessObjectEffectHook.IsEnabled) ProcessObjectEffectHook.Disable();
        }
        catch(Exception e)
        {
            e.LogWarning();
        }
    }

    public void Dispose()
    {
        try
        {
            Disable();
            ProcessObjectEffectHook.Dispose();
        }
        catch(Exception e)
        {
            e.LogWarning();
        }
    }
}
