using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.ChatMethods;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.LanguageHelpers;
using ECommons.PartyFunctions;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.Immutable;

namespace Splatoon.Gui.Scripting;
#nullable enable
public class ScriptUpdateWindow : Window
{
    public ScriptUpdateWindow() : base("Splatoon Scripting - load results", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse)
    {
        this.SetSizeConstraints(new(500, 100), new(float.MaxValue, float.MaxValue));
        RespectCloseHotkey = false;
    }

    private volatile ImmutableList<SplatoonScript> UpdatedScripts = [];
    private volatile ImmutableList<string> FailedScripts = [];

    public void UpdatedScripts_Add(SplatoonScript s) => UpdatedScripts = UpdatedScripts.Add(s);
    public void UpdatedScripts_RemoveAll(Predicate<SplatoonScript> predicate) => UpdatedScripts = UpdatedScripts.RemoveAll(predicate);
    public int UpdatedScripts_Count() => UpdatedScripts.Count;

    public void FailedScripts_Add(string s) => FailedScripts = FailedScripts.Add(s);
    public void FailedScripts_Remove(string s) => FailedScripts = FailedScripts.Remove(s);
    public int FailedScripts_Count() => FailedScripts.Count;

    public override void Draw()
    {
        var i = 0;
        if(UpdatedScripts.Count > 0)
        {
            ImGuiEx.TextWrapped($"以下腳本已更新。請確認您的設定仍然完整，若有需要請重新設定。");
            if(ImGui.BeginTable("##table1", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("1", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("2");

                foreach(var x in UpdatedScripts)
                {
                    ImGui.PushID($"id{i++}");
                    try
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGuiEx.TextWrapped($"{x.InternalData?.FullName} - v{x.Metadata?.Version ?? 0}");
                        var change = x.Changelog?.SafeSelect((int?)x.Metadata?.Version ?? 0);
                        if(change != null)
                        {
                            ImGui.Indent();
                            ImGuiEx.TextWrapped(ImGuiColors.DalamudGrey, change);
                            ImGui.Unindent();
                        }
                        ImGui.TableNextColumn();
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Cog))
                        {
                            P.ConfigGui.Open = true;
                            P.ConfigGui.TabRequest = "腳本".Loc();
                            Svc.Framework.RunOnTick(() =>
                            {
                                TabScripting.RequestOpen = x.InternalData.FullName;
                            }, delayTicks: 2);
                        }
                    }
                    catch(Exception e)
                    {
                        e.Log();
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }

        }
        if(FailedScripts.Count > 0)
        {
            ImGuiEx.TextWrapped($"以下腳本載入失敗。");
            if(ImGui.BeginTable("##table1", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("1", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("2");

                var rep = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "Scripts");
                foreach(var x in FailedScripts)
                {
                    ImGui.PushID($"failid{i++}");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGuiEx.TextWrapped($"{x?.Replace(rep, "..") ?? "未知"}");
                    ImGui.TableNextColumn();
                    if(ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                    {
                        new TickScheduler(() =>
                        {
                            FailedScripts.Remove(x);
                            GenericHelpers.DeleteFileToRecycleBin(x);
                        });
                    }
                    ImGuiEx.Tooltip("刪除此腳本");
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
    }

    public void Open()
    {
        IsOpen = true;
    }

    public void Reset()
    {
        UpdatedScripts = [];
        FailedScripts = [];
    }
}
