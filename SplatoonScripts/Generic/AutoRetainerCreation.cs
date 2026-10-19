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
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class AutoRetainerCreation : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [];
    public string SymbolsA = "qwrtpsdfghjklzxcvbnm";
    public string SymbolsB = "eyuioa";

    public override Metadata? Metadata => new(5, "NightmareXIV");

    public override Dictionary<int, string> Changelog => new()
    {
        [5] = """
        修正：台服的僱員性格選單是中文，原本只認英文「Polite.」，永遠比不中、靜默不選。
        現在改從遊戲資料表讀性格選單第一項的文字（台服為「開朗」），英文用戶端照舊。
        """,
        [4] = """
        逃生口從牆鐘改成遊戲幀：卡頓時牆鐘照樣前進，會讓「同一扇視窗只送一次」的封鎖
        在最危險的那一刻提早放行（卡一次 300 毫秒的頓，250 毫秒的逃生口只撐一幀就開門）。
        改成數遊戲幀之後，遊戲沒有推進封鎖就不會解開；取不到幀序時這一輪不送（fail-closed）。
        """,
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
    /// ⚠️ 刻意<b>不</b>在停用時清空：留著的舊項目是無害的（位址對不上就直接放行），
    /// 而清空反而會讓一扇正在關閉中的視窗重新變成可按。
    /// 項目數上限是四（三扇 _CharaMake* 加上 SelectString）。
    /// </remarks>
    private readonly Dictionary<string, (nint Address, uint AtFrame)> PressedByAddon = new(StringComparer.Ordinal);

    /// <summary>送出之後最久封鎖幾個<b>遊戲幀</b>。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    /// <remarks>
    /// 🔴 逃生口用遊戲幀而不是牆鐘：視窗「正在關閉中」的長度是用<b>幀</b>算的，而牆鐘在卡頓時
    /// 照樣前進，會正好在最危險的那一刻放行。<see cref="CurrentFrame"/> 取的是遊戲主迴圈的
    /// <c>Framework.FrameCounter</c>（不是繪製幀計數器），畫面隱藏期間照樣前進，所以逃生口不會永不到期。
    /// 🔴 有逃生口是刻意的：萬一上一次的動作根本沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「這個流程從此卡住」的靜默失效。120 幀在 60fps 下約 2 秒。
    /// </remarks>
    private const uint PressReleaseTimeoutFrames = 120;

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

    /// <summary>
    /// <c>SelectString</c> 在 <see cref="PressedByAddon"/> 裡的鍵。
    /// </summary>
    /// <remarks>
    /// 必須與 <c>TryGetAddonMaster&lt;AddonMaster.SelectString&gt;</c> 實際查的 addon 名字一致——
    /// 那個多載是拿 <c>typeof(T).Name</c> 當名字去查的，
    /// 對不上的話解除點會清錯條目、封鎖就永遠解不開。
    /// </remarks>
    private const string SelectStringKey = "SelectString";

    /// <summary>僱員性格選單的來源表與第一項（「開朗」）的列；第 1 欄是顯示文字。</summary>
    private const string RetainerDeskSheet = "custom/000/CmnDefRetainerDesk_00009";
    private const uint PersonalityFirstOptionRow = 68;

    private string? PersonalityFirstOption;
    private bool PersonalityLookupDone;

    /// <remarks>英文字面值保留給國際服；資料表讀不到時就只認英文（＝台服不選，與修正前相同）。</remarks>
    private bool IsPersonalityFirstOption(string text)
    {
        if(text == "Polite.") return true;
        if(!PersonalityLookupDone)
        {
            PersonalityLookupDone = true;
            try
            {
                if(GenericHelpers.TryGetRawRow(RetainerDeskSheet, PersonalityFirstOptionRow, out var row))
                    PersonalityFirstOption = row.ReadStringColumn(1).ExtractText();
            }
            catch(Exception e)
            {
                PluginLog.Warning($"[AutoRetainerCreation] 讀不到 {RetainerDeskSheet} 第 {PersonalityFirstOptionRow} 列：{e.Message}");
            }
        }
        return !string.IsNullOrEmpty(PersonalityFirstOption) && text == PersonalityFirstOption;
    }

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
            if(m.EntryCount > 0 && IsPersonalityFirstOption(m.Entries[0].Text)
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
    /// <b>擋不住「按下之後正在關閉中」的那幾幀</b>：那期間三關全過，此時再送一次點擊就是原生
    /// AccessViolationException（<c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// 🔴 <see cref="EzThrottler"/> 不是防護：它記的是時刻而不是「這扇視窗按過了」，首次必放行，低幀率時關閉中的那幾幀可以超過節流間隔。
    /// ⇒ 「同一個實例位址按過就不再按」，等 <c>TryGetAddonByName</c> 取不到（＝視窗真的收掉了）才清掉該名字的狀態；
    /// <see cref="OnUpdate"/> 每幀都會被呼叫，視窗消失的那一幀一定看得到。
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
        // 🔴 GetComponentButtonById 找不到節點會回 null;AtkComponentButton.IsEnabled 解的是
        // OwnerNode->AtkResNode.NodeFlags,對 OwnerNode 零空指標檢查(它與 AtkResNode 是兩個不同的指標欄位)。
        // AVE 是 corrupted-state exception,try/catch 攔不到,只能在讀取前逐層擋:
        // 指標先取到區域變數(同一幀重讀是 TOCTOU);任一層為空就這一幀不點,下一輪重來。
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
        // 🔴 取不到幀序就這一輪不送（fail-closed）：沒有時間基準就判斷不了「這扇視窗是不是正在關閉中」。
        var now = CurrentFrame();
        if(now == null) return false;
        // unchecked：FrameCounter 是 uint，溢位回繞時無號減法照樣給出正確的「過了幾幀」。
        if(PressedByAddon.TryGetValue(name, out var prev) && prev.Address == address
            && unchecked(now.Value - prev.AtFrame) < PressReleaseTimeoutFrames)
        {
            return false;
        }
        PressedByAddon[name] = (address, now.Value);
        return true;
    }
}
