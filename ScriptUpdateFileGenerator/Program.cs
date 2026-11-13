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

    /// <summary>
    /// 派送規則檔，放在腳本根目錄（不是 .cs，產生器不會把它當腳本）。
    /// 每行 <c>路徑|種類|說明</c>：路徑相對腳本根、正斜線，結尾 <c>/</c>＝整個資料夾；
    /// 種類 <c>exclude</c>＝不進 update.csv，其餘種類寫進第四欄（<c>;</c> 分隔），說明寫進第五欄。
    /// 🔴 規則檔有錯一律 exit 1：靜默略過＝要排除的腳本照樣派送出去。
    /// </summary>
    const string RulesFileName = "distribution-rules.txt";
    static readonly string[] KnownKinds = ["exclude", "actuates", "test"];

    sealed class Rule(string relPath, string kind, string note, int lineNo)
    {
        internal string RelPath { get; } = relPath;
        internal string Kind { get; } = kind;
        internal string Note { get; } = note;
        internal int LineNo { get; } = lineNo;
        internal bool Used;
        internal bool Matches(string rel) => RelPath.EndsWith('/') ? rel.StartsWith(RelPath, StringComparison.Ordinal) : rel == RelPath;
    }

    static List<Rule> Rules = [];

    static void Main(string[] args)
    {
        if(args.Length != 2)
        {
            Console.WriteLine("Input and output destinations must be defined");
            Environment.Exit(0);
        }
        LoadRules(Path.Combine(args[0], RulesFileName));
        ProcessDirectory([""], args[0]);
        var unused = Rules.Where(r => !r.Used).ToList();
        if(unused.Count > 0)
        {
            // 規則對不到任何腳本＝改名或搬檔後失效，該排除的腳本可能換個名字照樣派送。
            foreach(var r in unused) Console.Error.WriteLine($"{RulesFileName}:{r.LineNo}: rule matches no script: {r.RelPath}");
            Environment.Exit(1);
        }
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
                var matched = Rules.Where(r => r.Matches(virtualPath[1..])).ToList();
                matched.ForEach(r => r.Used = true);
                if(matched.Any(r => r.Kind == "exclude"))
                {
                    Console.WriteLine($"  Excluded by {RulesFileName}");
                    continue;
                }
                if (namespac != null && clas != null && version != null)
                {
                    var line = $"{namespac}@{clas},{version},{ScriptRepoBaseURL}{virtualPath}";
                    // 沒有標記的腳本維持三欄，與舊格式逐位元相同。
                    var kinds = matched.Select(r => r.Kind).Distinct().Order(StringComparer.Ordinal).ToList();
                    if(kinds.Count > 0)
                    {
                        var note = string.Join("；", matched.Select(r => r.Note).Where(n => n.Length > 0));
                        line += $",{string.Join(";", kinds)},{note}";
                    }
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
            // 腳本原始碼的複本一旦落進建置輸出,CSV 就會多出重複行,而且產生器不會報錯。
            if(string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Skipping build output directory {dir}");
                continue;
            }
            ProcessDirectory([..path, dirName], dir);
        }
    }

    static void LoadRules(string rulesPath)
    {
        if(!File.Exists(rulesPath))
        {
            Console.WriteLine($"No {RulesFileName}, every script is distributed");
            return;
        }
        var errors = new List<string>();
        var lines = File.ReadAllText(rulesPath).Replace("\r", "").Split('\n');
        for(var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if(raw.Length == 0 || raw.StartsWith('#')) continue;
            var parts = raw.Split('|', 3);
            var rel = parts[0].Trim();
            var kind = parts.Length > 1 ? parts[1].Trim() : "";
            var note = parts.Length > 2 ? parts[2].Trim() : "";
            if(rel.Length == 0 || rel.StartsWith('/') || rel.Contains('\\')) errors.Add($"line {i + 1}: path must be relative with forward slashes");
            else if(!KnownKinds.Contains(kind)) errors.Add($"line {i + 1}: unknown kind '{kind}'");
            // 說明寫進 CSV 第五欄：半形逗號會把欄位切壞。
            else if(note.Contains(',')) errors.Add($"line {i + 1}: note must not contain ASCII comma");
            else Rules.Add(new(rel, kind, note, i + 1));
        }
        if(errors.Count > 0)
        {
            foreach(var e in errors) Console.Error.WriteLine($"{RulesFileName}: {e}");
            Environment.Exit(1);
        }
        Console.WriteLine($"Loaded {Rules.Count} rule(s) from {RulesFileName}");
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
    // 上游寫死 ASCII,類別名結尾是希臘字母的腳本(TEA P4 Fate Projection α / β)比對不到、
    // 進不了 update.csv,而且產生器只是跳過、不會報錯。
    [GeneratedRegex("([\\w\\.]+)\\s*:\\s*SplatoonScript", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ClassRegex();
    [GeneratedRegex(@"override.+Metadata.+Metadata.+new\D+([0-9]+)")]
    private static partial Regex VersionRegex();
}
