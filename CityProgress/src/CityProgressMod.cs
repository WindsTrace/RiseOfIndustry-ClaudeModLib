// CityProgress: tall, narrow panel (like the game's town-center / shop panels) with one card per city:
// name, tier -> next tier, population (big accent number), progress bar to the next tier threshold and status.
// Built on ClaudeModLib: switch it on in the ClaudeModLib panel. English / 简体中文 via Lang; colours from the theme.
using System.Collections.Generic;
using ClaudeModLib;
using ProjectAutomata;
using UnityEngine;

namespace CityProgress
{
    public class CityProgressMod : Mod
    {
        const float RefreshSeconds = 2f;

        Feature _show, _onlyMine;
        readonly List<CityInfo> _rows = new List<CityInfo>();
        readonly HashSet<SettlementBase> _supplied = new HashSet<SettlementBase>();
        float _nextRefresh;

        public override void OnModWasLoaded()
        {
            base.OnModWasLoaded();
            var handle = ModLib.Register(this, LocText.Of("City progress", "城市升级进度"));
            // Session-only and off: every game starts with the window closed; open it from the ClaudeModLib panel,
            // close it with the title-bar ×. (A persisted "on" used to reopen it in every new game.)
            _show = handle.AddFeature("window", LocText.Of("Show city progress window", "显示城市升级进度窗口"), false,
                                      null, false);
            _onlyMine = handle.AddFeature("onlyMine", LocText.Of("Only cities I have supplied", "只显示我供过货的城市"), true);
            handle.AddWindow(LocText.Of("City progress", "城市升级进度"), new Rect(20, 110, 380, 640), DrawWindow, _show);
        }

        void Update()
        {
            if (!ModLib.inGame || !_show.enabled || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            _rows.Clear(); _supplied.Clear();
            var player = Cities.Player;
            foreach (var s in Cities.All())
            {
                if (s == null) continue;
                try
                {
                    _rows.Add(Cities.GetInfo(s));
                    if (Cities.HasSupplied(s, player)) _supplied.Add(s);
                }
                catch (System.Exception) { /* city not initialised yet (world still generating); retry next refresh */ }
            }
            _rows.Sort((a, b) => b.progress.CompareTo(a.progress));
        }

        void DrawWindow(LibWindow w)
        {
            int shown = 0;
            foreach (var r in _rows) if (!_onlyMine.enabled || _supplied.Contains(r.settlement)) shown++;

            Ui.SectionHeader((_onlyMine.enabled ? Lang.T("Cities I supply", "我供货的城市") : Lang.T("All cities", "全部城市")) + "  (" + shown + ")");
            GUILayout.Label(Lang.T("Population settles every 15 days as shops consume goods.", "人口每 15 天随商店消耗商品结算一次。"), Ui.small);

            w.scroll = GUILayout.BeginScrollView(w.scroll, GUILayout.ExpandHeight(true));
            foreach (var r in _rows)
            {
                if (_onlyMine.enabled && !_supplied.Contains(r.settlement)) continue;
                DrawCard(r);
                GUILayout.Space(6);
            }
            if (shown == 0)
                GUILayout.Label(_onlyMine.enabled
                    ? Lang.T("You have not supplied any city yet. Turn off \"Only cities I have supplied\" in the ClaudeModLib panel to see all.",
                             "还没有向任何城市供过货。可在 ClaudeModLib 面板关闭“只显示我供过货的城市”。")
                    : Lang.T("No city data.", "没有城市数据。"), Ui.label);
            GUILayout.EndScrollView();

            _onlyMine.enabled = Ui.Switch(_onlyMine.enabled, Lang.T("Only cities I have supplied", "只显示我供过货的城市"));
        }

        static void DrawCard(CityInfo r)
        {
            string status; Color color;
            Describe(r.status, out status, out color);

            GUILayout.BeginVertical(Ui.box);

            // name + tier
            GUILayout.BeginHorizontal();
            GUILayout.Label(r.name.ToUpperInvariant(), Ui.bold);
            GUILayout.FlexibleSpace();
            GUILayout.Label(r.nextTier != null ? TierName(r.tier) + "  →  " + TierName(r.nextTier) : TierName(r.tier), Ui.small);
            GUILayout.EndHorizontal();

            // population (big) + status
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(Lang.T("Population", "总人口"), Ui.small);
            GUILayout.Label(r.population.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), Ui.value);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.Label(Lang.T("Status", "城镇状态"), Ui.small);
            Ui.ColoredLabel(status, color, GUILayout.MaxWidth(170));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // progress to the next tier
            if (r.nextTier != null)
            {
                Ui.ProgressBar(r.progress, color, 10);
                GUILayout.Label(string.Format(Lang.T("Next tier at {0}  ·  {1} to go  ·  {2:0.0}%", "下一级门槛 {0}  ·  还差 {1}  ·  {2:0.0}%"),
                    Ui.Number(r.nextTierThreshold), Ui.Number(r.remaining), r.progress * 100f), Ui.small);
            }
            GUILayout.EndVertical();
        }

        // Tier names by tierId. SettlementTier.tierName is already translated by the game in some languages
        // (Chinese uses "村庄" for both Hamlet and Village), so it cannot be used to tell tiers apart.
        static string TierName(SettlementTier tier)
        {
            if (tier == null) return "?";
            switch (tier.tierId)
            {
                case 100: return Lang.T("Hamlet", "村庄");
                case 200: return Lang.T("Village", "村镇");
                case 300: return Lang.T("Town", "城镇");
                case 400: return Lang.T("City", "城市");
                case 500: return Lang.T("Metropolis", "大都市");
                case 1000: return Lang.T("Bloated", "超大都市");
                default: return tier.tierName;
            }
        }

        static void Describe(CityStatus s, out string text, out Color color)
        {
            switch (s)
            {
                case CityStatus.Growing: text = Lang.T("Growing", "成长中"); color = Ui.Green; break;
                case CityStatus.LimitReached: text = Lang.T("At limit · upgrade pending", "已封顶·待触发升级"); color = Ui.Yellow; break;
                case CityStatus.WaitingForSponsor: text = Lang.T("At limit · needs a sponsor (region permit)", "已封顶·等待赞助（需有人持有该区许可证）"); color = Ui.Red; break;
                case CityStatus.Advancing: text = Lang.T("Upgrade contracts running", "升级任务进行中"); color = Ui.Blue; break;
                case CityStatus.MaxTier: text = Lang.T("Max tier", "最高等级"); color = Ui.Blue; break;
                case CityStatus.Dead: text = Lang.T("Abandoned", "已废弃"); color = Ui.Grey; break;
                default: text = Lang.T("Stagnant (not enough goods)", "停滞（缺少商品供应）"); color = Ui.Yellow; break;
            }
        }
    }
}
