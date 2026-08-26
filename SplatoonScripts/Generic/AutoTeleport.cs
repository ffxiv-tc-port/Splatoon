using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.ExcelServices;
using ECommons.EzHookManager;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class AutoTeleport : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1252];
    public override Metadata Metadata => new(2, "NightmareXIV");

    delegate void DisplayPopupBanner(nint a1, int textureId, int a3, int a4);

    [EzHook("48 89 5C 24 ?? 57 48 83 EC 30 48 8B D9 89 91", false)]
    EzHook<DisplayPopupBanner> DisplayPopupBannerHook;

    TaskManager TaskManager;

    public override void OnSetup()
    {
        EzSignatureHelper.Initialize(this);
    }

    public override void OnEnable()
    {
        DisplayPopupBannerHook.Enable();
        TaskManager = new(new(timeLimitMS:20000, abortOnTimeout:true, showDebug:true));
    }

    public override void OnDisable()
    {
        DisplayPopupBannerHook.Disable();
        TaskManager.Dispose();
    }

    public override void OnSettingsDraw()
    {
        if(TaskManager.IsBusy && TaskManager.MaxTasks > 0)
        {
            ImGuiEx.Text(EColor.YellowBright, $"Plugin is processing tasks. Please wait.");
            ImGui.ProgressBar(TaskManager.Progress, new(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()), $"{TaskManager.MaxTasks - TaskManager.NumQueuedTasks}/{TaskManager.MaxTasks}");
            if(ImGui.Button($"Abort {TaskManager.NumQueuedTasks} tasks"))
            {
                TaskManager.Abort();
            }
        }
        if(ImGui.Button("Enqueue Teleport")) TaskManager.Enqueue(TryReturn);
        if(Player.Object.IsCasting())
        {
            ImGuiEx.Text($"{ExcelActionHelper.GetActionName(Player.Object.CastActionId)}");
        }
        ImGuiEx.Text($"{ActionManager.Instance()->GetActionStatus(ActionType.Action, 41343)}");
    }

    bool TryReturn()
    {
        if(Player.Object.IsCasting(41343))
        {
            return true;
        }
        // 🔴 AgentMap.Instance() 由 [Agent(AgentId.Map)] 產生:內部鏈
        //    AgentModule -> UIModule -> Framework,任一層回 null 整條就回 null(登入前、
        //    切場景時是常態),底層 [StaticAddress]/[MemberFunction] 特徵碼失配時改為擲
        //    InvalidOperationException——兩種失效模式並存。裸解參考 null 原生指標是
        //    AccessViolationException,在 .NET Core 屬 corrupted-state exception,
        //    try/catch 攔不到 ⇒ 只能事前判空。
        //    fail-closed:取不到 agent 就當成「還在移動」,這一輪不送 /return
        //    (寧可不傳送,也不要在狀態不明時送出傳送指令)。工作每幀重跑,不寫 log。
        AgentMap* agentMap;
        try
        {
            agentMap = AgentMap.Instance();
        }
        catch
        {
            agentMap = null;
        }

        if(agentMap != null && ActionManager.Instance()->GetActionStatus(ActionType.Action, 41343) == 0 && !agentMap->IsPlayerMoving)
        {
            if(FrameThrottler.Check("ReturnThrottle") && EzThrottler.Throttle("ReturnOC"))
            {
                Chat.ExecuteCommand("/return");
            }
        }
        else
        {
            FrameThrottler.Throttle("ReturnThrottle", 5, true);
        }
        return false;
    }

    void DisplayPopupBannerDetour(nint a1, int textureId, int a3, int a4)
    {
        try
        {
            if(textureId == 128388)
            {
                DuoLog.Warning("CE ended (hook)");
                TaskManager.Enqueue(TryReturn);
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        DisplayPopupBannerHook.Original(a1, textureId, a3, a4);
    }
}