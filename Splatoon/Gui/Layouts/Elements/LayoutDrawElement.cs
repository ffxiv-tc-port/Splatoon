using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.GameFunctions;
using ECommons.LanguageHelpers;
using Lumina.Excel.Sheets;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Splatoon.RenderEngines;
using Splatoon.Serializables;
using Splatoon.SplatoonScripting;

namespace Splatoon;

internal unsafe partial class CGui
{
    private string ActionName = "";
    private string BuffName = "";
    internal void LayoutDrawElement(Layout l, Element el, bool forceEnable = false)
    {
        ImGui.Checkbox("啟用".Loc(), ref el.Enabled);
        if(el.IsVisible())
        {
            ImGuiEx.HelpMarker("此元素目前正在渲染中".Loc(), EColor.GreenBright, FontAwesomeIcon.Eye.ToIconString());
        }
        else
        {
            ImGuiEx.HelpMarker("此元素目前未在渲染中".Loc(), EColor.White, FontAwesomeIcon.EyeSlash.ToIconString());
        }
        ImGui.SameLine();
        if(ImGui.Button("複製為 HTTP 參數".Loc()))
        {
            HTTPExportToClipboard(el);
        }
        if(ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("按住 ALT 複製原始 JSON（用於 POST body，否則需自行進行 URL 編碼）\n按住 CTRL 並點擊以複製已進行 URL 編碼的原始資料".Loc());
        }
        ImGui.SameLine();
        if(ImGui.Button("複製到剪貼簿".Loc()))
        {
            ImGui.SetClipboardText(JsonConvert.SerializeObject(el, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));
            Notify.Success("已複製到剪貼簿".Loc());
        }

        ImGui.SameLine();
        if(ImGui.Button("複製樣式".Loc()))
        {
            p.Clipboard = JsonConvert.DeserializeObject<Element>(JsonConvert.SerializeObject(el));
        }
        if(p.Clipboard != null)
        {
            ImGui.SameLine();
            if(ImGui.Button("貼上樣式".Loc()))
            {
                el.color = p.Clipboard.color;
                el.thicc = p.Clipboard.thicc;

                if(p.Clipboard.Filled)
                {
                    el.Filled = p.Clipboard.Filled;
                    el.fillIntensity = p.Clipboard.fillIntensity;
                    if(p.Clipboard.overrideFillColor)
                    {
                        el.overrideFillColor = p.Clipboard.overrideFillColor;
                        el.originFillColor = p.Clipboard.originFillColor;
                        el.endFillColor = p.Clipboard.endFillColor;
                    }
                }

                if(p.Clipboard.castAnimation != CastAnimationKind.Unspecified)
                {
                    el.castAnimation = p.Clipboard.castAnimation;
                    el.animationColor = p.Clipboard.animationColor;
                    el.pulseSize = p.Clipboard.pulseSize;
                    el.pulseFrequency = p.Clipboard.pulseFrequency;
                }

                el.overlayBGColor = p.Clipboard.overlayBGColor;
                el.overlayTextColor = p.Clipboard.overlayTextColor;
                el.tether = p.Clipboard.tether;
                el.ExtraTetherLength = p.Clipboard.ExtraTetherLength;
                el.LineEndA = p.Clipboard.LineEndA;
                el.LineEndB = p.Clipboard.LineEndB;
                el.overlayVOffset = p.Clipboard.overlayVOffset;
                if(ImGui.GetIO().KeyCtrl)
                {
                    el.radius = p.Clipboard.radius;
                    el.includeHitbox = p.Clipboard.includeHitbox;
                    el.includeOwnHitbox = p.Clipboard.includeOwnHitbox;
                    el.includeRotation = p.Clipboard.includeRotation;
                    el.onlyTargetable = p.Clipboard.onlyTargetable;
                }
                if(ImGui.GetIO().KeyShift && el.type != 2)
                {
                    el.refX = p.Clipboard.refX;
                    el.refY = p.Clipboard.refY;
                    el.refZ = p.Clipboard.refZ;
                }
            }
            if(ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGuiEx.Text("已複製樣式:".Loc());
                ImGuiEx.Text($"Color: 0x{p.Clipboard.color:X8}");
                ImGui.SameLine();
                ImGuiUtils.DisplayColor(p.Clipboard.color);
                ImGuiEx.Text($"Thickness: {p.Clipboard.thicc}");
                if(p.Clipboard.Filled)
                {
                    if(p.Clipboard.overrideFillColor)
                    {
                        ImGuiEx.Text($"Origin Fill Color: 0x{p.Clipboard.originFillColor:X8}");
                        ImGui.SameLine();
                        ImGuiUtils.DisplayColor(p.Clipboard.originFillColor ?? 0);
                        ImGuiEx.Text($"End Fill Color: 0x{p.Clipboard.endFillColor:X8}");
                        ImGui.SameLine();
                        ImGuiUtils.DisplayColor(p.Clipboard.endFillColor ?? 0);
                    }
                    else
                    {
                        ImGuiEx.Text($"Fill Intensity: {p.Clipboard.fillIntensity}");
                    }
                }
                if(p.Clipboard.castAnimation != CastAnimationKind.Unspecified)
                {
                    ImGuiEx.Text($"Animation: {CastAnimations.Names[(int)p.Clipboard.castAnimation]}");
                    ImGuiEx.Text($"Animation Color: 0x{p.Clipboard.animationColor:X8}");
                    ImGui.SameLine();
                    ImGuiUtils.DisplayColor(p.Clipboard.animationColor);
                    if(p.Clipboard.castAnimation == CastAnimationKind.Pulse)
                    {
                        ImGuiEx.Text($"Pulse Size: {p.Clipboard.pulseSize}");
                        ImGuiEx.Text($"Pulse Frequency: {p.Clipboard.pulseFrequency}");
                    }
                }
                ImGuiEx.Text($"Overlay BG color: 0x{p.Clipboard.overlayBGColor:X8}");
                ImGui.SameLine();
                ImGuiUtils.DisplayColor(p.Clipboard.overlayBGColor);
                ImGuiEx.Text($"Overlay text color: 0x{p.Clipboard.overlayTextColor:X8}");
                ImGui.SameLine();
                ImGuiUtils.DisplayColor(p.Clipboard.overlayTextColor);
                ImGuiEx.Text($"Overlay vertical offset: {p.Clipboard.overlayVOffset}");
                ImGuiEx.Text($"Tether: {p.Clipboard.tether}");
                ImGui.Separator();
                ImGuiEx.Text((ImGui.GetIO().KeyCtrl ? Colors.Green : Colors.Gray).ToVector4(),
                    "點擊時按住 CTRL 也會貼上:".Loc());
                ImGuiEx.Text($"Radius: {p.Clipboard.radius}");
                ImGuiEx.Text($"Include target hitbox: {p.Clipboard.includeHitbox}");
                ImGuiEx.Text($"Include own hitbox: {p.Clipboard.includeOwnHitbox}");
                ImGuiEx.Text($"Include rotation: {p.Clipboard.includeRotation}");
                ImGuiEx.Text($"Only targetable: {p.Clipboard.onlyTargetable}");
                ImGui.Separator();
                ImGuiEx.Text((ImGui.GetIO().KeyShift ? Colors.Green : Colors.Gray).ToVector4(),
                    "點擊時按住 SHIFT 也會貼上:".Loc());
                ImGuiEx.Text($"X offset: {p.Clipboard.offX}");
                ImGuiEx.Text($"Y offset: {p.Clipboard.offY}");
                ImGuiEx.Text($"Z offset: {p.Clipboard.offZ}");

                ImGui.EndTooltip();
            }
        }


        ImGuiUtils.SizedText("條件式:".Loc(), WidthElement);
        ImGui.SameLine();

        ImGui.SameLine();
        ImGui.Checkbox("##Conditional", ref el.Conditional);
        ImGuiEx.HelpMarker("""
            條件式元素會作為目前布局中，位於其下方所有元素的條件觸發器。
            若此元素可見，則下方元素也會被顯示。若有多個連續的條件式元素，會依照布局中定義的規則合併判斷。這是進階功能，可用於例如：玩家或首領身上存在特定 buff 時顯示元素，或首領施放特定招式時在玩家周圍繪製元素等。

            若在一般元素之後又遇到另一個條件式元素，會依照布局中定義的規則一起處理。例如：
            - 若元素結構為 A1B2，其中 A 和 B 為條件式元素，1 和 2 為一般元素：
            - - 在 OR 模式下，若 A 顯示但 B 隱藏，則 1 和 2 都會顯示；
            - - 在 AND 模式下，則只有 1 會顯示。
            - 若 A 隱藏但 B 顯示：
            - - OR 模式只會顯示 2；
            - - AND 模式則完全不顯示。

            條件式元素本身也可以作為一般元素使用並顯示資訊，或單純作為隱藏的輔助元素。
            """);
        ImGui.SameLine();
        ImGui.Checkbox("反轉條件", ref el.ConditionalInvert);
        ImGui.SameLine();
        ImGui.Checkbox("重置條件", ref el.ConditionalReset);
        ImGuiEx.HelpMarker("到達此元素時，先前的條件將會被重置");

        ImGuiUtils.SizedText("名稱:".Loc(), WidthElement);
        ImGui.SameLine();
        ImGuiEx.SetNextItemFullWidth();
        ImGui.InputText("##Name", ref el.Name, 100);

        ImGuiUtils.SizedText("國際化名稱:".Loc(), WidthElement);
        ImGui.SameLine();
        ImGuiEx.SetNextItemFullWidth();
        el.InternationalName.ImGuiEdit(ref el.Name);

        ImGuiUtils.SizedText("元素類型:".Loc(), WidthElement);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(WidthCombo);
        if(ImGui.Combo("##elemselecttype", ref el.type, Element.ElementTypes, Element.ElementTypes.Length))
        {
            if((el.type == 2 || el.type == 3) && el.radius == 0.35f)
            {
                el.radius = 0;
            }
        }
        if(el.type.EqualsAny(4, 5))
        {
            el.includeRotation = true;
        }
        if(el.type.EqualsAny(1, 3, 4, 5))
        {
            ImGuiUtils.SizedText("考慮旋轉角度:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##rota", ref el.includeRotation);
            if(el.includeRotation)
            {
                DrawRotationSelector(el);
            }
            ImGuiUtils.SizedText("覆寫旋轉:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##rotaOverride", ref el.RotationOverride);
            if(el.RotationOverride)
            {
                ImGui.SameLine();
                ImGuiEx.TextV("旋轉朝向:");
                ImGui.SameLine();
                ImGuiEx.Text($"X:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##rotateTowardsX", ref el.RotationOverridePoint.X, 0.1f);
                ImGui.SameLine();
                ImGuiEx.Text($"Y:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##rotateTowardsY", ref el.RotationOverridePoint.Y, 0.1f);
                ImGui.SameLine();
                ImGuiEx.Text($"Add angle:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##rotationOverrideAddAngle", ref el.RotationOverrideAddAngle, 0.1f);
            }
        }
        if(el.type.EqualsAny(1, 3, 4))
        {

            ImGuiUtils.SizedText("目標物件: ".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(WidthCombo);
            ImGui.Combo("##actortype", ref el.refActorType, Element.ActorTypes, Element.ActorTypes.Length);
            if(el.refActorType == 0)
            {
                ImGui.SameLine();
                if(ImGui.Button("複製 settarget 指令".Loc()))
                {
                    ImGui.SetClipboardText("/splatoon settarget " + l.Name + "~" + el.Name);
                }
                if(ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("此指令可讓您快速將\n搜尋屬性變更為您目前目標的名稱。\n可搭配巨集使用。".Loc());
                }
                ImGui.SetNextItemWidth(WidthElement + ImGui.GetStyle().ItemSpacing.X);
                if(ImGui.BeginCombo("##compare", el.refActorComparisonAnd ? "多個屬性".Loc() : "單一屬性".Loc()))
                {
                    if(ImGui.Selectable("符合單一屬性".Loc()))
                    {
                        el.refActorComparisonAnd = false;
                    }
                    if(ImGui.Selectable("符合多個屬性（AND 邏輯）".Loc()))
                    {
                        el.refActorComparisonAnd = true;
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(75f);
                ImGui.Combo($"##attrSelect", ref el.refActorComparisonType, Element.ComparisonTypes, Element.ComparisonTypes.Length);
                ImGui.SameLine();
                if(el.refActorComparisonType == 0)
                {
                    ImGui.SetNextItemWidth(150f);
                    //ImGui.InputText("##actorname", ref el.refActorName, 100);
                    el.refActorNameIntl.ImGuiEdit(ref el.refActorName);
                    if(NameNpcIDs.TryGetValue(el.refActorNameIntl.Get(el.refActorName).ToLower(), out var nameid))
                    {
                        ImGui.SameLine();
                        if(ImGui.Button($"Name ID: ??, convert?".Loc(nameid.Format()) + "##{i + k}"))
                        {
                            el.refActorComparisonType = 6;
                            el.refActorNPCNameID = nameid;
                        }
                        ImGuiComponents.HelpMarker("已為此字串找到對應的 Name ID。若您將比對方式轉換為 Name ID，此元素將可在任何語言下正常運作。此外，這樣的轉換也能提升效能。\n\n以 Name ID 選取只會針對角色（Character）（通常沒問題）。若您的目標是 GameObject、EventObj 或 EventNpc，請勿轉換。".Loc());
                    }
                }
                else if(el.refActorComparisonType == 1)
                {
                    ImGuiUtils.InputUintDynamic("##actormid", ref el.refActorModelID);
                }
                else if(el.refActorComparisonType == 2)
                {
                    ImGuiUtils.InputUintDynamic("##actoroid", ref el.refActorObjectID);
                }
                else if(el.refActorComparisonType == 3)
                {
                    ImGuiUtils.InputUintDynamic("##actordid", ref el.refActorDataID);
                }
                else if(el.refActorComparisonType == 4)
                {
                    ImGuiUtils.InputUintDynamic("##npcid", ref el.refActorNPCID);
                }
                else if(el.refActorComparisonType == 5)
                {
                    ImGui.SetNextItemWidth(200f);
                    ImGuiEx.InputListString("##pholder", el.refActorPlaceholder);
                    ImGui.SameLine();
                    if(ImGuiEx.IconButton(FontAwesomeIcon.AngleDoubleDown))
                    {
                        ImGui.OpenPopup("PlaceholderFastSelect");
                    }
                    if(ImGui.BeginPopup("PlaceholderFastSelect"))
                    {
                        for(var s = 1; s <= 8; s++)
                        {
                            if(ImGui.Selectable($"<{s}>", false, ImGuiSelectableFlags.DontClosePopups)) el.refActorPlaceholder.Add($"<{s}>");
                        }
                        if(ImGui.Selectable("2-8", false, ImGuiSelectableFlags.DontClosePopups))
                        {
                            for(var s = 2; s <= 8; s++)
                            {
                                el.refActorPlaceholder.Add($"<{s}>");
                            }
                        }
                        ImGui.EndPopup();
                    }
                    ImGuiComponents.HelpMarker(("Placeholder like you'd type in macro <1>, <2>, <mo> etc. You can add multiple." +
                        "\nAdditional placeholders are supported:" +
                        "\n<d1>, <d2>, <d3> etc - DPS player in a party" +
                        "\n<h1>, <h2> etc - Healer player in a party" +
                        "\n<t1>, <t2> etc - Tank player in a party" +
                        "\nNumber corresponds to the party list.").Loc());
                }
                else if(el.refActorComparisonType == 6)
                {
                    ImGuiUtils.InputUintDynamic("##nameID", ref el.refActorNPCNameID);
                    var npcnames = NameNpcIDsAll.FindKeysByValue(el.refActorNPCNameID);
                    if(npcnames.Any())
                    {
                        ImGuiComponents.HelpMarker($"{"NPC".Loc()}: \n{npcnames.Join("\n")}");
                    }
                }
                else if(el.refActorComparisonType == 7)
                {
                    ImGui.SetNextItemWidth(150f);
                    ImGui.InputText("##vfx", ref el.refActorVFXPath, 500);
                    ImGui.SameLine();
                    ImGuiEx.Text("存在時間:".Loc());
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    var a1 = (float)el.refActorVFXMin / 1000f;
                    if(ImGui.DragFloat("##age1", ref a1, 0.1f, 0, 99999, $"{a1:F1}"))
                    {
                        el.refActorVFXMin = (int)(a1 * 1000);
                    }
                    ImGui.SameLine();
                    ImGuiEx.Text("-");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);

                    var a2 = (float)el.refActorVFXMax / 1000f;
                    if(ImGui.DragFloat("##age2", ref a2, 0.1f, 0, 99999, $"{a2:F1}"))
                    {
                        el.refActorVFXMax = (int)(a2 * 1000);
                    }
                }
                else if(el.refActorComparisonType == 8)
                {
                    ImGui.SetNextItemWidth(50f);
                    ImGuiEx.InputUint("##edata1", ref el.refActorObjectEffectData1);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGuiEx.InputUint("##edata2", ref el.refActorObjectEffectData2);
                    ImGui.SameLine();
                    ImGui.Checkbox($"Last only", ref el.refActorObjectEffectLastOnly);
                    if(!el.refActorObjectEffectLastOnly)
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text("存在時間:".Loc());
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(50f);
                        var a1 = (float)el.refActorObjectEffectMin / 1000f;
                        if(ImGui.DragFloat("##eage1", ref a1, 0.1f, 0, 99999, $"{a1:F1}"))
                        {
                            el.refActorObjectEffectMin = (int)(a1 * 1000);
                        }
                        ImGui.SameLine();
                        ImGuiEx.Text("-");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(50f);

                        var a2 = (float)el.refActorObjectEffectMax / 1000f;
                        if(ImGui.DragFloat("##eage2", ref a2, 0.1f, 0, 99999, $"{a2:F1}"))
                        {
                            el.refActorObjectEffectMax = (int)(a2 * 1000);
                        }
                    }
                }
                else if(el.refActorComparisonType == 9)
                {
                    ImGui.SetNextItemWidth(200f);
                    ImGuiEx.InputUint("##nameplateiconid", ref el.refActorNamePlateIconID);
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("十進位輸入");
                    }
                }

                if(Svc.Targets.Target != null && !el.refActorComparisonType.EqualsAny(7, 8))
                {
                    ImGui.SameLine();
                    if(ImGui.Button("目標".Loc() + "##btarget"))
                    {
                        el.refActorNameIntl.CurrentLangString = Svc.Targets.Target.Name.ToString();
                        el.refActorDataID = Svc.Targets.Target.DataId;
                        el.refActorObjectID = Svc.Targets.Target.EntityId;
                        if(Svc.Targets.Target is ICharacter c)
                        {
                            el.refActorModelID = (uint)c.Struct()->ModelContainer.ModelCharaId;
                            el.refActorNPCNameID = c.NameId;
                        }
                        el.refActorNPCID = Svc.Targets.Target.Struct()->GetNameId();
                        el.refActorNamePlateIconID = Svc.Targets.Target.Struct()->NamePlateIconId;
                    }
                }
                ImGuiUtils.SizedText("可鎖定性: ".Loc(), WidthElement);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                if(ImGui.BeginCombo($"##TargetabilityCombo", el.onlyTargetable ? "可鎖定".Loc() : (el.onlyUnTargetable ? "不可鎖定".Loc() : "任意".Loc())))
                {
                    if(ImGui.Selectable("任意".Loc()))
                    {
                        el.onlyTargetable = false;
                        el.onlyUnTargetable = false;
                    }
                    if(ImGui.Selectable("僅可鎖定".Loc()))
                    {
                        el.onlyTargetable = true;
                        el.onlyUnTargetable = false;
                    }
                    if(ImGui.Selectable("僅不可鎖定".Loc()))
                    {
                        el.onlyTargetable = false;
                        el.onlyUnTargetable = true;
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.Checkbox("僅限可見角色".Loc(), ref el.onlyVisible);
                if(ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("勾選此項也會將搜尋範圍限制為「僅限角色」。\n（角色 - 指玩家、寵物或可戰鬥且擁有 HP 的友方/敵對 NPC）".Loc());
                }
            }

            ImGuiUtils.SizedText("物件種類:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(WidthCombo);
            if(ImGui.BeginCombo("##objectKindSel", el.ObjectKinds.Count == 0 ? "Any" : el.ObjectKinds.Print(), ImGuiComboFlags.HeightLarge))
            {
                if(ImGui.Button("全選".Loc())) el.ObjectKinds.AddRange(Enum.GetValues<ObjectKind>());
                ImGui.SameLine();
                if(ImGui.Button("取消全選".Loc())) el.ObjectKinds.Clear();
                foreach(var x in Enum.GetValues<ObjectKind>())
                {
                    ImGuiEx.CollectionCheckbox($"{x}", x, el.ObjectKinds);
                }
                ImGui.EndCombo();
            }

            ImGui.SetNextItemWidth(WidthElement + ImGui.GetStyle().ItemSpacing.X);
            if(ImGui.BeginCombo("##whilecasting", el.refActorCastReverse ? "非施法中".Loc() : "施法中".Loc()))
            {
                if(ImGui.Selectable("施法中".Loc())) el.refActorCastReverse = false;
                if(ImGui.Selectable("非施法中".Loc())) el.refActorCastReverse = true;
                ImGui.Separator();
                if(ImGui.Selectable("從剪貼簿貼上##castinfo"))
                {
                    try
                    {
                        var pasted = JsonConvert.DeserializeObject<Element>(Paste()) ?? throw new NullReferenceException();
                        el.refActorCastReverse = pasted.refActorCastReverse;
                        el.refActorRequireCast = pasted.refActorRequireCast;
                        el.refActorCastId = pasted.refActorCastId ?? throw new NullReferenceException();
                        el.refActorUseOvercast = pasted.refActorUseOvercast;
                        el.refActorCastTimeMax = pasted.refActorCastTimeMax;
                        el.refActorCastTimeMin = pasted.refActorCastTimeMin;
                    }
                    catch(Exception e)
                    {
                        e.Log();
                        Notify.Error(e.Message);
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.Checkbox("##casting", ref el.refActorRequireCast);
            if(el.refActorRequireCast)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(WidthCombo);
                ImGuiEx.InputListUint("##casts", el.refActorCastId, ActionNames);
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("依名稱全部加入:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputText("##ActionName", ref ActionName, 100);
                ImGui.SameLine();
                if(ImGui.Button("新增".Loc() + "##byactionname"))
                {
                    foreach(var lang in (ClientLanguage?[])[null, ClientLanguage.English])
                    {
                        foreach(var x in Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>(lang))
                        {
                            if(x.Name.ToString().Equals(ActionName, StringComparison.OrdinalIgnoreCase))
                            {
                                el.refActorCastId.Add(x.RowId);
                            }
                        }
                    }
                }
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("以施法時間限制".Loc(), ref el.refActorUseCastTime);
                if(el.refActorUseCastTime)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##casttime1", ref el.refActorCastTimeMin, 0.1f, 0f, 99999f, $"{el.refActorCastTimeMin:F1}");
                    ImGui.SameLine();
                    ImGuiEx.Text("-");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##casttime2", ref el.refActorCastTimeMax, 0.1f, 0f, 99999f, $"{el.refActorCastTimeMax:F1}");
                    ImGui.SameLine();
                    ImGui.Checkbox("過度施法".Loc(), ref el.refActorUseOvercast);
                    ImGuiComponents.HelpMarker("啟用超過施法時間的施法數值，效果如同施法條在施法結束後仍繼續顯示".Loc());
                }
            }

            ImGuiUtils.SizedText("狀態需求:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##buffreq", ref el.refActorRequireBuff);
            if(el.refActorRequireBuff)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(WidthCombo);
                ImGuiEx.InputListUint("##buffs", el.refActorBuffId, BuffNames);
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("依名稱全部加入:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputText("##BuffNames", ref BuffName, 100);
                ImGui.SameLine();
                if(ImGui.Button("新增".Loc() + "##bybuffname"))
                {
                    foreach(var lang in (ClientLanguage?[])[null, ClientLanguage.English])
                    {
                        foreach(var x in Svc.Data.GetExcelSheet<Status>(lang))
                        {
                            if(x.Name.ToString().Equals(BuffName, StringComparison.OrdinalIgnoreCase))
                            {
                                el.refActorBuffId.Add(x.RowId);
                            }
                        }
                    }
                }
                if(Svc.Targets.Target != null && Svc.Targets.Target is IBattleChara bchr)
                {
                    ImGui.SameLine();
                    if(ImGui.Button("從目標新增".Loc() + "##bybuffname"))
                    {
                        el.refActorBuffId.AddRange(bchr.StatusList.Select(x => x.StatusId));
                    }
                }
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("以剩餘時間限制".Loc(), ref el.refActorUseBuffTime);
                if(el.refActorUseBuffTime)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##btime1", ref el.refActorBuffTimeMin, 0.1f, 0f, 99999f, $"{el.refActorBuffTimeMin:F1}");
                    ImGui.SameLine();
                    ImGuiEx.Text("-");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##btime2", ref el.refActorBuffTimeMax, 0.1f, 0f, 99999f, $"{el.refActorBuffTimeMax:F1}");
                }
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("檢查狀態參數".Loc(), ref el.refActorUseBuffParam);
                if(el.refActorUseBuffParam)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(150f);
                    ImGui.InputInt("##btime1", ref el.refActorBuffParam);
                }
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox((el.refActorRequireBuffsInvert ? "要求任一狀態不存在".Loc() + "##" : "要求所列全部狀態皆存在".Loc() + "##"), ref el.refActorRequireAllBuffs);
                ImGui.SameLine();
                ImGui.Checkbox("反轉行為".Loc(), ref el.refActorRequireBuffsInvert);
            }

            ImGuiUtils.SizedText("距離限制".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##dstLim", ref el.LimitDistance);
            if(el.LimitDistance)
            {
                ImGui.SameLine();
                ImGuiEx.Text("X:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##distX", ref el.DistanceSourceX, 0.02f, float.MinValue, float.MaxValue);
                ImGui.SameLine();
                ImGuiEx.Text("Y:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##distY", ref el.DistanceSourceY, 0.02f, float.MinValue, float.MaxValue);
                ImGui.SameLine();
                ImGuiEx.Text("Z:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##distZ", ref el.DistanceSourceZ, 0.02f, float.MinValue, float.MaxValue);
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.Circle, "0 0 0##dist"))
                {
                    el.DistanceSourceX = 0;
                    el.DistanceSourceY = 0;
                    el.DistanceSourceZ = 0;
                }
                ImGuiEx.Tooltip("0 0 0");
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.MapMarked, "我的位置".Loc() + "##dist"))
                {
                    el.DistanceSourceX = Utils.GetPlayerPositionXZY().X;
                    el.DistanceSourceY = Utils.GetPlayerPositionXZY().Y;
                    el.DistanceSourceZ = Utils.GetPlayerPositionXZY().Z;
                }
                ImGuiEx.Tooltip("My position");
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.MousePointer, "螢幕轉世界座標".Loc() + "##dist"))
                {
                    SetCursorTo(el.DistanceSourceX, el.DistanceSourceY, el.DistanceSourceZ);
                    p.BeginS2W(el, "DistanceSourceX", "DistanceSourceY", "DistanceSourceZ");
                }
                ImGuiEx.Tooltip("在螢幕上選取".Loc());
                ImGui.SameLine();
                DrawRounding(ref el.DistanceSourceX, ref el.DistanceSourceY, ref el.DistanceSourceZ);
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##dstmin", ref el.DistanceMin, 0.1f, 0f, 99999f, $"{el.DistanceMin:F1}");
                ImGui.SameLine();
                ImGuiEx.Text("-");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##dstmax", ref el.DistanceMax, 0.1f, 0f, 99999f, $"{el.DistanceMax:F1}");
                ImGui.SameLine();
                ImGui.Checkbox("反轉".Loc() + "##dist", ref el.LimitDistanceInvert);
            }


            ImGuiUtils.SizedText("旋轉限制".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##rotaLimit", ref el.LimitRotation);
            if(el.LimitRotation)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                var rot1 = 180 - el.RotationMin.RadiansToDegrees();
                if(ImGui.DragFloat("##rotamax", ref rot1, 0.1f, -360f, 360f, $"{rot1:F1}"))
                {
                    el.RotationMin = (180 - rot1).DegreesToRadians();
                }

                ImGui.SameLine();
                ImGuiEx.Text("-");
                ImGui.SameLine();


                ImGui.SetNextItemWidth(50f);
                var rot2 = 180 - el.RotationMax.RadiansToDegrees();
                if(ImGui.DragFloat("##rotamin", ref rot2, 0.1f, -360f, 360f, $"{rot2:F1}"))
                {
                    el.RotationMax = (180 - rot2).DegreesToRadians();
                }
            }

            if(el.refActorType == 0)
            {
                ImGuiUtils.SizedText("物件存在時間:".Loc(), WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("##life", ref el.refActorObjectLife);
                if(el.refActorObjectLife)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##life1", ref el.refActorLifetimeMin, 0.1f, 0f, float.MaxValue);
                    ImGui.SameLine();
                    ImGuiEx.Text("-");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    ImGui.DragFloat("##life2", ref el.refActorLifetimeMax, 0.1f, 0f, float.MaxValue);
                    ImGui.SameLine();
                    ImGuiEx.Text("（單位：秒）".Loc());
                }
            }

            ImGuiUtils.SizedText("變身 ID:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##trans", ref el.refActorUseTransformation);
            if(el.refActorUseTransformation)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputInt("##transid", ref el.refActorTransformationID);
            }

            ImGuiUtils.SizedText("頭頂標記:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##marks", ref el.refMark);
            if(el.refMark)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                string[] markOptions = { "攻擊1".Loc(), "攻擊2".Loc(), "攻擊3".Loc(), "攻擊4".Loc(), "攻擊5".Loc(), "禁止1".Loc(), "禁止2".Loc(), "禁止3".Loc(), "停止1".Loc(), "停止2".Loc(), "方形".Loc(), "圓形".Loc(), "叉".Loc(), "三角".Loc(), "攻擊6".Loc(), "攻擊7".Loc(), "攻擊8".Loc() };
                if(ImGui.BeginCombo("##marks type", markOptions[el.refMarkID]))
                {
                    for(var j = 0; j < markOptions.Length; j++)
                    {
                        if(ImGui.Selectable(markOptions[j]))
                        {
                            el.refMarkID = j;
                        }
                    }
                    ImGui.EndCombo();
                }
            }

            ImGuiUtils.SizedText("正鎖定您:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox($"##targetYou", ref el.refTargetYou);
            if(el.refTargetYou)
            {
                ImGui.SameLine();
                if(ImGui.RadioButton("否".Loc(), el.refActorTargetingYou == 1))
                {
                    el.refActorTargetingYou = 1;
                }
                ImGui.SameLine();
                if(ImGui.RadioButton("是".Loc(), el.refActorTargetingYou == 2))
                {
                    el.refActorTargetingYou = 2;
                }
            }
            ImGuiUtils.SizedText("繩鏈資訊:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.Checkbox("##tether", ref el.refActorTether);
            if(el.refActorTether)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##tetherlife1", ref el.refActorTetherTimeMin, 0.1f, 0f, float.MaxValue);
                ImGui.SameLine();
                ImGuiEx.Text("-");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##tetherlife2", ref el.refActorTetherTimeMax, 0.1f, 0f, float.MaxValue);
                ImGui.SameLine();
                ImGuiEx.Text("（單位：秒）".Loc());

                ImGuiUtils.SizedText("         " + "參數:".Loc(), WidthElement);
                ImGui.SameLine();
                ImGuiEx.InputInt(100f, "##param1", ref el.refActorTetherParam1);
                ImGui.SameLine();
                ImGuiEx.InputInt(100f, "##param2", ref el.refActorTetherParam2);
                ImGui.SameLine();
                ImGuiEx.InputInt(100f, "##param3", ref el.refActorTetherParam3);

                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Checkbox("來源", ref el.refActorIsTetherSource);
                ImGuiEx.HelpMarker("勾選 - 僅檢查物件是否為繩鏈來源；未勾選 - 僅檢查物件是否為繩鏈目標；點狀 - 檢查物件是否為繩鏈來源或目標其中之一。");
                ImGui.SameLine();
                ImGui.Checkbox("反轉條件##tether", ref el.refActorIsTetherInvert);

                ImGuiUtils.SizedText("         " + "連結對象:".Loc(), WidthElement);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200f);
                ImGuiEx.InputListString("##pholderConnectedWith", el.refActorTetherConnectedWithPlayer);
                ImGui.SameLine();
                ImGuiEx.Text("空白 = 任意對象");
            }
        }

        if(el.type.EqualsAny(0, 2, 3, 5))
        {
            ImGuiUtils.SizedText((el.type == 2 || el.type == 3) ? "A 點".Loc() : "參考位置: ".Loc(), WidthElement);
            ImGui.SameLine();
            ImGuiEx.Text("X:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##refx", ref el.refX, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            ImGuiEx.Text("Y:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##refy", ref el.refY, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            ImGuiEx.Text("Z:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##refz", ref el.refZ, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            if(ImGuiEx.IconButton(FontAwesomeIcon.Copy))
            {
                ImGui.SetClipboardText(JsonConvert.SerializeObject(new Vector3(el.refX, el.refZ, el.refY)));
            }
            ImGui.SameLine();
            if(ImGuiEx.IconButton(FontAwesomeIcon.Paste))
            {
                try
                {
                    var v = JsonConvert.DeserializeObject<Vector3>(ImGui.GetClipboardText());
                    el.refX = v.X;
                    el.refY = v.Z;
                    el.refZ = v.Y;
                }
                catch(Exception e)
                {
                    e.Log();
                    Notify.Error(e.Message);
                }
            }
            ImGui.SameLine();
            if(ImGuiEx.IconButton(FontAwesomeIcon.Circle, "0 0 0##ref"))
            {
                el.refX = 0;
                el.refY = 0;
                el.refZ = 0;
            }
            ImGuiEx.Tooltip("0 0 0");
            if(el.type != 3)
            {
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.MapMarked, "我的位置".Loc() + "##ref"))
                {
                    el.refX = Utils.GetPlayerPositionXZY().X;
                    el.refY = Utils.GetPlayerPositionXZY().Y;
                    el.refZ = Utils.GetPlayerPositionXZY().Z;
                }
                ImGuiEx.Tooltip("我的位置".Loc());
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.MousePointer, "螢幕轉世界座標".Loc() + "##s2w1"))
                {
                    if(el.IsVisible())
                    {
                        SetCursorTo(el.refX, el.refZ, el.refY);
                        p.BeginS2W(el, "refX", "refY", "refZ");
                    }
                    else
                    {
                        Notify.Error("無法用於隱藏的元素".Loc());
                    }
                }
                ImGuiEx.Tooltip("在螢幕上選取".Loc());
                ImGui.SameLine();
                DrawRounding(ref el.refX, ref el.refY, ref el.refZ);
            }

            if(el.type.EqualsAny(1, 3) && el.includeRotation)
            {
                ImGui.SameLine();
                ImGuiEx.Text("角度: ".Loc() + Utils.RadToDeg(Utils.AngleBetweenVectors(0, 0, 10, 0, el.type == 1 ? 0 : el.refX, el.type == 1 ? 0 : el.refY, el.offX, el.offY)));
            }

            if((el.type == 3) && el.refActorType != 1)
            {
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("+我方碰撞箱 (XYZ):".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxXam", ref el.LineAddPlayerHitboxLengthXA);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxYam", ref el.LineAddPlayerHitboxLengthYA);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxZam", ref el.LineAddPlayerHitboxLengthZA);
                ImGui.SameLine();
                ImGuiEx.Text("+目標碰撞箱 (XYZ):".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxXa", ref el.LineAddHitboxLengthXA);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxYa", ref el.LineAddHitboxLengthYA);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxZa", ref el.LineAddHitboxLengthZA);
            }
        }

        if(true)
        {

            ImGuiUtils.SizedText((el.type == 2 || el.type == 3) ? "B 點".Loc() : "偏移: ".Loc(), WidthElement);
            ImGui.SameLine();
            ImGuiEx.Text("X:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##offx", ref el.offX, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            ImGuiEx.Text("Y:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##offy", ref el.offY, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            ImGuiEx.Text("Z:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##offz", ref el.offZ, 0.02f, float.MinValue, float.MaxValue);
            ImGui.SameLine();
            if(ImGuiEx.IconButton(FontAwesomeIcon.Circle, "0 0 0##off"))
            {
                el.offX = 0;
                el.offY = 0;
                el.offZ = 0;
            }
            ImGuiEx.Tooltip("0 0 0");
            if(el.type == 2)
            {
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.MapMarked, "我的位置".Loc() + "##off"))
                {
                    el.offX = Utils.GetPlayerPositionXZY().X;
                    el.offY = Utils.GetPlayerPositionXZY().Y;
                    el.offZ = Utils.GetPlayerPositionXZY().Z;
                }
                ImGuiEx.Tooltip("我的位置".Loc());
            }
            if((el.type == 3) && el.refActorType != 1)
            {
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("+我方碰撞箱 (XYZ):".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxXm", ref el.LineAddPlayerHitboxLengthX);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxYm", ref el.LineAddPlayerHitboxLengthY);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxZm", ref el.LineAddPlayerHitboxLengthZ);
                ImGui.SameLine();
                ImGuiEx.Text("+目標碰撞箱 (XYZ):".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxX", ref el.LineAddHitboxLengthX);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxY", ref el.LineAddHitboxLengthY);
                ImGui.SameLine();
                ImGui.Checkbox($"##lineTHitboxZ", ref el.LineAddHitboxLengthZ);
            }
        }

        if(el.type.EqualsAny(4, 5))
        {
            ImGuiUtils.SizedText("角度:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragInt("##angle", ref el.coneAngleMin, 0.1f);
            ImGui.SameLine();
            ImGuiEx.Text("-");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50f);
            ImGui.DragInt("##angle2", ref el.coneAngleMax, 0.1f);
        }

        //ImGui.SameLine();
        //ImGui.Checkbox("Actor relative##rota"+i+k, ref el.includeRotation);
        if(el.type == 2)
        {
            ImGui.SameLine();
            if(ImGuiEx.IconButton(FontAwesomeIcon.MousePointer, "螢幕轉世界座標".Loc() + "##s2w2"))
            {
                if(LayoutUtils.IsLayoutVisible(l) && (el.Enabled || forceEnable)/* && p.CamAngleY <= p.Config.maxcamY*/)
                {
                    SetCursorTo(el.offX, el.offZ, el.offY);
                    p.BeginS2W(el, "offX", "offY", "offZ");
                }
                else
                {
                    Notify.Error("無法用於隱藏的元素".Loc());
                }
            }
            ImGuiEx.Tooltip("在螢幕上選取".Loc());
            ImGui.SameLine();
            DrawRounding(ref el.offX, ref el.offY, ref el.offZ);
        }

        var style = el.GetDisplayStyle();
        if(ImGuiUtils.StyleEdit("Style", ref style))
        {
            el.SetDisplayStyle(style);
        }
        using(ImRaii.Disabled(!el.Filled))
        {
            if(el.type.EqualsAny(1, 3, 4) && el.Filled)
            {
                var canSetCastAnimation = el.refActorRequireCast && el.ConfiguredRenderEngineKind() == RenderEngineKind.DirectX11;
                using(ImRaii.Disabled(!canSetCastAnimation))
                {
                    ImGuiUtils.SizedText("施法動畫:".Loc(), WidthElement);
                    ImGui.SameLine();
                }
                ImGuiEx.HelpMarker("為此元素選擇施法動畫。需勾選「施法中」。\n舊版 ImGui 渲染器不支援此功能");
                ImGui.SameLine();
                using(ImRaii.Disabled(!canSetCastAnimation))
                {
                    ImGui.SetNextItemWidth(WidthElement);
                    ImGuiUtils.EnumCombo("##castanimation", ref el.castAnimation, CastAnimations.Names, CastAnimations.Tooltips);
                    using(ImRaii.Disabled(el.castAnimation is CastAnimationKind.Unspecified))
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text("顏色:".Loc());
                        ImGui.SameLine();
                        var v4 = ImGui.ColorConvertU32ToFloat4(el.animationColor);
                        if(ImGui.ColorEdit4("##animationcolorbutton", ref v4, ImGuiColorEditFlags.NoInputs))
                        {
                            el.animationColor = ImGui.ColorConvertFloat4ToU32(v4);
                        }
                        ImGui.SameLine();
                        if(ImGui.Button("複製".Loc() + "##copyfromstroke"))
                        {
                            el.animationColor = style.strokeColor;
                        }
                        if(ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("複製筆畫顏色".Loc());
                        }
                        if(el.castAnimation is CastAnimationKind.Pulse)
                        {
                            ImGuiUtils.SizedText("脈衝:".Loc(), WidthElement);
                            ImGui.SameLine();

                            ImGuiEx.Text("大小:".Loc());
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(60f);
                            el.pulseSize = MathF.Min(el.pulseSize, el.EffectiveLength());
                            ImGui.DragFloat("##animationsize", ref el.pulseSize, 0.01f, 0.1f, el.EffectiveLength());
                            ImGui.SameLine();

                            ImGuiEx.Text("頻率（秒）:".Loc());
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(60f);
                            ImGui.DragFloat("##animationfreq", ref el.pulseFrequency, 0.01f, 1, 10);
                        }
                    }
                }
            }
        }
        if((el.type != 3) || el.includeRotation)
        {
            if(!(el.type == 3 && !el.includeRotation))
            {
                ImGuiUtils.SizedText("半徑:".Loc(), WidthElement);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##radius", ref el.radius, 0.01f, 0, float.MaxValue);
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("保持為 0 以繪製單點".Loc());
                if(el.type == 1 || (el.type == 3 && el.includeRotation) || el.type == 4)
                {
                    if(el.refActorType != 1)
                    {
                        ImGui.SameLine();
                        ImGui.Checkbox("+目標碰撞箱".Loc(), ref el.includeHitbox);
                    }
                    ImGui.SameLine();
                    ImGui.Checkbox("+我方碰撞箱".Loc(), ref el.includeOwnHitbox);
                    ImGui.SameLine();
                    ImGuiEx.Text("(?)");
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(("When the game tells you that ability A has distance D,\n" +
                            "in fact it means that you are allowed to execute\n" +
                            "ability A if distance between edge of your hitbox\n" +
                            "and enemy's hitbox is less or equal than distance D,\n" +
                            "that is for targeted abilities.\n" +
                            "If an ability is AoE, such check is performed between\n" +
                            "middle point of your character and edge of enemy's hitbox.\n\n" +
                            "Summary: if you are trying to make targeted ability indicator -\n" +
                            "enable both \"+your hitbox\" and \"+target hitbox\".\n" +
                            "If you are trying to make AoE ability indicator - \n" +
                            "enable only \"+target hitbox\" to make indicators valid.").Loc());
                    }
                }
                if(el.type.EqualsAny(0, 1, 4, 5))
                {
                    ImGui.SameLine();
                    ImGuiEx.Text("圓環:".Loc());
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(60f);
                    ImGui.DragFloat("##radiusdonut", ref el.Donut, 0.01f, 0, float.MaxValue);
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Leave at 0 to not draw a donut.\n" +
                            "If greater than 0, the radius is the donut hole radius\n" +
                            "而此數值即為圓環的厚度。".Loc());
                    el.Donut.ValidateRange(0, float.MaxValue);
                }
            }
            if(el.type != 2 && el.type != 3)
            {
                ImGuiUtils.SizedText("繩鏈:".Loc(), WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("啟用##TetherEnable", ref el.tether);
                ImGui.SameLine();
                ImGuiEx.Text("額外長度:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##extratetherlength", ref el.ExtraTetherLength, 0.01f, 0, float.MaxValue);
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("為繩鏈加入額外長度以顯示擊退效果。".Loc());
            }
            var canSetLineEnds = el.tether ||
                ((el.type == 2 || el.type == 3) && el.radius == 0);
            if(!canSetLineEnds) ImGui.BeginDisabled();
            ImGuiUtils.SizedText("線條端點樣式:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGuiEx.Text("A: ".Loc());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGuiUtils.EnumCombo("##LineEndA", ref el.LineEndA, LineEnds.Names, LineEnds.Tooltips);
            ImGui.SameLine();
            ImGuiEx.Text("B: ".Loc());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGuiUtils.EnumCombo("##LineEndB", ref el.LineEndB, LineEnds.Names, LineEnds.Tooltips);
            if(!canSetLineEnds) ImGui.EndDisabled();
        }
        if(el.type == 0 || el.type == 1 || el.type == 4 || el.type == 5)
        {
            ImGuiUtils.SizedText("疊加文字:".Loc(), WidthElement);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            el.overlayTextIntl.ImGuiEdit(ref el.overlayText, "要顯示的疊加文字".Loc());
            if(el.overlayPlaceholders && el.type == 1)
            {
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.TextCopy("$NAME");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$OBJECTID");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$DATAID");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$MODELID");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$HITBOXR");
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.TextCopy("$KIND");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$NPCID");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$LIFE");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$NAMEID");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$DISTANCE");
                ImGui.SameLine();
                ImGuiEx.TextCopy("$TRANSFORM");
                ImGui.SameLine();
                ImGuiEx.TextCopy("\\n");
            }
            if(!el.overlayTextIntl.IsEmpty() || el.overlayText.Length > 0)
            {
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("垂直偏移:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##vtextadj", ref el.overlayVOffset, 0.02f);
                ImGui.SameLine();
                ImGuiEx.Text("字型縮放:".Loc());
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("##vtextsize", ref el.overlayFScale, 0.02f, 0.1f, 50f);
                if(el.overlayFScale < 0.1f) el.overlayFScale = 0.1f;
                if(el.overlayFScale > 50f) el.overlayFScale = 50f;

                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGuiEx.Text("背景色:".Loc());
                ImGui.SameLine();
                var v4b = ImGui.ColorConvertU32ToFloat4(el.overlayBGColor);
                if(ImGui.ColorEdit4("##colorbuttonbg", ref v4b, ImGuiColorEditFlags.NoInputs))
                {
                    el.overlayBGColor = ImGui.ColorConvertFloat4ToU32(v4b);
                }
                ImGui.SameLine();
                ImGuiEx.Text("文字顏色:".Loc());
                ImGui.SameLine();
                var v4t = ImGui.ColorConvertU32ToFloat4(el.overlayTextColor);
                if(ImGui.ColorEdit4("##colorbuttonfg", ref v4t, ImGuiColorEditFlags.NoInputs))
                {
                    el.overlayTextColor = ImGui.ColorConvertFloat4ToU32(v4t);
                }
            }
            if(el.type == 1)
            {
                ImGuiUtils.SizedText("", WidthElement);
                ImGui.SameLine();
                ImGui.Checkbox("啟用佔位符".Loc(), ref el.overlayPlaceholders);
            }
        }

        ImGui.Separator();

        ImGuiUtils.SizedText("渲染引擎:".Loc(), WidthElement);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.EnumCombo("##renderer", ref el.RenderEngineKind);

        ImGuiUtils.SizedText("機制類型:", WidthElement);
        ImGuiEx.HelpMarker("選擇最能代表此元素的機制類型。\n" +
                "此設定用於自動套用預設顏色。\n僅適用於 DirectX11 渲染器。");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(WidthElement);
        ImGuiUtils.EnumCombo("##mechtype", ref el.mechanicType, MechanicTypes.Names, MechanicTypes.Tooltips);

        if((el.type.EqualsAny(0, 1) && el.Donut > 0) || el.type == 4 || (el.type.EqualsAny(2, 3) && (el.radius > 0 || el.includeHitbox || el.includeOwnHitbox)))
        {
            ImGuiUtils.SizedText("填充間隔:".Loc(), WidthElement);
            ImGuiEx.HelpMarker("僅適用於舊版 ImGui 渲染器");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60f);
            ImGui.DragFloat("##fillstep", ref el.FillStep, 0.001f, 0, float.MaxValue);
            el.FillStep.ValidateRange(0.01f, float.MaxValue);
        }

    }
}
