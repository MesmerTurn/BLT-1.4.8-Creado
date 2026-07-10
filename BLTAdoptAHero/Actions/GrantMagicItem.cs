using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("Grant Magic Item (TOR)"),
     LocDescription("Equips a random magical / artifact item from The Old Realms (items that carry special traits), filtered to the hero's culture and, for weapons, their class. Use one command with Kind=Weapon (!smithmagicweapon) and one with Kind=Armor (!smithmagicarmor)."),
     UsedImplicitly]
    public class GrantMagicItem : HeroActionHandlerBase
    {
        public enum MagicItemKind { Weapon, Armor }

        private class Settings
        {
            [LocDisplayName("Item Kind"),
             LocDescription("Whether to grant a magic weapon or a magic armour piece"),
             PropertyOrder(1), UsedImplicitly]
            public MagicItemKind Kind { get; set; } = MagicItemKind.Weapon;

            [LocDisplayName("Gold Cost"),
             LocDescription("Gold cost to receive the magic item"),
             PropertyOrder(2), UsedImplicitly]
            public int GoldCost { get; set; }

            [LocDisplayName("Restrict To Hero Culture"),
             LocDescription("Only grant items from the hero's own culture (falls back to any culture if none are found)"),
             PropertyOrder(3), UsedImplicitly]
            public bool RestrictToCulture { get; set; } = true;
        }

        protected override Type ConfigType => typeof(Settings);

        private static readonly ItemObject.ItemTypeEnum[] WeaponTypes =
        {
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon,
            ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Crossbow,
            ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.Shield
        };

        private static readonly ItemObject.ItemTypeEnum[] ArmorTypes =
        {
            ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape
        };

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;

            if (Mission.Current != null)
            {
                onFailure("{=RoIXssEg}You cannot upgrade equipment, as a mission is active!".Translate());
                return;
            }

            int availableGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (availableGold < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost, availableGold));
                return;
            }

            var wantedTypes = settings.Kind == MagicItemKind.Weapon ? WeaponTypes : ArmorTypes;

            // All TOR magic items (those carrying special traits) of the requested kind
            var pool = CampaignHelpers.AllItems
                .Where(i => i != null
                            && TorMagicItems.Ids.Contains(i.StringId)
                            && wantedTypes.Contains(i.ItemType))
                .ToList();

            if (pool.Count == 0)
            {
                onFailure("No magic items of that kind are available (is The Old Realms loaded?).");
                return;
            }

            // Culture filter (fall back to any culture if the hero's culture has none)
            List<ItemObject> candidates = pool;
            if (settings.RestrictToCulture && adoptedHero.Culture != null)
            {
                var byCulture = pool.Where(i => i.Culture == adoptedHero.Culture).ToList();
                if (byCulture.Count > 0) candidates = byCulture;
            }

            // For weapons: prefer items matching the hero's class weapon types; if none match, keep candidates (nearest available)
            if (settings.Kind == MagicItemKind.Weapon)
            {
                var classDef = adoptedHero.GetClass();
                if (classDef?.SlotItems != null)
                {
                    var byClass = candidates
                        .Where(i => classDef.SlotItems.Any(t => t != EquipmentType.None && i.IsEquipmentType(t)))
                        .ToList();
                    if (byClass.Count > 0) candidates = byClass;
                }
            }

            var item = candidates.SelectRandom();
            if (item == null)
            {
                onFailure("Could not select a magic item.");
                return;
            }

            var slot = GetSlotForItem(item);
            adoptedHero.BattleEquipment[slot] = new EquipmentElement(item);

            int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost, isSpending: true);
            onSuccess("{=BLT_MagicItemReceived}equipped {ItemName}"
                .Translate(("ItemName", item.Name?.ToString() ?? item.StringId)));
            if (settings.GoldCost > 0)
                ActionManager.SendReply(context, $"{Naming.Dec}{settings.GoldCost}{Naming.Gold}{Naming.To}{newGold}{Naming.Gold}");
        }

        private static EquipmentIndex GetSlotForItem(ItemObject item)
        {
            foreach (var (slot, itemType) in SkillGroup.ArmorIndexType)
            {
                if (itemType == item.ItemType)
                    return slot;
            }
            // Weapons and shields go into the primary weapon slot
            return EquipmentIndex.Weapon0;
        }
    }
}
