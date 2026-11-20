// CityProgress: window showing each city's tier and population progress towards the next tier.
// Built on ClaudeModLib: switch it on in the ClaudeModLib panel (toolbar button), no hotkey of its own.
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
            var handle = ModLib.Register(this, "城市升级进度");
            _show = handle.AddFeature("window", "显示城市升级进度窗口", true);
            _onlyMine = handle.AddFeature("onlyMine", "只显示我供过货的城市", true);
            handle.AddWindow("城市升级进度", new Rect(20, 120, 620, 320), DrawWindow, _show);
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
                _rows.Add(Cities.GetInfo(s));
                if (Cities.HasSupplied(s, player)) _supplied.Add(s);
            }
            _rows.Sort((a, b) => b.progress.CompareTo(a.progress));
        }

        void DrawWindow(LibWindow w)
        {
            GUILayout.Label("每 " + RefreshSeconds + " 秒刷新；人口每 15 天随商店消耗结算一次", Ui.small);
            w.scroll = GUILayout.BeginScrollView(w.scroll, GUILayout.Height(w.rect.height - 60));
            int shown = 0;
            foreach (var r in _rows)
            {
                if (_onlyMine.enabled && !_supplied.Contains(r.settlement)) continue;
                shown++;
                DrawRow(r);
            }
            if (shown == 0)
                GUILayout.Label(_onlyMine.enabled ? "还没有向任何城市供过货。可在 ClaudeModLib 面板关闭“只显示我供过货的城市”。" : "没有城市数据。", Ui.label);
            GUILayout.EndScrollView();
        }

        static void DrawRow(CityInfo r)
        {
            string status; Color color;
            Describe(r.status, out status, out color);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(r.name, Ui.label, GUILayout.Width(170));
            GUILayout.Label(r.nextTierName != null ? r.tierName + " → " + r.nextTierName : r.tierName, Ui.label, GUILayout.Width(170));
            Ui.ColoredLabel(status, color);
            GUILayout.EndHorizontal();
            Ui.ProgressBar(r.progress, color);
            GUILayout.Label(r.nextTier != null
                ? string.Format("人口 {0}  /  下一级门槛 {1}   （{2:0.0}%，还差 {3}）",
                    Ui.Number(r.population), Ui.Number(r.nextTierThreshold), r.progress * 100f, Ui.Number(r.remaining))
                : "人口 " + Ui.Number(r.population), Ui.small);
            GUILayout.EndVertical();
        }

        static void Describe(CityStatus s, out string text, out Color color)
        {
            switch (s)
            {
                case CityStatus.Growing: text = "成长中"; color = Ui.Green; break;
                case CityStatus.LimitReached: text = "已封顶·待触发升级"; color = Ui.Yellow; break;
                case CityStatus.WaitingForSponsor: text = "已封顶·等待赞助（无人持有该区许可证）"; color = Ui.Orange; break;
                case CityStatus.Advancing: text = "升级任务进行中"; color = Ui.Yellow; break;
                case CityStatus.MaxTier: text = "最高等级"; color = Ui.Blue; break;
                case CityStatus.Dead: text = "已废弃"; color = Color.gray; break;
                default: text = "停滞（缺少商品供应）"; color = Ui.Grey; break;
            }
        }
    }
}
