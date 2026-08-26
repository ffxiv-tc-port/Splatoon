using FFXIVClientStructs.FFXIV.Client.Game;
using Splatoon.Memory;

namespace Splatoon.Data;

/// <summary>
/// 「招式類型 + 招式 ID」的組合鍵。
/// </summary>
/// <remarks>
/// 型別名、命名空間與每一個公開成員都與上游 PunishXIV/Splatoon 逐字相同，
/// 讓上游腳本裡的 <c>new ActionDescriptor(ActionType.Action, 12345)</c> 可以零改動編過。
/// 上游是由封包結構建構，我方改由 <see cref="PacketActorCast"/> 這個
/// 「從遊戲已解析好的 CastInfo 組出來的」結構建構——對呼叫端而言看不出差別。
/// </remarks>
public readonly record struct ActionDescriptor
{
    public readonly ActionType Type;
    public readonly uint Id;

    public ActionDescriptor()
    {
    }

    public ActionDescriptor(ActionType type, uint id)
    {
        Type = type;
        Id = id;
    }

    public ActionDescriptor(int type, uint id)
    {
        Type = (ActionType)type;
        Id = id;
    }

    public ActionDescriptor(uint id)
    {
        Type = ActionType.Action;
        Id = id;
    }

    public ActionDescriptor(PacketActorCast packet)
    {
        Type = (ActionType)packet.ActionType;
        Id = packet.ActionID;
    }
}
