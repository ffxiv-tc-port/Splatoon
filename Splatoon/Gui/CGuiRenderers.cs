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
    private bool ShowAttentionWindow = false;
    private int DemoRows = 3;

    // 注意視窗預覽用的假內容。刻意寫成長度不一的句子,才看得出視窗會被撐到多寬。
    private static readonly string[] PreviewLines =
    [
        "Adjust this window so it does not overlap the UI you need.",
        "Attention window preview line.",
        "This is what a script's message looks like.",
        "Short line.",
        "A considerably longer line, so you can see how wide the window can get.",
        "Another preview line.",
        "Yet another preview line.",
        "Last preview line.",
    ];

    // WindowBasePosition 的三個成員在 X 軸與 Y 軸要讀成不同的字(左中右 vs 上中下),
    // 所以不能用 LocEnum.Names<WindowBasePosition>()(那是按型別的單一對照表)。
    // 快取的理由與失效條件與 LocEnum 相同:這段在每幀的 Draw 裡,
    // 而 Splatoon 的一般設定可以在執行期切換介面語言,所以語言變了就要重建。
    private Dictionary<WindowBasePosition, string> AttentionX;
    private Dictionary<WindowBasePosition, string> AttentionY;
    private string AttentionNamesLanguage;
    private void EnsureAttentionNames()
    {
        if(AttentionX != null && AttentionNamesLanguage == Localization.CurrentLanguage) return;
        AttentionX = new()
        {
            [WindowBasePosition.Start] = "Left".Loc(),
            [WindowBasePosition.Middle] = "Middle".Loc(),
            [WindowBasePosition.End] = "Right".Loc(),
        };
        AttentionY = new()
        {
            [WindowBasePosition.Start] = "Top".Loc(),
            [WindowBasePosition.Middle] = "Middle".Loc(),
            [WindowBasePosition.End] = "Bottom".Loc(),
        };
        AttentionNamesLanguage = Localization.CurrentLanguage;
    }
    private void DisplayRenderers()
    {
        EnsureAttentionNames();
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
            .Section("Common Settings".Loc())
            .Widget(() =>
            {
                ImGuiEx.TextWrapped($"""
                    Splatoon supports few renderers. On this page, you can select which ones you want to use, configure them and set one of them as default.
                    Render engine can be set globally and per-element. When set render engine is not available, either due to load error or because user has disabled it, other available render engine will be used automatically.
                    Settings present in this section affect all available renderes.
                    """.Loc());
                ImGui.Separator();
                ImGuiUtils.SizedText("Drawing distance:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragFloat("##maxdistance", ref p.Config.maxdistance, 0.25f, 10f, 200f);
                ImGuiComponents.HelpMarker("Only try to draw objects that are not further away from you than this value".Loc());

                if(ImGui.Button("Edit Draw Zones".Loc()))
                {
                    P.RenderableZoneSelector.IsOpen = true;
                }
                ImGuiComponents.HelpMarker("Configure screen zones where Splatoon will draw its elements".Loc());
                ImGui.Checkbox($"Draw Splatoon's element under other plugins elements and windows".Loc(), ref P.Config.SplatoonLowerZ);
            })

            .Section("Attention Color".Loc())
            .TextWrapped("Attention color is used to highlight the most important and most critical elements, requiring immediate resolution. Typically it can only be used by scripts.".Loc())
            .Widget(() =>
            {
                ImGui.SetNextItemWidth(200f);
                ImGuiEx.EnumCombo("Attention Color Type".Loc(), ref P.Config.AttentionColorType, names: LocEnum.Names<AttentionColorType>());
                if(P.Config.AttentionColorType.EqualsAny(AttentionColorType.Rainbow, AttentionColorType.Gradient))
                {
                    ImGui.SetNextItemWidth(200f);
                    ImGui.InputFloat("Color switching cycle, seconds".Loc(), ref P.Config.AttentionColorCycle);
                }
                if(P.Config.AttentionColorType.EqualsAny(AttentionColorType.Gradient, AttentionColorType.Fixed))
                {
                    ImGui.ColorEdit4("Color 1".Loc(), ref P.Config.AttentionColor1, ImGuiColorEditFlags.NoInputs);
                }
                if(P.Config.AttentionColorType.EqualsAny(AttentionColorType.Gradient))
                {
                    ImGui.ColorEdit4("Color 2".Loc(), ref P.Config.AttentionColor2, ImGuiColorEditFlags.NoInputs);
                }
                ImGui.SameLine();
                ImGuiEx.Text(Utils.GetAttentionColor(), "  ####  ");
                ImGuiEx.Tooltip("Live preview of the current attention color.".Loc());
            })

            .Section("Attention Window".Loc())
            .Widget(() =>
            {
                ImGuiEx.TextWrapped("Attention window is a way for a script to display an useful message during a mechanic. Here you can configure where it appears and how big it is.".Loc());
                ImGui.Checkbox("Show attention window preview".Loc(), ref ShowAttentionWindow);
                if(ShowAttentionWindow)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100f);
                    ImGuiEx.SliderInt("Num rows".Loc(), ref DemoRows, 1, 8);
                    S.AttentionOverlayWindow.Title = "Attention window".Loc();
                    // 預覽是靠「持續每幀把列排進佇列」達成的,和腳本走的是同一條路徑 ——
                    // 取消勾選就不再排入,佇列在下一幀被 PostDraw 清空,視窗自己消失。
                    PreviewLines.Take(DemoRows).Each(x => S.AttentionOverlayWindow.ActionQueueCommand.Add((() => ImGuiEx.Text(x.Loc()), true)));
                }
                ImGuiEx.Text("Window screen position:".Loc());
                ImGui.Indent();
                ImGui.SetNextItemWidth(150f);
                ImGuiEx.EnumCombo("##attentionY", ref P.Config.AttentionBasePositionY, AttentionY);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGuiEx.EnumCombo("##attentionX", ref P.Config.AttentionBasePositionX, AttentionX);
                ImGui.SetNextItemWidth(140f);
                ImGui.DragFloat("Offset X".Loc(), ref P.Config.AttentionBaseOffset.X);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(140f);
                ImGui.DragFloat("Offset Y".Loc(), ref P.Config.AttentionBaseOffset.Y);
                ImGui.Unindent();
                ImGui.SetNextItemWidth(150f);
                ImGui.DragFloat("Font size".Loc(), ref P.Config.AttentionFontSize, 0.02f, 0.1f, 10f);
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "Apply".Loc()))
                {
                    // 字型 handle 要重建才會套用新的大小。不能在繪製迴圈中途做,
                    // 所以丟到下一個 tick(這也是上游的做法)。
                    new TickScheduler(() => S.AttentionOverlayWindow.RebuildFont());
                }
                ImGui.Checkbox("Disable appearance blinking".Loc(), ref P.Config.AttentionNoAnimate);
            })

            .Section("DirectX11 Renderer".Loc())
            .Widget(() =>
            {
                ImGuiEx.Text($"DirectX11 Render made by SourP. ");
                S.RenderManager.DrawCommonSettings(RenderEngineKind.DirectX11);

                ImGuiUtils.SizedText("[EXPERIMENTAL] Use VFX Rendering:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.Checkbox("##usevfxrendering", ref p.Config.UseVfxRendering);
                ImGuiComponents.HelpMarker("If possible, render elements with in-game omen VFX. Some donut and cone sizes are not supported; these will fall back to DirectX rendering.".Loc());

                ImGuiUtils.SizedText("Alpha Blend Mode:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGuiUtils.EnumCombo("##alphablendmode", ref p.Config.AlphaBlendMode, AlphaBlendModes.Names, AlphaBlendModes.Tooltips);
                if(ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Change how overlapping elements' transparency is blended".Loc());
                }

                ImGui.Checkbox("Automatically clip Splatoon's elements around native UI elements and windows".Loc(), ref P.Config.AutoClipNativeUI);
                ImGuiComponents.HelpMarker("Some native elements are not supported, but they may be added later. Text is currently not clipped.".Loc());

                if(ImGui.Button("Edit Clip Zones".Loc()))
                {
                    P.ClipZoneSelector.IsOpen = true;
                }
                ImGuiComponents.HelpMarker("Configure screen zones where Splatoon will NOT draw elements. Text is currently not clipped.".Loc());

                if(ImGui.CollapsingHeader("Global Style Overrides".Loc()))
                {
                    ImGui.Indent();
                    ImGuiUtils.SizedText("Minimum Fill Alpha:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##minfillalpha", ref P.Config.ElementMinFillAlpha, 0, P.Config.ElementMaxFillAlpha);

                    ImGuiUtils.SizedText("Maximum Fill Alpha:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##maxfillalpha", ref P.Config.ElementMaxFillAlpha, P.Config.ElementMinFillAlpha, 255);

                    ImGuiUtils.SizedText("Maximum Alpha:".Loc(), CGui.WidthElement);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    ImGui.SliderInt("##maxalpha", ref P.Config.MaxAlpha, P.Config.ElementMaxFillAlpha, 255);
                    ImGuiComponents.HelpMarker("The maximum alpha used for drawing.\nThis will only take effect for strokes or if using Alpha Blend Mode 'Add'. ".Loc());
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
                        ImGui.Checkbox("Override".Loc() + "##" + name, ref enableOverride);
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Text, style.strokeColor);
                        if(ImGui.Button("Reset To Default".Loc() + "##" + name))
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

            .Section("Legacy ImGui Renderer".Loc())
            .Widget(() =>
            {
                ImGuiEx.Text($"Default rendering engine. ".Loc());
                S.RenderManager.DrawCommonSettings(RenderEngineKind.ImGui_Legacy);

                ImGuiUtils.SizedText("Circle smoothness:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragInt("##circlesmoothness", ref p.Config.segments, 0.1f, 10, 150);
                ImGuiComponents.HelpMarker("Higher - smoother circle, higher cpu usage".Loc());

                ImGui.Checkbox("Disable circle fix while enabling drawing circles above your point of view".Loc(), ref P.Config.NoCircleFix);
                ImGuiComponents.HelpMarker("Do not enable it unless you actually need it. Large circles may be rendered incorrectly under certain camera angle with this option enabled.".Loc());

                ImGuiUtils.SizedText("Line segments:".Loc(), WidthLayout);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.DragInt("##linesegments", ref p.Config.lineSegments, 0.1f, 10, 50);
                p.Config.lineSegments.ValidateRange(10, 100);
                ImGuiComponents.HelpMarker("Increase this if your lines stop drawing too far from the screen edges or if line disappears when you are zoomed in and near it's edge. Increasing this setting hurts performance EXTRAORDINARILY.".Loc());
                if(p.Config.lineSegments > 10)
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudOrange, "Non-standard line segment setting. Performance of your game may be impacted. Please CAREFULLY increase this setting until everything works as intended and do not increase it further. \nConsider increasing minimal rectangle fill line thickness to mitigate performance loss, if you will experience it.".Loc());
                }
                if(p.Config.lineSegments > 25)
                {
                    ImGuiEx.TextWrapped(Environment.TickCount % 1000 > 500 ? ImGuiColors.DalamudRed : ImGuiColors.DalamudYellow,
                        "Your line segment setting IS EXTREMELY HIGH AND MAY SIGNIFICANTLY IMPACT PERFORMANCE.\nIf you really have to set it to this value to make it work, please contact developer and provide details.".Loc());
                }

                ImGui.Separator();
                ImGuiEx.Text("Fill settings:".Loc());
                ImGui.SameLine();
                ImGuiEx.Text("            Screwed up?".Loc());
                ImGui.SameLine();
                if(ImGui.SmallButton("Reset this section".Loc()))
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

                ImGuiComponents.HelpMarker("Fill rectangles with stroke instead of full color. This will remove clipping issues, but may feel more disturbing.".Loc());

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("Minimal rectangle fill line interval".Loc(), ref p.Config.AltRectStep, 0.001f, 0, float.MaxValue);
                ImGui.SameLine();
                ImGui.Checkbox($"{Loc("Always force this value")}##1", ref P.Config.AltRectStepOverride);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("Minimal rectangle fill line thickness".Loc(), ref p.Config.AltRectMinLineThickness, 0.001f, 0.01f, float.MaxValue);
                ImGuiComponents.HelpMarker("Problems with performance while rectangles are visible? Increase this value.".Loc());
                ImGui.SameLine();
                ImGui.Checkbox($"{Loc("Always force this value")}##2", ref P.Config.AltRectForceMinLineThickness);
                ImGui.Checkbox("Additionally highlight rectangle outline".Loc(), ref p.Config.AltRectHighlightOutline);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("Minimal donut fill line interval".Loc(), ref p.Config.AltDonutStep, 0.001f, 0.01f, float.MaxValue);
                ImGuiComponents.HelpMarker("Problems with performance while rectangles are visible? Increase this value.".Loc());
                ImGui.SameLine();
                ImGui.Checkbox("Always force this value".Loc() + "##3", ref P.Config.AltDonutStepOverride);

                ImGui.SetNextItemWidth(60f);
                ImGui.DragInt("Minimal cone fill line interval".Loc(), ref p.Config.AltConeStep, 0.1f, 1, int.MaxValue);
                ImGui.SameLine();
                ImGui.Checkbox("Always force this value".Loc() + "##4", ref P.Config.AltConeStepOverride);
                ImGui.Checkbox($"Use full donut filling".Loc(), ref P.Config.UseFullDonutFill);

            })

            .Draw();
    }
}
