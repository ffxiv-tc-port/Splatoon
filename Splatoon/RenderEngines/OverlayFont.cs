using Dalamud.Interface.ManagedFontAtlas;

namespace Splatoon.RenderEngines;

/// <summary>
/// 疊圖文字放大時用的清楚字型：用到才建、只常駐一階，閒置後釋放。
/// </summary>
/// <remarks>
/// 契約：只在繪製執行緒使用（建／換／釋放都在自己的 UiBuilder.Draw 處理器裡，不在文字推入期間）。
/// 新字建好前沿用舊字或拉伸，所以換階不會有空白幀；建置失敗＝不可用＝一直拉伸，不擲例外。
/// </remarks>
internal sealed class OverlayFont : IDisposable
{
    private readonly OverlayFontTierTracker tracker = new();
    private IFontHandle current;
    private float currentTier;
    private IFontHandle pending;
    private float pendingTier;
    private float failedTier;
    private float frameMax;
    private bool disposed;

    private OverlayFont()
    {
        Svc.PluginInterface.UiBuilder.Draw += Tick;
    }

    /// <summary>
    /// 給繪製文字用：回傳要推入的字型（null＝不推、照舊拉伸）與視窗字級。<see cref="S"/> 還沒建好時也安全。
    /// </summary>
    public static IFontHandle For(float fscale, out float windowScale)
    {
        var total = OverlayFontTiers.Total(fscale, P.Config.OverlayFontScale);
        var self = S.OverlayFont;
        if(self == null || self.disposed)
        {
            windowScale = total;
            return null;
        }
        var enabled = P.Config.OverlayFontSharp;
        if(enabled && total > self.frameMax && OverlayFontTiers.TierFor(total) > 0f) self.frameMax = total;
        var font = self.current;
        return OverlayFontTiers.Resolve(total, enabled, self.currentTier, font != null && font.Available, out windowScale) ? font : null;
    }

    private void Tick()
    {
        if(disposed) return;
        if(pending != null)
        {
            if(pending.Available)
            {
                current?.Dispose();
                current = pending;
                currentTier = pendingTier;
                pending = null;
                PluginLog.Information($"[OverlayFont] Sharp overlay font ready: {currentTier:0.0}x");
            }
            else if(pending.LoadException != null)
            {
                PluginLog.Warning($"[OverlayFont] Failed to build {pendingTier:0.0}x overlay font, enlarged text stays stretched: {pending.LoadException.Message}");
                failedTier = pendingTier;
                pending.Dispose();
                pending = null;
            }
        }

        var max = frameMax;
        frameMax = 0f;
        if(!P.Config.OverlayFontSharp) tracker.Reset();
        var wanted = P.Config.OverlayFontSharp ? tracker.Tick(max, Environment.TickCount64) : 0f;

        if(wanted <= 0f)
        {
            failedTier = 0f;
            if(current != null || pending != null)
            {
                PluginLog.Information($"[OverlayFont] Sharp overlay font released ({currentTier:0.0}x): no enlarged text for {OverlayFontTiers.IdleMs / 1000}s or option disabled");
                ReleaseAll();
            }
            return;
        }

        var existing = pending != null ? pendingTier : current != null ? currentTier : 0f;
        if(existing == wanted || failedTier == wanted) return;
        pending?.Dispose();
        pendingTier = wanted;
        pending = NewHandle(wanted);
    }

    // 另開方法：參數被 lambda 捕獲時閉包在方法入口就配置，放在 Tick 裡會變成每幀都配一次。
    private static IFontHandle NewHandle(float tier) =>
        Svc.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
            e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(-tier)));

    private void ReleaseAll()
    {
        pending?.Dispose();
        pending = null;
        current?.Dispose();
        current = null;
        currentTier = 0f;
    }

    public void Dispose()
    {
        disposed = true;
        Svc.PluginInterface.UiBuilder.Draw -= Tick;
        ReleaseAll();
    }
}
