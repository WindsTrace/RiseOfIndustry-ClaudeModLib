using System;
using System.Collections;
using System.Linq;
using ProjectAutomata;
using SVGImporter;
using UnityEngine;
using UnityEngine.UI;

namespace ClaudeModLib
{
    /// <summary>
    /// Adds the ClaudeModLib button to the game's top-right toolbar (MainUIToolbar).
    /// Technique: clone an existing toolbar button, strip the game's behaviour scripts from the clone,
    /// swap the SVG icon (assets/claudemodlib.svg) and wire our own click handler.
    /// If this fails (UI changed in a game update) a small IMGUI "Mods" button is shown instead.
    /// </summary>
    public static class Toolbar
    {
        public const string IconAsset = "claudemodlib";
        public static bool installed { get; private set; }
        static int _attempts;
        static GameObject _button;

        internal static void ResetForNextGame() { _attempts = 0; installed = false; _button = null; }

        internal static void TryInstall()
        {
            if (installed && _button != null) return;
            installed = false;
            if (_attempts >= 5) return;
            _attempts++;
            try
            {
                var toolbar = UnityEngine.Object.FindObjectOfType<MainUIToolbar>();
                if (toolbar == null) return;
                var buttons = Reflect.GetOr<IList>(toolbar, "buttons", null);
                if (buttons == null || buttons.Count == 0) return;

                var template = ((Component)buttons[buttons.Count - 1]).gameObject;
                if (_attempts == 1) Log.Info("toolbar hierarchy:\n" + DevTools.DescribeHierarchy(template.transform.parent.gameObject, 3));

                // Clone under the inactive prefab root first so no game script runs on the clone.
                var clone = UnityEngine.Object.Instantiate(template, ModAssets.PrefabRoot, false);
                clone.name = "ClaudeModLibButton";
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("UnityEngine") || ns.StartsWith("SVGImporter") || ns.StartsWith("TMPro")) continue;
                    UnityEngine.Object.DestroyImmediate(mb);
                }

                var btn = clone.GetComponentInChildren<Button>(true);
                if (btn == null) { Log.Warn("toolbar template has no Button; using fallback"); UnityEngine.Object.Destroy(clone); return; }
                btn.onClick = new Button.ButtonClickedEvent();   // drops the template's persistent listeners too
                btn.onClick.AddListener(() => LibPanel.visible = !LibPanel.visible);

                var icon = ModAssets.Get<SVGAsset>(IconAsset);
                var svgs = clone.GetComponentsInChildren<SVGImage>(true);
                if (icon != null && svgs.Length > 0)
                {
                    // The icon is usually the innermost SVGImage; set all of them to be safe.
                    foreach (var s in svgs.Where(s => s.transform != clone.transform || svgs.Length == 1)) s.vectorGraphics = icon;
                }

                clone.transform.SetParent(template.transform.parent, false);
                clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                clone.SetActive(true);
                _button = clone;
                installed = true;
                Log.Info("toolbar button installed next to '" + template.name + "'");
            }
            catch (Exception e)
            {
                Log.Warn("toolbar install failed (attempt " + _attempts + "): " + e.Message);
            }
        }
    }
}
