using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Generic;
public unsafe sealed class DenyAllTrades : SplatoonScript
{
    public override Metadata Metadata { get; } = new(2, "NightmareXIV");
    public override HashSet<uint>? ValidTerritories { get; }

    public override Dictionary<int, string> Changelog => new()
    {
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

    /// <summary>上面那一次送出的時刻（<see cref="Environment.TickCount64"/>）。</summary>
    private long PressedAt;

    /// <summary>送出之後最久封鎖多久（毫秒）。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    private const long PressReleaseTimeoutMs = 2000;

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
        var address = (nint)trade;
        if(PressedAddress == address && Environment.TickCount64 - PressedAt < PressReleaseTimeoutMs) return;
        PressedAddress = address;
        PressedAt = Environment.TickCount64;
        Callback.Fire(trade, true, -1);
    }
}
