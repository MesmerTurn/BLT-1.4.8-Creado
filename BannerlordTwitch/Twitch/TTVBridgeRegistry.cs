using System;

namespace BannerlordTwitch
{
    /// <summary>
    /// Extension point that lets sub-modules (e.g. BLTAdoptAHero, which core cannot
    /// reference directly) supply a JSON-serialisable game-state snapshot for
    /// TTVBridgeService to push to the BannerlordTTV Worker.
    /// </summary>
    public static class TTVBridgeRegistry
    {
        /// <summary>
        /// Set by the sub-module that owns the hero/power data. Must be safe to call
        /// only on the main game thread (it will be invoked via MainThreadSync).
        /// Returns null if there's nothing to report yet.
        /// </summary>
        public static Func<object> SnapshotProvider { get; set; }
    }
}
