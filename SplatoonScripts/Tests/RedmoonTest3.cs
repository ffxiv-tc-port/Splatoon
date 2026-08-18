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
    public override Metadata? Metadata => new(2, "Redmoon");

    /// <summary>
    ///     這支腳本會讀到的最大 IntArray 索引是 8 + 7*6 = 50，所以陣列至少要有 51 個 int。
    /// </summary>
    const int RequiredIntArrayLength = 8 + (7 * 6) + 1;

    /// <summary>
    ///     取 EnemyList 這個 NumberArray，逐層判空＋驗長度，任何一層取不到就回 null。
    ///     這條鏈上沒有一層是安全的：
    ///     AtkStage.Instance() 是 [StaticAddress(..., isPointer: true)]，只有「特徵碼沒解析到」
    ///     才會擲例外，解析到但遊戲尚未建好 AtkStage 時回傳的是存在該位址的 null 指標；
    ///     AtkArrayDataHolder 是 AtkStage 上的純指標欄位；
    ///     NumberArrays 是裸的 NumberArrayData**，索引沒有任何邊界檢查；
    ///     IntArray 又是裸的 int*，只有 AtkArrayData.Size 這個欄位知道它多長
    ///     （NumberArrayData.SetValue 的特徵碼 3B 51 08 就是在拿 index 比 [rcx+8]＝Size，
    ///     可證 Size 的單位是 int 個數而不是位元組）。
    ///     任何一層漏掉都是 try/catch 攔不到的 AccessViolationException。
    /// </summary>
    static NumberArrayData* GetEnemyListArray()
    {
        var stage = AtkStage.Instance();
        if (stage == null) return null;

        var holder = stage->AtkArrayDataHolder;
        if (holder == null || holder->NumberArrays == null) return null;

        // 7.2 → 7.3 在 CastBarEnemy 處插入一項，之後每個 NumberArray 索引都 +1：EnemyList 21 → 22。
        // 用具名列舉而不是魔術數字，下次再位移時會自己跟著動。
        var index = (int)NumberArrayType.EnemyList;
        if (index < 0 || index >= holder->NumberArrayCount) return null;

        var array = holder->NumberArrays[index];
        if (array == null || array->IntArray == null) return null;
        if (array->Size < RequiredIntArrayLength) return null;

        return array;
    }

    IBattleChara?[] GetEnemyList()
    {
        var list = new IBattleChara?[8];
        var array = GetEnemyListArray();
        if (array == null)
        {
            // 拿不到就回一整排 null。呼叫端本來就要處理每個項目為 null 的情況
            // （原本 id == 0xE0000000 或找不到對應物件時就是這個結果）。
            return list;
        }

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
        var array = GetEnemyListArray();
        if (array == null)
        {
            // 「取不到」要看得見，不能畫成一排 0 或空白讓人以為是「敵人清單是空的」。
            ImGuiEx.Text("EnemyList number array unavailable (not in combat, or UI not ready).");
            return;
        }

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
