using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.LanguageHelpers;
using Splatoon.SplatoonScripting;

namespace Splatoon.Gui.Scripting;

internal static class TabScripting
{
    internal static volatile bool ForceUpdate = false;
    internal static string Search = "";
    internal static string RequestOpen = null;
    internal static void Draw()
    {
        if(ImGui.IsWindowAppearing()) RequestOpen = null;
        if(ScriptingProcessor.ThreadIsRunning)
        {
            ImGuiEx.LineCentered("ThreadCompilerRunning", delegate
            {
                ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudWhite, ImGuiColors.ParsedPink), "腳本安裝中，請稍候...".Loc());
            });
        }
        else
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudOrange, "請注意，腳本可直接且不受限制地存取您的電腦與遊戲。請確認您了解您正在安裝的內容。".Loc());
        }
        var force = ForceUpdate;
        if(ImGui.Checkbox($"強制更新".Loc(), ref force)) ForceUpdate = force;
        ImGuiEx.Tooltip("Enable this checkbox and click \"Reload and Update\" button to forcibly redownload all scripts, even should they have no updates, that were installed from the Internet.");
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Undo, "重新載入並更新".Loc()))
        {
            var dir = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "ScriptCache");
            foreach(var x in Directory.GetFiles(dir))
            {
                if(x.EndsWith(".bin"))
                {
                    PluginLog.Information($"Deleting {x}");
                    File.Delete(x);
                }
            }
            ScriptingProcessor.ReloadAll();
        }
        ImGuiEx.Tooltip("Clears cache, recompiles and reloads all scripts and checks them for updates immediately.");
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "從剪貼簿安裝".Loc()))
        {
            var text = ImGui.GetClipboardText();
            if(ScriptingProcessor.IsUrlTrusted(text))
            {
                ScriptingProcessor.DownloadScript(text, false);
            }
            else
            {
                ScriptingProcessor.CompileAndLoad(text, null, false);
            }
        }
        ImGuiEx.Tooltip("從剪貼簿安裝腳本。您的剪貼簿內容應為腳本原始碼，或指向可信任網址的連結（來自 Splatoon 倉庫的腳本）");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.05f);
        ImGui.InputTextWithHint("##search", "搜尋...", ref Search, 50);
        ImGui.SameLine();
        ImGuiEx.SetNextItemFullWidth();
        if(ImGui.BeginCombo("##switch", "將腳本切換至此設定檔"))
        {
            var confs = new HashSet<string>();
            var toReload = new HashSet<SplatoonScript>();
            foreach(var s in ScriptingProcessor.Scripts)
            {
                if(s.IsDisabledByUser) continue;
                if(s.TryGetAvailableConfigurations(out var confList))
                {
                    foreach(var c in confList)
                    {
                        confs.Add(c.Value);
                    }
                }
            }
            if(ImGui.Selectable("預設設定檔"))
            {
                foreach(var s in ScriptingProcessor.Scripts)
                {
                    if(s.InternalData.CurrentConfigurationKey != "")
                    {
                        s.ApplyDefaultConfiguration(out var act);
                        if(act != null) toReload.Add(s);
                    }
                }
            }
            var i = 0;
            foreach(var confName in confs.Order())
            {
                var doReload = false;
                if(ImGui.Selectable($"{confName}"))
                {
                    doReload = true;
                }
                var sb = new StringBuilder("The following scripts will be switched:\n");
                foreach(var s in ScriptingProcessor.Scripts)
                {
                    if(s.IsDisabledByUser) continue;
                    if(P.Config.DefaultScriptConfigurationNames.TryGetValue(s.InternalData.FullName, out var defConName) && defConName == confName)
                    {
                        if(s.InternalData.CurrentConfigurationKey != "")
                        {
                            if(doReload)
                            {
                                s.ApplyDefaultConfiguration(out var act);
                                if(act != null) toReload.Add(s);
                            }
                            sb.Append(s.InternalData.FullName.Replace(".", " - "));
                            sb.Append('\n');
                        }
                    }
                    else if(s.TryGetAvailableConfigurations(out var confList) && confList.FindKeysByValue(confName).TryGetFirst(out var confKey) && s.InternalData.CurrentConfigurationKey != confKey)
                    {
                        if(doReload)
                        {
                            s.ApplyConfiguration(confKey, out var act);
                            if(act != null) toReload.Add(s);
                        }
                        sb.Append(s.InternalData.FullName.Replace(".", " - "));
                        sb.Append('\n');
                    }
                }
                ImGuiEx.Tooltip(sb.ToString());
            }
            if(toReload.Count > 0)
            {
                ScriptingProcessor.ReloadScripts(toReload, false);
            }
            ImGui.EndCombo();
        }
        ImGuiEx.Tooltip("Disabled scripts won't be switched.");

        var openConfig = ScriptingProcessor.Scripts.FirstOrDefault(x => x.InternalData.ConfigOpen);

        if(openConfig != null)
        {
            RequestOpen = null;
            DrawScriptGroup([openConfig]);
        }
        else
        {
            if(RequestOpen != null)
            {
                var candidate = ScriptingProcessor.Scripts.FirstOrDefault(x => x.InternalData?.FullName == RequestOpen);
                if(candidate != null)
                {
                    candidate.InternalData.ConfigOpen = true;
                    RequestOpen = null;
                }
            }
            if(Search != "")
            {
                DrawScriptGroup(ScriptingProcessor.Scripts);
            }
            var namespaces = ScriptingProcessor.Scripts.Select(x => x.InternalData.Namespace).Distinct().Order();
            foreach(var nsp in namespaces)
            {
                ImGuiEx.TreeNodeCollapsingHeader(nsp.Replace("_", " ").Replace(".", " - "), () =>
                {
                    ImGui.PushID(nsp);
                    DrawScriptGroup(ScriptingProcessor.Scripts.Where(x => x.InternalData.Namespace == nsp).OrderBy(x => x.InternalData.Name));
                    ImGui.PopID();
                });
            }
        }

        void DrawScriptGroup(IEnumerable<SplatoonScript> scripts)
        {
            if(ImGui.BeginTable("##scriptsTable", 7, ImGuiTableFlags.BordersInner | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Configuration", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("State");
                ImGui.TableSetupColumn("##c1");
                ImGui.TableSetupColumn("##c2");
                ImGui.TableSetupColumn("##c3");
                ImGui.TableSetupColumn("##c4");
                ImGui.TableHeadersRow();
                foreach(var script in scripts)
                {
                    var searchSplot = Search.Split(",", StringSplitOptions.TrimEntries);
                    if(!(Search == "" || script.InternalData.Name.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplot) || script.InternalData.Namespace.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplot))) continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.PushID(script.InternalData.GUID);
                    ImGuiEx.TextV($"{script.InternalData.Name.Replace("_", " ")}");
                    if(script.Metadata?.Description == null)
                    {
                        ImGuiEx.Tooltip($"{script.InternalData.Namespace}");
                    }
                    else
                    {
                        ImGuiEx.Tooltip($"{script.InternalData.Namespace}\n{script.Metadata.Description}");
                    }
                    if(ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        ImGui.SetClipboardText($"{script.InternalData.FullName}");
                        Notify.Success("Copied to clipboard");
                    }
                    if(script.Metadata?.Version != null)
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text(ImGuiColors.DalamudGrey2, $"v{script.Metadata.Version}");
                    }
                    if(script.Metadata?.Author != null)
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text(ImGuiColors.DalamudGrey2, $"by {script.Metadata.Author}");
                    }

                    ImGui.TableNextColumn();

                    script.DrawConfigurationSelector();

                    ImGui.TableNextColumn();

                    if(script.InternalData.Blacklisted)
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "已列入黑名單".Loc());
                        ImGuiComponents.HelpMarker("此腳本因相容性問題已被列入黑名單。請等待其新版本發布。".Loc());
                    }
                    else if(!script.InternalData.Allowed)
                    {
                        ImGuiEx.TextV(ImGuiColors.ParsedGold, "準備中".Loc());
                        ImGuiComponents.HelpMarker("此腳本正準備啟用中，即將可用。".Loc());
                    }
                    else if(script.IsDisabledByUser)
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudRed, "已停用".Loc());
                        ImGuiComponents.HelpMarker("此腳本已被您停用。".Loc());
                    }
                    else if(script.IsEnabled)
                    {
                        ImGuiEx.TextV(ImGuiColors.ParsedGreen, "啟用中".Loc());
                        ImGuiComponents.HelpMarker("此腳本目前為啟用中且正在執行。".Loc());
                    }
                    else
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudYellow, "非活動中".Loc());
                        ImGuiComponents.HelpMarker("此腳本目前為非活動狀態，因為您不在此腳本所設計的區域中。".Loc());
                    }
                    ImGui.TableNextColumn();

                    if(!script.InternalData.Allowed || script.InternalData.Blacklisted)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Play))
                        {
                            script.InternalData.Allowed = true;
                            script.InternalData.Blacklisted = false;
                            script.UpdateState();
                        }
                        ImGuiEx.Tooltip("Forcefully allow this script to be enabled. Consequences of this action will be unpredictable.");
                    }
                    else
                    {

                        var e = P.Config.DisabledScripts.Contains(script.InternalData.FullName);
                        if(ImGuiEx.IconButton(e ? FontAwesomeIcon.PlayCircle : FontAwesomeIcon.PauseCircle))
                        {
                            if(e)
                            {
                                P.Config.DisabledScripts.Remove(script.InternalData.FullName);
                            }
                            else
                            {
                                P.Config.DisabledScripts.Add(script.InternalData.FullName);
                            }
                            ScriptingProcessor.Scripts.Each(x => x.UpdateState());
                        }
                        ImGuiEx.Tooltip(e ? "啟用腳本".Loc() : "停用腳本".Loc());
                    }

                    ImGui.TableNextColumn();

                    if(script.InternalData.SettingsPresent)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Cog))
                        {
                            if(script.InternalData.ConfigOpen)
                            {
                                openConfig.Controller.SaveConfig();
                            }
                            script.InternalData.ConfigOpen = !script.InternalData.ConfigOpen;
                        }
                        ImGuiEx.Tooltip("開啟腳本設定".Loc());
                    }
                    else if(script.Controller.GetRegisteredElements().Count > 0)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.PaintBrush))
                        {
                            if(script.InternalData.ConfigOpen)
                            {
                                openConfig.Controller.SaveConfig();
                            }
                            script.InternalData.ConfigOpen = !script.InternalData.ConfigOpen;
                        }
                        ImGuiEx.Tooltip("開啟元素編輯器".Loc());
                    }
                    else
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0f);
                        ImGuiEx.IconButton(FontAwesomeIcon.Cog);
                        ImGui.PopStyleVar();
                        //ImGuiEx.Tooltip("This script contains no settings");
                    }

                    ImGui.TableNextColumn();

                    if(ImGuiEx.IconButton("\uf0e2"))
                    {
                        var isOpen = script.InternalData.ConfigOpen;
                        ScriptingProcessor.ReloadScript(script, true);
                        if(isOpen)
                        {
                            RequestOpen = script.InternalData.FullName;
                        }
                    }
                    ImGuiEx.Tooltip("Reload this script");

                    ImGui.TableNextColumn();

                    if(ImGuiEx.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyCtrl)
                    {
                        if(!script.InternalData.Path.IsNullOrEmpty() && script.InternalData.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        {
                            new TickScheduler(() =>
                            {
                                script.Disable();
                                ScriptingProcessor.RemoveScript(script);
                                DeleteFileToRecycleBin(script.InternalData.Path);
                            });
                        }
                        else
                        {
                            Notify.Error("刪除時發生錯誤".Loc());
                        }
                    }
                    ImGuiEx.Tooltip("刪除腳本。按住 CTRL 並點擊".Loc());
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }

        if(openConfig != null)
        {
            ImGuiEx.LineCentered("ScriptConfigTitle", delegate
            {
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"{openConfig.InternalData.FullName} configuration");
            });
            ImGui.Separator();
            ImGuiEx.EzTabBar($"##scriptConfig",
                (openConfig.InternalData.SettingsPresent ? "設定" : null, () =>
                {
                    try
                    {
                        openConfig.OnSettingsDraw();
                    }
                    catch(Exception ex)
                    {
                        ex.Log();
                    }
                }, null, false),
                (openConfig.Controller.GetRegisteredElements().Count > 0 ? "已註冊元素" : null, openConfig.DrawRegisteredElements, null, false),
                ("已儲存設定檔", openConfig.DrawConfigurations, null, false)
                );

            ImGuiEx.LineCentered("ScriptConfig", delegate
            {
                if(ImGui.Button("關閉並儲存設定"))
                {
                    openConfig.InternalData.ConfigOpen = false;
                    openConfig.Controller.SaveConfig();
                }
            });
        }
    }
}
