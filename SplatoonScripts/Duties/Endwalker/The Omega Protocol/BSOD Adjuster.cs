using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.Logging;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using static Splatoon.Splatoon;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SplatoonScriptsOfficial.Duties.Endwalker.The_Omega_Protocol;
internal class BSOD_Adjuster : SplatoonScript
{
    public override HashSet<uint> ValidTerritories => [1122];
    public override Metadata? Metadata => new(6, "Redmoon, mirage");
    public override Dictionary<int, string> Changelog => new()
    {
        [6] = "優先順序改用 Splatoon 共用的優先順序清單（舊版自訂的名單會自動搬過去），設定介面改為繁體中文",
    };

    private const int SceneBsod = 5;

    public class CastID
    {
        public const uint StackMarker = 22393u;
        public const uint StackCannon = 31615u;
        public const uint BSOD = 31611u;
    }

    private Config Conf => Controller.GetConfig<Config>();

    public class Config : IEzConfig
    {
        public bool DebugPrint = false;
        public PriorityData PriorityData = new()
        {
            Name = "BSOD left/right",
            Description = "Order from NorthWest to NorthEast (positions 1–8).",
        };
        // 舊版的名單格式：OnSetup 搬進 PriorityData 後清空。
        public List<string[]> LeftRightPriorities = [];
    }

    // 只存 GameObjectId，不跨幀持有物件表的包裝。
    private List<ulong> _sortedList = [];
    private List<ulong> _stackedList = [];
    private bool _mechanicActive = false;

    public override void OnSettingsDraw()
    {
        ImGui.Text("優先順序（由西北排到東北，第 1～8 位）");
        Conf.PriorityData.Draw();

        if(ImGui.CollapsingHeader("除錯"))
        {
            ImGui.Checkbox("在聊天視窗印出除錯訊息", ref Conf.DebugPrint);
            ImGui.Text($"_mechanicActive : {_mechanicActive}");
            ImGui.Text($"_stackedList : {NamesOf(_stackedList)}");
            ImGui.Text($"_sortedList : {NamesOf(_sortedList)}");
            ImGui.Text($"BasePlayer: {BasePlayer?.Name.ToString() ?? "null"}");
            ImGui.Text($"Controller.Scene: {Controller.Scene}");
        }
    }

    public override void OnSetup()
    {
        Controller.RegisterElementFromCode("StackLeft", "{\"Name\":\"\",\"refX\":95.74,\"refY\":112.62,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":5.0,\"tether\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("StackRight", "{\"Name\":\"\",\"Enabled\":false,\"refX\":103.92,\"refY\":112.46,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":5.0,\"tether\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        MigrateLegacyPriorities();
    }

    public override void OnUpdate()
    {
        if(Controller.Scene == SceneBsod)
        {
            if(TryGetPriorityList(out _sortedList))
                _mechanicActive = true;
        }
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if(source.GetObject() is IBattleNpc && castId == CastID.BSOD)
            OnReset();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(!_mechanicActive || set.Action == null)
            return;

        if(set.Action.Value.RowId == CastID.StackMarker)
        {
            try
            {
                DebugLog($"StackMarker: {set.Target?.Name}");
                if(set.Target is not IPlayerCharacter pcObj)
                    return;

                _stackedList.Add(pcObj.GameObjectId);
                DebugLog($"_stackedList: {NamesOf(_stackedList)}");
                if(_stackedList.Count == 2)
                {
                    if(BasePlayer == null)
                        return;

                    var me = BasePlayer.GameObjectId;
                    if(_stackedList.Contains(me))
                    {
                        DebugLog("Stacker");
                        // stacker show element
                        var myStacker = me;
                        var otherStacker = _stackedList.First(x => x != me);
                        DebugLog($"myStacker: {NameOf(myStacker)}, otherStacker: {NameOf(otherStacker)}");
                        var myIndex = _sortedList.IndexOf(myStacker);
                        var otherIndex = _sortedList.IndexOf(otherStacker);
                        if(myIndex == -1 || otherIndex == -1)
                        {
                            DuoLog.Warning($"優先順序清單裡找不到玩家");
                            _mechanicActive = false;
                            OnReset();
                            return;
                        }
                        var myPos = myIndex < otherIndex ? "Left" : "Right";
                        var otherPos = myIndex < otherIndex ? "Right" : "Left";

                        Controller.GetElementByName($"Stack{myPos}").Enabled = true;
                        Controller.GetElementByName($"Stack{myPos}").tether = true;

                        DebugLog($"myStacker: {NameOf(myStacker)} {myPos}, otherStacker: {NameOf(otherStacker)} {otherPos}");
                        var noneStackers = _sortedList.Where(x => !_stackedList.Contains(x)).ToList();
                        foreach(var x in noneStackers)
                        {
                            var dpos = noneStackers.IndexOf(x) < 3 ? "Left" : "Right";
                            DebugLog($"noneStacker: {NameOf(x)} {dpos}");
                        }
                    }
                    else
                    {
                        DebugLog("Non stacker");
                        // non stacker show element
                        var noneStackers = _sortedList.Where(x => !_stackedList.Contains(x)).ToList();

                        var myIndex = noneStackers.IndexOf(me);
                        if(myIndex == -1)
                        {
                            DuoLog.Warning($"優先順序清單裡找不到玩家");
                            _mechanicActive = false;
                            OnReset();
                            return;
                        }
                        var myPos = myIndex < 3 ? "Left" : "Right";

                        Controller.GetElementByName($"Stack{myPos}").Enabled = true;
                        Controller.GetElementByName($"Stack{myPos}").tether = true;

                        //Debug
                        var myStacker = _stackedList.First();
                        var otherStacker = _stackedList.Last();
                        myIndex = _sortedList.IndexOf(myStacker);
                        var otherIndex = _sortedList.IndexOf(otherStacker);
                        if(myIndex == -1 || otherIndex == -1)
                        {
                            DuoLog.Warning($"優先順序清單裡找不到玩家");
                            _mechanicActive = false;
                            OnReset();
                            return;
                        }
                        myPos = myIndex < otherIndex ? "Left" : "Right";
                        var otherPos = myIndex < otherIndex ? "Right" : "Left";
                        DebugLog($"FirstStacker: {NameOf(myStacker)} {myPos}, LastStacker: {NameOf(otherStacker)} {otherPos}");

                        foreach(var x in noneStackers)
                        {
                            var dpos = noneStackers.IndexOf(x) < 3 ? "Left" : "Right";
                            DebugLog($"noneStacker: {NameOf(x)} {dpos}");
                        }
                    }
                }
            }
            catch(Exception e)
            {
                DuoLog.Error(e.Message);
            }
            return;
        }

        if(set.Action.Value.RowId == CastID.StackCannon)
        {
            Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
            _stackedList.Clear();
            DebugLog("===============================");
            return;
        }
    }

    public override void OnReset()
    {
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        _stackedList.Clear();
        _mechanicActive = false;
    }

    private bool TryGetPriorityList([NotNullWhen(true)] out List<ulong> values)
    {
        var players = Conf.PriorityData.GetPlayers(_ => true);
        if(players == null || players.Count == 0)
        {
            values = [];
            return false;
        }

        values = players
            .Select(x => x.IGameObject as IPlayerCharacter)
            .Where(x => x != null)
            .Select(x => x!.GameObjectId)
            .ToList();

        return values.Count > 0;
    }

    // 舊版每份名單是 8 個名字；PriorityData 已經有填過的名單就不覆蓋（舊名單保留不動）。
    private void MigrateLegacyPriorities()
    {
        if(Conf.LeftRightPriorities is not { Count: > 0 } || Conf.PriorityData?.PriorityLists == null) return;
        if(Conf.PriorityData.PriorityLists.Any(l => l != null && (l.IsRole || l.List?.Any(p => p != null && !p.IsPlayerEmpty()) == true))) return;
        Conf.PriorityData.PriorityLists.Clear();
        foreach(var names in Conf.LeftRightPriorities)
        {
            if(names == null) continue;
            var list = new PriorityList();
            foreach(var name in names)
                list.List.Add(new JobbedPlayer { Name = name ?? "" });
            Conf.PriorityData.PriorityLists.Add(list);
        }
        Conf.LeftRightPriorities.Clear();
        Controller.SaveConfig();
    }

    private static string NameOf(ulong id)
        => Svc.Objects.SearchById(id)?.Name.ToString() ?? $"{id:X}";

    private static string NamesOf(List<ulong> ids)
        => string.Join(", ", ids.Select(NameOf));

    private void DebugLog(string log, [CallerLineNumber] int lineNum = 0)
    {
        if(Conf.DebugPrint)
            DuoLog.Information(log + $" : L({lineNum})");
    }
}
