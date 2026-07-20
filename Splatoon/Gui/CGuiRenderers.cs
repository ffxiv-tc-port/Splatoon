using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using ECommons.LanguageHelpers;
using NightmareUI.PrimaryUI;
using Pictomancy;
using Splatoon.RenderEngines;
using Splatoon.Serializables;
using System.Runtime.InteropServices;

namespace Splatoon;
internal partial class CGui
{
    private bool Tested = false;
    private void DisplayRenderers()
    {
        if(Utils.IsLinux())
        {
            new NuiBuilder()
                .Section("Mac OS/Linux detected")
                .Widget(() =>
                {
                    ImGuiEx.TextWrapped($"Mac OS or Linux operating system detected.");
                    if(P.Config.DX11EnabledOnMacLinux)
                    {
                        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Times, "Disable DirectX11 renderer on Mac OS/Linux"))
                        {
                            P.Config.DX11EnabledOnMacLinux = false;
                        }
                    }
                    else
                    {
                        ImGuiEx.TextWrapped($"Due to issues unrelated to Splatoon or Dalamud, DirectX11 renderer often causes crashes on these systems. Please press the following button to test whether you have this issue or not:");
                        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.ExclamationTriangle, "Reload DirectX11 render engine"))
                        {
                            P.ForceLoadDX11 = true;
                            S.RenderManager.ReloadEngine(RenderEngineKind.DirectX11);
                            P.AddDynamicElements("Test", [new(1)
                            {
                                refActorType = 1,
                                radius = 5f,
                                Filled = true,
                                RenderEngineKind = RenderEngineKind.DirectX11,
                            }], [-1]);
                            Tested = true;
                        }
                        ImGuiEx.Text($"If your game hasn't crashed and you see red circle around you, you should be safe to enable DirectX11 render engine.");
                        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "Enable DirectX11 renderer on Mac OS/Linux", enabled: Tested))
                        {
                            P.Config.DX11EnabledOnMacLinux = true;
                            P.RemoveDynamicElements("Test");
                        }
                    }
                }).Draw();
        }
        new NuiBuilder()
            .Section("通用設定".Loc())
            .Widget(() =>
            {
                ImGuiEx.TextWrapped($"""
                    Splatoon supports few renderers. On this page, you can select which ones you want to use, configure them and set one of them as default.
                    Render engine can be set globally and per-element. When set render engine is not available, either due to load error or because user has disabled it, other available render engine will be used automatically.
                    Settings present in this section affect all available renderes.
                    """.Loc());
                ImGui.Separator();
                ImGuiUtils.SizedText("繪製距離:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragFloat("##maxdistance", ref p.Config.maxdistance, 0.25f, 10f, 200f);
                ImGuiComponents.HelpMarker("僅嘗試繪製與您距離不超過此數值的物件".Loc());

                if(ImGui.Button("編輯繪製區域".Loc()))
                {
                    P.RenderableZoneSelector.IsOpen = true;
                }
                ImGuiComponents.HelpMarker("設定 Splatoon 繪製元素的螢幕區域".Loc());
                ImGui.Checkbox($"將 Splatoon 元素繪製在其他外掛元素與視窗之下".Loc(), ref P.Config.SplatoonLowerZ);
            })

            .Section("DirectX11 渲染器".Loc())
            .Widget(() =>
            {
                ImGuiEx.Text($"DirectX11 Render made by SourP. ");
                S.RenderManager.DrawCommonSettings(RenderEngineKind.DirectX11);

                ImGuiUtils.SizedText("[實驗性] 使用 VFX 渲染:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.Checkbox("##usevfxrendering", ref p.Config.UseVfxRendering);
                ImGuiComponents.HelpMarker("若可行，使用遊戲內範圍指示 VFX 渲染元素。部分圓環與扇形尺寸不支援，將退回使用 DirectX 渲染。".Loc());

                ImGuiUtils.SizedText("透明度混合模式:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGuiUtils.EnumCombo("##alphablendmode", ref p.Config.AlphaBlendMode, AlphaBlendModes.Names, AlphaBlendModes.Tooltips);
                if(ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("變更重疊元素透明度的混合方式");
                }

                ImGui.Checkbox("自動裁切 Splatoon 元素以避開遊戲內建 UI 元素與視窗".Loc(), ref P.Config.AutoClipNativeUI);
                ImGuiComponents.HelpMarker("部分遊戲內建元素目前不支援，但未來可能會加入。目前文字不會被裁切。".Loc());

                if(ImGui.Button("編輯裁切區域".Loc()))
                {
                    P.ClipZoneSelector.IsOpen = true;
                }
                ImGuiComponents.HelpMarker("設定 Splatoon「不會」繪製元素的螢幕區域。目前文字不會被裁切。".Loc());

                if(ImGui.CollapsingHeader("全域樣式覆寫".Loc()))
                {
                    ImGui.Indent();
                    ImGuiUtils.SizedText("最小填充透明度:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##minfillalpha", ref P.Config.ElementMinFillAlpha, 0, P.Config.ElementMaxFillAlpha);

                    ImGuiUtils.SizedText("最大填充透明度:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##maxfillalpha", ref P.Config.ElementMaxFillAlpha, P.Config.ElementMinFillAlpha, 255);

                    ImGuiUtils.SizedText("最大透明度:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##maxalpha", ref P.Config.MaxAlpha, P.Config.ElementMaxFillAlpha, 255);
                    ImGuiComponents.HelpMarker("繪製時使用的最大透明度。\n僅在筆畫或使用透明度混合模式「Add」時生效。".Loc());
                    // If min == max, users can break ints out of min and max values in the UI. Clamp to sane values for safety.
                    P.Config.ElementMinFillAlpha = Math.Clamp(P.Config.ElementMinFillAlpha, 0, P.Config.ElementMaxFillAlpha);
                    P.Config.ElementMaxFillAlpha = Math.Clamp(P.Config.ElementMaxFillAlpha, P.Config.ElementMinFillAlpha, 255);
                    P.Config.MaxAlpha = Math.Clamp(P.Config.MaxAlpha, P.Config.ElementMaxFillAlpha, 255);

                    ImGui.Separator();
                    foreach(var mech in MechanicTypes.Values)
                    {
                        if(!MechanicTypes.CanOverride(mech)) continue;
                        var name = MechanicTypes.Names[(int)mech];
                        var hasOverride = P.Config.StyleOverrides.ContainsKey(mech);

                        var enableOverride = false;
                        var style = MechanicTypes.DefaultMechanicColors[mech];
                        if(hasOverride)
                        {
                            (enableOverride, style) = P.Config.StyleOverrides[mech];
                        }

                        ImGui.PushStyleColor(ImGuiCol.Text, style.strokeColor);
                        ImGuiUtils.SizedText(name, CGui.WidthElement);
                        ImGui.PopStyleColor();

                        ImGui.SameLine();
                        ImGui.Checkbox("Override##" + name, ref enableOverride);
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Text, style.strokeColor);
                        if(ImGui.Button("Reset To Default##" + name))
                        {
                            style = MechanicTypes.DefaultMechanicColors[mech];
                        }
                        ImGui.PopStyleColor();

                        ImGuiUtils.StyleEdit(name, ref style);

                        P.Config.StyleOverrides[mech] = new(enableOverride, style);
                        ImGui.Separator();
                    }
                    ImGui.Unindent();
                }
            })

            .Section("舊版 ImGui 渲染器".Loc())
            .Widget(() =>
            {
                ImGuiEx.Text($"預設渲染引擎。".Loc());
                S.RenderManager.DrawCommonSettings(RenderEngineKind.ImGui_Legacy);

                ImGuiUtils.SizedText("圓形平滑度:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragInt("##circlesmoothness", ref p.Config.segments, 0.1f, 10, 150);
                ImGuiComponents.HelpMarker("數值越高 - 圓形越平滑，CPU 使用率越高".Loc());

                ImGui.Checkbox("啟用在視角上方繪製圓形時停用圓形修正".Loc(), ref P.Config.NoCircleFix);
                ImGuiComponents.HelpMarker("除非確實需要，否則請勿啟用。啟用此選項後，大型圓形在特定攝影機角度下可能會渲染錯誤。");

                ImGuiUtils.SizedText("線段數量:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragInt("##linesegments", ref p.Config.lineSegments, 0.1f, 10, 50);
                p.Config.lineSegments.ValidateRange(10, 100);
                ImGuiComponents.HelpMarker("若您的線條在距離螢幕邊緣過遠處便停止繪製，或在拉近視角且接近邊緣時消失，請增加此數值。提高此設定會「極大幅度」影響效能。".Loc());
                if(p.Config.lineSegments > 10)
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudOrange, "非標準線段設定。您的遊戲效能可能受到影響。請「小心地」逐步增加此設定，直到一切正常運作為止，不要再繼續增加。\n如果遇到效能損失，可考慮增加矩形填充線最小粗細以減緩影響。".Loc());
                }
                if(p.Config.lineSegments > 25)
                {
                    ImGuiEx.TextWrapped(Environment.TickCount % 1000 > 500 ? ImGuiColors.DalamudRed : ImGuiColors.DalamudYellow,
                        "您的線段設定「非常高，可能嚴重影響效能」。\n如果您確實需要設為此數值才能正常運作，請聯絡開發者並提供詳細資訊。".Loc());
                }

                ImGui.Separator();
                ImGuiEx.Text("填充設定:".Loc());
                ImGui.SameLine();
                ImGuiEx.Text("            搞砸了？".Loc());
                ImGui.SameLine();
                if(ImGui.SmallButton("重置此區塊".Loc()))
                {
                    var def = new Configuration();
                    P.Config.AltConeStep = def.AltConeStep;
                    P.Config.AltConeStepOverride = def.AltConeStepOverride;
                    P.Config.AltDonutStep = def.AltDonutStep;
                    P.Config.AltDonutStepOverride = def.AltDonutStepOverride;
                    P.Config.AltRectForceMinLineThickness = def.AltRectForceMinLineThickness;
                    P.Config.AltRectHighlightOutline = def.AltRectHighlightOutline;
                    P.Config.AltRectMinLineThickness = def.AltRectMinLineThickness;
                    P.Config.AltRectStep = def.AltRectStep;
                    P.Config.AltRectStepOverride = def.AltRectStepOverride;
                }

                ImGuiComponents.HelpMarker("以筆畫而非實心顏色填滿矩形。這樣可以消除裁切問題，但可能感覺較為干擾。".Loc());

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("矩形填充線最小間隔".Loc(), ref p.Config.AltRectStep, 0.001f, 0, float.MaxValue);
                ImGui.SameLine();
                ImGui.Checkbox($"{Loc("Always force this value")}##1", ref P.Config.AltRectStepOverride);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("矩形填充線最小粗細".Loc(), ref p.Config.AltRectMinLineThickness, 0.001f, 0.01f, float.MaxValue);
                ImGuiComponents.HelpMarker("矩形顯示時有效能問題嗎？請增加此數值。".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"{Loc("Always force this value")}##2", ref P.Config.AltRectForceMinLineThickness);
                ImGui.Checkbox("額外標示矩形外框".Loc(), ref p.Config.AltRectHighlightOutline);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("圓環填充線最小間隔".Loc(), ref p.Config.AltDonutStep, 0.001f, 0.01f, float.MaxValue);
                ImGuiComponents.HelpMarker("矩形顯示時有效能問題嗎？請增加此數值。".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("永遠強制使用此數值".Loc() + "##3", ref P.Config.AltDonutStepOverride);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragInt("扇形填充線最小間隔".Loc(), ref p.Config.AltConeStep, 0.1f, 1, int.MaxValue);
                ImGui.SameLine();
                ImGui.Checkbox("永遠強制使用此數值".Loc() + "##4", ref P.Config.AltConeStepOverride);
                ImGui.Checkbox($"使用完整圓環填充".Loc(), ref P.Config.UseFullDonutFill);

            })

            .Draw();
    }
}
