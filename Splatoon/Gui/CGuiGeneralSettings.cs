using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.LanguageHelpers;
using ECommons.Reflection;
using NightmareUI;
using NightmareUI.PrimaryUI;
using Splatoon.Gui.Priority;
using Splatoon.Modules;
using Splatoon.Serializables;
using Splatoon.SplatoonScripting.Priority;
using Splatoon.Utility;
using Localization = ECommons.LanguageHelpers.Localization;

namespace Splatoon;

internal partial class CGui
{
    private void DisplayGeneralSettings()
    {
        ImGuiEx.Text("遊戲版本: ".Loc());
        ImGui.SameLine(0, 0);
        ImGuiEx.TextCopy(p.loader.gVersion);
        new NuiBuilder().Section("Logging and Web API", collapsible: false)
            .Widget(() =>
            {
                ImGuiUtils.SizedText("使用 Web API".Loc(), WidthLayout);
                ImGui.SameLine();
                if(ImGui.Checkbox("##usewebapi", ref p.Config.UseHttpServer))
                {
                    p.SetupShutdownHttp(p.Config.UseHttpServer);
                }
                ImGui.SameLine();
                if(p.Config.UseHttpServer)
                {
                    ImGuiEx.Text("http://127.0.0.1:" + p.Config.port + "/");
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        if(ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left) == Vector2.Zero)
                        {
                            Utils.ProcessStart("http://127.0.0.1:" + p.Config.port + "/");
                        }
                    }
                }
                else
                {
                    ImGuiEx.Text("連接埠: ".Loc());
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100f);
                    ImGui.DragInt("##webapiport", ref p.Config.port, float.Epsilon, 1, 65535);
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("請僅在有充分理由時才變更此設定".Loc());
                    }
                    if(p.Config.port < 1 || p.Config.port > 65535) p.Config.port = 47774;
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100f);
                    if(ImGui.Button("預設".Loc()))
                    {
                        p.Config.port = 47774;
                    }
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(250f);
                if(ImGui.Button("開啟 Web API 指南".Loc()))
                {
                    Utils.ProcessStart("https://github.com/PunishXIV/Splatoon#web-api-beta");
                }

                if(ImGui.Checkbox("啟用記錄".Loc(), ref P.Config.Logging))
                {
                    Logger.OnTerritoryChanged();
                }
                ImGuiComponents.HelpMarker("啟用記錄功能，會將聊天訊息、施法與 VFX 資訊寫入記錄檔。".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("記錄位置".Loc(), ref P.Config.LogPosition);
                ImGuiComponents.HelpMarker("在施法資訊記錄行中記錄物件位置".Loc());
            })

            .Section("Language", collapsible: false)
            .Widget(() =>
            {
                ImGuiEx.TextV("Splatoon 語言: ".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f.Scale());
                if(ImGui.BeginCombo("##langsel", P.Config.PluginLanguage == null ? "遊戲語言".Loc() : P.Config.PluginLanguage.Loc()))
                {
                    if(ImGui.Selectable("遊戲語言".Loc()))
                    {
                        P.Config.PluginLanguage = null;
                        Localization.Init(GameLanguageString);
                    }
                    foreach(var x in GetAvaliableLanguages())
                    {
                        if(ImGui.Selectable(x.Loc()))
                        {
                            P.Config.PluginLanguage = x;
                            Localization.Init(P.Config.PluginLanguage);
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.Checkbox("本地化記錄".Loc(), ref Localization.Logging);
                ImGui.SameLine();
                if(ImGui.Button("Save entries: ??".Loc(P.Config.PluginLanguage ?? GameLanguageString)))
                {
                    Localization.Save(P.Config.PluginLanguage ?? GameLanguageString);
                }
                ImGui.SameLine();
                if(ImGui.Button("重新掃描語言檔案".Loc()))
                {
                    GetAvaliableLanguages(true);
                }
            })

            .Section("UI settings", collapsible: false)
            .Widget(() =>
            {
                ImGui.Checkbox("使用十六進位數字".Loc(), ref p.Config.Hexadecimal);
                ImGui.Checkbox("在 Splatoon 尋找指令中啟用繩鏈".Loc(), ref p.Config.TetherOnFind);
                ImGui.Checkbox("在遊戲 UI 隱藏時強制顯示 Splatoon UI".Loc(), ref p.Config.ShowOnUiHide);
            })

            .Section("Scripts configuration and priority lists", collapsible: false)
            .Widget(() =>
            {
                ImGui.Checkbox("停用腳本快取".Loc(), ref p.Config.DisableScriptCache);
                var state = DalamudReflector.GetDtrEntryState(InfoBar.EntryName);
                if(ImGui.Checkbox("啟用資訊列優先度指示器", ref state))
                {
                    DalamudReflector.SetDtrEntryState(InfoBar.EntryName, state);
                }
                ImGui.SetNextItemWidth(150f);
                ImGuiEx.EnumCombo("Priority assignment auto-loading notification", ref P.Config.ScriptPriorityNotification);
                ImGuiEx.TreeNodeCollapsingHeader("Preferred Role Assignments", () =>
                {
                    ImGuiEx.Text($"Select role assignments that you would like to assigned to yourself via autofill function");
                    foreach(var j in Enum.GetValues<Job>().Where(x => x > 0 && !x.IsUpgradeable() && x.IsCombat()).OrderBy(x => P.PriorityPopupWindow.GetOrderedRoleIndex(x)))
                    {
                        var pref = P.Config.PreferredPositions.SafeSelect(j);
                        var name = PriorityPopupWindow.ConfiguredNames.SafeSelect(pref) ?? "No preferred position";
                        ImGui.PushID(j.ToString());
                        ImGui.SetNextItemWidth(150f);
                        if(ImGui.BeginCombo("##jselect", name, ImGuiComboFlags.HeightLarge))
                        {
                            foreach(var x in Enum.GetValues<RolePosition>())
                            {
                                if(ImGui.Selectable(PriorityPopupWindow.ConfiguredNames.SafeSelect(x) ?? "No preferred position", pref == x))
                                {
                                    P.Config.PreferredPositions[j] = x;
                                }
                            }
                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        if(ThreadLoadImageHandler.TryGetIconTextureWrap(j.GetIcon(), true, out var tex))
                        {
                            ImGui.Image(tex.ImGuiHandle, new Vector2(ImGui.GetFrameHeight()));
                            ImGui.SameLine();
                        }
                        ImGuiEx.TextV(j.ToString());
                        ImGui.PopID();
                    }
                });
                ImGuiEx.TreeNodeCollapsingHeader("Edit saved priority lists", () =>
                {
                    Dictionary<uint, List<RolePlayerAssignment>> dict = [];
                    foreach(var x in P.Config.RolePlayerAssignments)
                    {
                        if(!dict.TryGetValue(x.Territory, out var list))
                        {
                            list = [];
                            dict[x.Territory] = list;
                        }
                        list.Add(x);
                    }
                    foreach(var x in dict)
                    {
                        ImGui.PushID(x.Key.ToString());
                        ImGuiEx.TreeNodeCollapsingHeader($"{ExcelTerritoryHelper.GetName(x.Key)} - {x.Value.Count} assignments###edit{x.Key}", () =>
                        {
                            if(ImGui.BeginTable($"PrioTable{x.Key}", 2, ImGuiEx.DefaultTableFlags))
                            {
                                ImGui.TableSetupColumn("1", ImGuiTableColumnFlags.WidthStretch);
                                ImGui.TableSetupColumn("2");

                                foreach(var a in x.Value)
                                {
                                    ImGui.PushID(a.ToString());
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    var lst = a.Players.Select(p => $"{PriorityPopupWindow.ConfiguredNames.SafeSelect(PriorityPopupWindow.RolePositions.SafeSelect(a.Players.IndexOf(p)))}: {p.Name}{(p.Jobs.Count > 0 ? $" - {p.Jobs.Print()}" : "")}");
                                    ImGuiEx.TextV($"{lst.Print()}");
                                    ImGuiEx.Tooltip(lst.Print("\n"));
                                    ImGui.TableNextColumn();
                                    if(ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                                    {
                                        new TickScheduler(() => P.Config.RolePlayerAssignments.Remove(a));
                                    }
                                    ImGui.PopID();
                                }

                                ImGui.EndTable();
                            }
                        });
                        ImGui.PopID();
                    }

                });
            })

            .Section("Miscellaneous", collapsible: false)
            .Widget(() =>
            {
                if(ImGui.Button("開啟備份目錄".Loc()))
                {
                    Utils.ProcessStart(Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "Backups"));
                }
                ImGui.Separator();
                ImGuiEx.Text("聯絡開發者:".Loc());
                ImGui.SameLine();
                if(ImGui.Button("Github".Loc()))
                {
                    Utils.ProcessStart("https://github.com/PunishXIV/Splatoon/issues");
                }
                ImGui.SameLine();
                if(ImGui.Button("Discord".Loc()))
                {
                    ImGui.SetClipboardText(Splatoon.DiscordURL);
                    Svc.Chat.Print("[Splatoon] 伺服器邀請連結: ".Loc() + Splatoon.DiscordURL);
                    Utils.ProcessStart(Splatoon.DiscordURL);
                }
                ImGui.Checkbox("停用直播提示（僅重啟後生效）".Loc(), ref P.Config.NoStreamWarning);
            })

            .Section("Script auto-reloading (for developers)", collapsible: true)
            .TextWrapped("Add pathes to folders that contain scripts that you are editing. Do NOT add Splatoon's own configuration folder here.")
            .Widget(() =>
            {
                for(var i = 0; i < P.Config.FileWatcherPathes.Count; i++)
                {
                    var index = i;
                    var f = P.Config.FileWatcherPathes[i];
                    ImGuiEx.InputWithRightButtonsArea(() =>
                    {
                        if(ImGui.InputTextWithHint("##path to folder" + index, "Path to folder...", ref f, 2000))
                        {
                            P.Config.FileWatcherPathes[index] = f;
                        }
                    }, () =>
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Trash, "Trash" + index))
                        {
                            new TickScheduler(() => P.Config.FileWatcherPathes.RemoveAt(index));
                        }
                    });
                }
                ImGuiEx.LineCentered(() =>
                {
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "Add New"))
                    {
                        P.Config.FileWatcherPathes.Add("");
                    }
                    ImGui.SameLine();
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "Apply settings"))
                    {
                        S.ScriptFileWatcher.StartWatching();
                    }
                });
            }).Draw();


        Svc.PluginInterface.UiBuilder.DisableUserUiHide = p.Config.ShowOnUiHide;
    }
}
