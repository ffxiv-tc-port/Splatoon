using ECommons.LanguageHelpers;
using System.Collections.ObjectModel;

namespace Splatoon.Utility;

/// <summary>
/// 為 <c>ImGuiEx.EnumCombo</c> 產生「已在地化的列舉成員名稱」對照表。
///
/// ECommons 的 EnumCombo 在沒有拿到 <c>names</c> 時，直接畫
/// <c>value.ToString().Replace("_", " ")</c>，所以即使周圍的標籤都翻成中文，
/// 下拉選單裡的選項仍然固定是英文成員名。這個類別把那個「預設會畫出來的字串」
/// 原封不動當成 <c>.Loc()</c> 的 key，因此：
///   - ini 有該條目 → 顯示譯文；
///   - ini 沒有該條目 → <c>.Loc()</c> 原樣回傳 → 顯示逐字與改動前相同。
/// 也就是說補上 names 本身不會改變任何未翻譯成員的顯示。
///
/// 快取：這些呼叫點全都在每幀執行的 Draw 裡，每幀重建字典等於每幀配置一個字典
/// 加上每個成員一個字串，所以按型別快取。
/// ⚠️ Splatoon 允許執行期切換介面語言（CGuiGeneralSettings 的語言下拉會再呼叫
/// <c>Localization.Init</c>），所以快取不能是「一輩子只算一次」——
/// 這裡把建表當下的 <see cref="Localization.CurrentLanguage"/> 一起記下來，
/// 語言變了就重建。（AutoRetainer 的同類快取可以永久保存，是因為它沒有執行期語言切換。）
/// </summary>
public static class LocEnum
{
    private static class Cache<T> where T : struct, Enum
    {
        internal static ReadOnlyDictionary<T, string> Value;
        internal static string Language;
    }

    /// <summary>
    /// 取得 <typeparamref name="T"/> 每個成員的在地化顯示名稱，可直接交給
    /// <c>ImGuiEx.EnumCombo(..., names: LocEnum.Names&lt;T&gt;())</c>。
    /// 回傳的是唯讀字典，避免呼叫端改到共用的快取實例。
    /// </summary>
    public static IDictionary<T, string> Names<T>() where T : struct, Enum
    {
        if(Cache<T>.Value == null || Cache<T>.Language != Localization.CurrentLanguage)
        {
            var dict = new Dictionary<T, string>();
            foreach(var value in Enum.GetValues<T>())
            {
                // 用索引子而不是 Add：同一個數值有兩個成員名時 Add 會擲例外。
                dict[value] = value.ToString().Replace("_", " ").Loc();
            }
            Cache<T>.Value = new(dict);
            Cache<T>.Language = Localization.CurrentLanguage;
        }
        return Cache<T>.Value;
    }
}
