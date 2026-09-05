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
        /// 🔴 原本這裡是一個沒有到期時間的布林旗標，唯一的清除點是招募看板真的走了 PostSetup
        /// （<c>OnDisable</c> 那個因為 <c>ValidTerritories</c> 是空集合，實際上只在登入／登出跑）。
        /// 只要那一次沒觸發 —— 最好走的一條是「看板本來就開著，<c>/pfinder</c> 是切換所以那次是關掉」，
        /// 而 addon 被重用時 <c>PostSetup</c> 也可能不再觸發 —— 旗標就一直武裝著，
        /// 使用者幾十分鐘後自己開招募看板會被帶進建立招募畫面。
        /// 改成幀序快照之後，逾時本身就是解除點，不需要任何額外的清除路徑。
        /// <para>
        /// 📌 這<b>不是</b>繪製幀計數器：<see cref="CurrentFrame"/> 取的是遊戲主迴圈的
        /// <c>Framework.FrameCounter</c>，畫面隱藏（過場／隱藏 UI 熱鍵）期間照樣前進，
        /// 所以逾時不會永遠不到。
        /// </para>
        /// <para>
        /// 🔴🔴 存入與讀取<b>必須是同一個時鐘</b>：遊戲側的 <c>FrameCounter</c> 從開機起算、
        /// Dalamud 的 <c>UiBuilder.FrameCount</c> 從外掛載入起算，兩者絕對值差好幾個數量級，
        /// 混用的結果不是「永遠不到期」就是「永遠已到期」。
        /// </para>
        /// <para>
        /// 300 幀在 60fps 下約 5 秒，遠大於「打完指令到看板開出來」需要的那幾幀。
        /// </para>
        /// </remarks>
        private const uint OpenCreateTimeoutFrames = 300;

        /// <summary>目前的遊戲幀序；取不到 <c>Framework</c> 時回 <see langword="null"/>。</summary>
        /// <remarks>
        /// 🔴 <c>Framework.Instance()</c> 宣告成 <c>[StaticAddress(..., isPointer: true)]</c>：回的是靜態位址裡
        /// 存放的那個指標，產生器只在特徵碼失配時擲例外、對取回的值<b>不判空</b>。登入前、登出後、
        /// 關閉流程中它真的會是 null，裸解參考就是 AccessViolationException（.NET Core 的
        /// corrupted-state exception，<c>try/catch</c> 攔不到）⇒ 只能事前判空。
        /// <para>
        /// 📌 <c>FrameCounter</c> 由遊戲主迴圈遞增，<b>不是</b>繪製幀計數器；ECommons 的
        /// <c>FrameDelayTask</c>（「延遲 N 幀」）用的就是同一個來源。
        /// </para>
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
        /// <para>
        /// 📌 <c>args.Addon.Address</c> 只在這個事件裡當場用掉，不跨幀保存。
        /// </para>
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
        /// 📌 先記幀序再送指令：<c>Chat.SendMessage</c> 會不會在自己這一次呼叫裡就把
        /// <c>LookingForGroup</c> 的 <c>PostSetup</c> 走完，是外面的實作細節；
        /// 先記下來就不必依賴那個順序，而且幀窗一樣管得住。
        /// <para>
        /// 🔴 取不到幀序就維持未武裝（fail-closed），但 <c>/pfinder</c> 照送 ——
        /// 那才是使用者打這個指令要的主要動作，少掉的只是自動跳到建立招募那一步。
        /// </para>
        /// <para>
        /// 📌 這裡的 <c>EzThrottler</c> 是 <c>SplatoonScript</c> 上同名的<b>實例</b>屬性
        /// （每個腳本各一份），不是 ECommons 那個全域靜態實例。
        /// </para>
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
