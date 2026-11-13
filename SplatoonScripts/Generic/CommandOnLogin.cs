using ECommons.DalamudServices;
using ECommons.Events;
using ECommons.Logging;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Generic;
public unsafe sealed class CommandOnLogin : SplatoonScript
{
    public override Metadata Metadata { get; } = new(2);
    public override HashSet<uint>? ValidTerritories { get; } = null;

    public override void OnEnable()
    {
        ProperOnLogin.RegisterInteractable(Action);
    }

    // 不用聊天指令送 /li（空參數＝傳送回本世界）；改走 Lifestream IPC，它不在或 IPC 失敗就不送。
    static void Action()
    {
        // 登入時才跑一次 ⇒ 即時讀 IsLoaded。
        if(!Svc.PluginInterface.InstalledPlugins.Any(p => p.InternalName == "Lifestream" && p.IsLoaded))
        {
            PluginLog.Information("[CommandOnLogin] Lifestream 未載入，不前往公會工坊");
            return;
        }
        try
        {
            // 簽章逐字對應 Lifestream 的 void ExecuteCommand(string)。
            Svc.PluginInterface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction("workshop");
        }
        catch(Exception e)
        {
            PluginLog.Information($"[CommandOnLogin] Lifestream.ExecuteCommand(workshop) 失敗（{e.GetType().Name}）：{e.Message}");
        }
    }

    public override void OnDisable()
    {
        ProperOnLogin.Unregister(Action);
    }
}