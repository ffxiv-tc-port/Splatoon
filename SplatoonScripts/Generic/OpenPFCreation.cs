using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SplatoonScriptsOfficial.Generic
{
    public unsafe class OpenPFCreation : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => [];
        public override Metadata Metadata => new(2, "NightmareXIV");

        public override Dictionary<int, string> Changelog => new()
        {
            [2] = """
            修正：如果 /createpf 那一次沒有真的把招募看板開出來（最好走的一條是看板本來就開著，
            /pfinder 是切換，所以那一次其實是把它關掉），「下次開招募看板就跳到建立招募」的旗標
            會一直留著。使用者過幾十分鐘自己開招募看板時，會被莫名其妙帶進建立招募畫面。
            現在改成記下送出指令的那個遊戲幀，超過時限就當作沒按過 —— 逾時本身就是解除點；
            取不到幀序時不動作（fail-closed）。
            """
        };

        /// <summary>
        /// 送出 <c>/createpf</c> 的那個<b>遊戲幀</b>序；<see langword="null"/> ＝目前沒有待處理的請求。
        /// </summary>
        /// <remarks>
        /// 🔴 這裡只存幀序，<b>不存任何原生指標</b>：兩次事件之間 addon 隨時可能被拆掉重建。
        /// </remarks>
        private uint? OpenCreateAtFrame;

        /// <summary>送出指令之後最多等幾個<b>遊戲幀</b>還算數。到期＝判定「那一次沒有開出看板」。</summary>
        /// <remarks>
        /// 🔴 用幀序快照而不是布林旗標：那一次沒開出看板時（看板本來就開著，<c>/pfinder</c> 是切換所以那次是關掉；
        /// addon 被重用時 <c>PostSetup</c> 也可能不觸發）旗標會一直武裝著，使用者之後自己開招募看板
        /// 會被帶進建立招募畫面。逾時本身就是解除點；300 幀在 60fps 下約 5 秒。
        /// 🔴🔴 存入與讀取<b>必須是同一個時鐘</b>：<see cref="CurrentFrame"/> 取的是遊戲主迴圈的
        /// <c>Framework.FrameCounter</c>（從開機起算、畫面隱藏期間照樣前進），Dalamud 的
        /// <c>UiBuilder.FrameCount</c> 從外掛載入起算，混用的結果不是「永遠不到期」就是「永遠已到期」。
        /// </remarks>
        private const uint OpenCreateTimeoutFrames = 300;

        /// <summary>目前的遊戲幀序；取不到 <c>Framework</c> 時回 <see langword="null"/>。</summary>
        /// <remarks>
        /// 🔴 <c>Framework.Instance()</c> 宣告成 <c>[StaticAddress(..., isPointer: true)]</c>：登入前、登出後、
        /// 關閉流程中它真的會是 null，裸解參考就是 AccessViolationException（<c>try/catch</c> 攔不到）
        /// ⇒ 只能事前判空。<c>FrameCounter</c> 由遊戲主迴圈遞增，<b>不是</b>繪製幀計數器。
        /// </remarks>
        private static uint? CurrentFrame()
        {
            var framework = CSFramework.Instance();
            if(framework == null) return null;
            return framework->FrameCounter;
        }

        public override void OnEnable()
        {
            Svc.Commands.AddHandler("/createpf", new(OpenPF));
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LookingForGroup", OpenRecruitment);
        }

        /// <remarks>
        /// 🔴 取不到幀序時<b>不送</b>（fail-closed）：沒有時間基準就分不出「這是剛才那次指令的結果」
        /// 還是「使用者自己開的看板」，而猜錯的代價是把使用者莫名其妙丟進建立招募畫面。
        /// 這種時候刻意<b>不</b>清掉請求 —— 幀窗本身保證它不會無限期有效，
        /// 下一次讀得到時鐘時就會判成逾時。
        /// 📌 <c>args.Addon.Address</c> 只在這個事件裡當場用掉，不跨幀保存。
        /// </remarks>
        private void OpenRecruitment(AddonEvent type, AddonArgs args)
        {
            if(OpenCreateAtFrame == null) return;
            var now = CurrentFrame();
            if(now == null) return;
            var requestedAt = OpenCreateAtFrame.Value;
            OpenCreateAtFrame = null;
            // unchecked：FrameCounter 是 uint，溢位回繞時無號減法照樣給出正確的「過了幾幀」。
            if(unchecked(now.Value - requestedAt) >= OpenCreateTimeoutFrames) return;
            Callback.Fire((AtkUnitBase*)args.Addon.Address, true, 14);
        }

        /// <remarks>
        /// 📌 先記幀序再送指令：不依賴 <c>Chat.SendMessage</c> 會不會在這一次呼叫裡就把
        /// <c>LookingForGroup</c> 的 <c>PostSetup</c> 走完。
        /// 🔴 取不到幀序就維持未武裝（fail-closed），但 <c>/pfinder</c> 照送 ——
        /// 那才是使用者打這個指令要的主要動作，少掉的只是自動跳到建立招募那一步。
        /// 📌 這裡的 <c>EzThrottler</c> 是 <c>SplatoonScript</c> 上同名的<b>實例</b>屬性
        /// （每個腳本各一份），不是 ECommons 那個全域靜態實例。
        /// </remarks>
        private void OpenPF(string command, string arguments)
        {
            if(EzThrottler.Throttle("CreatePF"))
            {
                OpenCreateAtFrame = CurrentFrame();
                Chat.Instance.SendMessage("/pfinder");
            }
        }

        public override void OnDisable()
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "LookingForGroup", OpenRecruitment);
            Svc.Commands.RemoveHandler("/createpf");
            OpenCreateAtFrame = null;
        }
    }
}
