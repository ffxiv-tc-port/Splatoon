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
    public override Metadata? Metadata { get; } = new(6, "NightmareXIV");
    int a1;
    string Filter = "";

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
        if(GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContentsFinder", out var addon) && GenericHelpers.IsAddonReady(addon))
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
        if(GenericHelpers.TryGetAddonByName<AddonContentsFinder>("ContentsFinder", out var addon) && GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
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
            private nint unitBasePtr;
            private int beginOffset;
            public DutyInfo(nint UnitBasePtr, int BeginOffset = 0) : base(UnitBasePtr, BeginOffset)
            {
                this.unitBasePtr = UnitBasePtr;
                this.beginOffset = BeginOffset;
            }

            public string DutyName => ReadSeString(0).ExtractText();
            public string DutyLevel => MemoryHelper.ReadStringNullTerminated((nint)((AtkUnitBase*)unitBasePtr)->AtkValues[beginOffset + 1].String.Value);
            public string DutyPF => ReadString(2);
        }
    }
}
