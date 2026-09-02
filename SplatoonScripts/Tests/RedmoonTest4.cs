// Ignore Spelling: Metadata Leve wks

using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Callback = ECommons.Automation.Callback;

namespace SplatoonScriptsOfficial.Tests;
internal unsafe class RedmoonTest4 :SplatoonScript
{
    private class NodeData
    {
        public AtkResNode* ResNode = null;
        public AtkComponentBase* Component = null;

        public bool HasData => ResNode != null && Component != null;
        public NodeData(AtkResNode* node, AtkComponentBase* component)
        {
            ResNode = node;
            Component = component;
        }
    }

    private enum Achevement
    {
        None = 0,
        Silver,
        Gold,
    }

    private class LeveData
    {
        public int index = 0;
        public uint RowId = 0u;
        public byte Rank = 0;
        public string Name { get; set; }
        public AtkResNode* ResNode = null;
        public int CosmoCredit = 0;
        public int MoonCredit = 0;
        public int TokenLv1 = 0;
        public int TokenLv2 = 0;
        public int TokenLv3 = 0;
        public int TokenLv4 = 0;
        public Achevement Achievement = Achevement.None;
        public LeveData(int index, uint rowId, byte rank, string name, AtkResNode* resNode, Achevement achievement)
        {
            this.index = index;
            Achievement = achievement;
            RowId = rowId;
            Rank = rank;
            Name = name;
            ResNode = resNode;
        }

        /// <summary>送出 callback 時第二個參數的實際數值（金 8／銀 4／無 0）。</summary>
        /// <remarks>抽成屬性只是為了讓 <see cref="PressKey"/> 與 <see cref="Select"/> 用的是同一份值；數值本身沒有改。</remarks>
        private uint AchievementValue => Achievement switch
        {
            Achevement.Gold => 8u,
            Achevement.Silver => 4u,
            _ => 0u,
        };

        /// <summary>這一筆在守衛裡的「參數組」鍵。</summary>
        /// <remarks>
        /// 🔴 守衛的粒度必須是（視窗，實例位址，<b>參數組</b>）而不是「一扇視窗只按一次」：
        /// 這支腳本是對<b>同一扇</b> WKSMission 逐筆送不同的 RowId／index 去把每個任務的代幣讀出來，
        /// 併成同一個鍵會讓第二筆之後全部被自己的守衛擋掉，整個列舉停在第一筆。
        /// </remarks>
        public string PressKey => $"12,{RowId},{AchievementValue},{index}";

        public void Select([DisallowNull] AtkUnitBase* wksMission)
        {
            Callback.Fire(wksMission, true, 12, RowId, AchievementValue, (uint)index);
        }

        public void GetTokenCounts([DisallowNull] AtkUnitBase* wksMission)
        {
            InternalLog.Information($"GetTokenCounts {RowId} {Name} {Rank}");
            for (uint i = 560001u; i < 560007u; i++)
            {
                if (!SearchNode(wksMission, i, out var multiNode))
                {
                    InternalLog.Error($"multiNode not found");
                    return;
                }

                if (!multiNode.ResNode->IsVisible()) continue;

                var ImageNode = (AtkImageNode*)SearchResNodeOnly(multiNode, 3);
                if (ImageNode == null)
                {
                    InternalLog.Error("texNode not found");
                }

                var str = GetTexturePath(ImageNode);

                if (!SearchNode(multiNode, 2, out var tokenCountNode))
                {
                    InternalLog.Error($"tokenCountNode not found");
                    return;
                }

                var textNode = (AtkTextNode*)SearchResNodeOnly(tokenCountNode, 2);
                if (textNode == null)
                {
                    InternalLog.Error($"textNode not found");
                    return;
                }

                InternalLog.Information($"GetTokenCounts result {str} {textNode->GetText()}");

                if (str.Contains("065000/065112_hr1.tex"))
                {
                    CosmoCredit = int.Parse(textNode->GetText().ToString());
                }
                if (str.Contains("065000/065126_hr1.tex"))
                {
                    MoonCredit = int.Parse(textNode->GetText().ToString());
                }
                if (str.Contains("070000/070803_hr1.tex"))
                {
                    TokenLv1 = int.Parse(textNode->GetText().ToString());
                }
                if (str.Contains("070000/070814_hr1.tex"))
                {
                    TokenLv2 = int.Parse(textNode->GetText().ToString());
                }
                if (str.Contains("070000/070825_hr1.tex"))
                {
                    TokenLv3 = int.Parse(textNode->GetText().ToString());
                }
                if (str.Contains("070000/070836_hr1.tex"))
                {
                    TokenLv4 = int.Parse(textNode->GetText().ToString());
                }
            }
        }
    }

    public class Config :IEzConfig
    {
        public bool SkipA = false;
    }

    public override HashSet<uint>? ValidTerritories { get; } = null;
    public override Metadata? Metadata => new(2, "Redmoon");

    public override Dictionary<int, string> Changelog => new()
    {
        [2] = """
        修正：對視窗送出點擊之後有「正在關閉中」的幾幀，這期間視窗仍然通過就緒檢查，
        此時再送一次就是攔不到的原生存取違規（遊戲當場關閉）。
        現在同一扇視窗的同一組參數在它收掉之前只送一次；
        除錯面板上那顆直接點確認框「是」的按鈕原本連就緒檢查都沒有，一併補上。
        """
    };

    private List<LeveData> _leveData = new();
    [DisallowNull]
    private long _delayTime = long.MinValue;
    private bool _setupDone = false;
    private Job _job = 0;
    private bool _start = false;

    /// <summary>宇宙探索任務清單視窗。</summary>
    private const string WksMissionAddon = "WKSMission";
    /// <summary>宇宙探索的常駐 HUD。</summary>
    private const string WksHudAddon = "WKSHud";
    /// <summary>確認框。</summary>
    private const string SelectYesnoAddon = "SelectYesno";

    /// <summary>會被守衛罩住的視窗名字，解除點逐一輪詢這一份。</summary>
    private static readonly string[] GuardedAddons = [WksMissionAddon, WksHudAddon, SelectYesnoAddon];

    /// <summary>
    /// 每個視窗名字底下、「已經送過點擊的那一組參數」對應的實例位址與送出的時刻。
    /// </summary>
    /// <remarks>
    /// 🔴 <b>位址只拿來做等值比較，永遠不解參</b>——所以 <see cref="TryBeginPress"/> 收的是
    /// <see cref="nint"/> 而不是指標，讓「不解參」變成型別上就辦不到的事。
    /// </remarks>
    private readonly Dictionary<string, Dictionary<string, (nint Address, long At)>> _pressed = new(StringComparer.Ordinal);

    /// <summary>多次互動窗（按下去視窗<b>不會</b>消失）的逃生口：15 幀，60fps 下約 250 毫秒。</summary>
    /// <remarks>
    /// WKSHud 與 WKSMission 按下之後都還留在畫面上、而且本來就要連續按（逐筆讀任務代幣），
    /// 逃生口取太長會把正常流程變成慢動作。
    /// </remarks>
    private const long RoutineRePressTimeoutMs = 250;

    /// <summary>「回答一次即終結」的視窗（確認框）的逃生口：2000 毫秒。</summary>
    /// <remarks>
    /// 遠大於「正在關閉中」那幾幀（60fps 下數十毫秒、卡頓時也就數百毫秒）。
    /// 🔴 有逃生口是刻意的：萬一上一次的點擊根本沒生效、視窗就是還開著，
    /// 沒有逾時的話會把崩潰換成「這顆按鈕從此按不動」的靜默失效。
    /// </remarks>
    private const long PressReleaseTimeoutMs = 2000;

    /// <summary>
    /// 登記「即將對這扇視窗送出這一組參數」。<b>回 <see langword="false"/> ＝這一輪絕對不能送。</b>
    /// </summary>
    /// <remarks>
    /// 🔴🔴 就緒檢查三關（非 null／<c>IsVisible</c>／<c>UldManager.LoadedState == Loaded</c>）
    /// <b>擋不住「送出之後正在關閉中」的那幾幀</b>：那期間三關全過，而視窗其實已經在拆，
    /// 此時再送就是原生 AccessViolationException（.NET Core 的 corrupted-state exception，
    /// <c>try/catch</c> 攔不到，遊戲當場關閉）。這裡走的是 <c>ClickAddonButton</c> ⇒ <c>ReceiveEvent</c>
    /// （直接模擬點擊），比送 callback 更早踩到關閉中的視窗。
    /// <para>
    /// 一回 <see langword="true"/> 就已經把「送過了」記下去，所以呼叫點必須<b>緊接在送出動作之前</b>；
    /// 登記完卻不送的話會白白封鎖到逾時為止。
    /// </para>
    /// <para>
    /// 📌 位址不同就放行：同一個名字底下現在掛的是另一個實例，我們送過的那扇已經取不到了。
    /// （位址被新視窗重用也不成問題：頂多多等到逾時，不會變成崩潰。）
    /// </para>
    /// <para>
    /// ⚠️ 用牆鐘（<see cref="Environment.TickCount64"/>）而不是繪製幀計數器是刻意的：
    /// 畫面隱藏（過場／隱藏 UI 熱鍵）期間繪製幀根本不前進，逃生口會永不到期。
    /// </para>
    /// </remarks>
    private bool TryBeginPress(string addonName, nint address, string parameters, long timeoutMs)
    {
        if (!_pressed.TryGetValue(addonName, out var byParameters))
        {
            byParameters = new(StringComparer.Ordinal);
            _pressed[addonName] = byParameters;
        }
        if (byParameters.TryGetValue(parameters, out var prev) && prev.Address == address
            && Environment.TickCount64 - prev.At < timeoutMs)
        {
            return false;
        }
        byParameters[parameters] = (address, Environment.TickCount64);
        return true;
    }

    /// <summary>視窗真的收掉了 ⇒ 解除封鎖，下一扇同名視窗照樣按。</summary>
    /// <remarks>
    /// ⚠️ 刻意<b>不</b>在 <see cref="OnReset"/>／<see cref="WarmReset"/> 裡清空：
    /// 留著的舊項目是無害的（位址對不上就直接放行），
    /// 而清空反而會讓一扇正在關閉中的視窗重新變成可按。
    /// </remarks>
    private void ReleaseWindowGuard(string addonName) => _pressed.Remove(addonName);

    public override void OnUpdate()
    {
        // 🔴 解除點必須擺在所有 early return 之前：OnSettingsDraw 上那三顆按鈕在 _start 為 false 時
        // 照樣按得下去，把輪詢擺在 _start 檢查之後會讓旗標永遠解不開。
        // 用「TryGetAddonByName 取不到」當解除點而不是生命週期事件，是因為 OnUpdate 由
        // Framework.Update 驅動、每一個遊戲幀都會被呼叫，視窗消失的那一幀一定看得到。
        foreach (var addonName in GuardedAddons)
        {
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out _)) ReleaseWindowGuard(addonName);
        }
        if (!_start)
        {
            this.OnReset();
            return;
        }
        if (_job != Player.Job)
        {
            InternalLog.Information($"Job changed {Player.Job}");
            WarmReset();
            _job = Player.Job;
        }
        if (!_setupDone)
        {
            SetUp();
            return;
        }
    }

    public override void OnReset()
    {
        _leveData.Clear();
        _setupDone = false;
        _delayTime = long.MinValue;
        _job = 0;
    }

    public override void OnSettingsDraw()
    {
        var c = Controller.GetConfig<Config>();
        ImGui.Checkbox("Skip A", ref c.SkipA);
        if (!_start)
        {
            if (ImGui.Button("Start")) _start = true;
        }
        else
        {
            if (ImGui.Button("Stop")) _start = false;
        }

        if (ImGui.CollapsingHeader("Debug"))
        {
            ImGuiEx.Text($"_job: {_job}");
            ImGuiEx.Text($"DelayTime: {_delayTime}");
            ImGuiEx.Text($"SetupDone: {_setupDone}");
            ImGuiEx.Text($"LeveData Count: {_leveData.Count}");
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(WksHudAddon, out var wksHud))
            {
                ImGuiEx.Text(EzColor.RedBright, "WKSHud not found");
                return;
            }
            if (wksHud == null || !wksHud->IsReady()) return;

            if (!SearchNode(wksHud, 6, out var btnData)) return;
            if (ImGui.Button("Toggle"))
            {
                var btn = (AtkComponentButton*)btnData.Component;
                // 守衛擺在送出動作正前方：連點兩下時第二下有機會正好落在視窗關閉中的那幾幀。
                if (TryBeginPress(WksHudAddon, (nint)wksHud, "node6", RoutineRePressTimeoutMs))
                {
                    btn->ClickAddonButton(wksHud);
                }
            }
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(WksMissionAddon, out var addon)) return;
            if (addon == null || !addon->IsReady())
            {
                _leveData.Clear();
                ImGuiEx.Text(EzColor.RedBright, "WKSMission not found");
                return;
            }

            if (_leveData.Count != 0)
            {
                ImGuiEx.Text("Leve Data");
                List<ImGuiEx.EzTableEntry> entries = new();
                foreach (var leve in _leveData)
                {
                    if (leve == null) continue;
                    entries.Add(new ImGuiEx.EzTableEntry("Index", delegate { ImGui.Text(leve.index.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("RowId", delegate { ImGui.Text(leve.RowId.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("Rank", delegate { ImGui.Text(leve.Rank.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("Name", delegate { ImGui.Text(leve.Name); }));
                    entries.Add(new ImGuiEx.EzTableEntry("Address", delegate { ImGui.Text(((nint)leve.ResNode).ToString("X")); }));
                    entries.Add(new ImGuiEx.EzTableEntry("CosmoCredit", delegate { ImGui.Text(leve.CosmoCredit.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("MoonCredit", delegate { ImGui.Text(leve.MoonCredit.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("TokenLv1", delegate { ImGui.Text(leve.TokenLv1.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("TokenLv2", delegate { ImGui.Text(leve.TokenLv2.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("TokenLv3", delegate { ImGui.Text(leve.TokenLv3.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("TokenLv4", delegate { ImGui.Text(leve.TokenLv4.ToString()); }));
                    entries.Add(new ImGuiEx.EzTableEntry("GoldAchievement", delegate { ImGui.Text(leve.Achievement.ToString()); }));
                }
                ImGuiEx.EzTable("LeveData", entries);
            }

            var buttonPtr = addon->GetComponentButtonById(94);
            if (buttonPtr == null)
            {
                ImGuiEx.Text(EzColor.RedBright, "buttonPtr not found");
                return;
            }
            if (ImGui.Button("Click##Button94"))
            {
                // ⚠️ 這顆按鈕按下去 WKSMission 會不會跟著收掉,離線查不出來(節點 94 的語意
                // 要有客戶端在跑才驗得到)。分不出來就取保守的那一檔:萬一它其實是
                // 「按下即關」,短逃生口會讓連點的第二下正好落在關閉中的那幾幀 = 原生存取違規。
                // 這是手動的除錯按鈕、沒有任何迴圈等它,多等兩秒的代價幾乎是零。
                if (TryBeginPress(WksMissionAddon, (nint)addon, "btn94", PressReleaseTimeoutMs))
                {
                    buttonPtr->ClickAddonButton(addon);
                }
            }

            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(SelectYesnoAddon, out var SelectYesno))
            {
                ImGuiEx.Text(EzColor.RedBright, "SelectYesno addon not found");
                return;
            }
            // 🔴🔴 這一段原本連就緒檢查都沒有（同一個方法裡的 WKSHud 與 WKSMission 兩段都有），
            // 直接對剛取到的指標呼叫 GetComponentButtonById 再 ClickAddonButton。
            // 用 IsAddonReady(AtkUnitBase*) 而不是 addon->IsReady()：後者是傳值多載，
            // 指標為 null 時複製動作發生在呼叫端，判空已經來不及。
            if (!GenericHelpers.IsAddonReady(SelectYesno))
            {
                ImGuiEx.Text(EzColor.RedBright, "SelectYesno not ready");
                return;
            }

            buttonPtr = SelectYesno->GetComponentButtonById(8);
            if (buttonPtr == null)
            {
                ImGuiEx.Text(EzColor.RedBright, "buttonPtr not found");
                return;
            }
            if (ImGui.Button("Click##SelectYesno"))
            {
                // 確認框是「回答一次就結束」的視窗：按下去它就開始關，所以整扇窗併成同一個鍵、
                // 逃生口取長的一檔（2000ms）。這是本波崩潰形狀最典型的那一種。
                if (TryBeginPress(SelectYesnoAddon, (nint)SelectYesno, "yes", PressReleaseTimeoutMs))
                {
                    buttonPtr->ClickAddonButton(SelectYesno);
                }
            }
        }
    }

    private void WarmReset()
    {
        _setupDone = false;
        _delayTime = long.MinValue;
        _leveData.Clear();
    }

    private void SetUp()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(WksMissionAddon, out var addon) ||
            addon == null ||
            !addon->IsReady())
        {
            WarmReset();
            _delayTime = Environment.TickCount64 + 500;
            return;
        }

        if (!SearchNode(addon, 8, out var btnData) || btnData.ResNode->IsVisible())
        {
            WarmReset();
            _delayTime = Environment.TickCount64 + 500;
            return;
        }

        if (_leveData.Count == 0 && _delayTime < Environment.TickCount64)
        {
            _leveData.Clear();
            _delayTime = long.MinValue;

            var treeComp = (AtkComponentTreeList*)addon->GetComponentByNodeId(18);
            if (treeComp == null) return;
            var treeCompRootNode = treeComp->OwnerNode;
            if (treeCompRootNode == null) return;
            var NodeList = treeComp->UldManager.NodeList;
            int index = 0;
            int prevRank = 0;
            for (var i = 0; i < treeComp->UldManager.NodeListCount; i++)
            {
                var node = NodeList[i];
                if (node == null) continue;
                if (node->Type != (NodeType)1027) continue;
                var comp = (AtkComponentListItemRenderer*)node->GetComponent();
                if (comp == null)
                {
                    ImGuiEx.Text(EzColor.RedBright, "Component not found");
                    continue;
                }
                var text = comp->ButtonTextNode;
                if (text == null || text->GetText() == "") break;
                var wKSMissionUnitData = Svc.Data.GetExcelSheet<WKSMissionUnit>()
                    .FirstOrNull(x => x.Name.ToString() == text->GetText().ToString()
                        && x.GoldStarRequirement == (ushort)(Player.Job + 1));
                if (wKSMissionUnitData == null) continue;
                var texNode = (AtkImageNode*)SearchResNodeOnly(new NodeData(node, (AtkComponentBase*)comp), 6);
                if (texNode == null)
                {
                    InternalLog.Error($"goldTexNode not found");
                    continue;
                }
                Achevement achevement = Achevement.None;
                if (texNode->IsVisible())
                {
                    string texturePath = GetTexturePath(texNode);
                    if (texturePath == "")
                    {
                        InternalLog.Error($"goldTexNode texturePath not found");
                        continue;
                    }
                    if (texturePath.Contains("WKSMission_hr1.tex") && (texNode->PartId == 19))
                    {
                        achevement = Achevement.Silver;
                    }
                    if (texturePath.Contains("WKSMission_hr1.tex") && (texNode->PartId == 18))
                    {
                        achevement = Achevement.Gold;
                    }
                }
                index++;
                if (wKSMissionUnitData.Value.LevelGroup != prevRank)
                {
                    if (prevRank != 0)
                    {
                        if (!((prevRank == 4 && wKSMissionUnitData.Value.LevelGroup == 5) ||
                              (prevRank == 5 && wKSMissionUnitData.Value.LevelGroup == 4)))
                        {
                            index++;
                        }
                    }
                    prevRank = wKSMissionUnitData.Value.LevelGroup;
                }
                _leveData.Add(new LeveData(
                    index,
                    wKSMissionUnitData.Value.RowId,
                    wKSMissionUnitData.Value.LevelGroup,
                    text->GetText().ToString(),
                    node,
                    achevement));
            }
        }

        if (_leveData.Count != 0 && addon != null)
        {
            if (_delayTime <= Environment.TickCount64)
            {
                if (_delayTime == long.MinValue)
                {
                    foreach (var leve in _leveData)
                    {
                        if (leve.CosmoCredit == 0 &&
                            leve.MoonCredit == 0 &&
                            leve.TokenLv1 == 0 &&
                            leve.TokenLv2 == 0 &&
                            leve.TokenLv3 == 0 &&
                            leve.TokenLv4 == 0)
                        {
                            // 被擋下就這一輪不送、也不推遲 _delayTime，下一個 framework tick 原路再來。
                            if (!TryBeginPress(WksMissionAddon, (nint)addon, leve.PressKey, RoutineRePressTimeoutMs)) return;
                            leve.Select(addon);
                            _delayTime = Environment.TickCount64 + 300;
                            return;
                        }
                    }
                }
                foreach (var leve in _leveData)
                {
                    if (leve.CosmoCredit == 0 &&
                        leve.MoonCredit == 0 &&
                        leve.TokenLv1 == 0 &&
                        leve.TokenLv2 == 0 &&
                        leve.TokenLv3 == 0 &&
                        leve.TokenLv4 == 0)
                    {
                        leve.GetTokenCounts(addon);
                        _delayTime = long.MinValue;
                        return;
                    }
                }
                _delayTime = long.MaxValue;
                _setupDone = true;
            }
        }
    }

    private static string GetTexturePath([NotNull] AtkImageNode* imageNode)
    {
        var PartsListFirst = imageNode->PartsList[0];
        if (PartsListFirst.Parts->UldAsset == null) return "";
        if (PartsListFirst.Parts->UldAsset->AtkTexture.Resource == null) return "";
        if (PartsListFirst.Parts->UldAsset->AtkTexture.Resource->TexFileResourceHandle == null) return "";
        if (imageNode->PartsList[0].Parts->UldAsset->AtkTexture.TextureType == TextureType.Resource)
        {
            return imageNode->PartsList[0].Parts->
                UldAsset->AtkTexture.Resource->TexFileResourceHandle->FileName.ToString();
        }
        return "";
    }

    private static bool SearchNode(AtkUnitBase* WKSMissionAddon, uint nodeId, out NodeData nodeData)
    {
        nodeData = new NodeData(null, null);
        if (WKSMissionAddon == null || !WKSMissionAddon->IsReady()) return false;
        AtkResNode* resNode = null;
        for (int i = 0; i < (int)WKSMissionAddon->UldManager.NodeListCount; i++)
        {
            var node = WKSMissionAddon->UldManager.NodeList[i];
            if (node == null) continue;
            if (node->NodeId == nodeId)
            {
                resNode = (AtkResNode*)node;
                break;
            }
        }
        if (resNode == null) return false;
        if (resNode->GetComponent() == null) return false;
        nodeData.ResNode = resNode;
        nodeData.Component = resNode->GetComponent();
        return true;
    }

    private static bool SearchNode(NodeData nodeData, uint nodeId, out NodeData output)
    {
        output = new NodeData(null, null);
        if (!nodeData.HasData) return false;
        if (nodeData.Component->UldManager.NodeListCount == 0) return false;

        AtkResNode* resNode = null;
        for (int i = 0; i < (int)nodeData.Component->UldManager.NodeListCount; i++)
        {
            var node = nodeData.Component->UldManager.NodeList[i];
            if (node == null) continue;
            if (node->NodeId == nodeId)
            {
                resNode = node;
            }
        }
        if (resNode == null) return false;
        if (resNode->GetComponent() == null) return false;
        output.ResNode = resNode;
        output.Component = resNode->GetComponent();
        return true;
    }

    private static AtkResNode* SearchResNodeOnly(NodeData nodeData, uint nodeId)
    {
        if (!nodeData.HasData) return null;
        if (nodeData.Component->UldManager.NodeListCount == 0) return null;

        for (int i = 0; i < (int)nodeData.Component->UldManager.NodeListCount; i++)
        {
            var node = nodeData.Component->UldManager.NodeList[i];
            if (node == null) continue;
            if (node->NodeId == nodeId)
            {
                return node;
            }
        }

        return null;
    }
}
