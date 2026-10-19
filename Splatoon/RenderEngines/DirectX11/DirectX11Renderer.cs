using Dalamud.Utility;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using ECommons.ObjectLifeTracker;
using Splatoon.Memory;
using Splatoon.Serializables;
using Splatoon.SplatoonScripting;
using Splatoon.Structures;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static Splatoon.RenderEngines.DirectX11.DirectX11DisplayObjects;

namespace Splatoon.RenderEngines.DirectX11;
public sealed unsafe class DirectX11Renderer : RenderEngine
{
    private DirectX11Scene DirectX11Scene;

    internal override RenderEngineKind RenderEngineKind { get; } = RenderEngineKind.DirectX11;

    /// <summary>True only when this renderer is running but VFX rendering could not be initialized.</summary>
    internal bool VfxUnavailable => DirectX11Scene is { VfxAvailable: false };

    // 上一幀的顯示物件：這一幀要加的物件若與其中一個逐欄位相同，就沿用那個物件，不再每幀配置。
    // 裡面只有數值與 id 字串，沒有遊戲物件；跟著角色的元素每幀照舊重新取位置，位置變了就比不中、照舊新建。
    private readonly List<DisplayObject> lastFrame = [];
    private int reuseCursor;
    // 元素進出視距會讓順位前後移動：往後多看幾格就能重新對上，對不上的照舊新建。
    private const int ReuseLookahead = 8;

    internal void BeginFrame()
    {
        lastFrame.Clear();
        lastFrame.AddRange(DisplayObjects);
        DisplayObjects.Clear();
        reuseCursor = 0;
    }

    private static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    private static bool SameBits(Vector3 a, Vector3 b) => SameBits(a.X, b.X) && SameBits(a.Y, b.Y) && SameBits(a.Z, b.Z);

    // 回 null＝上一幀沒有型別與每個欄位都相同的物件，呼叫端照舊新建。
    // 被凍結圖層收走過的物件（RenderEngineKind 已被標記）不沿用，所以沿用的物件與新建的沒有任何欄位不同。
    private T FindReusableFan<T>(string id, Vector3 origin, float innerRadius, float outerRadius, float angleMin, float angleMax, in DisplayStyle style) where T : DisplayObjectFan
    {
        var end = Math.Min(lastFrame.Count, reuseCursor + ReuseLookahead);
        for(var i = reuseCursor; i < end; i++)
        {
            if(lastFrame[i] is T fan && fan.GetType() == typeof(T) && fan.RenderEngineKind == RenderEngineKind.Unspecified
                && fan.id == id && SameBits(fan.origin, origin)
                && SameBits(fan.innerRadius, innerRadius) && SameBits(fan.outerRadius, outerRadius)
                && SameBits(fan.angleMin, angleMin) && SameBits(fan.angleMax, angleMax)
                && fan.style.BitwiseEquals(style))
            {
                reuseCursor = i + 1;
                return fan;
            }
        }
        return null;
    }

    private DisplayObjectLine FindReusableLine(string id, Vector3 start, Vector3 stop, float radius, in DisplayStyle style, LineEnd startStyle, LineEnd endStyle)
    {
        var end = Math.Min(lastFrame.Count, reuseCursor + ReuseLookahead);
        for(var i = reuseCursor; i < end; i++)
        {
            if(lastFrame[i] is DisplayObjectLine line && line.GetType() == typeof(DisplayObjectLine) && line.RenderEngineKind == RenderEngineKind.Unspecified
                && line.id == id && SameBits(line.start, start) && SameBits(line.stop, stop) && SameBits(line.radius, radius)
                && line.startStyle == startStyle && line.endStyle == endStyle
                && line.style.BitwiseEquals(style))
            {
                reuseCursor = i + 1;
                return line;
            }
        }
        return null;
    }

    // 以下四個的 Find 引數必須等於對應建構子寫進欄位的值（圓：內徑 0、角度 0～2π；甜甜圈：外徑＝內徑＋寬度）。
    private void AddCircleObject(string id, Vector3 origin, float radius, DisplayStyle style)
    {
        DisplayObjects.Add(FindReusableFan<DisplayObjectCircle>(id, origin, 0, radius, 0, 2 * MathF.PI, style)
            ?? new DisplayObjectCircle(id, origin, radius, style));
    }

    private void AddDonutObject(string id, Vector3 origin, float innerRadius, float donutRadius, DisplayStyle style)
    {
        DisplayObjects.Add(FindReusableFan<DisplayObjectDonut>(id, origin, innerRadius, innerRadius + donutRadius, 0, 2 * MathF.PI, style)
            ?? new DisplayObjectDonut(id, origin, innerRadius, donutRadius, style));
    }

    private void AddFanObject(string id, Vector3 origin, float innerRadius, float outerRadius, float angleMin, float angleMax, DisplayStyle style)
    {
        DisplayObjects.Add(FindReusableFan<DisplayObjectFan>(id, origin, innerRadius, outerRadius, angleMin, angleMax, style)
            ?? new DisplayObjectFan(id, origin, innerRadius, outerRadius, angleMin, angleMax, style));
    }

    private void AddLineObject(string id, Vector3 start, Vector3 stop, float radius, DisplayStyle style, LineEnd startStyle, LineEnd endStyle)
    {
        DisplayObjects.Add(FindReusableLine(id, start, stop, radius, style, startStyle, endStyle)
            ?? new DisplayObjectLine(id, start, stop, radius, style, startStyle, endStyle));
    }

    // 點與文字的欄位沒有 readonly，但建構之後沒有人改；沿用條件同上：型別與每個欄位都相同（文字比內容）。
    private void AddDotObject(string id, float x, float y, float z, float thickness, uint color)
    {
        var end = Math.Min(lastFrame.Count, reuseCursor + ReuseLookahead);
        for(var i = reuseCursor; i < end; i++)
        {
            if(lastFrame[i] is DisplayObjectDot dot && dot.GetType() == typeof(DisplayObjectDot) && dot.RenderEngineKind == RenderEngineKind.Unspecified
                && dot.id == id && SameBits(dot.x, x) && SameBits(dot.y, y) && SameBits(dot.z, z)
                && SameBits(dot.thickness, thickness) && dot.color == color)
            {
                reuseCursor = i + 1;
                DisplayObjects.Add(dot);
                return;
            }
        }
        DisplayObjects.Add(new DisplayObjectDot(id, x, y, z, thickness, color));
    }

    private void AddTextObject(string id, float x, float y, float z, string text, uint bgcolor, uint fgcolor, float fscale)
    {
        var end = Math.Min(lastFrame.Count, reuseCursor + ReuseLookahead);
        for(var i = reuseCursor; i < end; i++)
        {
            if(lastFrame[i] is DisplayObjectText t && t.GetType() == typeof(DisplayObjectText) && t.RenderEngineKind == RenderEngineKind.Unspecified
                && t.id == id && SameBits(t.x, x) && SameBits(t.y, y) && SameBits(t.z, z)
                && string.Equals(t.text, text, StringComparison.Ordinal)
                && t.bgcolor == bgcolor && t.fgcolor == fgcolor && SameBits(t.fscale, fscale))
            {
                reuseCursor = i + 1;
                DisplayObjects.Add(t);
                return;
            }
        }
        DisplayObjects.Add(new DisplayObjectText(id, x, y, z, text, bgcolor, fgcolor, fscale));
    }

    internal DirectX11Renderer()
    {
        if(!Enabled)
        {
            LoadError = new RenderEngineDisabledException();
            return;
        }
        try
        {
            if(Utils.IsLinux())
            {
                if(!P.Config.DX11EnabledOnMacLinux && !P.ForceLoadDX11) throw new InvalidOperationException("DirectX11 renderer was disabled because Mac/Linux was detected and user has not allowed it to be loaded.");
            }
            //throw new NotImplementedException("DirectX renderer is not updated");
            DirectX11Scene = new(this);
        }
        catch(Exception e)
        {
            LoadError = e;
            e.Log();
        }
    }

    public override void Dispose()
    {
        Safe(() => DirectX11Scene?.Dispose());
    }

    internal void DrawCircle(Layout layout, Element e, float x, float y, float z, float r, float angle, IGameObject go = null)
    {
        var cx = x + e.offX;
        var cy = y + e.offY;
        if(e.includeRotation)
        {
            var rotatedPoint = Utils.RotatePoint(x, y, -angle + e.AdditionalRotation, new Vector3(x - e.offX, y + e.offY, z));
            cx = rotatedPoint.X;
            cy = rotatedPoint.Y;
        }
        if(e.tether && !e.Nodraw)
        {
            Vector3 origin = new(cx, z + e.offZ, cy);
            var end = Utils.XZY(Utils.GetPlayerPositionXZY());
            if(e.ExtraTetherLength > 0)
            {
                end += Vector3.Normalize(end - origin) * e.ExtraTetherLength;
            }
            AddLineObject(e.GetUniqueId(go), origin, end, 0, e.GetDisplayStyleWithOverride(), e.LineEndA, e.LineEndB);
        }
        if(layout != null && e.IsCapturing)
        {
            AddCapturedObject(layout, e, new(cx, z + e.offZ, cy));
        }
        if(e.Nodraw) return;
        if(!LayoutUtils.ShouldDraw(cx, Utils.GetPlayerPositionXZY().X, cy, Utils.GetPlayerPositionXZY().Y)) return;
        if(r > 0)
        {
            if(e.Donut > 0)
            {
                AddDonutObject(e.GetUniqueId(go), new(cx, z + e.offZ, cy), r, e.Donut, e.GetDisplayStyleWithOverride(go));
            }
            else
            {
                AddCircleObject(e.GetUniqueId(go), new(cx, z + e.offZ, cy), r, e.GetDisplayStyleWithOverride(go));
            }
        }
        else
        {
            AddDotObject(e.GetUniqueId(go), cx, z + e.offZ, cy, e.thicc, e.color);
        }
        DrawText(e, go, cx, cy, z);
    }

    private void DrawText(Element element, IGameObject associatedGameObject, float cx, float cy, float z)
    {
        if(element.overlayTextIntl.Get(element.overlayText).Length > 0)
        {
            var text = element.overlayTextIntl.Get(element.overlayText);
            if(associatedGameObject != null)
            {
                text = text.ProcessPlaceholders(associatedGameObject);
            }
            AddTextObject(element.GetUniqueId(associatedGameObject), cx, cy, z + element.offZ + element.overlayVOffset, text, element.overlayBGColor, element.overlayTextColor, element.overlayFScale);
        }
    }

    internal void DrawCone(Layout layout, Element e, Vector3 origin, float? radius = null, float baseAngle = 0f, IGameObject go = null)
    {
        if(e.coneAngleMax > e.coneAngleMin)
        {
            var angleMin = -baseAngle + e.AdditionalRotation + e.coneAngleMin.DegreesToRadians();
            var angleMax = -baseAngle + e.AdditionalRotation + e.coneAngleMax.DegreesToRadians();
            var totalAngle = angleMax - angleMin;
            if(totalAngle >= 2 * MathF.PI)
            {
                angleMin = 0;
                angleMax = 2 * MathF.PI;
            }

            var center = Utils.XZY(Utils.RotatePoint(origin.X, origin.Y, -baseAngle, origin + new Vector3(-e.offX, e.offY, e.offZ)));
            if(layout != null && e.IsCapturing)
            {
                AddCapturedObject(layout, e, new(center.X, center.Z, center.Y));
            }
            if(e.Nodraw) return;
            if(!LayoutUtils.ShouldDraw(center.X, Utils.GetPlayerPositionXZY().X, center.Z, Utils.GetPlayerPositionXZY().Y)) return;
            float innerRadius = 0;
            var outerRadius = radius ?? e.radius;
            if(e.Donut > 0)
            {
                innerRadius = outerRadius;
                outerRadius = innerRadius + e.Donut;
            }
            if(e.tether && !e.Nodraw)
            {
                var end = Utils.XZY(Utils.GetPlayerPositionXZY());
                if(e.ExtraTetherLength > 0)
                {
                    end += Vector3.Normalize(end - center) * e.ExtraTetherLength;
                }
                AddLineObject(e.GetUniqueId(go), center, end, 0, e.GetDisplayStyleWithOverride(), e.LineEndA, e.LineEndB);
            }

            AddFanObject(e.GetUniqueId(go), center, innerRadius, outerRadius, angleMin, angleMax, e.GetDisplayStyleWithOverride());
            DrawText(e, null, center.X, center.Z, center.Y);
        }
    }

    internal void AddRotatedLine(Layout layout, Vector3 tPos, float angle, Element e, float aradius, float hitboxRadius, IGameObject go = null)
    {
        if(e.includeRotation)
        {
            if(aradius == 0f)
            {
                var (pointA, pointB) = CommonRenderUtils.GetRotatedPointsForZeroRadius(tPos, e, hitboxRadius, angle);
                if(layout != null && e.IsCapturing)
                {
                    AddCapturedObject(layout, e, new(pointA.X, pointA.Z, pointA.X));
                }
                if(e.Nodraw) return;
                if(!LayoutUtils.ShouldDraw(pointA.X, Utils.GetPlayerPositionXZY().X, pointA.Y, Utils.GetPlayerPositionXZY().Y)
                    && !LayoutUtils.ShouldDraw(pointB.X, Utils.GetPlayerPositionXZY().X, pointB.Y, Utils.GetPlayerPositionXZY().Y)) return;
                DisplayObjects.Add(new DisplayObjectLine(e.GetUniqueId(go), pointA.X, pointA.Y, pointA.Z,
                    pointB.X, pointB.Y, pointB.Z,
                    e.thicc, e.color, e.LineEndA, e.LineEndB));
            }
            else
            {
                var start = Utils.RotatePoint(tPos.X, tPos.Y,
                    -angle + e.AdditionalRotation, new Vector3(
                    tPos.X + -e.refX,
                    tPos.Y + e.refY,
                    tPos.Z + e.refZ));
                var stop = Utils.RotatePoint(tPos.X, tPos.Y,
                    -angle + e.AdditionalRotation, new Vector3(
                    tPos.X + -e.offX,
                    tPos.Y + e.offY,
                    tPos.Z + e.offZ));

                if(layout != null && e.IsCapturing)
                {
                    AddCapturedObject(layout, e, new(start.X, start.Z, start.X));
                }
                if(e.Nodraw) return;

                if(!LayoutUtils.ShouldDraw(start.X, Utils.GetPlayerPositionXZY().X, start.Y, Utils.GetPlayerPositionXZY().Y)
                    && !LayoutUtils.ShouldDraw(stop.X, Utils.GetPlayerPositionXZY().X, stop.Y, Utils.GetPlayerPositionXZY().Y)) return;

                AddLineObject(e.GetUniqueId(go), Utils.XZY(start), Utils.XZY(stop), aradius, e.GetDisplayStyleWithOverride(go), e.LineEndA, e.LineEndB);
            }
        }
        else
        {
            if(aradius == 0)
            {
                var (pointA, pointB) = CommonRenderUtils.GetNonRotatedPointsForZeroRadius(tPos, e, hitboxRadius, angle);
                if(layout != null && e.IsCapturing)
                {
                    AddCapturedObject(layout, e, new(pointA.X, pointA.Z, pointA.X));
                }
                if(e.Nodraw) return;
                if(!LayoutUtils.ShouldDraw(pointA.X, Utils.GetPlayerPositionXZY().X, pointA.Y, Utils.GetPlayerPositionXZY().Y)
                    && !LayoutUtils.ShouldDraw(pointB.X, Utils.GetPlayerPositionXZY().X, pointB.Y, Utils.GetPlayerPositionXZY().Y)) return;
                DisplayObjects.Add(new DisplayObjectLine(e.GetUniqueId(go), pointA.X, pointA.Y, pointA.Z,
                    pointB.X, pointB.Y, pointB.Z,
                    e.thicc, e.color, e.LineEndA, e.LineEndB));
            }
            else
            {
                var start = new Vector3(
                    tPos.X + e.refX,
                    tPos.Y + e.refY,
                    tPos.Z + e.refZ);
                var stop = new Vector3(
                    tPos.X + e.offX,
                    tPos.Y + e.offY,
                    tPos.Z + e.offZ);

                if(layout != null && e.IsCapturing)
                {
                    AddCapturedObject(layout, e, new(start.X, start.Z, start.X));
                }
                if(e.Nodraw) return;

                if(!LayoutUtils.ShouldDraw(start.X, Utils.GetPlayerPositionXZY().X, start.Y, Utils.GetPlayerPositionXZY().Y)
                    && !LayoutUtils.ShouldDraw(stop.X, Utils.GetPlayerPositionXZY().X, stop.Y, Utils.GetPlayerPositionXZY().Y)) return;

                AddLineObject(e.GetUniqueId(go), Utils.XZY(start), Utils.XZY(stop), aradius, e.GetDisplayStyleWithOverride(go), e.LineEndA, e.LineEndB);
            }
        }
    }

    internal override bool ProcessElement(Element element, Layout layout = null, bool forceEnable = false)
    {
        var ret = false;
        if(!element.Enabled && !forceEnable) return ret;
        P.ElementAmount++;
        var radius = element.radius;
        if(element.type == 0)
        {
            if(layout == null || !layout.UseDistanceLimit || LayoutUtils.CheckDistanceCondition(layout, element.refX, element.refY, element.refZ))
            {
                ret = true;
                DrawCircle(layout, element, element.refX, element.refY, element.refZ, radius, 0f);
            }
        }
        else if(element.type == 1 || element.type == 3 || element.type == 4)
        {
            if(element.includeOwnHitbox) radius += Svc.Objects.LocalPlayer.HitboxRadius;
            if(element.refActorType == 1 && LayoutUtils.CheckCharacterAttributes(element, Svc.Objects.LocalPlayer, true))
            {
                ret = true;
                if(element.type == 1)
                {
                    var pointPos = Utils.GetPlayerPositionXZY();
                    DrawCircle(layout, element, pointPos.X, pointPos.Y, pointPos.Z, radius, element.includeRotation ? Svc.Objects.LocalPlayer.GetRotationWithOverride(element) : 0f,
                        Svc.Objects.LocalPlayer);
                }
                else if(element.type == 3)
                {
                    AddRotatedLine(layout, Utils.GetPlayerPositionXZY(), Svc.Objects.LocalPlayer.GetRotationWithOverride(element), element, radius, 0f, Svc.Objects.LocalPlayer);
                }
                else if(element.type == 4)
                {
                    DrawCone(layout, element, Utils.GetPlayerPositionXZY(), radius, Svc.Objects.LocalPlayer.GetRotationWithOverride(element), Svc.Objects.LocalPlayer);
                }
            }
            else if(element.refActorType == 2 && Svc.Targets.Target != null
                && Svc.Targets.Target is IBattleNpc && LayoutUtils.CheckCharacterAttributes(element, Svc.Targets.Target, true))
            {
                if(layout == null || !layout.UseDistanceLimit || LayoutUtils.CheckDistanceCondition(layout, Svc.Targets.Target.GetPositionXZY()))
                {
                    ret = true;
                    if(element.includeHitbox) radius += Svc.Targets.Target.HitboxRadius;
                    if(element.type == 1)
                    {
                        DrawCircle(layout, element, Svc.Targets.Target.GetPositionXZY().X, Svc.Targets.Target.GetPositionXZY().Y,
                            Svc.Targets.Target.GetPositionXZY().Z, radius, element.includeRotation ? Svc.Targets.Target.GetRotationWithOverride(element) : 0f,
                            Svc.Targets.Target);
                    }
                    else if(element.type == 3)
                    {
                        if(element.FaceMe)
                        {
                            var list = Utils.GetFacePositions(layout, element, Svc.Targets.Target, element.faceplayer);
                            if(list != null)
                            {
                                foreach(var pos in list)
                                {
                                    var angle = ((element.FaceInvert ? 0 : 180) - (MathHelper.GetRelativeAngle(Svc.Targets.Target.Position.ToVector2(), pos.ToVector2()))).DegreesToRadians();
                                    AddRotatedLine(layout, element.FaceInvert ? pos.ToXZY() : Svc.Targets.Target.GetPositionXZY(), angle, element, radius, Svc.Targets.Target.HitboxRadius, Svc.Targets.Target);
                                }
                            }
                        }
                        else
                        {
                            var angle = Svc.Targets.Target.GetRotationWithOverride(element);
                            AddRotatedLine(layout, Svc.Targets.Target.GetPositionXZY(), angle, element, radius, Svc.Targets.Target.HitboxRadius, Svc.Targets.Target);
                        }
                    }
                    else if(element.type == 4)
                    {
                        if(element.FaceMe)
                        {
                            var list = Utils.GetFacePositions(layout, element, Svc.Targets.Target, element.faceplayer);
                            if(list != null)
                            {
                                foreach(var pos in list)
                                {
                                    var baseAngle = ((element.FaceInvert ? 0 : 180) - (MathHelper.GetRelativeAngle(Svc.Targets.Target.Position.ToVector2(), pos.ToVector2()))).DegreesToRadians();
                                    DrawCone(layout, element, element.FaceInvert ? pos.ToXZY() : Svc.Targets.Target.GetPositionXZY(), radius, baseAngle, Svc.Targets.Target);
                                }
                            }
                        }
                        else
                        {
                            var baseAngle = Svc.Targets.Target.GetRotationWithOverride(element);
                            DrawCone(layout, element, Svc.Targets.Target.GetPositionXZY(), radius, baseAngle, Svc.Targets.Target);
                        }
                    }
                }
            }
            else if(element.refActorType == 0)
            {
                foreach(var a in Svc.Objects)
                {
                    var targetable = a.Struct()->GetIsTargetable();
                    if(LayoutUtils.IsAttributeMatches(element, a)
                            && CommonRenderUtils.IsElementObjectMatches(layout, element, targetable, a))
                    {
                        if(layout == null || !layout.UseDistanceLimit || LayoutUtils.CheckDistanceCondition(layout, a.GetPositionXZY()))
                        {
                            ret = true;
                            // TargetAlteration.None always yields exactly [a]; skip building that one-item list (and its closure) per match per frame.
                            var altered = element.TargetAlteration == TargetAlteration.None ? null : Utils.AlterTargetIfNeeded(element, a);
                            var targetCount = altered?.Count ?? 1;
                            for(var targetIndex = 0; targetIndex < targetCount; targetIndex++)
                            {
                                var obj = altered == null ? a : altered[targetIndex];
                                var aradius = radius;
                                if(element.includeHitbox) aradius += obj.HitboxRadius;
                                if(element.type == 1)
                                {
                                    DrawCircle(layout, element, obj.GetPositionXZY().X, obj.GetPositionXZY().Y, obj.GetPositionXZY().Z, aradius,
                                        element.includeRotation ? obj.GetRotationWithOverride(element) : 0f,
                                        obj);
                                }
                                else if(element.type == 3)
                                {
                                    if(element.FaceMe)
                                    {
                                        var list = Utils.GetFacePositions(layout, element, obj, element.faceplayer);
                                        if(list != null)
                                        {
                                            foreach(var pos in list)
                                            {
                                                var angle = ((element.FaceInvert ? 0 : 180) - (MathHelper.GetRelativeAngle(obj.Position.ToVector2(), pos.ToVector2()))).DegreesToRadians();
                                                AddRotatedLine(layout, element.FaceInvert ? pos.ToXZY() : obj.GetPositionXZY(), angle, element, aradius, obj.HitboxRadius, obj);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var angle = obj.GetRotationWithOverride(element);
                                        AddRotatedLine(layout, obj.GetPositionXZY(), angle, element, aradius, obj.HitboxRadius, obj);
                                    }

                                }
                                else if(element.type == 4)
                                {
                                    if(element.FaceMe)
                                    {
                                        var list = Utils.GetFacePositions(layout, element, obj, element.faceplayer);
                                        if(list != null)
                                        {
                                            foreach(var pos in list)
                                            {
                                                var baseAngle = ((element.FaceInvert ? 0 : 180) - (MathHelper.GetRelativeAngle(obj.Position.ToVector2(), pos.ToVector2()))).DegreesToRadians();
                                                DrawCone(layout, element, element.FaceInvert ? pos.ToXZY() : obj.GetPositionXZY(), aradius, baseAngle, obj);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var baseAngle = obj.GetRotationWithOverride(element);
                                        DrawCone(layout, element, obj.GetPositionXZY(), aradius, baseAngle, obj);
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }
        else if(element.type == 2)
        {
            if(layout == null || !layout.UseDistanceLimit || LayoutUtils.CheckDistanceToLineCondition(layout, element))
            {
                if(!LayoutUtils.ShouldDraw(element.refX, Utils.GetPlayerPositionXZY().X, element.refY, Utils.GetPlayerPositionXZY().Y)
    && !LayoutUtils.ShouldDraw(element.offX, Utils.GetPlayerPositionXZY().X, element.offY, Utils.GetPlayerPositionXZY().Y)) return ret;
                ret = true;
                AddLine(new Vector3(element.refX, element.refZ, element.refY), new Vector3(element.offX, element.offZ, element.offY), element.radius, element.GetDisplayStyleWithOverride(), element.LineEndA, element.LineEndB);
            }
        }
        else if(element.type == 5)
        {
            if(element.FaceMe)
            {
                var list = Utils.GetFacePositions(layout, element, null, element.faceplayer);
                if(list != null)
                {
                    foreach(var fpos in list)
                    {
                        var baseAngle = ((element.FaceInvert ? 0 : 180) - (MathHelper.GetRelativeAngle(new Vector2(element.refX + element.offX, element.refY + element.offY), fpos.ToVector2()))).DegreesToRadians();
                        var pos = new Vector3(element.refX + element.offX, element.refY + element.offY, element.refZ + element.offZ);
                        DrawCone(layout, element, element.FaceInvert ? fpos.ToXZY() : pos, radius, baseAngle);
                    }
                }
            }
            else
            {
                var baseAngle = 0f;
                var pos = new Vector3(element.refX + element.offX, element.refY + element.offY, element.refZ + element.offZ);
                DrawCone(layout, element, pos, radius, baseAngle);
            }

        }
        return ret;
    }

    internal override void AddLine(float ax, float ay, float az, float bx, float by, float bz, float thickness, uint color, LineEnd startStyle = LineEnd.None, LineEnd endStyle = LineEnd.None)
    {
        DisplayObjects.Add(new DisplayObjectLine("", ax, ay, az, bx, by, bz, thickness, color, startStyle, endStyle));
    }

    internal void AddLine(Vector3 start, Vector3 stop, float radius, DisplayStyle style, LineEnd startStyle = LineEnd.None, LineEnd endStyle = LineEnd.None)
    {
        AddLineObject("", start, stop, radius, style, startStyle, endStyle);
    }
}
