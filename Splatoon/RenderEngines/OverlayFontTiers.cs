using System;

namespace Splatoon.RenderEngines;

/// <summary>
/// 疊圖文字放大字型的純計算（不碰 Dalamud／ImGui，離線可驗）。
/// 倍率一律相對 Dalamud 預設字型；字型只有 <see cref="Tiers"/> 幾階，畫的時候縮到 total／階數。
/// </summary>
internal static class OverlayFontTiers
{
    /// <summary>倍率不超過這個值照舊用拉伸（差異小到看不出來，不值得多建一份字型）。</summary>
    public const float MinTotalForFont = 1.05f;

    /// <summary>可建的階數，由小到大；上限壓在 2 倍，因為含中日韓字形的字型記憶體隨倍率平方成長。</summary>
    public static readonly float[] Tiers = [1.5f, 2f];

    /// <summary>沒用到某一階（或任何放大字）超過這段時間就降階／釋放。</summary>
    public const long IdleMs = 60_000;

    /// <summary>元素倍率乘上整體倍率；整體倍率不合法時當作 1（＝只看元素倍率，與舊行為相同）。</summary>
    public static float Total(float fscale, float globalScale) =>
        float.IsFinite(globalScale) && globalScale > 0f ? fscale * globalScale : fscale;

    /// <summary>這個倍率要用哪一階字型；0＝不用（照舊拉伸）。超過最高階時回最高階。</summary>
    public static float TierFor(float total)
    {
        if(!float.IsFinite(total) || total <= MinTotalForFont) return 0f;
        foreach(var tier in Tiers)
        {
            if(total <= tier) return tier;
        }
        return Tiers[^1];
    }

    /// <summary>
    /// 決定一段文字怎麼畫。回 true＝推入已建好的 <paramref name="builtTier"/> 字型、視窗字級設為 <paramref name="windowScale"/>；
    /// 回 false＝不推字型，視窗字級＝total（與舊行為逐位元相同）。
    /// </summary>
    public static bool Resolve(float total, bool enabled, float builtTier, bool builtAvailable, out float windowScale)
    {
        if(enabled && builtAvailable && builtTier > 0f && TierFor(total) > 0f)
        {
            windowScale = total / builtTier;
            return true;
        }
        windowScale = total;
        return false;
    }
}

/// <summary>
/// 依最近用到的最大倍率決定要常駐哪一階：升階立即、降階或釋放要連續 <see cref="OverlayFontTiers.IdleMs"/> 沒用到。
/// </summary>
internal sealed class OverlayFontTierTracker
{
    public float Wanted { get; private set; }
    private long lastWantedUse;

    /// <param name="frameMaxTotal">上一段期間畫過的最大 total；沒有放大字傳 0。</param>
    /// <param name="nowMs">單調遞增的毫秒時間。</param>
    /// <returns>現在應該常駐的階數；0＝不需要字型。</returns>
    public float Tick(float frameMaxTotal, long nowMs)
    {
        var tier = OverlayFontTiers.TierFor(frameMaxTotal);
        if(tier > 0f && tier >= Wanted)
        {
            Wanted = tier;
            lastWantedUse = nowMs;
        }
        else if(Wanted > 0f && nowMs - lastWantedUse > OverlayFontTiers.IdleMs)
        {
            Wanted = tier;
            lastWantedUse = nowMs;
        }
        return Wanted;
    }

    public void Reset()
    {
        Wanted = 0f;
        lastWantedUse = 0;
    }
}
