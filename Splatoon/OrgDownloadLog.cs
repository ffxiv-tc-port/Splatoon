using System.Net.Http;

namespace Splatoon;

/// <summary>
/// 本 fork 自家 GitHub 倉庫的下載結果觀測網:成功只報第一次,失敗每次都報。
/// repo 轉私有後沒填權杖就是 404,而既有路徑只把例外丟進 e.Log()／catch,
/// 使用者看到的症狀是「腳本永遠不更新」而不是任何錯誤。
/// </summary>
/// <remarks>任何執行緒都可呼叫。訊息只含主機名與 HTTP 狀態碼,絕不含權杖。</remarks>
internal static class OrgDownloadLog
{
    /// <summary>自家倉庫的 raw 前綴。只有它需要權杖,其他來源不給權杖提示。</summary>
    private const string OrgRawPrefix = "https://raw.githubusercontent.com/ffxiv-tc-port/";

    /// <summary>已經報過成功的用途名稱。</summary>
    private static readonly HashSet<string> ReportedOk = [];

    internal static void Report(string purpose, string url, HttpResponseMessage response)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "?";
        var code = (int)response.StatusCode;
        if(response.IsSuccessStatusCode)
        {
            lock(ReportedOk)
            {
                if(!ReportedOk.Add(purpose)) return;
            }
            PluginLog.Information($"[GitHub] {purpose}:已從 {host} 取得(HTTP {code})");
        }
        else
        {
            var hint = url.StartsWith(OrgRawPrefix, StringComparison.OrdinalIgnoreCase)
                ? "本 repo 已轉為私有,請在 Dalamud 設定中填入 GitHub 存取權杖。"
                : "";
            PluginLog.Warning($"[GitHub] {purpose}下載失敗:{host} 回應 HTTP {code}。{hint}");
        }
    }
}
