using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using static Splatoon.Splatoon;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Endwalker.The_Omega_Protocol;
internal unsafe class Cosmo_Meteor_Adjuster : SplatoonScript
{
    #region PublicDef
    public override HashSet<uint> ValidTerritories => [1122];
    public override Metadata? Metadata => new(5, "Redmoon, mirage");
    public override Dictionary<int, string> Changelog => new()
    {
        [5] = "優先順序改用 Splatoon 共用的優先順序清單（舊版自訂的名單會自動搬過去）；改由清單第 1 位有沒有被點名決定往南或往北分攤，往南分攤時另外兩個大圈維持原本的東西分配（清單順序在前的去東、在後的去西）；設定介面改為繁體中文",
    };
    #endregion

    #region PrivateDef
    // 只存點名當下抄下的 GameObjectId 與名字，不跨幀持有物件表的包裝。
    private class FlareContainer
    {
        public ulong Id;
        public string Name;
        public bool mine = false;

        public FlareContainer(IPlayerCharacter character, bool mine)
        {
            Id = character.GameObjectId;
            Name = character.Name.ToString();
            this.mine = mine;
        }
    }

    private class CastID
    {
        public const uint CosmoMeteor = 31664;
        public const uint CosmoMeteorFlare = 31668;
    }

    private class VfxPath
    {
        public const string Flare = "vfx/lockon/eff/all_at8s_0v.avfx";
    }

    private enum StackPos
    {
        Undefined = 0,
        North = 1,
        South = 2
    }

    private static readonly string[] NorthFlareElements = ["FlareEast", "FlareSouth", "FlareWest"];

    private StackPos _stackPos = StackPos.Undefined;
    private List<FlareContainer> _flarePos = [];
    private bool _gimmickActive = false;
    private bool _isFlareMine = false;
    private GameObjectManager* _gom = GameObjectManager.Instance();
    private (string, string)[] _flareData = new (string, string)[3];
    private Config C => Controller.GetConfig<Config>();
    #endregion

    #region public
    public override void OnSetup()
    {
        Controller.RegisterElementFromCode("FlareNorth", "{\"Name\":\"\",\"refX\":100.0,\"refY\":80.36,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("FlareEast", "{\"Name\":\"\",\"refX\":119.46,\"refY\":99.96,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("FlareWest", "{\"Name\":\"\",\"refX\":80.56,\"refY\":99.96,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("FlareSouth", "{\"Name\":\"\",\"refX\":100.0,\"refY\":119.56,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");

        Controller.RegisterElementFromCode("StackNorth", "{\"Name\":\"\",\"refX\":100.0,\"refY\":85.32,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("StackSouth", "{\"Name\":\"\",\"refX\":100.0,\"refY\":115.1,\"refZ\":-5.4569678E-12,\"radius\":0.3,\"color\":3355508546,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":15.0,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        MigrateLegacyPriorities();
    }

    public override void OnVFXSpawn(uint target, string vfxPath)
    {
        if(target.GetObject() is IPlayerCharacter character && _gimmickActive)
        {
            if(vfxPath == VfxPath.Flare)
            {
                _flarePos.Add(new FlareContainer(character, false));
                if(BasePlayer != null && character.GameObjectId == BasePlayer.GameObjectId)
                {
                    _flarePos.Last().mine = true;
                    _isFlareMine = true;
                }

                if(_flarePos.Count >= 3)
                {
                    ResolveStackPos();
                    Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
                    ArbitPosition();

                    if(!_isFlareMine)
                    {
                        if(_stackPos == StackPos.North)
                        {
                            Controller.GetElementByName("StackNorth").Enabled = true;
                            Controller.GetElementByName("StackNorth").tether = true;
                        }
                        else if(_stackPos == StackPos.South)
                        {
                            Controller.GetElementByName("StackSouth").Enabled = true;
                            Controller.GetElementByName("StackSouth").tether = true;
                        }
                    }
                }
            }
        }
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if(castId == CastID.CosmoMeteor)
        {
            _gimmickActive = true;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(set.Action == null)
            return;

        if(set.Action.Value.RowId == 40165)
        {
            OnReset();
        }
    }

    public override void OnUpdate()
    {
        if(Controller.GetElementByName("FlareNorth").Enabled == true)
            Controller.GetElementByName("FlareNorth").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
        if(Controller.GetElementByName("FlareEast").Enabled == true)
            Controller.GetElementByName("FlareEast").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
        if(Controller.GetElementByName("FlareWest").Enabled == true)
            Controller.GetElementByName("FlareWest").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
        if(Controller.GetElementByName("FlareSouth").Enabled == true)
            Controller.GetElementByName("FlareSouth").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
        if(Controller.GetElementByName("StackNorth").Enabled == true)
            Controller.GetElementByName("StackNorth").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
        if(Controller.GetElementByName("StackSouth").Enabled == true)
            Controller.GetElementByName("StackSouth").color = GradientColor.Get(0xFF00FF00.ToVector4(), 0xFF0000FF.ToVector4()).ToUint();
    }

    public override void OnReset()
    {
        _flarePos.Clear();
        _stackPos = StackPos.Undefined;
        _isFlareMine = false;
        _gimmickActive = false;
        _flareData = new (string, string)[3];
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
    }

    public class Config : IEzConfig
    {
        public bool Debug = false;
        public PriorityData PriorityData = new()
        {
            Name = "Flare Priority",
            Description = "Clockwise from North (Range LB Player). Position 1 is Range LB; if they have a flare, party stacks South.",
        };
        // 舊版的名單格式：OnSetup 搬進 PriorityData 後清空。
        public List<string[]> clockWisePriorities = [];
    }

    public override void OnSettingsDraw()
    {
        ImGui.Text("大圈點名的優先順序");
        ImGui.Text("從北邊（負責遠程物理極限技的人）開始順時針排；第 1 位被點名時全隊往南分攤");
        C.PriorityData.Draw();

        if(ImGui.CollapsingHeader("除錯"))
        {
            ImGui.Text("分攤位置: " + _stackPos.ToString());
            ImGui.Text("機制進行中: " + _gimmickActive.ToString());
            ImGui.Text("自己被點名: " + _isFlareMine.ToString());

            foreach(var data in _flareData)
            {
                if(data.Item1 == null || data.Item2 == null) continue;
                ImGui.Text(data.Item1 + ": " + data.Item2);
            }

            List<ImGuiEx.EzTableEntry> entries = [];
            foreach(var character in _flarePos)
            {
                entries.Add(new ImGuiEx.EzTableEntry("名字", () => ImGui.Text(character.Name)));
                entries.Add(new ImGuiEx.EzTableEntry("是自己", () => ImGui.Text(character.mine.ToString())));
            }
            ImGuiEx.EzTable(entries);
        }
    }
    #endregion

    #region private
    private bool TryGetPriorityList([NotNullWhen(true)] out List<IPlayerCharacter> values)
    {
        var players = C.PriorityData.GetPlayers(_ => true);
        if(players == null || players.Count == 0)
        {
            values = [];
            return false;
        }

        values = players
            .Select(x => x.IGameObject as IPlayerCharacter)
            .Where(x => x != null)
            .Cast<IPlayerCharacter>()
            .ToList();

        return values.Count > 0;
    }

    private IPlayerCharacter? GetRangeLbPlayer()
    {
        if(!TryGetPriorityList(out var priorityList) || priorityList.Count == 0)
            return null;

        return priorityList[0];
    }

    private void ResolveStackPos()
    {
        var rangePlayer = GetRangeLbPlayer();
        if(rangePlayer != null && _flarePos.Any(x => x.Id == rangePlayer.GameObjectId))
            _stackPos = StackPos.South;
        else
            _stackPos = StackPos.North;
    }

    private void ArbitPosition()
    {
        if(!TryGetPriorityList(out var priorityList))
            return;

        var flarePlayers = priorityList
            .Where(p => _flarePos.Any(f => f.Id == p.GameObjectId))
            .ToList();

        if(flarePlayers.Count < 3)
            return;

        _flareData = new (string, string)[3];
        var dataIndex = 0;

        if(_stackPos == StackPos.North)
        {
            for(var i = 0; i < 3; i++)
                AssignFlareElement(NorthFlareElements[i], flarePlayers[i], ref dataIndex);
        }
        else if(_stackPos == StackPos.South)
        {
            var rangePlayer = GetRangeLbPlayer();
            var nonRange = flarePlayers
                .Where(p => rangePlayer == null || p.Address != rangePlayer.Address)
                .ToList();

            // 刻意不照上游對調東西：維持我方舊版「順序在前的去東、在後的去西」。
            if(rangePlayer != null && flarePlayers.Any(p => p.Address == rangePlayer.Address))
                AssignFlareElement("FlareNorth", rangePlayer, ref dataIndex);
            if(nonRange.Count >= 1)
                AssignFlareElement("FlareEast", nonRange[0], ref dataIndex);
            if(nonRange.Count >= 2)
                AssignFlareElement("FlareWest", nonRange[1], ref dataIndex);
        }
    }

    private void AssignFlareElement(string elementName, IPlayerCharacter player, ref int dataIndex)
    {
        if(dataIndex < _flareData.Length)
            _flareData[dataIndex] = (elementName, player.Name.ToString());

        if(BasePlayer != null && player.GameObjectId == BasePlayer.GameObjectId)
        {
            Controller.GetElementByName(elementName).Enabled = true;
            Controller.GetElementByName(elementName).tether = true;
        }

        dataIndex++;
    }

    // 舊版每份名單是 8 個名字；PriorityData 已經有填過的名單就不覆蓋（舊名單保留不動）。
    private void MigrateLegacyPriorities()
    {
        if(C.clockWisePriorities is not { Count: > 0 } || C.PriorityData?.PriorityLists == null) return;
        if(C.PriorityData.PriorityLists.Any(l => l != null && (l.IsRole || l.List?.Any(p => p != null && !p.IsPlayerEmpty()) == true))) return;
        C.PriorityData.PriorityLists.Clear();
        foreach(var names in C.clockWisePriorities)
        {
            if(names == null) continue;
            var list = new PriorityList();
            foreach(var name in names)
                list.List.Add(new JobbedPlayer { Name = name ?? "" });
            C.PriorityData.PriorityLists.Add(list);
        }
        C.clockWisePriorities.Clear();
        Controller.SaveConfig();
    }
    #endregion
}
