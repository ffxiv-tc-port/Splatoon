using Splatoon.Gui.Windows;
using Splatoon.RenderEngines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.Services;
public static class S
{
    public static ThreadPool ThreadPool { get; private set; }
    internal static RenderManager RenderManager { get; private set; }
    //internal static VbmCamera VbmCamera { get; private set; }
    internal static ScriptFileWatcher ScriptFileWatcher { get; private set; }
    internal static InfoBar InfoBar { get; private set; }
    //internal static StatusEffectManager StatusEffectManager { get; private set; }
    internal static DataMigrator DataMigrator { get; private set; }
    // SingletonServiceManager 會在 Splatoon.cs 呼叫 Initialize(typeof(S)) 時建立它,
    // 並在外掛卸載時呼叫它的 Dispose()(它實作了 IDisposable,用來釋放字型 handle)。
    internal static AttentionOverlayWindow AttentionOverlayWindow { get; private set; }
}
