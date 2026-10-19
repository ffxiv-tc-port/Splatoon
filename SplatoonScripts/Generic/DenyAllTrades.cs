using ECommons;
using ECommons.Automation;
using ECommons.EzIpcManager;
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
    public override Metadata Metadata { get; } = new(4, "NightmareXIV");
    public override HashSet<uint>? ValidTerritories { get; }

    public override Dictionary<int, string> Changelog => new()
    {
        [4] = """
        Lifestream 忙碌時讓位：Lifestream 跑傳送／移動流程時自己也會取消交易，兩邊同一幀各送一次取消，
        第二次會打在正在關閉的視窗上（攔不到的原生存取違規）。現在每扇交易視窗都要等 Lifestream
        連續 30 幀（約半秒）不忙、視窗還在才拒絕，Lifestream 忙的期間絕不自己按。
        代價：每次拒絕約慢半秒（沒裝 Lifestream 也一樣）；Lifestream 走路時要等它停下才拒。
        """,
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

    /// <summary><see cref="PressedAddress"/> 那一次送出時的<b>遊戲幀序</b>（<see cref="CurrentFrame"/>）。</summary>
    private uint PressedAtFrame;

    /// <summary>送出之後最久封鎖幾個<b>遊戲幀</b>。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    /// <remarks>
    /// 🔴 逃生口用遊戲幀而不是牆鐘：視窗「正在關閉中」的長度是用<b>幀</b>算的，而牆鐘在卡頓時
    /// 照樣前進，會正好在最危險的那一刻放行。<see cref="CurrentFrame"/> 取的是遊戲主迴圈的
    /// <c>Framework.FrameCounter</c>（不是繪製幀計數器），畫面隱藏期間照樣前進，所以逃生口不會永不到期。
    /// 🔴 有逃生口是刻意的：萬一上一次的動作根本沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「這個流程從此卡住」的靜默失效。120 幀在 60fps 下約 2 秒。
    /// </remarks>
    private const uint PressReleaseTimeoutFrames = 120;

    /// <summary>正在等「連續不忙」的那一扇交易視窗位址；<c>0</c>＝沒有。只做等值比較，永遠不解參。</summary>
    /// <remarks>
    /// 不變式：每一次按（新視窗、逃生口補按）都只在 <see cref="LifestreamIsBusy"/> 連續 <see cref="YieldIdleFrames"/> 幀為 false 之後。
    /// Lifestream 只在任務佇列忙（⊂ <c>IsBusy</c>）時按 Trade ⇒ 腳本不可能跟它同幀；30 幀涵蓋它剛按完、視窗關閉中的那段。
    /// </remarks>
    private nint YieldedAddress;

    /// <summary>等待中最近一次問 <c>IsBusy</c> 的遊戲幀序。</summary>
    private uint YieldPolledAtFrame;

    /// <summary>等待中第一次看到「不忙」的遊戲幀序；<see langword="null"/>＝最近一次還是忙（或視窗未就緒）。</summary>
    private uint? YieldIdleSinceFrame;

    /// <summary>Lifestream 還在忙時，隔幾幀重問一次。</summary>
    private const uint YieldPollFrames = 10;

    /// <summary>連續幾幀不忙才按。看到不忙之後每幀都問，才算得上「連續」。</summary>
    private const uint YieldIdleFrames = 30;

    /// <summary>
    /// 對方沒載入、沒有這個端點或型別不合時，<see cref="SafeWrapper.AnyException"/> 吞掉例外回 <see langword="false"/>＝當作不忙。
    /// </summary>
    [EzIPC("Lifestream.IsBusy", false)] private Func<bool> LifestreamIsBusy;

    public override void OnSetup()
    {
        EzIPC.Init(this, safeWrapper: SafeWrapper.AnyException);
    }

    /// <summary>目前的遊戲幀序；取不到 <c>Framework</c> 時回 <see langword="null"/>。</summary>
    /// <remarks>
    /// 🔴 <c>Framework.Instance()</c> 宣告成 <c>[StaticAddress(..., isPointer: true)]</c>：登入前、登出後、
    /// 關閉流程中它真的會是 null，裸解參考就是 AccessViolationException（<c>try/catch</c> 攔不到）
    /// ⇒ 只能事前判空。<c>FrameCounter</c> 由遊戲主迴圈遞增，<b>不是</b>繪製幀計數器。
    /// </remarks>
    private static uint? CurrentFrame()
    {
        var framework = CSFramework.Instance();
        if(framework == null) return null;
        return framework->FrameCounter;
    }

    /// <remarks>
    /// 🔴🔴 按下之後有<b>「正在關閉中」的幾幀</b>，這期間 <c>GetAddonByName</c> 仍然回得到實例、
    /// <c>IsVisible</c> 與 <c>UldManager.LoadedState == Loaded</c> 也全部成立，
    /// 而此時再送 callback 就是原生 AccessViolationException（<c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// ⇒ 兩道防線：①<see cref="GenericHelpers.IsAddonReady(AtkUnitBase*)"/>
    /// ②同一個實例位址送過就不再送，等 <c>TryGetAddonByName</c> 取不到（＝視窗真的收掉了）才清狀態。
    /// 🔴 逾時放行是刻意的：上一次的 callback 沒生效、視窗還開著時，沒有逾時會變成「永遠拒絕不了交易」。
    /// </remarks>
    public override void OnUpdate()
    {
        if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>("Trade", out var trade))
        {
            // 視窗收掉了 ⇒ 清狀態，下一扇交易視窗照樣拒絕。
            PressedAddress = 0;
            YieldedAddress = 0;
            return;
        }
        var address = (nint)trade;
        if(!GenericHelpers.IsAddonReady(trade))
        {
            // 未就緒打斷「連續不忙」的計數：就緒之後重新數滿才按。
            if(YieldedAddress == address) YieldIdleSinceFrame = null;
            return;
        }
        // 🔴 取不到幀序就這一幀不送（fail-closed）：沒有時間基準就判斷不了「這扇視窗是不是正在關閉中」。
        var now = CurrentFrame();
        if(now == null) return;
        if(YieldedAddress != address)
        {
            // unchecked：FrameCounter 是 uint，溢位回繞時無號減法照樣給出正確的「過了幾幀」。
            if(PressedAddress == address && unchecked(now.Value - PressedAtFrame) < PressReleaseTimeoutFrames) return;
            // 新視窗與逃生口補按都從第 0 幀數起：那扇窗可能已經被 Lifestream 按過、正在關閉。
            YieldedAddress = address;
            YieldIdleSinceFrame = null;
            YieldPolledAtFrame = unchecked(now.Value - YieldPollFrames);
        }
        if(!YieldConfirmedIdle(now.Value)) return;
        YieldedAddress = 0;
        PressedAddress = address;
        PressedAtFrame = now.Value;
        Callback.Fire(trade, true, -1);
    }

    /// <summary>等待中每次 <c>OnUpdate</c> 呼叫；回 <see langword="true"/>＝已連續 <see cref="YieldIdleFrames"/> 幀不忙，可以按。</summary>
    private bool YieldConfirmedIdle(uint now)
    {
        if(YieldIdleSinceFrame == null && unchecked(now - YieldPolledAtFrame) < YieldPollFrames) return false;
        YieldPolledAtFrame = now;
        if(LifestreamIsBusy?.Invoke() == true)
        {
            YieldIdleSinceFrame = null;
            return false;
        }
        YieldIdleSinceFrame ??= now;
        return unchecked(now - YieldIdleSinceFrame.Value) >= YieldIdleFrames;
    }
}
