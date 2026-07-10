using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=tLSFX9Xc}Retinue"),
     LocDescription("{=bhC3VcmU}Add and improve adopted heroes retinue"),
     UsedImplicitly]
    public class Retinue : ActionHandlerBase
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=tLSFX9Xc}Retinue"),
             LocDescription("{=iNoFrKsN}Retinue Upgrade Settings"),
             PropertyOrder(1), ExpandableObject, Expand, UsedImplicitly]
            public BLTAdoptAHeroCampaignBehavior.RetinueSettings Retinue { get; set; } = new();

            [LocDisplayName("{=nIsuuFMC}All By Default"),
             LocDescription("{=mJSGvWlR}Whether this action should attempt to buy/upgrade as many times as possible when called with no parameter."),
             PropertyOrder(2), UsedImplicitly]
            public bool AllByDefault { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                Retinue.GenerateDocumentation(generator);
            }
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=mCcpMwrN}You cannot modify retinue while a mission is active!".Translate());
                return;
            }

            int numToUpgrade = settings.AllByDefault ? int.MaxValue : 1;

            if (!string.IsNullOrEmpty(context.Args))
            {
                var args = context.Args.Split(' ');

                // Handle !retinue clear <index>
                if (args.Length > 0 && string.Compare(args[0], "clear", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    // Refund a third of what was spent hiring/upgrading a retinue member up to
                    // its current Level (tier costs 0..Level-1), same idea as Discard's smith refund.
                    int TotalSpentForLevel(int level)
                    {
                        int total = 0;
                        for (int i = 0; i < level; i++) total += settings.Retinue.GetTierCost(i);
                        return total;
                    }

                    // Handle !secondary retinue clear all
                    if (args.Length > 1 && args[1].ToLower() == "all")
                    {
                        int count = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).Count();
                        int totalRefund = 0;
                        for (int i = 0; i < count; i++)
                        {
                            int level = BLTAdoptAHeroCampaignBehavior.Current.KillRetinueAtIndex(adoptedHero, 0);
                            if (level > 0) totalRefund += TotalSpentForLevel(level) / 3;
                        }
                        if (totalRefund > 0) BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, totalRefund);
                        onSuccess($"Cleared all retinue slots, refunded {totalRefund}{Naming.Gold}.");
                    }
                    else if (args.Length > 1 && int.TryParse(args[1], out int index))
                    {
                        int level = BLTAdoptAHeroCampaignBehavior.Current.KillRetinueAtIndex(adoptedHero, index - 1);
                        if (level <= 0)
                        {
                            onFailure("You must specify a valid retinue index to clear.");
                        }
                        else
                        {
                            int refund = TotalSpentForLevel(level) / 3;
                            if (refund > 0) BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, refund);
                            onSuccess($"Removed retinue at slot {index}, refunded {refund}{Naming.Gold}.");
                        }
                    }
                    else
                    {
                        onFailure("You must specify a valid retinue index to clear.");
                    }
                    return; // exit after clear
                }

                // Handle upgrades (!retinue all or !retinue <number>)
                if (string.Compare(args[0], "all", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    numToUpgrade = int.MaxValue;
                }
                else if (!int.TryParse(args[0], out numToUpgrade) || numToUpgrade <= 0)
                {
                    onFailure(context.ArgsErrorMessage("{=NexXxYvj}(number, or all)".Translate()));
                    return;
                }
            }

            // Perform upgrade
            (bool success, string status) = BLTAdoptAHeroCampaignBehavior.Current
                .UpgradeRetinue(adoptedHero, settings.Retinue, numToUpgrade);

            if (success)
                onSuccess(status);
            else
                onFailure(status);
        }   
    }
}
