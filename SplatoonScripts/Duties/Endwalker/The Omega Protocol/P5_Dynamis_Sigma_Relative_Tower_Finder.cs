using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Splatoon.Splatoon;

namespace SplatoonScriptsOfficial.Duties.Endwalker.The_Omega_Protocol;

public unsafe class P5_Dynamis_Sigma_Relative_Tower_Finder : SplatoonScript
{
    #region Metadata
    public override Metadata Metadata { get; } = new(3, "mirage");
    public override Dictionary<int, string> Changelog => new()
    {
        [3] = "台服移植：設定頁說明與除錯資訊改為繁體中文",
    };
    public override HashSet<uint>? ValidTerritories => [TerritoryTop];
    #endregion

    #region Constant
    private const uint TerritoryTop = 1122;
    public const uint SceneId = 6;

    private const uint CastCodeDynamisSigma = 32788;
    private const uint CastWaveCannon = 31603;
    private const uint ActionIdSingleTower = 31492;
    private const uint ActionIdDualTower = 31493;

    public const uint TowerSingle = 2013245;
    public const uint TowerDual = 2013246;

    public const uint GlitchFar = 3428;
    public const uint GlitchMiddle = 3427;

    public const uint TowerCountGlitchFar = 5;
    public const uint TowerCountGlitchMiddle = 6;

    public const float AngleDiff = 45f;
    public const float PlayerRotateAngle = 22.5f;
    private const float AngleSnapStep = 22.5f;
    private static readonly Vector3 ArenaCenter = new(100f, 0f, 100f);
    #endregion

    #region Config
    // No IEZConfig in this script.
    #endregion

    #region State
    private bool _isSigma = false;
    private uint _currentGlitchStatus = 0;
    private float _localPlayerAngle = PlayerRotateAngle;
    private List<ObjectInfo> _allPlayerInfos = [];
    private List<ObjectInfo> _allTowerInfos = [];
    private List<ObjectInfo> _candidateTowerInfos = [];
    private ObjectInfo? _selectedTowerInfo = null;
    #endregion

    #region Private Class
    // 只存抄下的值：玩家清單在波動砲那一刻建好、之後好幾幀都還在用，不能持有物件表的包裝。
    private readonly record struct ObjectInfo(uint EntityId, uint BaseId, Vector3 Position, float Angle)
    {
        public ObjectInfo(IGameObject obj, float angle) : this(obj.EntityId, obj.BaseId, obj.Position, angle) { }
    }
    #endregion

    #region LifeCycle
    public override void OnSetup()
        => Controller.RegisterElementFromCode("navigation", """{"Name":"navigation","Enabled":false,"radius":3.0,"thicc":6.0,"fillIntensity":0.1,"tether":true}""", overwrite: true);

    public override void OnUpdate()
    {
        if(!IsPhaseFive()) return;
        if(!_isSigma)
        {
            DisableElement("navigation");
            return;
        }

        _currentGlitchStatus = GetCurrentGlitchStatus();
        if(_currentGlitchStatus == 0)
        {
            DisableElement("navigation");
            return;
        }

        if(!TryGetLocalPlayerInfo(out var localPlayerInfo))
        {
            DisableElement("navigation");
            return;
        }
        _localPlayerAngle = localPlayerInfo.Angle;

        _allTowerInfos = GetTowerInfos();
        if(!IsExpectedTowerCount(_currentGlitchStatus, _allTowerInfos.Count))
        {
            DisableElement("navigation");
            return;
        }

        var minAngle = NormalizeAngle(_localPlayerAngle - AngleDiff);
        var maxAngle = NormalizeAngle(_localPlayerAngle + AngleDiff);

        _candidateTowerInfos = _allTowerInfos
            .Where(info => IsAngleInRange(info.Angle, minAngle, maxAngle))
            .Where(info => info.Angle != _localPlayerAngle)
            .ToList();

        if(_candidateTowerInfos.Count == 1)
        {
            _selectedTowerInfo = _candidateTowerInfos[0];
        }
        else if(_candidateTowerInfos.Count == 2)
        {
            _selectedTowerInfo = _candidateTowerInfos.FirstOrDefault(x => x.BaseId == TowerDual);
        }
        else
        {
            _selectedTowerInfo = null;
        }

        UpdateNavigationElement(_selectedTowerInfo);
    }

    public override void OnReset()
    {
        _isSigma = false;
        _currentGlitchStatus = 0;
        _localPlayerAngle = PlayerRotateAngle;
        _allPlayerInfos = [];
        _allTowerInfos = [];
        _candidateTowerInfos = [];
        _selectedTowerInfo = null;
        DisableElement("navigation");
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(!IsPhaseFive()) return;
        if(set.Action == null) return;

        var actionId = set.Action.Value.RowId;
        if(actionId == CastCodeDynamisSigma)
        {
            _isSigma = true;
            return;
        }

        if(!_isSigma) return;

        if(actionId == CastWaveCannon)
        {
            _allPlayerInfos = GetPlayerInfos();
            return;
        }

        if(actionId.EqualsAny(ActionIdSingleTower, ActionIdDualTower))
            OnReset();
    }

    public override void OnSettingsDraw()
    {
        ImGui.Text($"這個打法在日服叫「マクロ押さない式」（不用按巨集）。");
        ImGui.Text($"依你被波動砲分散點名時的方位，找出相對位置上該去踩的塔。");
        ImGui.Text($"注意：本腳本不引導前面的「PlayStation 符號」排位與波動砲分散，只指出之後要踩的塔。");

        ImGui.Separator();
        ImGui.Text($"BasePlayer: {BasePlayer?.Name.ToString() ?? "null"}");
        ImGui.Text($"Scene: {Controller.Scene}");
        ImGui.Text($"西格瑪: {_isSigma}");
        ImGui.Text($"故障: {FormatGlitchStatus(_currentGlitchStatus)}");
        ImGui.Text($"自己的角度: {(_localPlayerAngle - PlayerRotateAngle)} + {PlayerRotateAngle} => {_localPlayerAngle}");
        ImGui.Text($"找塔範圍: {(_localPlayerAngle - AngleDiff)} 到 {(_localPlayerAngle + AngleDiff)}");
        ImGui.Text($"玩家: {FormatObjectInfoList(_allPlayerInfos)}");
        ImGui.Text($"全部的塔: {FormatObjectInfoList(_allTowerInfos)}");
        ImGui.Text($"候選的塔: {FormatObjectInfoList(_candidateTowerInfos)}");
        ImGui.Text($"選中的塔: {_selectedTowerInfo?.Angle ?? 0f}");
    }
    #endregion

    #region Private Method
    // True when scene id is P5 Dynamis.
    private bool IsPhaseFive()
        => Controller.Scene == SceneId;

    // Checks tower count matches glitch debuff (far vs middle).
    private static bool IsExpectedTowerCount(uint glitchStatus, int towerCount)
        => (glitchStatus == GlitchFar && towerCount == TowerCountGlitchFar)
           || (glitchStatus == GlitchMiddle && towerCount == TowerCountGlitchMiddle);

    // Finds this player in the cached wave-cannon angle list.
    private bool TryGetLocalPlayerInfo(out ObjectInfo localPlayerInfo)
    {
        localPlayerInfo = default;
        if(BasePlayer == null) return false;

        foreach(var info in _allPlayerInfos)
        {
            if(info.EntityId == BasePlayer.EntityId)
            {
                localPlayerInfo = info;
                return true;
            }
        }
        return false;
    }

    // True if angle lies on the shorter arc between min and max (handles wrap).
    private static bool IsAngleInRange(float angle, float minAngle, float maxAngle)
    {
        if(minAngle <= maxAngle)
            return minAngle <= angle && angle <= maxAngle;
        return minAngle <= angle || angle <= maxAngle;
    }

    // Normalizes to [0,360) and snaps to 22.5° steps.
    private static float NormalizeAngle(float angle)
    {
        var normalized = (angle + 360f) % 360f;
        return (float)(Math.Round(normalized / AngleSnapStep) * AngleSnapStep);
    }

    // Towers on field with compass angle from arena center.
    private List<ObjectInfo> GetTowerInfos()
        => Svc.Objects
            .Where(x => x.BaseId.EqualsAny(TowerSingle, TowerDual))
            .Select(x => new ObjectInfo(x, NormalizeAngle(GetRelativeAngleFromArenaCenter(x.Position))))
            .OrderBy(x => x.Angle)
            .ToList();

    // Party PCs with wave-spread facing angle (player rotate offset applied).
    private List<ObjectInfo> GetPlayerInfos()
        => Svc.Objects.OfType<IPlayerCharacter>()
            .Select(x => new ObjectInfo(x, NormalizeAngle(GetRelativeAngleFromArenaCenter(x.Position) + PlayerRotateAngle)))
            .OrderBy(x => x.Angle)
            .Distinct()
            .ToList();

    // Glitch far/middle status id on BasePlayer, or 0.
    private uint GetCurrentGlitchStatus()
        => BasePlayer?.StatusList.FirstOrDefault(x => x.StatusId == GlitchFar || x.StatusId == GlitchMiddle)?.StatusId ?? 0;

    // Compass degrees from arena center toward position.
    private static float GetRelativeAngleFromArenaCenter(Vector3 position)
        => MathHelper.GetRelativeAngle(ArenaCenter, position);

    // Hides element when missing.
    private void DisableElement(string name)
    {
        if(Controller.TryGetElementByName(name, out var element))
            element.Enabled = false;
    }

    // Points navigation tether at chosen tower; color follows Splatoon Attention Color.
    private void UpdateNavigationElement(ObjectInfo? targetTowerInfo)
    {
        if(!Controller.TryGetElementByName("navigation", out var element))
            return;

        if(targetTowerInfo == null)
        {
            element.Enabled = false;
            return;
        }

        element.SetRefPosition(targetTowerInfo.Value.Position);
        element.color = Controller.AttentionColor;
        element.Enabled = true;
    }

    // Short glitch label for debug UI.
    private static string FormatGlitchStatus(uint glitchStatus)
        => glitchStatus == GlitchFar ? "遠" : glitchStatus == GlitchMiddle ? "中" : "無";

    // One-line list of tower angles and single/dual type.
    private static string FormatObjectInfoList(List<ObjectInfo> infos)
        => $"[{string.Join(", ", infos.Select(x => $"{x.Angle} ({(x.BaseId == TowerSingle ? "單人" : "雙人")})"))}]";
    #endregion
}
