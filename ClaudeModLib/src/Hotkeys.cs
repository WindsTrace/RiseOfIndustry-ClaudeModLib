using System.Collections;
using ProjectAutomata;

namespace ClaudeModLib
{
    /// <summary>
    /// Rebindable hotkeys through the game's own input system.
    /// 1) Declare an InputButton in your content JSON (it appears in the game's key-binding settings):
    ///    { "type": "ProjectAutomata.ContentCreationModels.CCInputButtonModel",   // "ProjectAutomata.InputButton" has no lookup registered
    ///      "object": { "name": "MyModToggle", "category": "UI", "displayName": "My Mod: toggle window",
    ///                  "description": "...", "uiOrder": 1000, "keys": [ { "keyCode": "F9" } ] } }
    ///    Key: { "keyCode": UnityEngine.KeyCode name, "axisType": None|MouseX|MouseY|ScrollWheel,
    ///           "modifier": { "keyCode": "LeftControl" } }
    /// 2) Poll it every frame from Update(): if (Hotkeys.Pressed("MyModToggle")) ...
    /// </summary>
    public static class Hotkeys
    {
        public const string LibToggle = "ClaudeModLibToggle";

        /// <summary>True on the frame the button was pressed. False if the input system is not ready or locked.</summary>
        public static bool Pressed(string inputButtonName)
        {
            var button = Ensure(inputButtonName);
            if (button == null) return false;
            try { return InputManager.GetButtonDown(button); } catch { return false; }
        }

        /// <summary>True while held.</summary>
        public static bool Held(string inputButtonName)
        {
            var button = Ensure(inputButtonName);
            if (button == null) return false;
            try { return InputManager.GetButton(button); } catch { return false; }
        }

        /// <summary>Human readable binding, e.g. "F9" (uses the game's formatter, reflects user rebinding).</summary>
        public static string Describe(string inputButtonName)
        {
            var button = Ensure(inputButtonName);
            if (button == null) return "?";
            try { return InputManager.ToString(button); } catch { return "?"; }
        }

        // Content (and our InputButton) may load after InputManager.Initialize; add the entry if missing.
        static InputButton Ensure(string name)
        {
            var button = Data.Get<InputButton>(name);
            if (button == null) return null;
            InputManager im;
            try { im = InputManager.instance; } catch { return null; }
            if (im == null) return null;
            var map = Reflect.GetOr<IDictionary>(im, "inputMap", null);
            if (map != null && !map.Contains(button))
            {
                try { Reflect.Call(im, "AddInputEntry", button); Log.Info("hotkey registered with InputManager: " + name); }
                catch (System.Exception e) { Log.Warn("AddInputEntry(" + name + ") failed: " + e.Message); return null; }
            }
            return button;
        }

        internal static void InitLibHotkey()
        {
            if (Data.Get<InputButton>(LibToggle) == null) Log.Warn("InputButton " + LibToggle + " missing (content/ not loaded?)");
        }

        internal static bool LibToggleDown() { return Pressed(LibToggle); }
    }
}
