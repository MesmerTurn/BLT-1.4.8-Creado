using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    // Shared logic for EnchantWeapon / EnchantArmor: applies a freshly rolled random ItemModifier
    // to a piece of gear the hero already has equipped (does NOT replace the base item, only
    // (re)rolls its modifier), so a viewer can gamble on improving gear they already own instead
    // of always getting brand new items from Smith.
    public abstract class EnchantItemBase : HeroActionHandlerBase
    {
        public class Settings
        {
            [LocDisplayName("{=}Item Power"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=}Enchant power multiplier, applies on top of the global multiplier"),
             Range(0, 5), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
             PropertyOrder(1), UsedImplicitly]
            public float ItemPower { get; set; } = 1f;

            [LocDisplayName("{=}Item Name"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=vqNeCCNy}Name format for custom item, {ITEMNAME} is the placeholder for the base item name"),
             PropertyOrder(2), UsedImplicitly]
            public string ItemName { get; set; } = "{=}Enchanted {ITEMNAME}";

            [LocDisplayName("{=HOZnxjGb}Gold Cost"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=OQISx7Jz}Gold cost to enchant"),
             PropertyOrder(3), UsedImplicitly]
            public int GoldCost { get; set; } = 500;
        }

        protected override Type ConfigType => typeof(Settings);

        protected abstract RewardHelpers.RewardType TargetType { get; }

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;

            int availableGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (availableGold < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost, availableGold));
                return;
            }

            var candidateSlots = TargetType == RewardHelpers.RewardType.Armor
                ? adoptedHero.BattleEquipment.YieldFilledArmorSlots().Select(e => e.index).ToList()
                : adoptedHero.BattleEquipment.YieldFilledWeaponSlots()
                    .Where(w => w.element.Item.ItemType != ItemObject.ItemTypeEnum.Shield)
                    .Select(w => w.index).ToList();

            if (!candidateSlots.Any())
            {
                onFailure(TargetType == RewardHelpers.RewardType.Armor
                    ? "{=}You have no armor equipped to enchant!".Translate()
                    : "{=}You have no weapon equipped to enchant!".Translate());
                return;
            }

            var slot = candidateSlots[MBRandom.RandomInt(candidateSlots.Count)];
            var item = adoptedHero.BattleEquipment[slot].Item;

            var modifier = BLTAdoptAHeroModule.CommonConfig.CustomRewardModifiers
                .Generate(item, settings.ItemName, settings.ItemPower);
            if (modifier == null)
            {
                onFailure("{=}Could not enchant that item!".Translate());
                return;
            }

            BLTCustomItemsCampaignBehavior.Current.SetPurchaseCost(modifier, settings.GoldCost);
            var element = new EquipmentElement(item, modifier);
            adoptedHero.BattleEquipment[slot] = element;
            BLTAdoptAHeroCampaignBehavior.Current.AddCustomItem(adoptedHero, element);

            int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost);
            onSuccess("{=}enchanted {ItemName}: {Modifiers}"
                .Translate(
                    ("ItemName", item.Name.ToString()),
                    ("Modifiers", RewardHelpers.GetModifiersDescription(modifier, item))));
            ActionManager.SendReply(context, $"{Naming.Dec}{settings.GoldCost}{Naming.Gold}{Naming.To}{newGold}{Naming.Gold}");
        }
    }

    [LocDisplayName("{=}Enchant Weapon"),
     LocDescription("{=}Rolls a new random modifier onto one of the hero's currently equipped weapons"),
     UsedImplicitly]
    public class EnchantWeapon : EnchantItemBase
    {
        protected override RewardHelpers.RewardType TargetType => RewardHelpers.RewardType.Weapon;
    }

    [LocDisplayName("{=}Enchant Armor"),
     LocDescription("{=}Rolls a new random modifier onto one of the hero's currently equipped armor pieces"),
     UsedImplicitly]
    public class EnchantArmor : EnchantItemBase
    {
        protected override RewardHelpers.RewardType TargetType => RewardHelpers.RewardType.Armor;
    }
}
