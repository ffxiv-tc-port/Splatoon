using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using ECommons.SimpleGui;
using Splatoon.Serializables;
using System;
using System.Collections.Generic;

namespace Splatoon.Gui.Windows;

/// <summary>
/// 「注意視窗」——腳本在機制進行中用來顯示一行必須立刻讀到的提示。
///
/// 生命週期由 <c>SingletonServiceManager.Initialize(typeof(S))</c> 建立、
/// 由它在 Dispose 時回收（本類別實作 <see cref="IDisposable"/>）。
///
/// 顯示規則：腳本每一幀呼叫 <c>Controller.DisplayAttentionWindowLine</c> 把要畫的東西
/// 排進 <see cref="ActionQueueCommand"/>，本視窗畫完就清空。因此**沒有腳本持續呼叫時，
/// 佇列恆為空、<see cref="DrawConditions"/> 回 false，這個視窗完全不會出現。**
/// 這是本次移植維持既有行為的關鍵：新增這個視窗本身不改變任何現有畫面。
/// </summary>
internal class AttentionOverlayWindow : Window, IDisposable
{
    private long OpenedAt = 0;
    public string Title = "";
    public Vector2 TableSize;
    private IFontHandle Font;
    private static ImGuiWindowFlags SharedFlags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysUseWindowPadding;

    public AttentionOverlayWindow() : base($"###SplatoonAttention", SharedFlags | ImGuiWindowFlags.NoBackground, true)
    {
        EzConfigGui.WindowSystem.AddWindow(this);
        IsOpen = true;
        RespectCloseHotkey = false;
        AllowPinning = false;
        DisableFadeInFadeOut = true;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        AllowClickthrough = false;
        // 上游此處還有 AllowBackgroundBlur = false。那個屬性是 API15 之後才加進 Dalamud 的
        // Window 基底類別，我方釘的 API13 沒有，照抄會編不過。
        // 它的預設行為就是不套背景模糊，而且本視窗已經帶 NoBackground 旗標，所以刪掉這行沒有差別。
        TitleBarButtons.Clear();
        Size = ImGuiHelpers.MainViewport.Size;
        Position = new(0, 0);
        RebuildFont();
    }

    /// <summary>
    /// 本幀要畫的內容。<c>Centered</c> 為 true 時該列會置中。
    /// 由 <c>Controller.DisplayAttentionWindowLine</c> 填入，畫完在 <see cref="PostDraw"/> 清空。
    /// </summary>
    public List<(Action Action, bool Centered)> ActionQueueCommand = [];

    private IDisposable FontDispose = null;

    public void RebuildFont()
    {
        Font?.Dispose();
        Font = null;
        if(P.Config.AttentionFontSize != 1f)
        {
            Font = Svc.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(
            Svc.PluginInterface.UiBuilder.DefaultFontSpec.SizePx * P.Config.AttentionFontSize.ValidateRange(0.1f, 10f))));
        }
    }

    public override void PreDraw()
    {
        Size = ImGuiHelpers.MainViewport.Size;
        if(Font?.Available == true)
        {
            FontDispose = Font.Push();
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw()
    {
        ActionQueueCommand.Clear();
        FontDispose?.Dispose();
        FontDispose = null;
        ImGui.PopStyleVar();
    }

    private bool ShouldDisplay = false;

    public override void PreOpenCheck()
    {
        if(ActionQueueCommand.Count == 0)
        {
            OpenedAt = Environment.TickCount64;
            ShouldDisplay = false;
            TableSize = default;
            RowWidth.Clear();
        }
        else
        {
            bool ret;

            var time = Environment.TickCount64 - OpenedAt;
            var tAppear = 400;
            if(!P.Config.AttentionNoAnimate && time < tAppear * 3)
            {
                ret = time % tAppear < tAppear / 2;
            }
            else
            {
                ret = true;
            }
            if(!ret)
            {
                ActionQueueCommand.Clear();
            }
            ShouldDisplay = ret;
        }
    }

    public override bool DrawConditions()
    {
        return ShouldDisplay;
    }

    public override void Draw()
    {
        var t = TimeSpan.FromMilliseconds(Environment.TickCount64 - OpenedAt);
        var titleBar = $"{Title} [{(int)t.TotalMinutes:D2}:{t.Seconds:D2}]";
        var position = new Vector2(P.Config.AttentionBasePositionX switch
        {
            WindowBasePosition.Start => 0,
            WindowBasePosition.End => ImGuiHelpers.MainViewport.Size.X - TableSize.X,
            _ => (ImGuiHelpers.MainViewport.Size.X / 2) - (TableSize.X / 2)
        }, P.Config.AttentionBasePositionY switch
        {
            WindowBasePosition.Middle => (ImGuiHelpers.MainViewport.Size.Y / 2) - (TableSize.Y / 2),
            WindowBasePosition.End => ImGuiHelpers.MainViewport.Size.Y - TableSize.Y,
            _ => 0
        }) + P.Config.AttentionBaseOffset;
        ImGui.SetCursorPos(position);
        var bgColor = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg].ToUint();
        var colWidth = 0f;
        if(ImGui.BeginTable("AttentionTable", 1, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.BlendColors(bgColor, ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg].ToUint()));
            ImGui.BeginGroup();
            DrawCenteredRow(-1, () => ImGuiEx.Text(titleBar));
            {
                var c = ImGui.GetItemRectSize().X + ImGui.GetStyle().CellPadding.X;
                if(c > colWidth) colWidth = c;
            }
            ImGui.EndGroup();
            for(var i = 0; i < ActionQueueCommand.Count; i++)
            {
                var x = ActionQueueCommand[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, bgColor);
                ImGui.BeginGroup();
                if(x.Centered)
                {
                    DrawCenteredRow(i, x.Action);
                }
                else
                {
                    try
                    {
                        x.Action();
                    }
                    catch(Exception e)
                    {
                        e.Log();
                    }
                }
                ImGui.EndGroup();
                var c = ImGui.GetItemRectSize().X + ImGui.GetStyle().CellPadding.X;
                if(c > colWidth) colWidth = c;
            }
            ImGui.EndTable();
            ImGui.SameLine(0, 0);
        }
        TableSize = new(colWidth, ImGui.GetItemRectSize().Y);
    }

    private Dictionary<int, float> RowWidth = [];
    private void DrawCenteredRow(int rowIndex, Action drawContent)
    {
        RowWidth.TryGetValue(rowIndex, out var lastWidth);
        var offset = MathF.Max(0f, (ImGui.GetContentRegionAvail().X - lastWidth) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        ImGui.BeginGroup();
        try
        {
            drawContent();
        }
        catch(Exception e)
        {
            e.Log();
        }
        ImGui.EndGroup();
        RowWidth[rowIndex] = ImGui.GetItemRectSize().X;
    }

    public void Dispose()
    {
        Font?.Dispose();
    }
}
