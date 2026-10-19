using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using Splatoon;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Text;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using NpcYell = Lumina.Excel.Sheets.NpcYell;

namespace SplatoonScriptsOfficial.Duties.Stormblood;

public class UCOB_Nael_Quotes_International : SplatoonScript
{
    #region Metadata

    public override Metadata? Metadata => new(1, "mirage");
    public override HashSet<uint>? ValidTerritories => [733];

    #endregion

    #region Config

    private Config C => Controller.GetConfig<Config>();

    #endregion

    #region State

    private Element? _inDonut;
    private Element? _outCircle;
    private Element? _stackMarker;
    private Element? _spreadMarker;
    private Dictionary<string, string>? _clientYells;
    // 每則聊天訊息都要比對 14 句×6 種寫法；設定文字可在介面上改，所以用原字串當鍵快取。
    private readonly Dictionary<string, string> _normalizedQuotes = [];

    #endregion

    // 台詞的 NpcYell 列號：執行期依用戶端語言讀表，台服不靠手填文字。
    private static readonly Dictionary<string, uint> YellRows = new()
    {
        [nameof(Config.QuoteSpreadIn)] = 6497,
        [nameof(Config.QuoteSpreadOut)] = 6496,
        [nameof(Config.QuoteStackIn)] = 6495,
        [nameof(Config.QuoteStackOut)] = 6494,
        [nameof(Config.QuoteInStack)] = 6493,
        [nameof(Config.QuoteInOut)] = 6492,
        [nameof(Config.QuoteTankStack)] = 6501,
        [nameof(Config.QuoteSpreadTank)] = 6500,
        [nameof(Config.QuoteSpreadInStars)] = 6502,
        [nameof(Config.QuoteInSpread)] = 6503,
        [nameof(Config.QuoteInOutSpread)] = 6507,
        [nameof(Config.QuoteInSpreadStack)] = 6506,
        [nameof(Config.QuoteOutStackSpread)] = 6504,
        [nameof(Config.QuoteOutSpreadStack)] = 6505,
    };

    #region Types

    // NpcYell lines as they appear in chat. Line breaks are spaces.
    private sealed class Config : IEzConfig
    {
        public bool EnableSpreadIn = true;
        public InternationalString QuoteSpreadIn = new(
            en: "From on high I descend, the hallowed moon to call!",
            jp: "我、舞い降りて 月を仰がん！",
            de: "Seht, ich steige herab, vom rotglühenden Monde!",
            fr: "Des cieux je vais descendre et révérer la lune !",
            other: "我降臨於此， 對月長嘯！");

        public bool EnableSpreadOut = true;
        public InternationalString QuoteSpreadOut = new(
            en: "From on high I descend, the iron path to walk!",
            jp: "我、舞い降りて 鉄の覇道を征く！",
            de: "Seht, ich steige herab, um euch zu beherrschen!",
            fr: "Du haut des cieux, je vais descendre pour conquérir !",
            other: "我降臨於此， 征戰鐵血霸道！");

        public bool EnableStackIn = true;
        public InternationalString QuoteStackIn = new(
            en: "Take fire, O hallowed moon!",
            jp: "赤熱せし 月の祝福を！",
            de: "Flammender Pfad, geschaffen vom roten Mond!",
            fr: "Baignez dans la bénédiction de la lune incandescente !",
            other: "熾熱燃燒！ 給予我月亮的祝福！");

        public bool EnableStackOut = true;
        public InternationalString QuoteStackOut = new(
            en: "Blazing path, lead me to iron rule!",
            jp: "赤熱し、焼かれし道を 鉄の覇道と成す！",
            de: "Umloderter Pfad, führe mich zur Herrschaft!",
            fr: "La voie marquée par l'incandescence mène à la domination !",
            other: "被熾熱灼燒過的軌跡 乃成鐵血霸道！");

        public bool EnableInStack = true;
        public InternationalString QuoteInStack = new(
            en: "O hallowed moon, take fire and scorch my foes!",
            jp: "月よ！ 赤熱し、神敵を焼け！",
            de: "O roter Mond! Umlodere meinen Pfad!",
            fr: "Que l'incandescence de la lune brûle mes ennemis !",
            other: "月光啊！ 用你的熾熱燒盡敵人！");

        public bool EnableInOut = true;
        public InternationalString QuoteInOut = new(
            en: "O hallowed moon, shine you the iron path!",
            jp: "月よ！ 鉄の覇道を照らせ！",
            de: "O roter Mond! Führe mich zur Herrschaft!",
            fr: "Ô lune ! Éclaire la voie de la domination !",
            other: "月光啊！ 照亮鐵血霸道！");

        public bool EnableTankStack = true;
        public InternationalString QuoteTankStack = new(
            en: "Fleeting light! 'Neath the red moon, scorch you the earth!",
            jp: "超新星よ、輝きを増せ！ 紅月下の赤熱せし地を照らせ！",
            de: "Neues Gestirn! Glühe herab und umlodere meinen Pfad!",
            fr: "Supernova, brille de tout ton feu et irradie la terre rougie !",
            other: "超新星啊，更加閃耀吧！ 照亮紅月下熾熱之地！");

        public bool EnableSpreadTank = true;
        public InternationalString QuoteSpreadTank = new(
            en: "Fleeting light! Amid a rain of stars, exalt you the red moon!",
            jp: "超新星よ、輝きを増せ！ 星降りの夜に、紅月を称えよ！",
            de: "Neues Gestirn! Überstrahle jede Sternschnuppe!",
            fr: "Supernova, brille de tout ton feu et glorifie la lune rouge !",
            other: "超新星啊，更加閃耀吧！ 在星降之夜，稱讚紅月！");

        public bool EnableSpreadInStars = true;
        public InternationalString QuoteSpreadInStars = new(
            en: "From on high I descend, the moon and stars to bring!",
            jp: "我、舞い降りて月を仰ぎ 星降りの夜を招かん！",
            de: "Ich steige herab zu Ehre des roten Mondes! Einer Sternschnuppe gleich!",
            fr: "Du haut des cieux, j'appelle une pluie d'étoiles !",
            other: "我降臨於此對月長嘯！ 召喚星降之夜！");

        public bool EnableInSpread = true;
        public InternationalString QuoteInSpread = new(
            en: "From hallowed moon I descend, a rain of stars to bring!",
            jp: "我、月より舞い降りて 星降りの夜を招かん！",
            de: "O roter Mond, sieh mich herabsteigen! Einer Sternschnuppe gleich!",
            fr: "Depuis la lune, j'invoque une pluie d'étoiles !",
            other: "我自月而來降臨於此， 召喚星降之夜！");

        public bool EnableInOutSpread = true;
        public InternationalString QuoteInOutSpread = new(
            en: "From hallowed moon I bare iron, in my descent to wield!",
            jp: "我、月より鉄を備え 舞い降りん！",
            de: "O roter Mond, als Künder deiner Herrschaft stieg ich herab!",
            fr: "De la lune je m'arme d'acier et descends !",
            other: "我自月而來攜鋼鐵降臨於此！");

        public bool EnableInSpreadStack = true;
        public InternationalString QuoteInSpreadStack = new(
            en: "From hallowed moon I descend, upon burning earth to tread!",
            jp: "我、月より舞い降りて 赤熱せし地を歩まん！",
            de: "O roter Mond! Ich stieg herab, um deine Herrschaft zu bringen!",
            fr: "De la lune, je descends et marche sur la terre ardente !",
            other: "我自月而來降臨於此， 踏過熾熱之地！");

        public bool EnableOutStackSpread = true;
        public InternationalString QuoteOutStackSpread = new(
            en: "Unbending iron, take fire and descend!",
            jp: "鉄よ、赤熱せよ！ 舞い降りし我が刃となれ！",
            de: "Zur Herrschaft führt mein umloderter Pfad! Auf diesen steige ich herab!",
            fr: "Ô noble acier ! Rougis ardemment et deviens ma lame transperçante !",
            other: "鋼鐵燃燒吧！ 成為我降臨於此的刀劍吧！");

        public bool EnableOutSpreadStack = true;
        public InternationalString QuoteOutSpreadStack = new(
            en: "Unbending iron, descend with fiery edge!",
            jp: "鉄よ、舞い降りし 我の刃となり赤熱せよ！",
            de: "Zur Herrschaft steige ich herab, auf umlodertem Pfad!",
            fr: "Fier acier ! Sois ma lame plongeante et deviens incandescent !",
            other: "鋼鐵成為我降臨於此的燃燒之劍！");
    }

    #endregion

    #region LifeCycle

    public override void OnSetup()
    {
        _inDonut = Controller.RegisterElementFromCode(
            "in_donut",
            """{"Name":"in_donut","type":1,"Enabled":false,"radius":5.7,"Donut":16.56,"fillIntensity":0.17,"thicc":5.0,"refActorNPCID":2612,"refActorComparisonType":4,"mechanicType":1}""",
            overwrite: true);
        _inDonut.Enabled = false;

        _outCircle = Controller.RegisterElementFromCode(
            "out_circle",
            """{"Name":"out_circle","type":1,"Enabled":false,"radius":9.0,"fillIntensity":0.17,"thicc":5.0,"refActorNPCID":2612,"refActorComparisonType":4,"mechanicType":1}""",
            overwrite: true);
        _outCircle.Enabled = false;

        _stackMarker = Controller.RegisterElementFromCode(
            "stack_marker",
            """{"Name":"stack_self","type":1,"Enabled":false,"radius":3.75,"Donut":0.25,"fillIntensity":0.5,"refActorName":"Ieni Telara","refActorType":1}""",
            overwrite: true);
        _stackMarker.Enabled = false;

        _spreadMarker = Controller.RegisterElementFromCode(
            "spread_marker",
            """{"Name":"player circles","type":1,"Enabled":false,"radius":4.0,"color":3355507967,"fillIntensity":0.5,"thicc":4.0,"refActorPlaceholder":["<1>","<2>","<3>","<4>","<5>","<6>","<7>","<8>"],"refActorComparisonType":5,"DistanceSourceX":0.21287155,"DistanceSourceY":9.486858,"DistanceMax":24.0}""",
            overwrite: true);
        _spreadMarker.Enabled = false;
    }

    public override void OnReset()
    {
        if(_inDonut != null) _inDonut.Enabled = false;
        if(_outCircle != null) _outCircle.Enabled = false;
        if(_stackMarker != null) _stackMarker.Enabled = false;
        if(_spreadMarker != null) _spreadMarker.Enabled = false;
    }

    public override void OnMessage(string message)
    {
        message = Normalize(message);
        if(Matches(message, C.EnableSpreadIn, C.QuoteSpreadIn, nameof(Config.QuoteSpreadIn)) || Matches(message, C.EnableSpreadInStars, C.QuoteSpreadInStars, nameof(Config.QuoteSpreadInStars)))
        {
            Activate(_spreadMarker, false);
            Activate(_inDonut, true);
            return;
        }

        if(Matches(message, C.EnableSpreadOut, C.QuoteSpreadOut, nameof(Config.QuoteSpreadOut)))
        {
            Activate(_spreadMarker, false);
            Activate(_outCircle, true);
            return;
        }

        if(Matches(message, C.EnableStackIn, C.QuoteStackIn, nameof(Config.QuoteStackIn)))
        {
            Activate(_stackMarker, false);
            Activate(_inDonut, true);
            return;
        }

        if(Matches(message, C.EnableStackOut, C.QuoteStackOut, nameof(Config.QuoteStackOut)))
        {
            Activate(_stackMarker, false);
            Activate(_outCircle, true);
            return;
        }

        if(Matches(message, C.EnableInStack, C.QuoteInStack, nameof(Config.QuoteInStack)))
        {
            Activate(_inDonut, false);
            Activate(_stackMarker, true);
            return;
        }

        if(Matches(message, C.EnableInOut, C.QuoteInOut, nameof(Config.QuoteInOut)))
        {
            Activate(_inDonut, false);
            Activate(_outCircle, true);
            return;
        }

        if(Matches(message, C.EnableTankStack, C.QuoteTankStack, nameof(Config.QuoteTankStack)))
        {
            Activate(_stackMarker, true);
            return;
        }

        if(Matches(message, C.EnableSpreadTank, C.QuoteSpreadTank, nameof(Config.QuoteSpreadTank)))
        {
            Activate(_spreadMarker, false);
            return;
        }

        if(Matches(message, C.EnableInSpread, C.QuoteInSpread, nameof(Config.QuoteInSpread)))
        {
            Activate(_inDonut, false);
            Activate(_spreadMarker, true);
            return;
        }

        if(Matches(message, C.EnableInOutSpread, C.QuoteInOutSpread, nameof(Config.QuoteInOutSpread)))
        {
            Activate(_inDonut, false);
            Activate(_outCircle, true);
            Activate3(_spreadMarker);
            return;
        }

        if(Matches(message, C.EnableInSpreadStack, C.QuoteInSpreadStack, nameof(Config.QuoteInSpreadStack)))
        {
            Activate(_inDonut, false);
            Activate(_spreadMarker, true);
            Activate3(_stackMarker);
            return;
        }

        if(Matches(message, C.EnableOutStackSpread, C.QuoteOutStackSpread, nameof(Config.QuoteOutStackSpread)))
        {
            Activate(_outCircle, false);
            Activate(_stackMarker, true);
            Activate3(_spreadMarker);
            return;
        }

        if(Matches(message, C.EnableOutSpreadStack, C.QuoteOutSpreadStack, nameof(Config.QuoteOutSpreadStack)))
        {
            Activate(_outCircle, false);
            Activate(_spreadMarker, true);
            Activate3(_stackMarker);
        }
    }

    public override void OnSettingsDraw()
    {
        ImGuiEx.TextWrapped(ImGuiColors.DalamudGrey, "奈爾說出台詞時，依台詞顯示接下來的月環、鋼鐵、分攤、分散位置。台詞會自動從遊戲資料讀取（台服不必手填），下拉選單裡的文字是額外的比對對象。");
        DrawQuote("分散 → 月環", ref C.EnableSpreadIn, C.QuoteSpreadIn);
        DrawQuote("分散 → 鋼鐵", ref C.EnableSpreadOut, C.QuoteSpreadOut);
        DrawQuote("分攤 → 月環", ref C.EnableStackIn, C.QuoteStackIn);
        DrawQuote("分攤 → 鋼鐵", ref C.EnableStackOut, C.QuoteStackOut);
        DrawQuote("月環 → 分攤", ref C.EnableInStack, C.QuoteInStack);
        DrawQuote("月環 → 鋼鐵", ref C.EnableInOut, C.QuoteInOut);
        DrawQuote("遠離坦克 → 分攤", ref C.EnableTankStack, C.QuoteTankStack);
        DrawQuote("分散 → 遠離坦克", ref C.EnableSpreadTank, C.QuoteSpreadTank);
        DrawQuote("分散 → 月環（隕石流）", ref C.EnableSpreadInStars, C.QuoteSpreadInStars);
        DrawQuote("月環 → 分散", ref C.EnableInSpread, C.QuoteInSpread);
        DrawQuote("月環 → 鋼鐵 → 分散", ref C.EnableInOutSpread, C.QuoteInOutSpread);
        DrawQuote("月環 → 分散 → 分攤", ref C.EnableInSpreadStack, C.QuoteInSpreadStack);
        DrawQuote("鋼鐵 → 分攤 → 分散", ref C.EnableOutStackSpread, C.QuoteOutStackSpread);
        DrawQuote("鋼鐵 → 分散 → 分攤", ref C.EnableOutSpreadStack, C.QuoteOutSpreadStack);
    }

    #endregion

    #region Private Method

    // message 已經 Normalize 過；比對設定裡的各語言文字，以及遊戲資料表裡用戶端語言的那一句。
    private bool Matches(string message, bool enabled, InternationalString quote, string key)
    {
        if(!enabled)
            return false;

        return StartsWith(message, quote.En)
            || StartsWith(message, quote.Jp)
            || StartsWith(message, quote.De)
            || StartsWith(message, quote.Fr)
            || StartsWith(message, quote.Other)
            || (GetClientYells().TryGetValue(key, out var yell) && StartsWith(message, yell));
    }

    // Skip a blank language so an empty field does not match every line.
    private bool StartsWith(string message, string quote)
    {
        if(string.IsNullOrEmpty(quote))
            return false;
        if(!_normalizedQuotes.TryGetValue(quote, out var q))
        {
            if(_normalizedQuotes.Count > 256)
                _normalizedQuotes.Clear();
            _normalizedQuotes[quote] = q = Normalize(quote);
        }
        return q.Length > 0 && message.StartsWith(q, StringComparison.Ordinal);
    }

    // 換行在聊天訊息、資料表、設定檔裡寫法各不相同，比對前一律去掉所有空白與 Splatoon 聊天會剝掉的符號。
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach(var c in text)
        {
            if(char.IsWhiteSpace(c) || c is '\u201C' or '\u201D' || (c >= '\uE000' && c <= '\uF8FF'))
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    // 第一次用到才讀表；讀不到就只靠設定裡的文字（記一行 Warning）。
    private Dictionary<string, string> GetClientYells()
    {
        if(_clientYells != null)
            return _clientYells;

        var result = new Dictionary<string, string>();
        try
        {
            var sheet = Svc.Data.GetExcelSheet<NpcYell>();
            foreach(var (key, rowId) in YellRows)
            {
                var text = sheet?.GetRowOrDefault(rowId)?.Text.ExtractText();
                if(!string.IsNullOrEmpty(text))
                    result[key] = Normalize(text);
            }
        }
        catch(Exception e)
        {
            PluginLog.Warning($"[UCOB Nael Quotes International] 讀不到 NpcYell 表：{e.Message}");
        }
        if(result.Count != YellRows.Count)
            PluginLog.Warning($"[UCOB Nael Quotes International] NpcYell 只讀到 {result.Count}/{YellRows.Count} 句，其餘只比對設定裡的文字");
        return _clientYells = result;
    }

    // Show the marker now, or after the first marker, then hide it.
    private void Activate(Element? element, bool later)
    {
        if(element == null)
        {
            return;
        }

        if(later)
        {
            Controller.Schedule(() => element.Enabled = true, 6000);
            Controller.Schedule(() => element.Enabled = false, 9000);
        }
        else
        {
            element.Enabled = true;
            Controller.Schedule(() => element.Enabled = false, 6000);
        }
    }

    // Show the third marker after the second one ends.
    private void Activate3(Element? element)
    {
        if(element == null)
        {
            return;
        }

        Controller.Schedule(() => element.Enabled = true, 9000);
        Controller.Schedule(() => element.Enabled = false, 12000);
    }

    // Draw one quote as an editable international string.
    private static void DrawQuote(string label, ref bool enabled, InternationalString quote)
    {
        ImGui.Checkbox($"##{label}", ref enabled);
        ImGui.SameLine();
        var disabled = !enabled;
        if(disabled)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(400f);
        if(ImGui.BeginCombo($"##{label}quote", quote.Get()))
        {
            ImGui.InputText($"English##{label}", ref quote.En, 500);
            ImGui.InputText($"日本語##{label}", ref quote.Jp, 500);
            ImGui.InputText($"Deutsch##{label}", ref quote.De, 500);
            ImGui.InputText($"Français##{label}", ref quote.Fr, 500);
            ImGui.InputText($"其他語言##{label}", ref quote.Other, 500);
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGuiEx.Text(label);
        if(disabled)
            ImGui.EndDisabled();
    }

    #endregion
}
