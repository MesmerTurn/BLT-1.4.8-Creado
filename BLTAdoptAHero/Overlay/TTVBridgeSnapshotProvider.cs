using System.Linq;
using BannerlordTwitch;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.Powers;

namespace BLTAdoptAHero.Overlay
{
    /// <summary>
    /// Supplies BannerlordTTV's TTVBridgeService with a snapshot of adopted heroes:
    /// name, class, HP, gold, kills and currently-active powers.
    /// </summary>
    internal static class TTVBridgeSnapshotProvider
    {
        internal static void Register() => TTVBridgeRegistry.SnapshotProvider = BuildSnapshot;

        private static object BuildSnapshot()
        {
            var current = BLTAdoptAHeroCampaignBehavior.Current;
            if (current == null) return null;

            var heroes = BLTAdoptAHeroCampaignBehavior.GetAllAdoptedHeroes()
                .Select(hero =>
                {
                    var heroClass = hero.GetClass();
                    var activePowers = heroClass?.ActivePower?.GetUnlockedPowers(hero)
                        .Where(p => p.IsActive(hero))
                        .Select(p => (p as HeroPowerDefBase)?.Name.ToString())
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToArray() ?? new string[0];

                    return new
                    {
                        name = hero.Name?.ToString(),
                        className = heroClass?.Name.ToString(),
                        hp = hero.HitPoints,
                        maxHp = hero.MaxHitPoints,
                        gold = current.GetHeroGold(hero),
                        kills = current.GetAchievementTotalStat(hero, AchievementStatsData.Statistic.TotalKills),
                        activePowers,
                    };
                })
                .ToArray();

            return new { heroes };
        }
    }
}
