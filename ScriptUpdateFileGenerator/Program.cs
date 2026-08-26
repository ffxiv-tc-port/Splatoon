using System.Text.RegularExpressions;

namespace ScriptUpdateFileGenerator;

internal partial class Program
{
    /// <summary>
    /// 逐腳本下載網址的前綴。這是整條更新鏈真正的來源 ——
    /// 外掛端只是抓 update.csv,實際去哪裡下載每一支腳本是由這裡烤進 CSV 每一行的。
    /// 只改外掛端的 update.csv 位址而不改這裡,使用者拿到的還是上游的腳本檔。
    /// 必須與 Splatoon/SplatoonScripting/ScriptingProcessor.cs 的 ScriptRepoBaseURL 一致。
    /// </summary>
    const string ScriptRepoBaseURL = "https://raw.githubusercontent.com/ffxiv-tc-port/Splatoon/HEAD/SplatoonScripts";

    static List<string> Content = [];

    static void Main(string[] args)
    {
        if(args.Length != 2)
        {
            Console.WriteLine("Input and output destinations must be defined");
            Environment.Exit(0);
        }
        ProcessDirectory([""], args[0]);
        // Directory.GetFiles 的回傳順序在 Linux 上是 readdir 順序(等同亂序),
        // 不排序的話 CI 每次重新產生都會出現整份洗牌的 diff,真正的版本異動會被淹掉。
        // update.csv 現在是我們的出貨清單,它的 diff 必須是可讀的。
        Content.Sort(StringComparer.Ordinal);
        File.WriteAllText(args[1], string.Join("\n",Content));
    }

    static void ProcessDirectory(string[] path, string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.cs"))
        {
            try
            {
                var fname = Path.GetFileName(file);
                var virtualPath = $"{string.Join("/", path)}/{fname}";
                Console.WriteLine($"Processing file {file} ({virtualPath})");
                var content = File.ReadAllText(file);
                var namespac = ExtractNamespaceFromCode(content);
                var clas = ExtractClassFromCode(content);
                var version = ExtractVersionFromCode(content);
                Console.WriteLine($"  Namespace: {namespac}, Class: {clas}, Version: {version}");
                if (namespac != null && clas != null && version != null)
                {
                    var line = $"{namespac}@{clas},{version},{ScriptRepoBaseURL}{virtualPath}";
                    Console.WriteLine($"  {line}");
                    Content.Add(line);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        foreach(var dir in Directory.GetDirectories(directory))
        {
            var dirName = Path.GetFileName(dir);
            // 產生器是無條件遞迴的,不排除就會走進 SplatoonScripts/bin 與 SplatoonScripts/obj。
            // 目前那裡的 .cs 剛好都比對不到 ": SplatoonScript" 所以 update.csv 沒被汙染,
            // 但那是巧合不是保證 —— 一旦有腳本原始碼的複本落進建置輸出,CSV 就會多出重複行,
            // 而且失敗是靜默的:產生器不會報錯,外掛端也只是照著多出來的那行去下載。
            if(string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Skipping build output directory {dir}");
                continue;
            }
            ProcessDirectory([..path, dirName], dir);
        }
    }

    internal static string? ExtractNamespaceFromCode(string code)
    {
        var regex = NamespaceRegex();
        var matches = regex.Match(code);
        if (matches.Success && matches.Groups.Count > 1)
        {
            return matches.Groups[1].Value;
        }
        return null;
    }

    static string? ExtractClassFromCode(string code)
    {
        var regex = ClassRegex();
        var matches = regex.Match(code);
        if (matches.Success && matches.Groups.Count > 1)
        {
            return matches.Groups[1].Value;
        }
        return null;
    }

    static int? ExtractVersionFromCode(string code)
    {
        var regex = VersionRegex();
        var matches = regex.Match(code);
        if (matches.Success && matches.Groups.Count > 1)
        {
            return int.Parse(matches.Groups[1].Value);
        }
        return null;
    }

    [GeneratedRegex("namespace[\\s]+([a-z0-9_\\.]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex NamespaceRegex();

    // 類別名用 \w 而不是 [a-z0-9_] —— .NET 的 \w 認得 Unicode 文字字元。
    // 上游寫死 ASCII,於是 TEA P4 Fate Projection α / β 這兩支(類別名結尾是希臘字母)
    // 整條比對失敗、從來沒有進過 update.csv,也就是說它們自始至終無法被自動更新,
    // 而且失敗是完全靜默的:產生器只是「跳過」,不會報錯。
    [GeneratedRegex("([\\w\\.]+)\\s*:\\s*SplatoonScript", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ClassRegex();
    [GeneratedRegex(@"override.+Metadata.+Metadata.+new\D+([0-9]+)")]
    private static partial Regex VersionRegex();
}
