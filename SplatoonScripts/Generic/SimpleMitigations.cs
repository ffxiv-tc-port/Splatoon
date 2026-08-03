using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using ECommons.Schedulers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using NotificationMasterAPI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = System.Action;

namespace SplatoonScriptsOfficial.Generic;
public sealed class SimpleMitigations : SplatoonScript
{
    public override Metadata Metadata { get; } = new(2, "NightmareXIV");
    public override HashSet<uint>? ValidTerritories { get; } = [];

    Dictionary<string, long> TrackedTimes = [];

    [EzIPC("WrathCombo.ActionRequest.RequestBlacklist", false)] public Action<ActionType, uint, int> RequestBlacklist;
    [EzIPC("WrathCombo.ActionRequest.ResetBlacklist", false)] public Action<ActionType, uint> ResetBlacklist;
    [EzIPC("WrathCombo.ActionRequest.ResetAllBlacklist", false)] public Action ResetAllBlacklist;

    public override void OnDirectorUpdate(DirectorUpdateCategory category)
    {
        if(category == DirectorUpdateCategory.Commence || category == DirectorUpdateCategory.Recommence || category == DirectorUpdateCategory.Wipe)
        {
            TrackedTimes.Clear();
        }
    }

    public override void OnSetup()
    {
        // ⚠️ 原本是 EzIPC.Init(this)，也就是 SafeWrapper.None。
        // 下面三個訂閱端指向的 WrathCombo.ActionRequest.* 在我們出貨的 WrathCombo 裡**完全不存在**
        // （WrathCombo 只提供 WrathCombo.* 的租約／設定類 IPC，沒有任何 ActionRequest 命名空間），
        // 所以每次呼叫都會丟 IpcNotReadyError。
        //
        // 更糟的是 ProcessActionBlock() 裡的 ResetBlacklist(ActionType.Action, 25788) 那條路徑
        // **沒有節流**，只要場上有「Fatebreaker」或「Striking Dummy」可選取就每幀丟一次，
        // 而 Splatoon 的腳本框架會每幀 catch + LogError —— 啟用這個腳本就等於洗爆 log。
        //
        // 改成 AnyException：例外交給 Splatoon 自己的 EzIpcFailureLog 觀測網，
        // 節流後只會印出一行帶方法名的 Information，而腳本不靠 WrathCombo 的那半邊功能
        // （直接施放減傷）照常運作。
        EzIPC.Init(this, safeWrapper: SafeWrapper.AnyException);
    }

    public override void OnCombatStart()
    {
        TrackedTimes.Clear();
    }

    public override void OnCombatEnd()
    {
        TrackedTimes.Clear();
    }

    public unsafe override void OnUpdate()
    {
        ProcessActionBlock();
        if(!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]) return;
        var names = C.Data.Select(x => x.Name).ToHashSet();
        foreach(var x in Svc.Objects)
        {
            if(x is IBattleNpc && x.IsTargetable)
            {
                var n = x.Name.ToString();
                if(names.Contains(n))
                {
                    if(!TrackedTimes.ContainsKey(n))
                    {
                        TrackedTimes[n] = Environment.TickCount64;
                    }
                    if(C.Data.TryGetFirst(d => d.Name == n && GetTime(d.Name).InRange(d.Time, d.Time + 5), out var data))
                    {
                        if(!data.Prohibit)
                        {
                            var s = ActionManager.Instance()->GetActionStatus(data.General ? ActionType.GeneralAction : ActionType.Action, (uint)data.Action);
                            if(s == 0)
                            {
                                if(EzThrottler.Throttle("Cast", 50) && !Player.Object.IsDead && GenericHelpers.IsScreenReady())
                                {
                                    if(data.General)
                                    {
                                        Chat.ExecuteGeneralAction((uint)data.Action);
                                    }
                                    else
                                    {
                                        Chat.ExecuteAction((uint)data.Action);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if(EzThrottler.Throttle($"DisallowAction_{data.Type}_{data.Action}"))
                            {
                                RequestBlacklist(data.Type, (uint)data.Action, 1000);
                            }
                        }
                    }
                }
            }
        }
    }

    public override void OnSettingsDraw()
    {
        if(ImGuiEx.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Plus, "Add")) C.Data.Add(new());
        if(ImGuiEx.BeginDefaultTable(["~Boss Name", "Action", "Time", "##trash"]))
        {
            foreach(var x in C.Data)
            {
                ImGui.PushID($"{x.GUID}");
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputText("##name", ref x.Name);
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputInt("##action", ref x.Action);
                ImGuiEx.Tooltip($"{ExcelActionHelper.GetActionName((uint)x.Action)}");
                ImGui.SameLine();
                ImGuiEx.ButtonCheckbox(Dalamud.Interface.FontAwesomeIcon.PeopleGroup, ref x.General);
                ImGuiEx.Tooltip("Is general action");
                ImGui.SameLine();
                ImGuiEx.ButtonCheckbox(Dalamud.Interface.FontAwesomeIcon.Times, ref x.Prohibit, EColor.RedBright);
                ImGuiEx.Tooltip("Disallow action");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputFloat("##time", ref x.Time);
                ImGui.TableNextColumn();
                if(ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.Trash))
                {
                    new TickScheduler(() => C.Data.Remove(x));
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if(ImGui.CollapsingHeader("Debug"))
        {

            var names = C.Data.Select(x => x.Name).ToHashSet();
            foreach(var n in names)
            {
                ImGuiEx.Text($"{n}: {GetTime(n)}");
            }
        }
    }

    void ProcessActionBlock()
    {
        {
            if(Svc.Objects.OfType<IBattleNpc>().TryGetFirst(x => x.IsTargetable && x.Name.ToString().EqualsAny("Fatebreaker", "Striking Dummy"), out var data))
            {
                var hp = (float)data.CurrentHp / (float)data.MaxHp;
                /*if(hp < 0.05f)
                {
                    if(EzThrottler.Throttle("BlockFatebreaker"))
                    {
                        PluginLog.Information($"Preventing bursts...");
                        this.RequestBlacklist(ActionType.Action, 2878, 1000); //wf
                        this.RequestBlacklist(ActionType.Action, 7414, 1000); //stab
                        this.RequestBlacklist(ActionType.Action, 16501, 1000); //robot
                        if(hp < 0.1f) this.RequestBlacklist(ActionType.Action, 17209, 1000); //hyper
                    }
                }*/
                if(Controller.CombatSeconds.InRange(120-20, 120+20) && ExcelActionHelper.GetActionCooldown(25788) > 0)
                {
                    this.RequestBlacklist(ActionType.Action, 25788, 20000); //robot
                }
                else
                {
                    this.ResetBlacklist(ActionType.Action, 25788);
                }
            }
        }
        {
            if(Svc.Objects.OfType<IBattleNpc>().TryGetFirst(x => x.IsTargetable && x.Name.ToString() == "Usurper of Frost", out var data))
            {
                var hp = (float)data.CurrentHp / (float)data.MaxHp;
                if(hp < 0.3f)
                {
                    if(EzThrottler.Throttle("BlockUsurper"))
                    {
                        PluginLog.Information($"Preventing bursts...");
                        this.RequestBlacklist(ActionType.Action, 2878, 1000); //wf
                        this.RequestBlacklist(ActionType.Action, 7414, 1000); //stab
                        this.RequestBlacklist(ActionType.Action, 16501, 1000); //robot
                        if(hp < 0.1f) this.RequestBlacklist(ActionType.Action, 17209, 1000); //hyper
                    }
                }
            }
        }
    }

    float GetTime(string s)
    {
        if(TrackedTimes.TryGetValue(s, out var ret))
        {
            return (Environment.TickCount64 - ret) / 1000f;
        }
        return -999;
    }

    public override void OnReset()
    {
        this.ResetAllBlacklist();
    }

    Config C => Controller.GetConfig<Config>();
    public class Config : IEzConfig
    {
        public List<MitData> Data = [];
    }

    public class MitData
    {
        internal Guid GUID = Guid.NewGuid();
        public string Name;
        public float Time;
        public bool Prohibit = false;
        public ActionType Type = ActionType.Action;
        public int Action;
        public bool General = false;
    }
}