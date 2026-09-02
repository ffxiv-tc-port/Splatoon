using Dalamud.Memory;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.EzIpcManager;
using ECommons.EzSharedDataManager;
using ECommons.ImGuiMethods;
using ECommons.UIHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Callback = ECommons.Automation.Callback;
#pragma warning disable
namespace SplatoonScriptsOfficial.Tests;
public unsafe class GenericTest4 : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => new();
    public override Metadata? Metadata { get; } = new(8, "NightmareXIV");

    public override Dictionary<int, string> Changelog => new()
    {
        [8] = """
        修正：對「任務搜尋器」視窗送 callback 之後有「正在關閉中」的幾幀，
        這期間視窗仍然通過就緒檢查，此時再送一次就是攔不到的原生存取違規（遊戲當場關閉）。
        現在同一扇視窗的同一組參數在它收掉之前只送一次。
        """
    };

    int a1;
    string Filter = "";

    /// <summary>
    /// 每個視窗名字底下、「已經送過 callback 的那一組參數」對應的實例位址與送出的時刻。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>位址只拿來做等值比較，永遠不解參</b>——所以 <see cref="TryBeginPress"/> 收的是
    /// <see cref="nint"/> 而不是指標，讓「不解參」變成型別上就辦不到的事。
    /// <para>
    /// ⚠️ 粒度刻意是（視窗名字，實例位址，<b>參數組</b>）而不是「一扇視窗只按一次」：
    /// <see cref="ClearDuty"/> 送的是 <c>(12, 1)</c>、<see cref="SelectDuty"/> 送的是 <c>(3, cnt)</c>，
    /// 兩者會在同一扇視窗上前後接力，併成同一個鍵會把正常流程擋掉。
    /// </para>
    /// </remarks>
    private readonly Dictionary<string, Dictionary<string, (nint Address, long At)>> Pressed = new(StringComparer.Ordinal);

    /// <summary>同一組參數送出之後最久封鎖多久（毫秒）。到期＝判定「上一次沒生效」而不是「正在關閉」。</summary>
    /// <remarks>
    /// 這扇視窗按下這兩個按鈕都<b>不會</b>讓它消失（＝多次互動窗，和確認框那種「回答一次即終結」不同），
    /// 所以逃生口取短的一檔：15 幀，60fps 下約 250 毫秒。<see cref="ClearDuty"/> 本來就是
    /// 「每一輪重送直到按鈕變停用」的刻意重試迴圈，逃生口取太長會把它變成慢動作。
    /// <para>
    /// 用牆鐘（<see cref="Environment.TickCount64"/>）而不是繪製幀計數器是刻意的：
    /// 畫面隱藏（過場／隱藏 UI 熱鍵）期間繪製幀根本不前進，逃生口會永不到期。
    /// </para>
    /// </remarks>
    private const long RoutineRePressTimeoutMs = 250;

    /// <summary>這扇視窗在 <see cref="Pressed"/> 裡的鍵，同時也是查詢用的 addon 名字。</summary>
    private const string ContentsFinderAddon = "ContentsFinder";

    /// <summary>
    /// 登記「即將對這扇視窗送出這一組參數」。<b>回 <see langword="false"/> ＝這一輪絕對不能送。</b>
    /// </summary>
    /// <remarks>
    /// 🔴🔴 <c>IsAddonReady</c> 三關（非 null／<c>IsVisible</c>／<c>UldManager.LoadedState == Loaded</c>）
    /// <b>擋不住「送出之後正在關閉中」的那幾幀</b>：那期間三關全過，而視窗其實已經在拆，
    /// 此時再送就是原生 AccessViolationException（.NET Core 的 corrupted-state exception，
    /// <c>try/catch</c> 攔不到，遊戲當場關閉）。
    /// <para>
    /// 一回 <see langword="true"/> 就已經把「送過了」記下去，所以呼叫點必須<b>緊接在送出動作之前</b>；
    /// 登記完卻不送的話會白白封鎖到逾時為止。
    /// </para>
    /// <para>
    /// 📌 位址不同就放行：同一個名字底下現在掛的是另一個實例，我們送過的那扇已經取不到了。
    /// （位址被新視窗重用也不成問題：頂多多等到逾時，不會變成崩潰。）
    /// </para>
    /// <para>
    /// 🔴 被擋下時回 <see langword="false"/>，不回 <see langword="null"/>：呼叫端是
    /// <c>Func&lt;bool?&gt;</c> 型的工作，<see langword="false"/> 的意義就是「這一輪沒做到、下一輪再來」，
    /// 與「視窗還沒出現」走同一條既有路徑，控制流完全不變；<see langword="null"/> 則會清掉整條佇列。
    /// </para>
    /// </remarks>
    private bool TryBeginPress(string addonName, nint address, string parameters, long timeoutMs)
    {
        if(!Pressed.TryGetValue(addonName, out var byParameters))
        {
            byParameters = new(StringComparer.Ordinal);
            Pressed[addonName] = byParameters;
        }
        if(byParameters.TryGetValue(parameters, out var prev) && prev.Address == address
            && Environment.TickCount64 - prev.At < timeoutMs)
        {
            return false;
        }
        byParameters[parameters] = (address, Environment.TickCount64);
        return true;
    }

    /// <summary>視窗真的收掉了 ⇒ 解除封鎖，下一扇同名視窗照樣按。</summary>
    /// <remarks>
    /// 解除點刻意用「<c>TryGetAddonByName</c> 取不到」而不是生命週期事件：
    /// <see cref="ClearDuty"/>／<see cref="SelectDuty"/> 是工作管理器每一輪都會叫到的，
    /// 視窗消失的那一輪一定看得到，不必額外註冊監聽器。
    /// <para>
    /// ⚠️ 刻意<b>不</b>在停用或重置時清空：留著的舊項目是無害的（位址對不上就直接放行），
    /// 而清空反而會讓一扇正在關閉中的視窗重新變成可按。
    /// </para>
    /// </remarks>
    private void ReleaseContentsFinderGuard() => Pressed.Remove(ContentsFinderAddon);

    public override void OnSettingsDraw()
    {

        if(TaskManager?.IsBusy == true)
        {
            if(ImGui.Button("Stop")) TaskManager.Abort();
            return;
        }
        ImGui.InputInt("Duty ID", ref a1);
        if(ImGui.BeginCombo("Duty", Svc.Data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault((uint)a1)?.Name.ExtractText()))
        {
            ImGui.InputText($"Filter", ref Filter, 50);
            foreach(var x in Svc.Data.GetExcelSheet<ContentFinderCondition>())
            {
                var name = x.Name.ExtractText();
                if(Filter != "" && !name.Contains(Filter, StringComparison.OrdinalIgnoreCase)) continue;
                if(name == "") continue;
                if(x.TerritoryType.Value.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Quest_Battle || x.TerritoryType.Value.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Quest_Battle_2) continue;
                if(ImGui.Selectable(x.Name.ExtractText()))
                {
                    a1 = (int)x.RowId;
                }
            }
            ImGui.EndCombo();
        }
        if(ImGui.Button("SelectDuty"))
        {
            TaskSelectDuty((uint)a1);
        }
        if(GenericHelpers.TryGetAddonByName<AddonContentsFinder>("ContentsFinder", out var addon))
        {
            // GetNodeById 找不到節點會回 null,而 GetAsAtkComponentList() 是 [MemberFunction]
            // 原生呼叫、不是受管理的 null-safe 存取器 —— 對空節點呼叫就是攔不到的 AVE,
            // 所以先驗節點再呼叫。
            var listNode = addon->AtkUnitBase.GetNodeById(52);
            AtkComponentList* list = null;
            if(listNode != null) list = listNode->GetAsAtkComponentList();
            var length = addon->NumEntries;
            var reader = new ReaderAddonContentsFinder(&addon->AtkUnitBase);
            for(int i = 0; i < length; i++)
            {
                var duty = reader.Duties[i];
                if(Regex.IsMatch(duty.DutyLevel, @"Lv\. ([0-9]{1,3})"))
                {
                    ImGuiEx.Text($"{duty.DutyName}");
                }
            }
        }
    }

    TaskManager TaskManager;

    public override void OnEnable()
    {
        TaskManager = new();
    }

    public override void OnDisable()
    {
        TaskManager.Dispose();
    }


    public void TaskSelectDuty(uint cfc)
    {
        TaskManager.Enqueue(EnsureDFClosed);
        TaskManager.Enqueue(() => OpenDF(cfc));
        TaskManager.Enqueue(ClearDuty);
        TaskManager.Enqueue(() => SelectDuty(cfc));
    }

    public void EnsureDFClosed()
    {
        if(GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContentsFinder", out var addon))
        {
            addon->Close(true);
        }
    }

    public bool OpenDF(uint cfc)
    {
        if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContentsFinder", out _))
        {
            // 🔴 AgentContentsFinder.Instance() 由 [Agent(AgentId.ContentsFinder)] 產生:
            //    內部鏈 AgentModule -> UIModule -> Framework,任一層回 null 整條就回 null
            //    (登入前、切場景時是常態),底層 [StaticAddress]/[MemberFunction] 特徵碼
            //    失配時改為擲 InvalidOperationException——兩種失效模式並存。
            //    裸解參考 null 原生指標是 AccessViolationException,在 .NET Core 屬
            //    corrupted-state exception,try/catch 攔不到 ⇒ 只能事前判空。
            //    fail-closed:取不到 agent 就回 false,讓呼叫端下一輪重試而不是崩潰。
            AgentContentsFinder* agent;
            try
            {
                agent = AgentContentsFinder.Instance();
            }
            catch
            {
                return false;
            }

            if(agent == null) return false;

            agent->OpenRegularDuty(cfc);
            return true;
        }
        return false;
    }

    public bool? ClearDuty()
    {
        if(!GenericHelpers.TryGetAddonByName<AtkUnitBase>(ContentsFinderAddon, out var addon))
        {
            // 視窗真的收掉了 ⇒ 解除封鎖（原本這裡與就緒檢查併在同一個條件式裡，分不出
            // 「視窗不見了」和「視窗還沒就緒」——只有前者才可以清掉按下紀錄）。
            ReleaseContentsFinderGuard();
            return false;
        }
        if(GenericHelpers.IsAddonReady(addon))
        {
            var btn = addon->GetComponentButtonById(73);
            // 🔴 GetComponentButtonById 找不到會回 null,而 IsEnabled 解的是
            // OwnerNode->AtkResNode.NodeFlags(FFXIVClientStructs 對 OwnerNode 零空指標檢查),
            // 兩層都要在讀取前驗。AVE 是 corrupted-state exception,try/catch 攔不到。
            // 讀不到就回 false(這一幀不做事、下一輪重來)—— 不能當成「按鈕已停用」回 true,
            // 那會把工作標成完成卻其實什麼都沒做。
            if(btn == null || btn->OwnerNode == null) return false;
            if(btn->IsEnabled)
            {
                // 🔴 這是刻意的重試迴圈：送出之後會落到最後那個 return false，於是每一輪都回來重送，
                // 直到按鈕變停用為止 —— 而「正在關閉中」的那幾幀 IsAddonReady 照樣三關全過。
                // 守衛擺在送出動作正前方：一回 true 就已經記成「送過了」。
                if(!TryBeginPress(ContentsFinderAddon, (nint)addon, "12,1", RoutineRePressTimeoutMs)) return false;
                Callback.Fire(addon, true, 12, 1);
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    public bool? SelectDuty(uint cfc)
    {
        if(!GenericHelpers.TryGetAddonByName<AddonContentsFinder>(ContentsFinderAddon, out var addon))
        {
            // 視窗真的收掉了 ⇒ 解除封鎖。
            ReleaseContentsFinderGuard();
            return false;
        }
        if(GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
        {
            var cfcData = Svc.Data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(cfc);
            if(cfcData == null || cfcData.Value.Name.ExtractText() == "") throw new ArgumentOutOfRangeException(nameof(cfc));
            // GetNodeById 找不到節點會回 null,而 GetAsAtkComponentList() 是 [MemberFunction]
            // 原生呼叫、不是受管理的 null-safe 存取器 —— 對空節點呼叫就是攔不到的 AVE,
            // 所以先驗節點再呼叫。
            var listNode = addon->AtkUnitBase.GetNodeById(52);
            AtkComponentList* list = null;
            if(listNode != null) list = listNode->GetAsAtkComponentList();
            var length = addon->NumEntries;
            var reader = new ReaderAddonContentsFinder(&addon->AtkUnitBase);
            int cnt = 0;
            for(int i = 0; i < length; i++)
            {
                var duty = reader.Duties[i];
                if(Regex.IsMatch(duty.DutyLevel, @"^Lv\. ([0-9]{1,3})$"))
                {
                    cnt++;
                    if(duty.DutyName == cfcData.Value.Name.ExtractText())
                    {
                        // 與 ClearDuty 送的是不同參數組（3,cnt 對 12,1），鍵刻意分開：
                        // 併成同一個鍵會讓緊接在 ClearDuty 之後的這一送被自己的守衛擋掉。
                        if(!TryBeginPress(ContentsFinderAddon, (nint)(&addon->AtkUnitBase), $"3,{cnt}", RoutineRePressTimeoutMs)) return false;
                        Callback.Fire(&addon->AtkUnitBase, true, 3, cnt);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public class ReaderAddonContentsFinder : AtkReader
    {
        public ReaderAddonContentsFinder(AtkUnitBase* UnitBase, int BeginOffset = 0) : base(UnitBase, BeginOffset)
        {
        }

        public List<DutyInfo> Duties => Loop<DutyInfo>(788, 3, 0x78);

        public class DutyInfo : AtkReader
        {
            public DutyInfo(nint UnitBasePtr, int BeginOffset = 0) : base(UnitBasePtr, BeginOffset)
            {
            }

            public string DutyName => ReadSeString(0).ExtractText();

            // 🔴 原本這一行繞過 AtkReader 自己存 UnitBasePtr／BeginOffset 再裸讀:
            //    ((AtkUnitBase*)unitBasePtr)->AtkValues[beginOffset + 1].String.Value
            //    三個都沒驗 —— AtkValues 是指標欄位(可為 null)、索引沒和 AtkValuesCount 比、
            //    型別對也不代表 String.Value 非空。任一條不成立就是 AccessViolationException,
            //    而 AVE 在 .NET Core 是 corrupted-state exception,try/catch 攔不到。
            //    改走基底的 ReadString(1):它等價於同一個索引,但帶三道守衛,
            //    讀不到回 null(與同類 DutyPF 的失敗語意一致)。
            public string DutyLevel => ReadString(1);
            public string DutyPF => ReadString(2);
        }
    }
}
