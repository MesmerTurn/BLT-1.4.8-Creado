using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("Prestige Hero"),
     LocDescription("Shows prestige status when requirements are not met. "
                    + "Resets hero to T1 and increases Prestige level when all requirements are met. "
                    + "Configure bonuses in Global Common Config > Prestige System."),
     UsedImplicitly]
    public class PrestigeHero : ActionHandlerBase
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("Allow Companion Prestige"),
             LocDescription("Allow player companions to prestige."),
             PropertyOrder(0), UsedImplicitly]
            public bool AllowCompanionPrestige { get; set; } = false;

            [LocDisplayName("Status Only"),
             LocDescription("If enabled, command only shows status — never actually prestiges. Useful for a separate '!prestigeinfo' command."),
             PropertyOrder(1), UsedImplicitly]
            public bool StatusOnly { get; set; } = false;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var cfg = BLTAdoptAHeroModule.CommonConfig.PrestigeConfig;
                generator.PropertyValuePair("Min Tier Required", $"T{cfg.MinTierRequired}");
                generator.PropertyValuePair("Required Kills", $"{cfg.RequireKills}");
                generator.PropertyValuePair("Requires Channel Points", $"{cfg.RequireChannelPoints}");
                if (cfg.RequireChannelPoints)
                    generator.PropertyValuePair("Channel Points Cost", $"{cfg.ChannelPointsCost}");
                generator.PropertyValuePair("Max Prestige Level", $"P{cfg.MaxPrestigeLevel}");
            }
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = config as Settings ?? new Settings();
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            var prestigeCfg = BLTAdoptAHeroModule.CommonConfig.PrestigeConfig;
            int currentTier    = BLTAdoptAHeroCampaignBehavior.Current.GetEquipmentTier(adoptedHero) + 1; // 1-based
            int currentPrestige = BLTAdoptAHeroCampaignBehavior.Current.GetPrestigeLevel(adoptedHero);
            int killCount      = BLTAdoptAHeroCampaignBehavior.Current.GetPrestigeKillCount(adoptedHero);
            string title       = prestigeCfg.GetChatTitle(currentPrestige);
            string titlePrefix = string.IsNullOrEmpty(title) ? "" : $"{title} ";

            // --- Always build a status string ---
            string status;
            bool canPrestige = true;
            string blocker = null;

            if (!settings.AllowCompanionPrestige && adoptedHero.IsPlayerCompanion)
            {
                status = $"{titlePrefix}P{currentPrestige} | T{currentTier} | Companions cannot prestige.";
                onSuccess(status);
                return;
            }

            if (currentPrestige >= prestigeCfg.MaxPrestigeLevel)
            {
                string bonuses = prestigeCfg.GetBonusSummary(currentPrestige);
                status = $"{titlePrefix}P{currentPrestige} MAX | Bonuses: {bonuses}";
                onSuccess(status);
                return;
            }

            // Check requirements
            bool tierOk  = currentTier >= prestigeCfg.MinTierRequired;
            bool killsOk = prestigeCfg.RequireKills <= 0 || killCount >= prestigeCfg.RequireKills;

            if (!tierOk)
            {
                canPrestige = false;
                blocker = $"Need T{prestigeCfg.MinTierRequired} (current T{currentTier})";
            }
            else if (!killsOk)
            {
                canPrestige = false;
                int needed = prestigeCfg.RequireKills - killCount;
                blocker = $"Need {needed} more kills ({killCount}/{prestigeCfg.RequireKills})";
            }

            int nextLevel = currentPrestige + 1;
            string nextBonuses = prestigeCfg.GetBonusSummary(nextLevel);

            if (!canPrestige || settings.StatusOnly)
            {
                string blockMsg = blocker != null ? $" | BLOCKED: {blocker}" : "";
                status = $"{titlePrefix}P{currentPrestige} → P{nextLevel} | T{currentTier}/{prestigeCfg.MinTierRequired} | Kills: {killCount}/{prestigeCfg.RequireKills}{blockMsg} | Next bonuses: {nextBonuses}";
                if (canPrestige && settings.StatusOnly)
                    status += " | Ready to prestige! Redeem channel points to confirm.";
                onSuccess(status);
                return;
            }

            // Channel points check — when used as free command and channel points required
            if (prestigeCfg.RequireChannelPoints && !context.IsSubscriber && !context.IsModerator && !context.IsBroadcaster)
            {
                status = $"{titlePrefix}P{currentPrestige} → P{nextLevel} | Ready! Redeem {prestigeCfg.ChannelPointsCost} Channel Points to prestige. | Next: {nextBonuses}";
                onSuccess(status);
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("Cannot prestige during an active battle!");
                return;
            }

            // Execute prestige
            bool success = BLTAdoptAHeroCampaignBehavior.Current.DoPrestige(adoptedHero);
            if (!success)
            {
                onFailure("Prestige failed — maximum level already reached!");
                return;
            }

            // Re-equip with T1 gear — custom items are kept via keepFilter
            var classDef = BLTAdoptAHeroCampaignBehavior.Current.GetEquipmentClass(adoptedHero);
            var customItems = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero);
            EquipHero.UpgradeEquipment(
                adoptedHero,
                targetTier: 0,          // T1
                classDef: classDef,
                replaceSameTier: true,
                customKeepFilter: element => customItems.Any(c => c.Item == element.Item),
                restrictedItemIds: BLTAdoptAHeroModule.CommonConfig.RestrictedItemIds
            );
            // DoPrestige() only marks EquipmentTier as -1 (a "needs reset" sentinel) - it never
            // persists the actual post-reset tier, so without this the hero would be stuck forever
            // re-equipping T1 (GetEquipmentTier() + 1 = 0) instead of progressing again after prestige.
            BLTAdoptAHeroCampaignBehavior.Current.SetEquipmentTier(adoptedHero, 0);

            int newPrestige = BLTAdoptAHeroCampaignBehavior.Current.GetPrestigeLevel(adoptedHero);
            string newTitle = prestigeCfg.GetChatTitle(newPrestige);
            string allBonuses = prestigeCfg.GetBonusSummary(newPrestige);

            onSuccess($"{newTitle} {context.UserName} prestiged to P{newPrestige}! Standard gear reset to T1, custom items kept. Total bonuses: {allBonuses}");
        }
    }
}
