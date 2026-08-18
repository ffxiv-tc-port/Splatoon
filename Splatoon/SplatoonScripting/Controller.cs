using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.Configuration;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using Splatoon.Gui.Priority;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
#nullable enable
namespace Splatoon.SplatoonScripting;

public unsafe class Controller
{
    internal SplatoonScript Script;
    internal Dictionary<string, Layout> Layouts = [];
    internal Dictionary<string, Element> Elements = [];
    internal List<TickScheduler> TickSchedulers = [];
    internal IEzConfig? Configuration;
    internal long AutoResetAt = long.MaxValue;

    internal int autoIncrement = 0;
    internal int AutoIncrement => ++autoIncrement;

    internal Controller(SplatoonScript s)
    {
        Script = s;
    }

    public Splatoon Plugin => Splatoon.P;

    /// <summary>
    /// Indicates whether player is in combat.
    /// </summary>
    public bool InCombat => Svc.Condition[ConditionFlag.InCombat];

    /// <summary>
    /// Indicates phase of a battle.
    /// </summary>
    public int Phase => P.Phase;

    /// <summary>
    /// Amount of seconds that have passed since combat start. Returns -1 if not in combat.
    /// </summary>
    public float CombatSeconds => InCombat ? (float)CombatMiliseconds / 1000f : -1;

    /// <summary>
    /// Amount of miliseconds that have passed since combat start. Returns -1 if not in combat.
    /// </summary>
    public float CombatMiliseconds => InCombat ? Environment.TickCount64 - P.CombatStarted : -1;

    public int Scene => *global::Splatoon.Memory.Scene.ActiveScene;

    /// <summary>
    /// Loads if unloaded and returns script configuration file.
    /// </summary>
    /// <typeparam name="T">Configuration class, implementing IEzConfig</typeparam>
    /// <returns>Loaded configuration</returns>
    public T GetConfig<T>() where T : IEzConfig, new()
    {
        Configuration ??= EzConfig.LoadConfiguration<T>(Script.InternalData.ConfigurationPath, false);
        return (T)Configuration;
    }

    /// <summary>
    /// Saves script's configuration, if present.
    /// </summary>
    public void SaveConfig()
    {
        if(Configuration != null)
        {
            //PluginLog.Information($"Saving to {Script.InternalData.ConfigurationPath}");
            EzConfig.SaveConfiguration(Configuration, Script.InternalData.ConfigurationPath, true, false);
        }
    }

    /// <summary>
    /// Attempts to register previously exported from plugin layout for further usage. End user will be able to edit this layout as they wish and results of the edit will be saved. Enabled layouts are subject for immediate processing when the script is enabled.
    /// </summary>
    /// <param name="UniqueName">Internal unique (within current script) name of the layout.</param>
    /// <param name="ExportString">An exported layout string.</param>
    /// <param name="layout">Decoded layout object.</param>
    /// <param name="overwrite">Whether to overwrite existing layout with same name if it's present.</param>
    /// <returns>Whether layout was successfully registered.</returns>
    public bool TryRegisterLayoutFromCode(string UniqueName, string ExportString, [NotNullWhen(true)] out Layout? layout, bool overwrite = false)
    {
        return ScriptingEngine.TryDecodeLayout(ExportString, out layout) && TryRegisterLayout(UniqueName, layout, overwrite);
    }

    public bool TryRegisterLayoutFromCode(string ExportString, [NotNullWhen(true)] out Layout? layout, bool overwrite = false)
    {
        return TryRegisterLayoutFromCode($"unnamed-{AutoIncrement}", ExportString, out layout, overwrite);
    }

    /// <summary>
    /// Attempts to register previously constructed layout for further usage. End user will be able to edit this layout as they wish and results of the edit will be saved. Enabled layouts are subject for immediate processing when the script is enabled.
    /// </summary>
    /// <param name="UniqueName">Internal unique (within current script) name of the layout.</param>
    /// <param name="layout">Layout object.</param>
    /// <param name="overwrite">Whether to overwrite existing layout with same name if it's present.</param>
    /// <returns>Whether layout was successfully registered.</returns>
    public bool TryRegisterLayout(string UniqueName, Layout layout, bool overwrite = false)
    {
        if(!overwrite && Layouts.ContainsKey(UniqueName))
        {
            PluginLog.Warning($"There is a layout named {UniqueName} already.");
            return false;
        }
        Layouts[UniqueName] = layout;
        return true;
    }


    public bool TryRegisterLayout(Layout layout, bool overwrite = false)
    {
        return TryRegisterLayout($"unnamed-{AutoIncrement}", layout, overwrite);
    }

    /// <summary>
    /// Attempts to register previously constructed element for further usage. End user will be able to edit this element as they wish and results of the edit will be saved. Enabled elements are subject for immediate processing when the script is enabled.
    /// </summary>
    /// <param name="UniqueName">Internal unique (within current script) name of the element.</param>
    /// <param name="element">Element object.</param>
    /// <param name="overwrite">Whether to overwrite existing element with same name if it's present.</param>
    /// <returns>Whether element was successfully registered.</returns>
    public bool TryRegisterElement(string UniqueName, Element element, bool overwrite = false)
    {
        if(!overwrite && Layouts.ContainsKey(UniqueName))
        {
            PluginLog.Warning($"There is an element named {UniqueName} already.");
            return false;
        }
        Elements[UniqueName] = element;
        return true;
    }

    /// <summary>
    /// Attempts to register previously exported from plugin element for further usage. End user will be able to edit this element as they wish and results of the edit will be saved. Enabled elements are subject for immediate processing when the script is enabled.
    /// </summary>
    /// <param name="UniqueName">Internal unique (within current script) name of the element</param>
    /// <param name="ExportString">An exported element string.</param>
    /// <param name="element">Decoded element object.</param>
    /// <param name="overwrite">Whether to overwrite existing element with same name if it's present.</param>
    /// <returns>Whether element was successfully registered.</returns>
    public bool TryRegisterElementFromCode(string UniqueName, string ExportString, [NotNullWhen(true)] out Element? element, bool overwrite = false)
    {
        return ScriptingEngine.TryDecodeElement(ExportString, out element) && TryRegisterElement(UniqueName, element, overwrite);
    }

    /// <summary>
    /// Tries to get previously registered layout by name.
    /// </summary>
    /// <param name="name">Layout's internal name.</param>
    /// <param name="layout">Result.</param>
    /// <returns>Whether operation succeeded.</returns>
    public bool TryGetLayoutByName(string name, [NotNullWhen(true)] out Layout? layout)
    {
        return Layouts.TryGetValue(name, out layout);
    }

    /// <summary>
    /// Tries to get previously registered element by name.
    /// </summary>
    /// <param name="name">Element's internal name.</param>
    /// <param name="element">Result.</param>
    /// <returns>Whether operation succeeded.</returns>
    public bool TryGetElementByName(string name, [NotNullWhen(true)] out Element? element)
    {
        return Elements.TryGetValue(name, out element);
    }

    /// <summary>
    /// Unregisters previously registered layout.
    /// </summary>
    /// <param name="name">Layout name.</param>
    /// <returns>Whether operation succeeded.</returns>
    public bool TryUnregisterLayout(string name)
    {
        return Layouts.Remove(name);
    }

    /// <summary>
    /// Unregisters previously registered element.
    /// </summary>
    /// <param name="name">Element name.</param>
    /// <returns>Whether operation succeeded.</returns>
    public bool TryUnregisterElement(string name)
    {
        return Elements.Remove(name);
    }

    public void RegisterElement(string UniqueName, Element element, bool overwrite = false)
    {
        if(!TryRegisterElement(UniqueName, element, overwrite))
        {
            throw new InvalidOperationException($"RegisterElement failed: Could not register element {UniqueName}");
        }
    }

    public Element RegisterElementFromCode(string UniqueName, string ExportString, bool overwrite = false)
    {
        if(TryRegisterElementFromCode(UniqueName, ExportString, out var ret, overwrite))
        {
            return ret;
        }
        else
        {
            throw new InvalidOperationException($"RegisterElementFromCode failed: Could not register element {UniqueName}");
        }
    }

    public Element? GetElementByName(string name)
    {
        if(TryGetElementByName(name, out var ret))
        {
            return ret;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a dictionary of currently registered layouts.
    /// </summary>
    /// <returns>Read only dictionary of currently registered layouts.</returns>
    public ReadOnlyDictionary<string, Layout> GetRegisteredLayouts()
    {
        return new ReadOnlyDictionary<string, Layout>(Layouts);
    }

    /// <summary>
    /// Returns a dictionary of currently registered elements.
    /// </summary>
    /// <returns>Read only dictionary of currently registered elements.</returns>
    public ReadOnlyDictionary<string, Element> GetRegisteredElements()
    {
        return new ReadOnlyDictionary<string, Element>(Elements);
    }

    /// <summary>
    /// Removes all layouts.
    /// </summary>
    public void ClearRegisteredLayouts()
    {
        Layouts.Clear();
    }

    /// <summary>
    /// Removes all elements
    /// </summary>
    public void ClearRegisteredElements()
    {
        Elements.Clear();
    }

    /// <summary>
    /// Removes all elements and layouts
    /// </summary>
    public void Clear()
    {
        ClearRegisteredElements();
        ClearRegisteredLayouts();
    }

    /// <summary>
    /// Retrieve valid and visible party members. Non cross-world parties only. Duty recorder supported.
    /// </summary>
    /// <returns>Enumberator of PlayerCharacter objects.</returns>
    public IEnumerable<IPlayerCharacter> GetPartyMembers()
    {
        return FakeParty.Get();
    }

    public void ApplyOverrides()
    {
        foreach(var x in Script.InternalData.Overrides.Elements)
        {
            if(Elements.ContainsKey(x.Key))
            {
                PluginLog.Debug($"[{Script.InternalData.FullName}] Overriding {x.Key} element with custom data");
                Elements[x.Key] = x.Value.JSONClone();
            }
        }
    }

    public void SaveOverrides()
    {
        if(Script.InternalData.Overrides.Elements.Count > 0)
        {
            EzConfig.SaveConfiguration(Script.InternalData.Overrides, Script.InternalData.OverridesPath, true, false);
        }
        else
        {
            if(File.Exists(Script.InternalData.OverridesPath))
            {
                PluginLog.Debug($"No overrides for {Script.InternalData.FullName}, deleting {Script.InternalData.OverridesPath}");
                File.Delete(Script.InternalData.OverridesPath);
            }
        }
    }

    /// <summary>
    /// Resets the state of the script, calling your OnReset method AND performing additional cleanup tasks.
    /// </summary>
    public void Reset()
    {
        ScriptingProcessor.OnReset(Script);
        CancelSchedulers();
    }

    /// <summary>
    /// Schedules a task to be executed in specified amount of time. All tasks are cleaned up when script is reset.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="delayMs"></param>
    public void Schedule(Action action, int delayMs)
    {
        TickSchedulers.Add(new(() =>
        {
            try
            {
                action();
            }
            catch(Exception ex)
            {
                ScriptingProcessor.LogError(Script, ex, nameof(Schedule));
            }
            TickSchedulers.RemoveAll(x => x.Disposed);
        }, delayMs));
    }

    /// <summary>
    /// Cancels all scheduled tasks. Auto-called upon calling Reset.
    /// </summary>
    public void CancelSchedulers()
    {
        PluginLog.Debug($"CancelSchedulers called for script {Script.InternalData.Name}");
        TickSchedulers.Each(x => x.Dispose());
        TickSchedulers.Clear();
    }

    /// <summary>
    /// Schedules reset of script after specified amount of miliseconds have passed. 
    /// </summary>
    /// <param name="delayMs"></param>
    public void ScheduleReset(uint delayMs = uint.MaxValue)
    {
        AutoResetAt = Environment.TickCount64 + delayMs;
    }


    /// <summary>
    /// 本腳本專屬的 NeoTaskManager 實例。第一次取用時才建立,腳本停用時釋放。
    /// 預設組態由 <see cref="SplatoonScript.TaskManagerConfiguration"/> 提供,腳本可以覆寫。
    /// </summary>
    public TaskManager TaskManager
    {
        get
        {
            return TaskManagerInternal ??= new(Script.TaskManagerConfiguration);
        }
        internal set
        {
            TaskManagerInternal = value;
        }
    }

    internal TaskManager? TaskManagerInternal = null;

    /// <summary>
    /// 目前玩家在優先度分配裡的定位(T1/T2/H1/...)。沒有分配到就回
    /// <c>RolePosition.Not_Selected</c>(列舉的 0 值)。
    /// </summary>
    public RolePosition RolePosition
    {
        get
        {
            if(P.PriorityPopupWindow?.Assignments != null)
            {
                for(var i = 0; i < P.PriorityPopupWindow.Assignments.Count; i++)
                {
                    var ass = P.PriorityPopupWindow.Assignments[i];
                    // 上游這裡比對的是 Splatoon.BasePlayer(錄影回放時可以換人)。
                    // 我方本體沒有 BasePlayer 那層,直接用本機玩家 ——
                    // 非回放情境下兩者等價。LocalPlayer 為 null 時 GetNameWithWorld 回 null,
                    // 與 NameWithWorld 比對必為 false,不會擲例外。
                    if(ass.IsInParty(false, out var m) && m.NameWithWorld == Svc.Objects.LocalPlayer.GetNameWithWorld())
                    {
                        return PriorityPopupWindow.RolePositions.SafeSelect(i);
                    }
                }
            }
            return default;
        }
    }

    /// <summary>
    /// 嘗試註冊一個從外掛匯出的元素,**以元素自己的名稱當 key**。
    /// </summary>
    /// <param name="ExportString">匯出字串。</param>
    /// <param name="element">解碼出來的元素。</param>
    /// <param name="overwrite">同名時是否覆寫。</param>
    /// <returns>是否註冊成功。</returns>
    public bool TryRegisterElementFromCode(string ExportString, [NotNullWhen(true)] out Element? element, bool overwrite = false)
    {
        return ScriptingEngine.TryDecodeElement(ExportString, out element) && TryRegisterElement(element.Name, element, overwrite);
    }

    /// <summary>
    /// 註冊一個從外掛匯出的元素,以元素自己的名稱當 key。失敗擲例外。
    /// </summary>
    public void RegisterElementFromCode(string ExportString, bool overwrite = false)
    {
        if(!TryRegisterElementFromCode(ExportString, out _, overwrite))
        {
            throw new InvalidOperationException($"RegisterElementFromCode failed: Could not register element {ExportString.Trim(100)}");
        }
    }

    /// <summary>
    /// 一行一個匯出字串,逐行註冊。空行會被略過。任何一行失敗就擲例外。
    /// </summary>
    public void RegisterElementsFromMultilineCode(string ExportStringPerLine, bool overwrite = false)
    {
        foreach(var ExportString in ExportStringPerLine.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if(!TryRegisterElementFromCode(ExportString, out _, overwrite))
            {
                throw new InvalidOperationException($"RegisterElementFromCode failed: Could not register element {ExportString.Trim(100)}");
            }
        }
    }

    /// <summary>
    /// 註冊一個從外掛匯出的佈局,以指定的 key 註冊。失敗擲例外。
    /// </summary>
    public void RegisterLayoutFromCode(string key, string code)
    {
        if(!TryRegisterLayoutFromCode(key, code, out _, false))
        {
            throw new InvalidOperationException($"Duplicate layout registration: {key}");
        }
    }

    /// <summary>
    /// 註冊一個從外掛匯出的佈局,**以佈局自己的名稱當 key**。失敗擲例外。
    /// </summary>
    /// <remarks>
    /// ⚠️ 這裡刻意**不是**轉呼叫 <c>TryRegisterLayoutFromCode(code, out _, false)</c>。
    /// 那個既有多載用的 key 是 <c>unnamed-{AutoIncrement}</c>,而上游的
    /// <c>RegisterLayoutFromCode(code)</c> 用的是佈局自己的 Name,呼叫端(上游腳本)
    /// 會拿那個名字去 TryGetLayoutByName、也會靠它對應使用者的自訂覆寫。
    /// 為了「既有行為一個位元都不動」,既有多載維持 unnamed-N 語意不改;
    /// 這個新多載則採用上游語意,未來同步上游腳本才不會靜默對不到 key。
    /// </remarks>
    public void RegisterLayoutFromCode(string code)
    {
        if(!ScriptingEngine.TryDecodeLayout(code, out var layout) || !TryRegisterLayout(layout.Name, layout, false))
        {
            throw new InvalidOperationException($"Duplicate layout registration: {code.Trim(100)}");
        }
    }

    /// <summary>
    /// 停用所有元素與佈局(不是移除註冊,只是把 Enabled 設 false)。
    /// </summary>
    public void Hide(bool elements = true, bool layouts = true)
    {
        if(elements)
        {
            foreach(var x in GetRegisteredElements())
            {
                x.Value.Enabled = false;
            }
        }
        if(layouts)
        {
            foreach(var x in GetRegisteredLayouts())
            {
                x.Value.Enabled = false;
            }
        }
    }

    /// <summary>
    /// 用指定的內容覆蓋單一佈局。
    /// </summary>
    /// <remarks>
    /// 上游這裡同時會寫進 OriginalLayoutsDirect(它有一組「註冊當下的原始副本」API)。
    /// 我方本體沒有那組 API,所以只寫實際會被渲染的那份。
    /// 另外沿用 JSONClone 而不是上游的 DSFClone —— 我方沒有那條序列化路徑。
    /// </remarks>
    public void ApplySingleLayoutOverride(string key, Layout value)
    {
        Layouts[key] = value.JSONClone();
    }

    /// <summary>
    /// 用指定的內容覆蓋單一元素。其餘同 <see cref="ApplySingleLayoutOverride"/> 的說明。
    /// </summary>
    public void ApplySingleElementOverride(string key, Element value)
    {
        Elements[key] = value.JSONClone();
    }

    /// <summary>
    /// 目前這一幀的「注意色」(ImGui packed uint)。用來標示必須立刻處理的元素。
    /// 顏色的產生方式由使用者在「渲染引擎 - 注意色」設定。
    /// </summary>
    public uint AttentionColor => Utils.GetAttentionColor().ToUint();

    /// <summary>
    /// 在注意視窗顯示一列置中的內容。<br />
    /// 重要:你交進來的 action 不會在呼叫的同一幀被執行。資料要先準備好再傳進來。<br />
    /// 重要:你的 action 可能被呼叫多次。不要在裡面做會改狀態的事。<br />
    /// 要讓視窗保持開著,必須持續每幀呼叫這個方法。
    /// </summary>
    public void DisplayAttentionWindowLine(Action action)
    {
        if(P.Config.DisabledAttentionWindowScripts.Contains(Script.InternalData.FullName)) return;
        S.AttentionOverlayWindow.Title = Script.InternalData.Name.Replace("_", " ") ?? "";
        S.AttentionOverlayWindow.ActionQueueCommand.Add((action, true));
    }

    /// <summary>
    /// 在注意視窗顯示一列置中的文字。<br />
    /// 要讓視窗保持開著,必須持續每幀呼叫這個方法。
    /// </summary>
    public void DisplayAttentionWindowLine(string text)
    {
        DisplayAttentionWindowLine(() => ImGuiEx.Text(text));
    }

    /// <summary>
    /// 在注意視窗顯示一列置中的文字。<br />
    /// 要讓視窗保持開著,必須持續每幀呼叫這個方法。<br />
    /// 參數以 $1、$2、$3 ... 代入,從 1 開始。
    /// </summary>
    public void DisplayAttentionWindowLine(string text, params string[] arguments)
    {
        for(var i = 0; i < arguments.Length; i++)
        {
            var a = arguments[i];
            text = text.Replace($"${i + 1}", a);
        }
        DisplayAttentionWindowLine(() => ImGuiEx.Text(text));
    }

    /// <summary>
    /// 在注意視窗顯示一列置中的彩色文字。<br />
    /// 要讓視窗保持開著,必須持續每幀呼叫這個方法。<br />
    /// 參數以 $1、$2、$3 ... 代入,從 1 開始。
    /// </summary>
    public void DisplayAttentionWindowLine(Vector4? color, string text, params string[] arguments)
    {
        for(var i = 0; i < arguments.Length; i++)
        {
            var a = arguments[i];
            text = text.Replace($"${i + 1}", a);
        }
        DisplayAttentionWindowLine(() => ImGuiEx.Text(color, text));
    }

    /// <summary>
    /// 能用 <see cref="DisplayAttentionWindowLine(Action)"/> 就優先用它。<br />
    /// 在注意視窗裡畫一段不做置中處理的原始內容。<br />
    /// 重要:你交進來的 action 不會在呼叫的同一幀被執行。<br />
    /// 重要:你的 action 可能被呼叫多次。不要在裡面做會改狀態的事。<br />
    /// 要讓視窗保持開著,必須持續每幀呼叫這個方法。
    /// </summary>
    public void DisplayAttentionWindowRaw(Action action)
    {
        if(P.Config.DisabledAttentionWindowScripts.Contains(Script.InternalData.FullName)) return;
        S.AttentionOverlayWindow.Title = Script.InternalData.Name.Replace("_", " ") ?? "";
        S.AttentionOverlayWindow.ActionQueueCommand.Add((action, false));
    }
}
