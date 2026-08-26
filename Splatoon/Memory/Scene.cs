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
        // EnvManager 是 [StaticAddress("0F 28 F2 48 8B 05", 6, isPointer: true)]：產生器對
        // isPointer 的實作是 `return *ppInstance;`，也就是解出來的是「存放指標的位址」，
        // 圖形環境管理器在標題畫面／讀取畫面尚未建立時，解參考結果就是 null。
        //
        // 🔴 原本 Init() 只在外掛載入時跑一次（Splatoon.cs 的 Init 流程），EnvManager 當下
        //    是 null 就只記一行 Error，ActiveScene 從此定格在 null 而且再也不會重查 ——
        //    之後每一幀的視窗標題都會對 null 解參考，那是攔不到的 AccessViolationException
        //    （corrupted-state exception，try/catch 與 HookSafety.ExecuteSafe 都無效）。
        //
        // ⇒ 改成懶重查：取用時若還沒解析成功就再解析一次，成功之後沿用快取。
        //    快取這個指標是安全的，它與「跨幀保存原生物件指標」不同 —— 目標是
        //    EnvManager 這個單例的靜態位址 + 36，在遊戲行程存活期間不會搬家。
        internal static byte* ActiveScene = null;

        /// <summary>
        /// 目前場景編號；環境管理器還沒建立時回 null（呼叫端必須自己處理「不知道」，
        /// 不要拿 0 頂替 —— 0 是 byte 範圍內的合法值，謊報會誤導使用者與腳本）。
        /// </summary>
        internal static byte? Current => TryResolve() ? *ActiveScene : (byte?)null;

        /// <summary>靜默解析（失敗不記 log，因為這條路每幀都會走到）。回傳位址是否可用。</summary>
        private static bool TryResolve()
        {
            if(ActiveScene != null) return true;
            var n = (nint)EnvManager.Instance();
            if(n == nint.Zero) return false;
            ActiveScene = (byte*)(n + 36);
            return true;
        }

        /// <summary>載入時呼叫一次。記錄行為與原本相同（失敗記一行 Error），
        /// 差別只在失敗不再是終局 —— 後續取用會自己重試。</summary>
        internal static void Init()
        {
            PluginLog.Debug($"Init Scene");
            if(!TryResolve())
            {
                PluginLog.Error($"EnvManager was zero (will retry on demand)");
            }
        }
    }
}
