using ECommons;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Text;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class AutoRetainerCreation : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [];
    public string SymbolsA = "qwrtpsdfghjklzxcvbnm";
    public string SymbolsB = "eyuioa";

    public override Metadata? Metadata => new(3, "NightmareXIV");

    public override Dictionary<int, string> Changelog => new()
    {
        [3] = """
        修正：僱員外觀確認鈕按下之後有「正在關閉中」的幾幀，這期間視窗仍然通過就緒檢查，
        對它再送一次點擊就是攔不到的原生存取違規（遊戲當場關閉）。
        現在每一扇視窗都只按一次，等它真的收掉才會再對下一扇動作。
        """
    };

    /// <summary>
    /// 每個 addon 名字底下「已經按過的那一個實例位址」與按下的時刻。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>位址只拿來做等值比較，永遠不解參</b>——所以 <see cref="TryBeginPress"/> 收的是
    /// <see cref="nint"/> 而不是指標，讓「不解參」變成型別上就辦不到的事。
    /// <para>
    /// ⚠️ 刻意<b>不</b>在停用時清空：留著的舊項目是無害的（位址對不上就直接放行），
    /// 而清空反而會讓一扇正在關閉中的視窗重新變成可按。
    /// 項目數上限是四（三扇 _CharaMake* 加上 SelectString）。
    /// </para>
    /// </remarks>
    private readonly Dictionary<string, (nint Address, long At)> PressedByAddon = new(StringComparer.Ordinal);

    /// <summary>按下之後最久封鎖多久（毫秒）。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    /// <remarks>
    /// 用時鐘而不是幀數是刻意的：僱員外觀視窗開在載入密集的場景，幀率不穩，
    /// 而且畫面隱藏（過場／隱藏 UI 熱鍵）期間繪製幀計數器根本不前進，逃生口會永不到期。
    /// <see cref="Environment.TickCount64"/> 單調遞增且不受這些影響。
    /// 2000ms 遠大於「關閉中」那幾幀（60fps 下數十毫秒、卡頓時也就數百毫秒）。
    /// </remarks>
    private const long PressReleaseTimeoutMs = 2000;

    /// <summary>
    /// <c>SelectString</c> 在 <see cref="PressedByAddon"/> 裡的鍵。
    /// </summary>
    /// <remarks>
    /// 必須與 <c>TryGetAddonMaster&lt;AddonMaster.SelectString&gt;</c> 實際查的 addon 名字一致——
    /// 那個多載是拿 <c>typeof(T).Name</c> 當名字去查的（ECommons AddonHelpers.cs:209-215），
    /// 對不上的話解除點會清錯條目、封鎖就永遠解不開。
    /// </remarks>
    private const string SelectStringKey = "SelectString";

    private string GenerateRandomName()
    {
        var len = Random.Shared.Next(4, 21);
        StringBuilder sb = new();
        var start = Random.Shared.Next(0, 2);
        for(var i = 0; i < len; i++)
        {
            sb.Append((i % 2 == start ? SymbolsA : SymbolsB).GetRandom());
        }
        return sb.ToString();
    }

    public override void OnUpdate()
    {
        if(!Svc.ClientState.IsLoggedIn) return;
        Process("_CharaMakeRaceGender", 18);
        Process("_CharaMakeTribe", 18);
        Process("_CharaMakeFeature", 37);
        if(!GenericHelpers.TryGetAddonMaster<AddonMaster.SelectString>(out var m))
        {
            // 視窗真的收掉了 ⇒ 解除封鎖，下一扇 SelectString 照樣選。
            PressedByAddon.Remove(SelectStringKey);
        }
        else if(m.IsAddonReady)
        {
            // TryBeginPress 擺在 && 鏈的最後一項：它一回 true 就已經把「按過了」記下去，
            // 短路求值保證只有真的要送出的那一次才會登記。
            if(m.EntryCount > 0 && m.Entries[0].Text.Equals("Polite.")
                && EzThrottler.Throttle(InternalData.FullName + "SelectString")
                && TryBeginPress(SelectStringKey, (nint)m.Base))
            {
                m.Entries[0].Select();
                var name = GenerateRandomName();
                Svc.Toasts.ShowQuest($"Suggested name: \"{name}\" copied to clipboard");
                GenericHelpers.Copy(name);
            }
        }
        if(EzThrottler.Throttle(InternalData.FullName + "PeriodicNotify", 60000))
        {
            DuoLog.Warning($"{InternalData.Name} is running. Disable the script if you don't need it anymore.");
        }
    }

    /// <remarks>
    /// 🔴🔴 <c>IsReady</c> 三關（非 null／<c>IsVisible</c>／<c>UldManager.LoadedState == Loaded</c>）
    /// <b>擋不住「按下之後正在關閉中」的那幾幀</b>：那期間三關全過，而視窗其實已經在拆，
    /// 此時再送一次點擊就是原生 AccessViolationException
    /// （corrupted-state exception，<c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// 這裡走的是 <c>ClickAddonButton</c> ⇒ <c>ReceiveEvent</c>（直接模擬點擊），
    /// 比送 callback 更早踩到關閉中的視窗。
    /// <para>
    /// 🔴 <see cref="EzThrottler"/> 不是防護：它記的是「上一次動作在哪個時刻」，
    /// 不是「這扇視窗按過了」，而且首次必放行。低幀率時（角色／僱員外觀視窗正是載入密集區）
    /// 關閉中的那幾幀可以超過節流間隔，於是照樣重按。
    /// </para>
    /// <para>
    /// ⇒ 加一道「同一個實例位址按過就不再按」的閘門，等 <c>TryGetAddonByName</c> 取不到
    /// （＝視窗真的收掉了）才清掉該名字的狀態。這裡用「取不到」當解除點而不是生命週期事件，
    /// 因為 <see cref="OnUpdate"/> 本來就每幀都會被呼叫，視窗消失的那一幀一定看得到。
    /// </para>
    /// </remarks>
    private void Process(string addonName, uint buttonId)
    {
        if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon))
        {
            // 視窗真的收掉了 ⇒ 解除封鎖，下一扇同名視窗照樣按。
            PressedByAddon.Remove(addonName);
            return;
        }
        if(!addon->IsReady()) return;
        if(!EzThrottler.Throttle(InternalData.FullName + addonName)) return;
        // 🔴 GetComponentButtonById 找不到節點會回 null;而 AtkComponentButton.IsEnabled
        // 在 FFXIVClientStructs 解的是 OwnerNode->AtkResNode.NodeFlags,對 OwnerNode
        // 零空指標檢查。AtkComponentBase 有兩個指標欄位:[0xA0] 的 AtkResNode 與
        // [0xA8] 的 OwnerNode,IsEnabled 解的是後者 —— 原本的寫法先讀 IsEnabled 才驗
        // AtkResNode,既驗錯欄位、順序上又太晚。同一幀呼叫三次也是 TOCTOU。
        // AVE 是 corrupted-state exception,try/catch 攔不到,只能在讀取前逐層擋。
        // 指標先取到區域變數;任一層為空就這一幀不點,下一輪重來。
        var button = addon->GetComponentButtonById(buttonId);
        if(button == null) return;
        var ownerNode = button->OwnerNode;
        var buttonResNode = button->AtkResNode;
        if(ownerNode == null || buttonResNode == null) return;
        if(!button->IsEnabled || !buttonResNode->IsVisible()) return;
        // 🔴 登記必須緊接在按下動作之前：一回 true 就已經記成「按過了」，
        // 登記完卻沒按的話會白白封鎖到逾時為止。
        if(!TryBeginPress(addonName, (nint)addon)) return;
        button->ClickAddonButton(addon);
    }

    /// <summary>
    /// 登記「即將對這扇視窗按下去」。<b>回 <see langword="false"/> ＝這一幀絕對不能按。</b>
    /// </summary>
    /// <remarks>
    /// 📌 位址不同就放行：同一個名字底下現在掛的是另一個實例，我們按過的那扇已經取不到了。
    /// （位址被新視窗重用也不成問題：頂多多等到逾時，不會變成崩潰。）
    /// <para>
    /// 🔴 逾時放行是刻意的：萬一上一次的點擊沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「僱員建立流程永遠卡住」的靜默失效。
    /// </para>
    /// </remarks>
    private bool TryBeginPress(string name, nint address)
    {
        if(PressedByAddon.TryGetValue(name, out var prev) && prev.Address == address
            && Environment.TickCount64 - prev.At < PressReleaseTimeoutMs)
        {
            return false;
        }
        PressedByAddon[name] = (address, Environment.TickCount64);
        return true;
    }
}
