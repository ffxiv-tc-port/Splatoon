using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class CloseReplayWindows : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [];
    public override Metadata? Metadata => new(4, "NightmareXIV");

    public override Dictionary<int, string> Changelog => new()
    {
        [4] = """
        逃生口從牆鐘改成遊戲幀：卡頓時牆鐘照樣前進，會讓「同一扇視窗只送一次」的封鎖
        在最危險的那一刻提早放行（卡一次 300 毫秒的頓，250 毫秒的逃生口只撐一幀就開門）。
        改成數遊戲幀之後，遊戲沒有推進封鎖就不會解開；取不到幀序時這一輪不送（fail-closed）。
        """,
        [3] = """
        修正：彈窗按下之後有「正在關閉中」的幾幀，這期間 IsReady 三關仍然全部成立，
        對它再送一次 callback 就是攔不到的原生存取違規（遊戲當場關閉）。
        現在同一扇視窗只送一次，等它真的收掉才會再對下一扇動作。
        """
    };

    private string[] StandardAddons = [
        "ShopExchangeItem",
        "TelepotTown",
        "Talk",
        "MKDTowerEntry",
        "ShopExchangeCurrency",
        ];
    private (string Name, Action<Pointer<AtkUnitBase>> Action)[] SpecialAddons = [];

    /// <summary>
    /// 每個 addon 名字底下「已經送過 callback 的那一個實例位址」與送出的時刻。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>位址只拿來做等值比較，永遠不解參</b>——所以 <see cref="TryBeginPress"/> 收的是
    /// <see cref="nint"/> 而不是指標，讓「不解參」變成型別上就辦不到的事。
    /// <para>
    /// ⚠️ 刻意<b>不</b>在停用時清空：留著的舊項目是無害的（位址對不上就直接放行），
    /// 而清空反而會讓一扇正在關閉中的視窗重新變成可按。
    /// 項目數上限就是上面兩個陣列的長度。
    /// </para>
    /// </remarks>
    private readonly Dictionary<string, (nint Address, uint AtFrame)> Pressed = new(StringComparer.Ordinal);

    /// <summary>送出之後最久封鎖幾個<b>遊戲幀</b>。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    /// <remarks>
    /// 🔴 逃生口用遊戲幀而不是牆鐘：視窗「正在關閉中」的長度是用<b>幀</b>算的，而牆鐘在卡頓時
    /// 照樣前進 —— 卡一次 300 毫秒的頓，牆鐘版的逃生口只撐了一幀就開門，正好在最危險的那一刻放行。
    /// 改用遊戲幀之後，遊戲沒有推進，逃生口就不會走。
    /// <para>
    /// 📌 這<b>不是</b>繪製幀計數器：<see cref="CurrentFrame"/> 取的是遊戲主迴圈的
    /// <c>Framework.FrameCounter</c>，畫面隱藏（過場／隱藏 UI 熱鍵）期間照樣前進，
    /// 所以逃生口不會永不到期。
    /// </para>
    /// <para>
    /// 🔴 有逃生口是刻意的：萬一上一次的動作根本沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「這個流程從此卡住」的靜默失效。
    /// 120 幀在 60fps 下約 2 秒，遠大於「關閉中」那幾幀。
    /// </para>
    /// </remarks>
    private const uint PressReleaseTimeoutFrames = 120;

    /// <summary>目前的遊戲幀序；取不到 <c>Framework</c> 時回 <see langword="null"/>。</summary>
    /// <remarks>
    /// 🔴 <c>Framework.Instance()</c> 宣告成 <c>[StaticAddress(..., isPointer: true)]</c>：回的是靜態位址裡
    /// 存放的那個指標，產生器只在特徵碼失配時擲例外、對取回的值<b>不判空</b>。登入前、登出後、
    /// 關閉流程中它真的會是 null，裸解參考就是 AccessViolationException（.NET Core 的
    /// corrupted-state exception，<c>try/catch</c> 攔不到）⇒ 只能事前判空。
    /// <para>
    /// 📌 <c>FrameCounter</c> 由遊戲主迴圈遞增，<b>不是</b>繪製幀計數器；ECommons 的
    /// <c>FrameDelayTask</c>（「延遲 N 幀」）用的就是同一個來源。
    /// </para>
    /// </remarks>
    private static uint? CurrentFrame()
    {
        var framework = CSFramework.Instance();
        if(framework == null) return null;
        return framework->FrameCounter;
    }

    /// <remarks>
    /// 🔴🔴 <c>IsReady</c> 三關（非 null／<c>IsVisible</c>／<c>UldManager.LoadedState == Loaded</c>）
    /// <b>擋不住「按下之後正在關閉中」的那幾幀</b>：那期間三關全過，而視窗其實已經在拆，
    /// 此時再送 callback 就是原生 AccessViolationException
    /// （corrupted-state exception，<c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// 而這支腳本是<b>每一幀</b>都會回來重送的，清單裡又含確認框類的視窗。
    /// <para>
    /// ⇒ 加一道「同一個實例位址送過就不再送」的閘門，等 <c>TryGetAddonByName</c> 取不到
    /// （＝視窗真的收掉了）才清掉該名字的狀態。這裡刻意用「取不到」當解除點而不是輪詢生命週期事件，
    /// 因為 <see cref="OnUpdate"/> 本來就每幀都會被呼叫，視窗消失的那一幀一定看得到。
    /// </para>
    /// <para>
    /// 🔴 逾時放行是刻意的：萬一上一次的 callback 沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「回放中彈窗永遠關不掉」的靜默失效。
    /// </para>
    /// </remarks>
    public override void OnUpdate()
    {
        if(!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback]) return;
        foreach(var x in StandardAddons)
        {
            if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>(x, out var addon))
            {
                Pressed.Remove(x);
                continue;
            }
            if(!GenericHelpers.IsAddonReady(addon)) continue;
            if(!TryBeginPress(x, (nint)addon)) continue;
            Callback.Fire(addon, true, -1);
        }
        foreach(var x in SpecialAddons)
        {
            if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>(x.Name, out var addon))
            {
                Pressed.Remove(x.Name);
                continue;
            }
            if(!GenericHelpers.IsAddonReady(addon)) continue;
            if(!TryBeginPress(x.Name, (nint)addon)) continue;
            x.Action(addon);
        }
    }

    /// <summary>
    /// 登記「即將對這扇視窗送出 callback」。<b>回 <see langword="false"/> ＝這一幀絕對不能送。</b>
    /// </summary>
    /// <remarks>
    /// 一回 <see langword="true"/> 就已經把「送過了」記下去，所以呼叫點必須<b>緊接在送出動作之前</b>；
    /// 登記完卻不送的話會白白封鎖到逾時為止。
    /// <para>
    /// 📌 位址不同就放行：同一個名字底下現在掛的是另一個實例，我們送過的那扇已經取不到了。
    /// （位址被新視窗重用也不成問題：頂多多等到逾時，不會變成崩潰。）
    /// </para>
    /// </remarks>
    private bool TryBeginPress(string name, nint address)
    {
        // 🔴 取不到幀序就這一輪不送（fail-closed）：沒有時間基準就判斷不了「這扇視窗是不是正在關閉中」。
        var now = CurrentFrame();
        if(now == null) return false;
        // unchecked：FrameCounter 是 uint，溢位回繞時無號減法照樣給出正確的「過了幾幀」。
        if(Pressed.TryGetValue(name, out var prev) && prev.Address == address
            && unchecked(now.Value - prev.AtFrame) < PressReleaseTimeoutFrames)
        {
            return false;
        }
        Pressed[name] = (address, now.Value);
        return true;
    }
}
