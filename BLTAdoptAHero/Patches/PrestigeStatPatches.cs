using HarmonyLib;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Patches
{
    // HP: T8 double-HP + cumulative flat HP bonus from prestige
    [HarmonyPatch(typeof(Agent), "BaseHealthLimit", MethodType.Getter), UsedImplicitly]
    public static class PrestigeHealthPatch
    {
        [UsedImplicitly]
        public static void Postfix(Agent __instance, ref float __result)
        {
            if (__instance == null || !__instance.IsActive()) return;
            if (BLTAdoptAHeroModule.CommonConfig == null) return;   // not set outside Campaign game type (e.g. Custom Battle)
            var hero = (__instance.Character as CharacterObject)?.HeroObject;
            if (hero == null) return;

            int tier = BLTAdoptAHeroCampaignBehavior.Current?.GetEquipmentTier(hero) ?? -1;
            // tier index 7 == "Tier 8 (Legendary)"
            if (tier >= 7 && BLTAdoptAHeroModule.CommonConfig.EnableTier8)
                __result *= BLTAdoptAHeroModule.CommonConfig.Tier8HealthMultiplier;

            int prestige = BLTAdoptAHeroCampaignBehavior.Current?.GetPrestigeLevel(hero) ?? 0;
            if (prestige > 0 && BLTAdoptAHeroModule.CommonConfig.PrestigeConfig != null)
                __result += BLTAdoptAHeroModule.CommonConfig.PrestigeConfig.GetCumulativeHPBonus(prestige);
        }
    }

    // Damage: multiply blow damage by prestige damage bonus
    // Parameter is named "b" in TaleWorlds.MountAndBlade.Mission.RegisterBlow
    [HarmonyPatch(typeof(Mission), "RegisterBlow"), UsedImplicitly]
    public static class PrestigeDamagePatch
    {
        [UsedImplicitly]
        public static void Prefix(Agent attacker, ref Blow b)
        {
            if (attacker == null || !attacker.IsActive()) return;
            if (BLTAdoptAHeroModule.CommonConfig == null) return;   // not set outside Campaign game type (e.g. Custom Battle)
            var hero = (attacker.Character as CharacterObject)?.HeroObject;
            if (hero == null) return;

            float mult = 1f;

            // T7+ elite combat power: scale outgoing damage (tier index 6 == "Tier 7 (Elite)")
            int tier = BLTAdoptAHeroCampaignBehavior.Current?.GetEquipmentTier(hero) ?? -1;
            if (tier >= 6 && BLTAdoptAHeroModule.CommonConfig.EnableTier7)
                mult *= BLTAdoptAHeroModule.CommonConfig.Tier7PowerMultiplier;

            int prestige = BLTAdoptAHeroCampaignBehavior.Current?.GetPrestigeLevel(hero) ?? 0;
            int dmgBonus = prestige > 0 && BLTAdoptAHeroModule.CommonConfig.PrestigeConfig != null
                ? BLTAdoptAHeroModule.CommonConfig.PrestigeConfig.GetCumulativeDamageBonusPercent(prestige)
                : 0;
            if (dmgBonus > 0)
                mult *= 1f + dmgBonus / 100f;

            if (mult == 1f) return;
            b.BaseMagnitude *= mult;
            b.InflictedDamage = (int)(b.InflictedDamage * mult);
        }
    }

    // Armor: add flat armor bonus to adopted heroes
    [HarmonyPatch(typeof(Agent), "GetBaseArmorEffectivenessForBodyPart"), UsedImplicitly]
    public static class PrestigeArmorPatch
    {
        [UsedImplicitly]
        public static void Postfix(Agent __instance, ref float __result)
        {
            if (__instance == null || !__instance.IsActive()) return;
            if (BLTAdoptAHeroModule.CommonConfig == null) return;   // not set outside Campaign game type (e.g. Custom Battle)
            var hero = (__instance.Character as CharacterObject)?.HeroObject;
            if (hero == null) return;

            int prestige = BLTAdoptAHeroCampaignBehavior.Current?.GetPrestigeLevel(hero) ?? 0;
            if (prestige > 0 && BLTAdoptAHeroModule.CommonConfig.PrestigeConfig != null)
            {
                int armorBonus = BLTAdoptAHeroModule.CommonConfig.PrestigeConfig.GetCumulativeArmorBonus(prestige);
                if (armorBonus > 0)
                    __result += armorBonus;
            }

            // T7+ elite combat power: scale armor effectiveness (tier index 6 == "Tier 7 (Elite)")
            int tier = BLTAdoptAHeroCampaignBehavior.Current?.GetEquipmentTier(hero) ?? -1;
            if (tier >= 6 && BLTAdoptAHeroModule.CommonConfig.EnableTier7)
                __result *= BLTAdoptAHeroModule.CommonConfig.Tier7PowerMultiplier;
        }
    }
}
