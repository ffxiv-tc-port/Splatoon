using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.Reflection;
using NightmareUI;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.Gui;
public class LogWindow : Window
{
    internal CircularBuffer<InternalLogMessage> FilteredLog = new(1000);
    public LogWindow() : base("Splatoon Log")
    {
    }

    public void Log(string s)
    {
        FilteredLog.PushBack(new(s, LogEventLevel.Information));
    }

    public override void Draw()
    {
        NuiTools.ButtonTabs([[new("標準記錄", InternalLog.PrintImgui), new("篩選記錄", PrintFiltered)]]);
    }

    private bool Autoscroll = true;
    private string Search = "";

    private void PrintFiltered()
    {
        ImGui.Checkbox("##Autoscroll", ref Autoscroll);
        ImGuiEx.Tooltip("自動捲動");
        ImGui.SameLine();
        if(ImGui.Button("全部複製"))
        {
#pragma warning disable
            GenericHelpers.Copy(FilteredLog.Select(x => $"[{x.Level}@{x.Time}] {x.Message}").Join("\n"));
#pragma warning restore
        }
        ImGui.SameLine();
        if(ImGui.Button("清除"))
        {
            FilteredLog.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##Filter", "篩選...", ref Search, 100);

        ImGui.BeginChild($"Plugin_log{DalamudReflector.GetPluginName()}");
        foreach(var x in FilteredLog)
        {
            if(Search == String.Empty || x.Level.ToString().EqualsIgnoreCase(Search) || x.Message.Contains(Search, StringComparison.OrdinalIgnoreCase))
                ImGuiEx.TextWrappedCopy(x.Level == LogEventLevel.Fatal ? ImGuiColors.DPSRed
                    : x.Level == LogEventLevel.Error ? ImGuiColors.DalamudRed
                    : x.Level == LogEventLevel.Warning ? ImGuiColors.DalamudOrange
                    : x.Level == LogEventLevel.Information ? ImGuiColors.DalamudWhite
                    : x.Level == LogEventLevel.Debug ? ImGuiColors.DalamudGrey
                    : x.Level == LogEventLevel.Verbose ? ImGuiColors.DalamudGrey2
                    : ImGuiColors.DalamudWhite2, $"> [{x.Time}] {x.Message}");
        }
        if(Autoscroll)
        {
            ImGui.SetScrollHereY();
        }
        ImGui.EndChild();
    }
}
