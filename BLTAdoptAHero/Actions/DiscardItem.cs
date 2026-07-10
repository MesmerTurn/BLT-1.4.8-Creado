using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=fmftNyHh}Discard Item"),
     LocDescription("{=f3LrrLHP}Allows viewers to discard one of their own custom items"),
     UsedImplicitly]
    public class DiscardItem : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            // var customItems = 
            //     BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero).ToList();
            //
            // if (!customItems.Any())
            // {
            //     ActionManager.SendReply(context, "{=oXQ4En4P}You have no items to discard".Translate());
            //     return;
            // }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, context.ArgsErrorMessage("{=by80aboy}(custom item index)".Translate()));
                return;
            }

            // Allow "!discard 6 force" to bypass the equipped-item guard below.
            var argParts = context.Args.Trim().Split(' ');
            bool force = argParts.Length > 1 && argParts[1].Equals("force", StringComparison.OrdinalIgnoreCase);

            (var element, string error) = BLTAdoptAHeroCampaignBehavior.Current.FindCustomItemByIndex(adoptedHero, argParts[0]);
            if (element.IsEqualTo(EquipmentElement.Invalid))
            {
                ActionManager.SendReply(context, error ?? "(unknown error)");
                return;
            }

            // Viewers kept losing gear they were actively wearing by discarding it thinking it
            // only cleared spare inventory - refuse unless they either unequip it first or
            // explicitly confirm with "force".
            bool isEquipped = adoptedHero.BattleEquipment.YieldFilledEquipmentSlots().Any(e => e.element.IsEqualTo(element))
                            || adoptedHero.CivilianEquipment.YieldFilledEquipmentSlots().Any(e => e.element.IsEqualTo(element));
            if (isEquipped && !force)
            {
                ActionManager.SendReply(context,
                    $"'{RewardHelpers.GetItemNameAndModifiers(element)}' is currently equipped - equip something else in that slot first, or use \"!discard {argParts[0]} force\" to discard it anyway.");
                return;
            }

            // Refund an eighth of whatever was actually spent creating this item (Smith/Enchant);
            // items that were never bought (tournament prizes etc) have no tracked cost, so 0 refund.
            int refund = BLTCustomItemsCampaignBehavior.Current.GetPurchaseCost(element.ItemModifier) / 8;

            BLTAdoptAHeroCampaignBehavior.Current.DiscardCustomItem(adoptedHero, element);

            if (refund > 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, refund);
                ActionManager.SendReply(context,
                    "{=bNqd3AzN}'{ItemName}' was discarded"
                        .Translate(
                            ("ItemName", RewardHelpers.GetItemNameAndModifiers(element))
                            )
                    + $" ({Naming.Inc}{refund}{Naming.Gold})");
            }
            else
            {
                ActionManager.SendReply(context,
                    "{=bNqd3AzN}'{ItemName}' was discarded"
                        .Translate(
                            ("ItemName", RewardHelpers.GetItemNameAndModifiers(element))
                            ));
            }
        }
    }
}