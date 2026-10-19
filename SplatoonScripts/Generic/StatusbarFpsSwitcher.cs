using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Reflection;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Generic;
public class StatusbarFpsSwitcher : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = null;

    private IDtrBarEntry Entry;

    public override Metadata? Metadata => new(2, "NightmareXIV");

    public override void OnEnable()
    {
        Entry = Svc.DtrBar.Get("Splatoon.FpsSwitcher", "");
        Entry.Shown = true;
        Entry.Tooltip = "左鍵：切換 30／60 FPS" + "\n" + "右鍵：開啟／關閉 Splatoon 設定";
        Entry.OnClick = (x) =>
        {
            if(x.ClickType == MouseClickType.Left)
            {
                Toggle();
            }
            else
            {
                ToggleSplatoonSettings();
            }
        };
        Update();
    }

    private void Update()
    {
        Entry = Svc.DtrBar.Get("Splatoon.FpsSwitcher", "");
        var cfg = Svc.GameConfig.System.GetUInt("Fps");
        if(cfg == 0)
        {
            Entry.Text = $"FPS: U";
        }
        else if(cfg == 1)
        {
            Entry.Text = $"FPS: D";
        }
        else if(cfg == 2)
        {
            Entry.Text = $"FPS: 60";
        }
        else if(cfg == 3)
        {
            Entry.Text = $"FPS: 30";
        }
    }

    private void Toggle()
    {
        var cfg = Svc.GameConfig.System.GetUInt("Fps");
        Svc.GameConfig.System.Set("Fps", cfg == 3 ? 2u : 3u);
        Update();
    }

    // 腳本可能比插件先更新，所以不呼叫插件新增的成員；Splatoon 設定視窗的開關是 internal 欄位 ConfigGui.Open。
    private static void ToggleSplatoonSettings()
    {
        var gui = Splatoon.Splatoon.P.GetFoP("ConfigGui");
        gui.SetFoP("Open", !(bool)gui.GetFoP("Open"));
    }

    public override void OnDisable()
    {
        Entry = Svc.DtrBar.Get("Splatoon.FpsSwitcher", "");
        Entry.Remove();
    }
}
