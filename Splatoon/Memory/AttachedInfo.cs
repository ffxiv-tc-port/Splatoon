using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using ECommons.DalamudServices.Legacy;
using ECommons.EzHookManager;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using Reloaded.Hooks.Definitions.X64;
using Splatoon.Modules;
using Splatoon.SplatoonScripting;
using Splatoon.Structures;

namespace Splatoon.Memory;


#nullable enable
public static unsafe class AttachedInfo
{
    private delegate nint GameObject_ctor(nint obj);
    private static Hook<GameObject_ctor>? GameObject_ctor_hook = null;
    /// <summary>
    /// 指向原始建構子位址的委派（不經 trampoline）。<see cref="Dispose"/> 會把上面的 hook 欄位
    /// 設回 null，那時 Disable()+Dispose() 都已跑完、原始位元組已還原，直接呼叫不會遞迴回 detour。
    /// 🔴 這個 fallback 是必要的：被 hook 的是「設定 vtable 並回傳 this」的建構子，
    /// 略過原始呼叫會留下沒有 vtable 的物件，之後必定崩潰 —— 這一處不能用「略過」收尾。
    /// </summary>
    private static GameObject_ctor? GameObject_ctor_original = null;
    public static Dictionary<nint, CachedCastInfo> CastInfos = [];
    public static Dictionary<nint, List<CachedObjectEffectInfo>> ObjectEffectInfos = [];
    public static Dictionary<nint, Dictionary<string, VFXInfo>> VFXInfos = [];
    public static Dictionary<nint, List<CachedTetherInfo>> TetherInfos = [];
    /// <summary>
    /// 目前正在施法的物件：位址 → 上一次派送出去的招式 ID。
    /// </summary>
    /// <remarks>
    /// 🔴 值是招式 ID 而不是單純的位址集合（原本是 <c>HashSet&lt;nint&gt;</c>）：
    /// 同一個施法者背靠背連續施兩招、中間沒有任何一幀處於「沒在施法」的狀態時，
    /// 只比對位址會把第二招當成同一次而**完全不觸發事件**。
    /// </remarks>
    private static Dictionary<nint, uint> Casters = [];

    /// <summary>
    /// 派給 <see cref="SplatoonScript.OnStartingCast(uint, PacketActorCast*)"/> 的共用緩衝區。
    /// </summary>
    /// <remarks>
    /// 🔴 刻意用外掛生命週期內只配置一次的非受管記憶體，而不是 <c>stackalloc</c>：
    /// 指標會交到第三方腳本手上，指向已經彈出的堆疊框是「哪天有腳本把它存起來就崩」的地雷。
    /// 內容每次事件都會被覆寫，腳本仍然只該在回呼內就地讀。
    /// </remarks>
    private static PacketActorCast* CastPacketBuffer = null;

    [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
    private delegate nint ActorVfxCreateDelegate2(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7);
    private static Hook<ActorVfxCreateDelegate2>? ActorVfxCreateHook;

    internal static void Init()
    {
        Safe(delegate
        {
            var ctorAddress = Svc.SigScanner.ScanText("48 8D 05 ?? ?? ?? ?? C7 81 ?? ?? ?? ?? ?? ?? ?? ?? 48 89 01 48 8B C1 C3");
            GameObject_ctor_original ??= EzDelegate.Get<GameObject_ctor>(ctorAddress);
            GameObject_ctor_hook = Svc.Hook.HookFromAddress<GameObject_ctor>(ctorAddress, GameObject_ctor_detour);
            GameObject_ctor_hook.Enable();
        });
        Safe(delegate
        {
            var actorVfxCreateAddress = Svc.SigScanner.ScanText("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8");
            ActorVfxCreateHook = Svc.Hook.HookFromAddress<ActorVfxCreateDelegate2>(actorVfxCreateAddress, ActorVfxNewHandler);
            ActorVfxCreateHook.Enable();
        });
        if(CastPacketBuffer == null)
        {
            CastPacketBuffer = (PacketActorCast*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(PacketActorCast));
            *CastPacketBuffer = default;
        }
        Svc.Framework.Update += Tick;
    }


    internal static void Dispose()
    {
        Svc.Framework.Update -= Tick;
        if(GameObject_ctor_hook != null)
        {
            GameObject_ctor_hook.Disable();
            GameObject_ctor_hook.Dispose();
            GameObject_ctor_hook = null;
        }
        ActorVfxCreateHook?.Disable();
        ActorVfxCreateHook?.Dispose();
        // Tick 已在本方法第一行解除訂閱，且它與 Dispose 同在主執行緒，
        // 釋放之後不會再有人讀到這塊記憶體。指標歸零讓重複 Dispose 也安全。
        if(CastPacketBuffer != null)
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal((nint)CastPacketBuffer);
            CastPacketBuffer = null;
        }
        CastInfos = null!;
        VFXInfos = null!;
        ObjectEffectInfos = null!;
    }

    private static nint ActorVfxNewHandler(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7)
    {
        try
        {
            var vfxPath = Dalamud.Memory.MemoryHelper.ReadString(new nint(a1), Encoding.ASCII, 256);
            if(!VFXInfos.ContainsKey(a2))
            {
                VFXInfos[a2] = [];
            }
            VFXInfos[a2][vfxPath] = new()
            {
                SpawnTime = Environment.TickCount64
            };
            var obj = Svc.Objects.CreateObjectReference(a2)!;
            ScriptingProcessor.OnVFXSpawn(obj.EntityId, vfxPath);
            if(!Utils.BlacklistedVFX.Contains(vfxPath))
            {
                if(obj is ICharacter c)
                {
                    var targetText = c.AddressEquals(Svc.Objects.LocalPlayer) ? "me" : (c is IPlayerCharacter pc ? pc.GetJob().ToString() : c.BaseId.ToString() ?? "Unknown");
                    var text = $"VFX {vfxPath} spawned on {targetText} npc id={c.NameId}, model id={c.Struct()->ModelContainer.ModelCharaId}, name npc id={c.NameId}, position={c.Position}, name={c.Name}";
                    P.ChatMessageQueue.Enqueue(text);
                    if(P.Config.Logging) Logger.Log(text);
                    if(c is IBattleNpc) P.LogWindow.Log(text);
                }
                else
                {
                    var text = $"VFX {vfxPath} spawned on {obj.BaseId} npc id={obj.Struct()->GetNameId()}, position={obj.Position}";
                    P.ChatMessageQueue.Enqueue(text);
                    if(P.Config.Logging) Logger.Log(text);
                    if(obj is IBattleNpc) P.LogWindow.Log(text);
                }
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return ActorVfxCreateHook!.OriginalDisposeSafe(a1, a2, a3, a4, a5, a6, a7);
    }

    public static bool TryGetVfx(this IGameObject go, out Dictionary<string, VFXInfo>? fx)
    {
        if(VFXInfos.ContainsKey(go.Address))
        {
            fx = VFXInfos[go.Address];
            return true;
        }
        fx = default;
        return false;
    }

    public static List<CachedTetherInfo> GetOrCreateTetherInfo(nint ptr)
    {
        if(TetherInfos.TryGetValue(ptr, out var list))
        {
            return list;
        }
        TetherInfos[ptr] = [];
        return TetherInfos[ptr];
    }

    public static List<CachedTetherInfo> GetOrCreateTetherInfo(Character* ptr) => GetOrCreateTetherInfo((nint)ptr);

    public static bool TryGetSpecificVfxInfo(this IGameObject go, string path, out VFXInfo info)
    {
        if(TryGetVfx(go, out var dict) && dict?.ContainsKey(path) == true)
        {
            info = dict[path];
            return true;
        }
        info = default;
        return false;
    }

    private static nint GameObject_ctor_detour(nint ptr)
    {
        // 🔴 Dispose() 會把 hook 欄位與 CastInfos／VFXInfos／ObjectEffectInfos 三個字典
        //    一起設回 null，而本 detour 可能還在執行中（in-flight 呼叫）。原本這裡對五個
        //    欄位全是裸解參考、整段又沒有 try —— 任何一個為 null 就把 NullReferenceException
        //    擲回原生呼叫端，而且原始建構子完全沒被呼叫。
        //    全部先快照到區域變數，之後只用區域變數。
        var hook = GameObject_ctor_hook;
        Dictionary<nint, CachedCastInfo>? castInfos = CastInfos;
        Dictionary<nint, Dictionary<string, VFXInfo>>? vfxInfos = VFXInfos;
        Dictionary<nint, List<CachedObjectEffectInfo>>? objectEffectInfos = ObjectEffectInfos;
        try
        {
            castInfos?.Remove(ptr);
            Casters.Remove(ptr);
            vfxInfos?.Remove(ptr);
            objectEffectInfos?.Remove(ptr);
            TetherInfos.Remove(ptr);
        }
        catch(Exception e)
        {
            // 與同檔 ActorVfxNewHandler 一致：記錄用的簿記不得把例外送回原生層。
            e.Log();
        }

        // 🔴 建構子絕不能略過，理由見 GameObject_ctor_original 的註解。
        var original = hook?.OriginalDisposeSafe ?? GameObject_ctor_original;
        if(original == null)
        {
            // 實務上到不了：GameObject_ctor_original 在 hook 建立之前就已指派。
            PluginLog.Information($"GameObject constructor hook is gone and no original delegate is available; the object at {ptr:X16} is left unconstructed.");
            return ptr;
        }
        return original(ptr);
    }
    private static void Tick(object _)
    {
        foreach(var x in Svc.Objects)
        {
            if(x is IBattleChara b)
            {
                bool isCasting;
                try
                {
                    isCasting = b.Struct()->GetCastInfo() != null && b.IsCasting;
                }
                catch
                {
                    // Ignore invalid BattleChara objects that exist during cutscenes
                    continue;
                }

                if(isCasting)
                {
                    var castActionId = b.CastActionId;
                    if(!Casters.TryGetValue(b.Address, out var lastCastActionId) || lastCastActionId != castActionId)
                    {
                        CastInfos[b.Address] = new(castActionId, Environment.TickCount64 - (long)(b.CurrentCastTime * 1000));
                        Casters[b.Address] = castActionId;
                        string text;
                        if(P.Config.LogPosition)
                        {
                            text = $"{b.Name} ({x.Position}) starts casting {castActionId} ({b.NameId}>{castActionId})";
                        }
                        else
                        {
                            text = $"{b.Name} starts casting {castActionId} ({b.NameId}>{castActionId})";
                        }
                        // 🔴 順序不能顛倒：上游是 hook（早）→ 輪詢（晚），
                        // 有腳本同時 override 這兩個多載並靠先後順序區分事件來源。
                        if(CastPacketBuffer != null && PacketActorCast.TryFill(CastPacketBuffer, (Character*)b.Struct()))
                        {
                            ScriptingProcessor.OnStartingCast(b.EntityId, CastPacketBuffer);
                        }
                        ScriptingProcessor.OnStartingCast(b.EntityId, castActionId);
                        P.ChatMessageQueue.Enqueue(text);
                        if(P.Config.Logging)
                        {
                            Logger.Log(text);
                            if(b is IBattleNpc) P.LogWindow.Log(text);
                        }
                    }
                }
                else
                {
                    Casters.Remove(b.Address);
                }
            }
        }
    }

    public static bool TryGetCastTime(nint ptr, IEnumerable<uint> castId, out float castTime)
    {
        if(CastInfos.TryGetValue(ptr, out var info))
        {
            if(castId.Contains(info.ID))
            {
                castTime = (float)(Environment.TickCount64 - info.StartTime) / 1000f;
                return true;
            }
        }
        castTime = default;
        return false;
    }
}
