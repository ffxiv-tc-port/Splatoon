using ECommons.Hooks;
using Lumina.Excel.Sheets;
using Splatoon.Modules;
using Splatoon.SplatoonScripting;

namespace Splatoon.Memory
{
    internal static unsafe class DirectorUpdateProcessor
    {
        internal static void ProcessDirectorUpdate(long a1, long a2, DirectorUpdateCategory a3, uint a4, uint a5, int a6, int a7)
        {
            if(P.Config.Logging)
            {
                var text = $"Director Update: {a3:X}, {a4:X8}, {a5:X8}, {a6:X8}, {a7:X8}";
                Logger.Log(text);
                PluginLog.Verbose(text);
                P.LogWindow.Log(text);
            }
            PhaseUpdater.UpdateFromDirector(a3);
            ScriptingProcessor.OnDirectorUpdate(a3);
            // 上游的完整參數多載。🔴 台服 7.20 的 director update 函式只吃 7 個引數
            // (離線反組譯 0x140B6A260 實證,第 8/9 個引數的堆疊槽從未被讀取),
            // 我方 ECommons 的委派也就只有 7 個 —— a8／a9 補 0。
            ScriptingProcessor.OnDirectorUpdate((nint)a1, (uint)a2, a3, a4, a5, a6, a7, 0, 0);
        }
    }
}
