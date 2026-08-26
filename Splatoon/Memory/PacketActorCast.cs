using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Splatoon.Data;

#nullable enable
namespace Splatoon.Memory;

/// <summary>
/// 施法開始事件的資料載體。型別名、命名空間與每一個公開成員都與上游
/// PunishXIV/Splatoon 的 <c>PacketActorCast</c> 逐字相同，讓依賴它的上游腳本可以零改動編過。
/// </summary>
/// <remarks>
/// 🔴 **與上游的本質差別：這裡沒有任何封包。**
/// 上游是用一條寫死的特徵碼 hook 住遊戲的封包處理函式，搶在遊戲之前解參考封包 payload、
/// 自己把量化過的角度與座標解回來。我方改成純輪詢：<see cref="AttachedInfo"/> 每幀
/// 掃描物件表，偵測到新的施法時直接讀遊戲**已經解析好**的 <see cref="CastInfo"/>
/// 與 <c>Character.CastRotation</c>，組出這個結構再派給腳本。
/// 因此本檔沒有特徵碼、沒有 opcode、沒有手寫封包佈局，
/// 遊戲改版時不會靜默解錯欄位（欄位偏移由 FFXIVClientStructs 維護）。
///
/// 代價有兩個，兩個都只會讓值不準或少一次事件，不會崩：
/// <list type="number">
/// <item>事件最多晚一幀（約 16ms）。</item>
/// <item><see cref="Position"/> 只有在「地面指定技」才是本次施法的值，見該成員的說明。</item>
/// </list>
///
/// 另外，上游把 <c>RawRotation</c> / <c>PosX,Y,Z</c> 當成封包原始欄位、把
/// <c>Rotation</c> / <c>Position</c> 當成由它們算出來的屬性；我方剛好相反——
/// 真值是未量化的 <see cref="Rotation"/> / <see cref="Position"/>，
/// 量化欄位是反推回去的，只為了讓來源相容。目前沒有任何腳本讀量化欄位。
/// </remarks>
public struct PacketActorCast
{
    /// <summary>上游用的角度量化步長（弧度/單位）。反量化時逐字沿用，確保與上游同值域。</summary>
    private const float RotationQuantum = 0.0095875263f * 0.0099999998f;
    /// <summary>上游用的座標量化步長。</summary>
    private const float PositionQuantum = 3.0518043f * 0.0099999998f;
    /// <summary>上游用的座標原點偏移。</summary>
    private const float PositionOrigin = 1000.0f;

    /// <summary>施法招式 ID。取 <c>CastInfo.ActionId</c> 的低 16 位，與上游封包欄位等寬。</summary>
    public ushort ActionID;

    /// <summary>招式類型，逐字取自 <c>CastInfo.ActionType</c>。可與 <c>FFXIVClientStructs 的 ActionType</c> 比對。</summary>
    public byte ActionType;

    /// <summary>把 <see cref="ActionType"/> 與 <see cref="ActionID"/> 包成一個可直接比對的組合鍵。</summary>
    public readonly ActionDescriptor ActionDescriptor => new(this);

    /// <summary>上游封包裡用途不明的欄位。輪詢式來源沒有這個值，恆為 0。</summary>
    [Obsolete("Unknown")]
    public byte Unknown;

    /// <summary>上游封包裡的第二份 action id。輪詢式來源沒有這個值，恆為 0。</summary>
    [Obsolete("Unknown")]
    public uint Unknown1;

    /// <summary>
    /// 詠唱長度（秒）。取 <c>CastInfo.BaseCastTime</c>。
    /// 🔴 不是 <c>TotalCastTime</c>——後者含伺服器加上去的餘裕值，會比封包裡的值大一點點。
    /// </summary>
    public float CastTime;

    /// <summary>施法目標的 EntityId。取 <c>CastInfo.TargetId</c> 的低 32 位。</summary>
    public uint TargetID;

    /// <summary>
    /// 施法朝向（弧度，[-π, π]）。取 <c>Character.CastRotation</c>。
    /// </summary>
    /// <remarks>
    /// 🔴 刻意不用 <c>CastInfo.Rotation</c>：NPC 施法時遊戲不寫那個欄位，讀到的是殘值。
    /// <c>CastRotation</c> 才是遊戲從施法事件裡解出來、逐位元等同上游封包角度的那一份。
    /// </remarks>
    public float Rotation;

    /// <summary>以正北為 0 的施法朝向（弧度）。與上游同一條換算式。</summary>
    public readonly float RotationFromNorth => -Rotation + MathF.PI;

    /// <summary>把 <see cref="Rotation"/> 反量化回上游封包的原始整數表示。只為來源相容而存在。</summary>
    public readonly ushort RawRotation => Quantize(Rotation + MathF.PI, 0f, RotationQuantum);

    /// <summary>上游封包裡用途不明的欄位。輪詢式來源沒有這個值，恆為 0。</summary>
    [Obsolete("Unknown")]
    public uint Unknown2;

    /// <summary>
    /// 施法的目標地點。取 <c>CastInfo.TargetLocation</c>。
    /// </summary>
    /// <remarks>
    /// ⚠️ **遊戲只在「地面指定技」才會把本次施法的座標寫進這個欄位**，其餘招式讀到的是
    /// 同一個施法者上一次地面指定技留下的殘值（不會報錯、不會是零）。
    /// 需要「施法者站在哪」時請改用 <see cref="CasterPosition"/>。
    /// </remarks>
    public Vector3 Position;

    /// <summary>把 <see cref="Position"/> 的 X 反量化回上游封包的原始整數表示。只為來源相容而存在。</summary>
    public readonly ushort PosX => Quantize(Position.X, PositionOrigin, PositionQuantum);
    /// <summary>把 <see cref="Position"/> 的 Y 反量化回上游封包的原始整數表示。只為來源相容而存在。</summary>
    public readonly ushort PosY => Quantize(Position.Y, PositionOrigin, PositionQuantum);
    /// <summary>把 <see cref="Position"/> 的 Z 反量化回上游封包的原始整數表示。只為來源相容而存在。</summary>
    public readonly ushort PosZ => Quantize(Position.Z, PositionOrigin, PositionQuantum);

    /// <summary>上游封包裡用途不明的欄位。輪詢式來源沒有這個值，恆為 0。</summary>
    [Obsolete("Unknown")]
    public ushort Unknown3;

    /// <summary>
    /// 施法者在事件當幀的座標。上游沒有這個成員，是輪詢式來源附帶的補償——
    /// <see cref="Position"/> 對非地面指定技不可信時用它。
    /// </summary>
    public Vector3 CasterPosition;

    /// <summary>本次施法是否可被打斷。取 <c>CastInfo.Interruptible</c>。上游沒有開放這個值。</summary>
    public bool Interruptible;

    /// <summary>
    /// 反量化：把浮點值換回上游封包的 <c>ushort</c> 表示。
    /// 夾在 <c>ushort</c> 值域內，避免異常座標造成未定義的轉型結果。
    /// </summary>
    private static ushort Quantize(float value, float origin, float quantum)
        => (ushort)Math.Clamp(MathF.Round((value + origin) / quantum), 0f, ushort.MaxValue);

    /// <summary>
    /// 從遊戲已解析好的結構填出一次施法事件。
    /// </summary>
    /// <param name="dest">要填入的緩衝區。呼叫端負責它的壽命。</param>
    /// <param name="chara">施法者。只在當幀使用，本方法不保存任何原生指標。</param>
    /// <returns>成功填入時為 <c>true</c>；施法者或 CastInfo 不可用時為 <c>false</c>（此時 <paramref name="dest"/> 內容不變）。</returns>
    internal static unsafe bool TryFill(PacketActorCast* dest, Character* chara)
    {
        if(dest == null || chara == null) return false;
        var castInfo = chara->GetCastInfo();
        if(castInfo == null) return false;

        // 先整份清零，未知欄位才不會留著上一次事件的殘值。
        *dest = default;
        dest->ActionID = (ushort)castInfo->ActionId;
        dest->ActionType = (byte)castInfo->ActionType;
        dest->CastTime = castInfo->BaseCastTime;
        dest->TargetID = castInfo->TargetId.ObjectId;
        dest->Rotation = chara->CastRotation;
        dest->Position = castInfo->TargetLocation;
        dest->CasterPosition = chara->Position;
        dest->Interruptible = castInfo->Interruptible;
        return true;
    }
}
