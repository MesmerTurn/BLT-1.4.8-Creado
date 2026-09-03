using System;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero.Patches
{
    // 2026-08-17: lets an addon (e.g. PeasantRebellionPerks) contribute a perk/prestige-adjusted
    // stat summary for a hero, for display on the Hero Appearance Gallery page, without the core
    // fork referencing the addon assembly - same reasoning as ExternalHealthLimitModifiers in
    // PrestigeStatPatches.cs (a separate Harmony instance patching Agent.HealthLimit/
    // BaseHealthLimit broke pose rendering under game v1.4.8; this hook needs no Harmony patch at
    // all, it's a plain event, so that risk doesn't apply here - it's used purely for its
    // dependency-direction benefit).
    //
    // First non-null response wins (there's realistically only ever one subscriber - the perk
    // addon - so no merge logic is needed). If nothing subscribes, HeroGalleryGenerator falls
    // back to native/base values.
    public static class HeroStatSummaryHook
    {
        public static event Func<Hero, HeroStatSummary> Contributor;

        public static HeroStatSummary Describe(Hero hero)
        {
            if (Contributor == null) return null;
            foreach (Func<Hero, HeroStatSummary> handler in Contributor.GetInvocationList())
            {
                try
                {
                    var result = handler(hero);
                    if (result != null) return result;
                }
                catch
                {
                    // one bad subscriber shouldn't break the gallery for every hero
                }
            }
            return null;
        }
    }

    public class HeroStatSummary
    {
        public float MaxHP;
        public float DamageBonusPercent;
        public float ArmorBonus;
    }
}
