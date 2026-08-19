using Dalamud.Memory;
using ECommons;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Dalamud.Bindings.ImGui;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Generic;
public unsafe class ExportBlacklist : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [9999];
    public override Metadata? Metadata => new(2, "NightmareXIV");

    public override void OnSettingsDraw()
    {
        if(ImGui.Button("Export blacklist"))
        {
            // 兩個擁有者指標都可能是 null:InfoProxyBlacklist 走 InfoModule 鏈(UIModule／InfoModule
            // 皆可能為 null,proxy 未註冊時 GetInfoProxyById 也回 null),BlackListStringArray 走
            // AtkStage->GetStringArrayData(未就緒時回 null)。
            // 🔴 BlockedCharacters／PlayerNames／Notes 都是 [FixedSizeArray] 的 Span 屬性 ——
            // 對 Span 判空恆為 false,擋得住的位置只有這裡的擁有者指標。
            var blacklist = InfoProxyBlacklist.Instance();
            var strings = BlackListStringArray.Instance();
            if(blacklist == null || strings == null)
            {
                DuoLog.Error("Blacklist data is not available right now.");
                return;
            }
            var s = "";
            var array = blacklist->BlockedCharacters;
            for(var i = 0; i < array.Length; i++)
            {
                var x = array[i];
                if(strings->PlayerNames[i].ToString() != "")
                {
                    s += $"{BlockedCharaToString(x, i, strings)}\n==========================\n";
                }
            }
            GenericHelpers.Copy(s);
        }
    }

    private string BlockedCharaToString(InfoProxyBlacklist.BlockedCharacter c, int index, BlackListStringArray* strings)
    {
        return $"""
            Name: {MemoryHelper.ReadStringNullTerminated((nint)c.Name.Value)},
            ID: {c.Id}
            Comment: {strings->Notes[index]}
            Flag: {c.Flag}
            """;
    }
}
