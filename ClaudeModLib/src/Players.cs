using ProjectAutomata;

namespace ClaudeModLib
{
    /// <summary>
    /// The local human player and their headquarters. Note: at map generation no HQ exists yet; the player places
    /// it after looking at the map, so "where is the HQ" can only be answered once <see cref="HqBuilt"/> is true.
    /// </summary>
    public static class Players
    {
        /// <summary>The human player (null outside a game).</summary>
        public static Player Local { get { return Player.activeActor as Player; } }

        /// <summary>The local player as an IActor (for sales, permits, contracts, world events).</summary>
        public static IActor Actor { get { return Player.activeActor; } }

        public static bool HqBuilt { get { var p = Local; return p != null && p.hqBuilt; } }

        /// <summary>HQ building, or null before it is placed.</summary>
        public static Building Hq { get { var p = Local; return p != null ? p.hq : null; } }

        /// <summary>Region of the HQ, or null before it is placed.</summary>
        public static Region HqRegion { get { var hq = Hq; return hq != null ? hq.region : null; } }

        /// <summary>True if <paramref name="region"/>'s permit is owned by the local player.</summary>
        public static bool OwnsPermit(Region region)
        {
            if (region == null) return false;
            try { return PermitManager.instance.GetPermitOwner(region) == Actor; } catch { return false; }
        }
    }
}
