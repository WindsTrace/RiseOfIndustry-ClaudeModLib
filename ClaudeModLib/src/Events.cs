using ProjectAutomata;

namespace ClaudeModLib
{
    /// <summary>
    /// World events = the banners on the right side of the screen (news, weather, "Settlement Leveled Up" ...).
    /// Define one in content JSON (type "ProjectAutomata.ContentCreationModels.CCWorldEventDataModel":
    /// name, displayName, description, svgIcon, type Good|Neutral|Bad, when, easy/normal/hard
    /// { durationMin, durationMax, effects: [] }), then show it with <see cref="Show"/>.
    /// The JSON model cannot set triggerMode, so call <see cref="MakeManual"/> in OnAllModsLoaded or the random event
    /// scheduler may fire your event by itself.
    /// </summary>
    public static class Events
    {
        /// <summary>Prevents the random scheduler from picking these events (sets triggerMode = MANUAL).</summary>
        public static void MakeManual(params string[] eventNames)
        {
            foreach (var n in eventNames)
            {
                var e = Data.Get<StaticWorldEventData>(n);
                if (e != null) e.triggerMode = WorldEventTriggerMode.MANUAL;
                else Log.Warn("Events.MakeManual: event not found: " + n);
            }
        }

        /// <summary>
        /// Shows an event banner for the local player, optionally tied to a region, for <paramref name="days"/> days
        /// (0 = the event's own duration). Same call the game uses for "Settlement Leveled Up". Returns false if it
        /// could not be triggered.
        /// </summary>
        public static bool Show(string eventName, Region region = null, int days = 0)
        {
            var data = Data.Get<StaticWorldEventData>(eventName);
            if (data == null) { Log.Warn("Events.Show: event not found: " + eventName); return false; }
            var actor = Players.Actor;
            if (actor == null) return false;
            var p = WorldEventCreationParams.GetFromPool();
            p.region = region;
            if (days > 0) p.duration = days;
            p.forceEvent = true;
            return WorldEventManager.instance.TriggerStaticEvent(data, actor.worldEvents, p);
        }

        /// <summary>Shows <paramref name="englishEvent"/> or <paramref name="chineseEvent"/> according to <see cref="Lang"/>.</summary>
        public static bool ShowLocalized(string englishEvent, string chineseEvent, Region region = null, int days = 0)
        {
            return Show(Lang.isChinese ? chineseEvent : englishEvent, region, days);
        }
    }
}
