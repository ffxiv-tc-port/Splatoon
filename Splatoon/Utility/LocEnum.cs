using ECommons.LanguageHelpers;
using System.Collections.ObjectModel;

namespace Splatoon.Utility;

/// <summary>
/// 為 <c>ImGuiEx.EnumCombo</c> 產生「已在地化的列舉成員名稱」對照表。
/// ECommons 的 EnumCombo 沒拿到 <c>names</c> 時畫的是 <c>value.ToString().Replace("_", " ")</c>；這個類別把那個字串
/// 原封不動當成 <c>.Loc()</c> 的 key：ini 有該條目就顯示譯文，沒有就原樣回傳（顯示不變）。
/// 呼叫點全都在每幀執行的 Draw 裡，所以按型別快取；⚠️ Splatoon 允許執行期切換介面語言，
/// 所以把建表當下的 <see cref="Localization.CurrentLanguage"/> 一起記下來，語言變了就重建。
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
