using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SplatoonScriptsOfficial.Tests;
internal unsafe class RedmoonTest3 :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = null;
    public override Metadata? Metadata => new(1, "Redmoon");

    IBattleChara?[] GetEnemyList()
    {
        var list = new IBattleChara?[8];
        // 7.2 → 7.3 在 CastBarEnemy 處插入一項，之後每個 NumberArray 索引都 +1：EnemyList 21 → 22。
        // 🔴 原生取陣列函式對 index 完全沒有邊界檢查，索引寫錯＝任意記憶體讀取 → AVE，攔不到。
        // 用具名列舉而不是魔術數字，下次再位移時會自己跟著動。
        var array = AtkStage.Instance()->AtkArrayDataHolder->NumberArrays[(int)NumberArrayType.EnemyList];
        var characters = Svc.Objects.OfType<IBattleChara>().ToArray();
        for (int i = 0; i < 8; i++)
        {
            var id = *(uint*)&array->IntArray[8 + (i * 6)];
            if (id != 0xE0000000)
            {
                list[i] = characters.FirstOrDefault(x => x.EntityId == id);
            }
        }
        return list;
    }

    public override void OnSettingsDraw()
    {
        var list = new IBattleChara?[8];
        // 同上：EnemyList 在 7.3 世代是 22，不是 21。
        var array = AtkStage.Instance()->AtkArrayDataHolder->NumberArrays[(int)NumberArrayType.EnemyList];
        var characters = Svc.Objects.OfType<IBattleChara>().ToArray();
        for (int i = 0; i < 8; i++)
        {
            var ptr = (uint*)&array->IntArray[8 + (i * 6)];
            var id = *ptr;
            ImGuiEx.Text($"Address: 0x{((IntPtr)ptr).ToString("X8")}, ID: {id}");
            if (id != 0xE0000000)
            {
                list[i] = characters.FirstOrDefault(x => x.EntityId == id);
            }
        }
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null)
            {
                ImGuiEx.Text($"Enemy {i}: NULL");
                continue;
            }
            ImGuiEx.Text($"Enemy {i}: {list[i]?.Name} HP:{list[i].CurrentHp}");
        }
    }
}
