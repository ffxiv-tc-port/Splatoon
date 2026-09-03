#nullable enable
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.LanguageHelpers;
using Splatoon.SplatoonScripting;

namespace Splatoon.Gui.Scripting;

/// <summary>
/// 「可用腳本」瀏覽器:把 update.csv 的內容列出來讓使用者自己挑著裝。
///
/// 🔴 這裡不會自動安裝任何東西 —— 每一次下載都必須由使用者按下該列的按鈕。
/// 🔴 安裝一律走既有的 <see cref="ScriptingProcessor.DownloadScript(string, bool)"/>
///    (它自己會 Task.Run 到背景),而且一律先過 <see cref="ScriptingProcessor.IsUrlTrusted(string)"/>。
/// 🔴 繪製路徑上沒有任何網路或磁碟 I/O,只讀 <see cref="ScriptCatalog"/> 已經算好的欄位。
/// </summary>
internal static class TabScriptBrowser
{
    /// <summary>
    /// 刻意與已安裝清單的 <see cref="TabScripting.Search"/> 分開。
    /// 兩個分頁列的是不同的東西,共用同一個過濾字串會讓使用者切過來時看到
    /// 一份被上一個分頁的搜尋詞篩掉的清單 —— 那和「這裡沒有腳本」長得一樣。
    /// 語法與已安裝清單相同:逗號分隔,任一命中即顯示。
    /// </summary>
    internal static string Search = "";

    internal static bool HideInstalled = false;

    internal static void Draw()
    {
        // 只在「從沒載過」時起一次背景下載。失敗之後不自動重試,由使用者按重新整理,
        // 免得畫面每一幀都往網路上打一次。
        ScriptCatalog.EnsureRequested();

        var loading = ScriptCatalog.State == ScriptCatalog.CatalogState.Loading;
        if(loading) ImGui.BeginDisabled();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh list".Loc()))
        {
            ScriptCatalog.RequestRefresh();
        }
        if(loading) ImGui.EndDisabled();
        ImGuiEx.Tooltip("Re-downloads the list of available scripts. This only downloads the list itself; nothing is installed until you press an install button.".Loc());

        ImGui.SameLine();
        ImGui.Checkbox("Hide installed".Loc(), ref HideInstalled);

        // 🔴 這一段是「還沒載到」與「真的沒有」的分界線。
        // 兩種情況都會產生零列,所以絕不能只畫一張空表格就算了 —— 每一種狀態
        // 都要在列上寫清楚自己是哪一種。
        switch(ScriptCatalog.State)
        {
            case ScriptCatalog.CatalogState.NotLoaded:
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "The script list has not been downloaded yet.".Loc());
                return;

            case ScriptCatalog.CatalogState.Loading:
                ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudWhite, ImGuiColors.ParsedPink), "Downloading the list of available scripts...".Loc());
                return;

            case ScriptCatalog.CatalogState.Failed:
                ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, "The list of available scripts could not be downloaded, so nothing can be shown here. This is not the same as there being no scripts.".Loc());
                ImGuiEx.Text(ImGuiColors.DalamudGrey3, $"{ScriptCatalog.LastError}");
                return;
        }

        var entries = ScriptCatalog.Entries;
        if(entries.Count == 0)
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "The script list was downloaded successfully, but it does not contain a single usable entry.".Loc());
            if(ScriptCatalog.SkippedLines > 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey3, "?? line(s) were ignored because they are not in the expected format.".Loc(ScriptCatalog.SkippedLines));
            }
            return;
        }

        // 每幀建一次索引,避免對每一列做線性搜尋。
        // 這只是把「目前已載入的腳本」對照過來,不持有任何跨幀狀態。
        var installed = new Dictionary<string, SplatoonScript>();
        foreach(var s in ScriptingProcessor.Scripts)
        {
            var fn = s.InternalData?.FullName;
            if(fn != null) installed[fn] = s;
        }

        var installedCount = 0;
        var updateCount = 0;
        foreach(var e in entries)
        {
            if(installed.TryGetValue(e.FullName, out var s))
            {
                installedCount++;
                if(s.Metadata != null && s.Metadata.Version < e.Version) updateCount++;
            }
        }

        ImGuiEx.Text(ImGuiColors.DalamudGrey, "?? available, ?? installed, ?? with updates.".Loc(entries.Count, installedCount, updateCount));
        var updated = ScriptCatalog.LastUpdated;
        if(updated != null)
        {
            ImGuiEx.Tooltip("List last downloaded at ??".Loc(updated.Value.ToString("yyyy-MM-dd HH:mm:ss")));
        }
        if(ScriptCatalog.SkippedLines > 0)
        {
            ImGui.SameLine();
            ImGuiEx.Text(ImGuiColors.DalamudYellow, "!");
            ImGuiEx.Tooltip("?? line(s) were ignored because they are not in the expected format.".Loc(ScriptCatalog.SkippedLines));
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.05f);
        ImGui.InputTextWithHint("##browserSearch", "Search...".Loc(), ref Search, 100);

        var searchSplit = Search.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        bool Matches(ScriptCatalog.CatalogEntry e)
        {
            if(HideInstalled && installed.ContainsKey(e.FullName)) return false;
            if(searchSplit.Length == 0) return true;
            return e.Name.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplit)
                || e.Namespace.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplit);
        }

        var visible = entries.Where(Matches).ToList();
        if(visible.Count == 0)
        {
            ImGuiEx.Text(ImGuiColors.DalamudGrey3, "No entry in the downloaded list matches your current filter.".Loc());
            return;
        }

        // 用命名空間分組,顯示方式與已安裝清單一致(底線變空白、點變破折號)。
        // 🔴 標題字串就是 TreeNodeCollapsingHeader 的 ImGui ID,所以刻意不把會變動的
        //    數字(例如已安裝數)寫進去 —— 那會讓摺疊狀態每次數字一變就被重設。
        foreach(var nsp in visible.Select(x => x.Namespace).Distinct().Order())
        {
            ImGuiEx.TreeNodeCollapsingHeader(nsp.Replace("_", " ").Replace(".", " - "), () =>
            {
                ImGui.PushID($"browse{nsp}");
                DrawGroup(visible.Where(x => x.Namespace == nsp).OrderBy(x => x.Name), installed);
                ImGui.PopID();
            });
        }
    }

    private static void DrawGroup(IEnumerable<ScriptCatalog.CatalogEntry> group, Dictionary<string, SplatoonScript> installed)
    {
        if(!ImGui.BeginTable("##browserTable", 3, ImGuiTableFlags.BordersInner | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit)) return;
        ImGui.TableSetupColumn("Name".Loc(), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("State".Loc());
        ImGui.TableSetupColumn("##action");
        ImGui.TableHeadersRow();

        foreach(var entry in group)
        {
            ImGui.PushID(entry.FullName);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGuiEx.TextV($"{entry.Name.Replace("_", " ")}");
            ImGuiEx.Tooltip($"{entry.FullName}\n{entry.Url}");
            if(ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText($"{entry.FullName}");
                Notify.Success("Copied to clipboard");
            }
            ImGui.SameLine();
            ImGuiEx.Text(ImGuiColors.DalamudGrey2, $"v{entry.Version}");
            if(!entry.IsOfficial)
            {
                // 🔴 使用者自行加入的清單一定要在列上看得見來源,不能只藏在 tooltip 裡。
                ImGui.SameLine();
                ImGuiEx.Text(ImGuiColors.DalamudOrange, "[custom list]".Loc());
                ImGuiEx.Tooltip("This entry does not come from the official script repository. It comes from an update list you added yourself:\n??".Loc(entry.SourceUrl ?? ""));
            }

            ImGui.TableNextColumn();

            var trusted = ScriptingProcessor.IsUrlTrusted(entry.Url);
            installed.TryGetValue(entry.FullName, out var script);
            // 「版本不明」= 腳本裝了但沒有宣告 Metadata。自動更新在這種情況會把它
            // 當成版本 0,但這裡不能替使用者假裝知道 —— 未知就寫未知。
            var versionKnown = script?.Metadata != null;
            var localVersion = script?.Metadata?.Version ?? 0;

            if(!trusted)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudRed, "Untrusted source".Loc());
                ImGuiComponents.HelpMarker("The download address of this entry is not on the trusted list, so it can not be installed from here. If you trust it, add its address under Tools - Trusted Repos first.".Loc());
            }
            else if(script == null)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "Not installed".Loc());
                ImGuiComponents.HelpMarker("This script is not installed. Nothing will be downloaded until you press the install button.".Loc());
            }
            else if(!versionKnown)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudYellow, "Installed, version ?".Loc());
                ImGuiComponents.HelpMarker("This script is installed but does not report a version, so it can not be compared with the list. Reinstalling it is the only way to be sure it is current.".Loc());
            }
            else if(localVersion < entry.Version)
            {
                ImGuiEx.TextV(ImGuiColors.ParsedGold, "Update available".Loc());
                ImGuiComponents.HelpMarker("Installed version ?? is older than listed version ??.".Loc(localVersion, entry.Version));
            }
            else
            {
                ImGuiEx.TextV(ImGuiColors.ParsedGreen, "Installed".Loc());
                ImGuiComponents.HelpMarker("Installed version ?? is up to date.".Loc(localVersion));
            }

            ImGui.TableNextColumn();

            if(!trusted)
            {
                // 保持欄寬一致,但不給任何可以按下去的東西。
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.3f);
                ImGui.BeginDisabled();
                ImGuiEx.IconButton(FontAwesomeIcon.Ban);
                ImGui.EndDisabled();
                ImGui.PopStyleVar();
            }
            else
            {
                // 版本不明時刻意不說成「更新」—— 我們並不知道它是不是舊的,
                // 說「重新安裝」才是這個按鈕真正會做的事。
                var isUpdate = script != null && versionKnown && localVersion < entry.Version;
                var icon = script == null ? FontAwesomeIcon.Download : FontAwesomeIcon.Sync;
                var upToDate = script != null && versionKnown && localVersion >= entry.Version;
                if(upToDate) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                if(ImGuiEx.IconButton(icon))
                {
                    // 🔴 唯一的安裝入口:使用者親手按下去。走既有的下載路徑,
                    //    它自己會 Task.Run 到背景,不會卡住繪製。
                    //    isFirst: false 與「從剪貼簿安裝」一致,不觸發更新彈窗。
                    ScriptingProcessor.DownloadScript(entry.Url, false);
                }
                if(upToDate) ImGui.PopStyleVar();
                ImGuiEx.Tooltip(script == null
                    ? "Install this script".Loc()
                    : (isUpdate ? "Update this script".Loc() : "Reinstall this script".Loc()));
            }

            ImGui.PopID();
        }
        ImGui.EndTable();
    }
}
