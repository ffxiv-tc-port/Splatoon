using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SplatoonScriptsOfficial.Generic;
public unsafe sealed class DenyAllTrades : SplatoonScript
{
    public override Metadata Metadata { get; } = new(3, "NightmareXIV");
    public override HashSet<uint>? ValidTerritories { get; }

    public override Dictionary<int, string> Changelog => new()
    {
        [3] = """
        逃生口從牆鐘改成遊戲幀：卡頓時牆鐘照樣前進，會讓「同一扇視窗只送一次」的封鎖
        在最危險的那一刻提早放行（卡一次 300 毫秒的頓，250 毫秒的逃生口只撐一幀就開門）。
        改成數遊戲幀之後，遊戲沒有推進封鎖就不會解開；取不到幀序時這一輪不送（fail-closed）。
        """,
        [2] = """
        修正：拒絕交易之後有「正在關閉中」的幾幀，這期間仍然取得到交易視窗，
        對它再送一次 callback 就是攔不到的原生存取違規（遊戲當場關閉）。
        現在同一扇視窗只送一次，等它真的收掉才會再對下一扇動作；
        另外補上原本完全沒有的「視窗是否就緒」檢查。
        """
    };

    /// <summary>
    /// 已經送過 callback 的那一扇交易視窗的實例位址；<c>0</c> ＝目前沒有按過的視窗。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>只拿來做等值比較，永遠不解參。</b>記下來的那個位址隨時可能已經失效。
    /// </remarks>
    private nint PressedAddress;

    /// <summary>上面那一次送出時的<b>遊戲幀序</b>（<see cref="CurrentFrame"/>）。</summary>
    private uint PressedAtFrame;

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
    /// 🔴🔴 這裡原本每一幀無條件對「Trade」送 callback，連視窗有沒有就緒都沒有檢查。
    /// 按下之後有<b>「正在關閉中」的幾幀</b>，這期間 <c>GetAddonByName</c> 仍然回得到實例、
    /// <c>IsVisible</c> 與 <c>UldManager.LoadedState == Loaded</c> 也全部成立，
    /// 而此時再送 callback 就是原生 AccessViolationException
    /// （.NET Core 的 corrupted-state exception，<c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// <para>
    /// ⇒ 兩道防線：①<see cref="GenericHelpers.IsAddonReady(AtkUnitBase*)"/>
    /// ②同一個實例位址送過就不再送，等 <c>TryGetAddonByName</c> 取不到（＝視窗真的收掉了）才清狀態。
    /// </para>
    /// <para>
    /// 🔴 逾時放行是刻意的：萬一上一次的 callback 根本沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「永遠拒絕不了交易」的靜默失效。
    /// 逾時值遠大於「關閉中」那幾幀（60fps 下數十毫秒、卡頓時也就數百毫秒）。
    /// </para>
    /// <para>
    /// 📌 行為面：使用者原本看到的就是「交易視窗一出現就被關掉」，改完仍然是每一扇關一次，語意不變。
    /// </para>
    /// </remarks>
    public override void OnUpdate()
    {
        if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>("Trade", out var trade))
        {
            // 視窗收掉了 ⇒ 清狀態，下一扇交易視窗照樣拒絕。
            PressedAddress = 0;
            return;
        }
        if(!GenericHelpers.IsAddonReady(trade)) return;
        // 🔴 取不到幀序就這一幀不送（fail-closed）：沒有時間基準就判斷不了「這扇視窗是不是正在關閉中」。
        var now = CurrentFrame();
        if(now == null) return;
        var address = (nint)trade;
        // unchecked：FrameCounter 是 uint，溢位回繞時無號減法照樣給出正確的「過了幾幀」。
        if(PressedAddress == address && unchecked(now.Value - PressedAtFrame) < PressReleaseTimeoutFrames) return;
        PressedAddress = address;
        PressedAtFrame = now.Value;
        Callback.Fire(trade, true, -1);
    }
}
