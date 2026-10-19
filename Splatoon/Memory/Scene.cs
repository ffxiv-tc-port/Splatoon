using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.Memory
{
    public static unsafe class Scene
    {
        // EnvManager 單例 + 36：目前天氣在該區天氣表裡的槽位（遊戲存成有號 byte，無對應時這裡讀到 255）。
        // 契約：只讀不寫；不保存指標，每次取用都從 EnvManager.Instance() 重取 ——
        // 單例還沒建立或關遊戲時已釋放就回 null，對舊位址解參考是攔不到的 AccessViolationException。
        private const int ActiveSceneOffset = 36;

        /// <summary>
        /// 目前場景編號；環境管理器還沒建立時回 null（呼叫端必須自己處理「不知道」，
        /// 不要拿 0 頂替 —— 0 是 byte 範圍內的合法值，謊報會誤導使用者與腳本）。
        /// </summary>
        internal static byte? Current
        {
            get
            {
                var env = EnvManager.Instance();
                if(env == null) return null;
                return *((byte*)env + ActiveSceneOffset);
            }
        }

        /// <summary>載入時呼叫一次。失敗記一行 Error，
        /// 但不是終局 —— 後續取用會自己重試。</summary>
        internal static void Init()
        {
            PluginLog.Debug($"Init Scene");
            if(EnvManager.Instance() == null)
            {
                PluginLog.Error($"EnvManager was zero (will retry on demand)");
            }
        }
    }
}
