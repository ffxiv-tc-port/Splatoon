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

    public override Metadata? Metadata => new(2, "NightmareXIV");

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
        if(GenericHelpers.TryGetAddonMaster<AddonMaster.SelectString>(out var m) && m.IsAddonReady)
        {
            if(m.EntryCount > 0 && m.Entries[0].Text.Equals("Polite.") && EzThrottler.Throttle(InternalData.FullName + "SelectString"))
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

    private void Process(string addonName, uint buttonId)
    {
        if(GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && addon->IsReady())
        {
            if(EzThrottler.Throttle(InternalData.FullName + addonName))
            {
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
                if(button->IsEnabled && buttonResNode->IsVisible())
                    button->ClickAddonButton(addon);
            }
        }
    }
}
