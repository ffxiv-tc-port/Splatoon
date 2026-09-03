#nullable enable
using System.Collections.Immutable;
using System.Threading;

namespace Splatoon.SplatoonScripting;

/// <summary>
/// 「可用腳本」瀏覽器的資料來源:把 update.csv 解析成可瀏覽的清單並快取起來。
///
/// 🔴 這裡**只讀清單、不安裝任何東西**。安裝一律由使用者在 UI 上按下去,
///    走 <see cref="ScriptingProcessor.DownloadScript(string, bool)"/>。
/// 🔴 下載一律在背景執行緒(<see cref="RequestRefresh"/> 內的 Task),
///    UI 繪製路徑上永遠只讀已經算好的欄位。
/// </summary>
internal static class ScriptCatalog
{
    /// <summary>
    /// 🔴 這四個狀態必須在 UI 上分得出來。特別是 <see cref="NotLoaded"/>/<see cref="Loading"/>
    /// 與「清單真的是空的」——兩者都會畫出零列,如果只畫一張空表格,使用者無從判斷
    /// 是「還沒載到」還是「真的沒有腳本」。
    /// </summary>
    internal enum CatalogState
    {
        NotLoaded,
        Loading,
        Loaded,
        Failed,
    }

    /// <param name="FullName">形如 <c>SplatoonScriptsOfficial.Duties.Dawntrail.Xxx@Yyy</c>,與 <see cref="InternalData.FullName"/> 同格式。</param>
    /// <param name="Version">update.csv 第二欄。</param>
    /// <param name="Url">update.csv 第三欄,下載網址。</param>
    /// <param name="SourceUrl">來源清單。<c>null</c> = 本 fork 的官方清單;非 null = 使用者自行加入的 ExtraUpdateLinks 那條網址。</param>
    internal sealed record CatalogEntry(string FullName, int Version, string Url, string? SourceUrl)
    {
        internal bool IsOfficial => SourceUrl == null;

        /// <summary>FullName 的 '@' 之前的部分。沒有 '@' 時退回空字串,不擲例外。</summary>
        internal string Namespace
        {
            get
            {
                var idx = FullName.IndexOf('@');
                return idx < 0 ? "" : FullName[..idx];
            }
        }

        /// <summary>FullName 的 '@' 之後的部分。沒有 '@' 時就是整個 FullName。</summary>
        internal string Name
        {
            get
            {
                var idx = FullName.IndexOf('@');
                return idx < 0 ? FullName : FullName[(idx + 1)..];
            }
        }
    }

    private static volatile CatalogState StateInternal = CatalogState.NotLoaded;
    internal static CatalogState State => StateInternal;

    private static ImmutableList<CatalogEntry> EntriesInternal = ImmutableList<CatalogEntry>.Empty;
    internal static IReadOnlyList<CatalogEntry> Entries => EntriesInternal;

    /// <summary>最後一次失敗的訊息(成功之後清成 null)。UI 只在 Failed 狀態顯示它。</summary>
    internal static volatile string? LastError = null;

    /// <summary>
    /// 最後一次成功寫入清單的時間(本地時間)。從沒成功過就是 null。
    /// 🔴 存 ticks 而不是 DateTime?:寫入端是背景執行緒、讀取端是 UI 執行緒,
    ///    而 DateTime? 的賦值不是原子操作(會撕裂讀成不存在的時間)。
    /// </summary>
    private static long LastUpdatedTicks = 0;
    internal static DateTime? LastUpdated
    {
        get
        {
            var t = Interlocked.Read(ref LastUpdatedTicks);
            return t == 0 ? null : new DateTime(t);
        }
    }

    /// <summary>被丟掉的行數(格式不合)。0 以外的值代表來源清單有問題,值得顯示出來。</summary>
    internal static int SkippedLines = 0;

    private static readonly object Gate = new();
    private static bool RefreshRunning = false;

    /// <summary>
    /// 第一次畫瀏覽器時呼叫:清單從沒載過就在背景起一次下載。
    /// 已經在載、載過、或載失敗過都不做事(失敗要由使用者按「重新整理」重試,
    /// 免得畫面每幀都重打一次網路)。
    /// </summary>
    internal static void EnsureRequested()
    {
        if(StateInternal == CatalogState.NotLoaded) RequestRefresh();
    }

    /// <summary>
    /// 在背景重新下載並解析清單。重複呼叫時只會有一個下載在跑。
    /// 🔴 絕不從這裡安裝任何腳本。
    /// </summary>
    internal static void RequestRefresh()
    {
        lock(Gate)
        {
            if(RefreshRunning) return;
            RefreshRunning = true;
        }
        StateInternal = CatalogState.Loading;
        Task.Run(delegate
        {
            try
            {
                // 走 ScriptingProcessor 的共用下載,連同 ExtraUpdateLinks 的 Fatal 警告一起。
                var lists = ScriptingProcessor.DownloadUpdateLists();
                Parse(lists);
            }
            catch(Exception e)
            {
                e.Log();
                LastError = $"{e.GetType().Name}: {e.Message}";
                StateInternal = CatalogState.Failed;
            }
            finally
            {
                lock(Gate)
                {
                    RefreshRunning = false;
                }
            }
        });
    }

    /// <summary>
    /// 把「別人已經下載好的」清單餵進來(目前是 <see cref="ScriptingProcessor.BlockingBeginUpdate"/>),
    /// 免得為了同一份 update.csv 多打一次網路。
    /// 🔴 自己吞掉所有例外:這是搭便車的附加功能,絕不能打斷既有的腳本更新流程。
    /// </summary>
    internal static void Ingest(List<(string Source, string Content)> lists)
    {
        try
        {
            Parse(lists);
        }
        catch(Exception e)
        {
            e.Log();
        }
    }

    private static void Parse(List<(string Source, string Content)> lists)
    {
        var builder = ImmutableList.CreateBuilder<CatalogEntry>();
        // 同一個 FullName 可能同時出現在官方清單與使用者自訂清單裡。先到的贏,
        // 也就是官方清單優先 —— 自訂清單不能靜默地把官方腳本換成別的網址。
        var seen = new HashSet<string>();
        var skipped = 0;
        foreach(var (source, content) in lists)
        {
            if(content == null) continue;
            foreach(var line in content.Replace("\r", "").Split("\n"))
            {
                // 空行/純空白行不算「格式不合」—— 把它們算進 SkippedLines 會讓一份
                // 完全正常的清單顯示「有 N 行被忽略」,那是假警報。
                if(string.IsNullOrWhiteSpace(line)) continue;
                // 與 BlockingBeginUpdate 的解析規則刻意保持一致(Length >= 3、第二欄可轉 int),
                // 否則瀏覽器會顯示出自動更新根本不認得的條目。
                var data = line.Split(",");
                if(data.Length >= 3 && int.TryParse(data[1], out var ver))
                {
                    if(!seen.Add(data[0])) continue;
                    builder.Add(new(data[0], ver, data[2], source));
                }
                else
                {
                    skipped++;
                }
            }
        }
        EntriesInternal = builder.ToImmutable();
        SkippedLines = skipped;
        Interlocked.Exchange(ref LastUpdatedTicks, DateTime.Now.Ticks);
        LastError = null;
        StateInternal = CatalogState.Loaded;
        PluginLog.Information($"Script catalog parsed: {EntriesInternal.Count} entries, {skipped} lines skipped");
    }
}
