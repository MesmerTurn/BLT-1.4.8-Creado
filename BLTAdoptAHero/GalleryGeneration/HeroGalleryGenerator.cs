using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BannerlordTwitch.Documentation;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero.GalleryGeneration
{
    public static class HeroGalleryGenerator
    {
        // Same convention as DocumentationGenerator.DocumentationRootDir - a sibling folder
        // under the same Configs directory, not mixed into the documentation output.
        public static string GalleryRootDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord",
            "Configs", "BLT-hero-gallery");

        public static string GalleryPath => Path.Combine(GalleryRootDir, "gallery.html");

        private class ItemIcon
        {
            public string FileName;
            public string ItemName;
            public int Tier;
            public string StatSummary;
        }

        // Type-specific combat stat instead of gold value (2026-08-17) - value tells you nothing
        // about how strong an item actually is, and a flat "Dmg" number was wrong for shields
        // (which have no damage, only a defense rating) and lost real information for weapons
        // that have both a thrust and a swing attack (each with its own speed) or are couch-lance
        // capable. Mounts (horse/camel/dragon/chariot/mammoth/elephant all use the same
        // HorseComponent) show speed + HP. Armor pieces show their armor rating.
        private static string GetStatSummary(ItemObject item)
        {
            var w = item.PrimaryWeapon;
            if (w != null)
            {
                if (w.IsShield)
                {
                    return $"Def {w.BodyArmor}";
                }
                if (w.IsRangedWeapon)
                {
                    return $"Dmg {w.MissileDamage} &middot; Acc {w.Accuracy}";
                }

                bool isCouchable = w.ItemUsage?.Contains("couch") == true;
                var parts = new List<string>();
                if (w.ThrustDamage > 0)
                {
                    parts.Add(isCouchable
                        ? $"Couch {w.ThrustDamage}"
                        : $"Thrust {w.ThrustDamage}/{w.ThrustSpeed}");
                }
                if (w.SwingDamage > 0)
                {
                    parts.Add($"Swing {w.SwingDamage}/{w.SwingSpeed}");
                }
                return parts.Count > 0 ? string.Join(" &middot; ", parts) : $"T{(int)item.Tier}";
            }
            if (item.HorseComponent != null)
            {
                return $"Spd {item.HorseComponent.Speed:0} &middot; HP {item.HorseComponent.HitPoints}";
            }
            if (item.ArmorComponent != null)
            {
                int armor = item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor
                    + item.ArmorComponent.ArmArmor + item.ArmorComponent.LegArmor;
                return $"Armor {armor}";
            }
            return $"T{(int)item.Tier}";
        }

        private class HeroCard
        {
            public string Name;
            public string ClassName;
            public int Tier;
            public int Kills;
            public int Battles;
            public int Level;
            public float MaxHP;
            public float DamageBonusPercent;
            public float ArmorBonus;
            public string PortraitFileName;
            public List<ItemIcon> Items = new();

            // Clan/Kingdom/Family (2026-08-17) - shown in their own columns further out than the
            // item columns, so the card grows wider rather than taller.
            public string ClanName;
            public int ClanTier;
            public int ClanMembers;
            public List<string> FiefNames = new();
            public string KingdomName;
            public string SpouseName;
            public List<string> ChildrenNames = new();
        }

        // Renders every currently-adopted hero's portrait + equipped-item icons and writes
        // gallery.html. Must be awaited from a context where the game is fully loaded
        // (Campaign.Current != null) - callers are responsible for that check (see
        // BLTConfigureWindow.xaml.cs).
        public static async Task<int> GenerateAsync()
        {
            Directory.CreateDirectory(GalleryRootDir);

            var heroes = BLTAdoptAHeroCampaignBehavior.GetAllAdoptedHeroes().ToList();
            var cards = new List<HeroCard>();

            foreach (var hero in heroes)
            {
                string portraitFileName = $"portrait_{hero.StringId}.png";
                string portraitPath = Path.Combine(GalleryRootDir, portraitFileName);

                bool rendered = await CharacterPortraitRenderer.RenderAsync(
                    CharacterCode.CreateFrom(hero.CharacterObject),
                    portraitPath,
                    hero.Name?.ToString() ?? hero.StringId);

                if (!rendered)
                {
                    Log.Error($"HeroGalleryGenerator: skipping '{hero.Name}' - portrait render failed.");
                    continue;
                }

                var classDef = BLTAdoptAHeroCampaignBehavior.Current?.GetClass(hero);
                var summary = HeroStatSummaryHook.Describe(hero);

                var card = new HeroCard
                {
                    Name = hero.Name?.ToString() ?? hero.StringId,
                    ClassName = classDef?.Name.ToString() ?? "(no class)",
                    Tier = (BLTAdoptAHeroCampaignBehavior.Current?.GetEquipmentTier(hero) ?? 0) + 1,
                    Kills = BLTAdoptAHeroCampaignBehavior.Current?.GetTotalKills(hero) ?? 0,
                    Battles = BLTAdoptAHeroCampaignBehavior.Current?.GetTotalBattles(hero) ?? 0,
                    Level = hero.Level,
                    MaxHP = summary?.MaxHP ?? 0f,
                    DamageBonusPercent = summary?.DamageBonusPercent ?? 0f,
                    ArmorBonus = summary?.ArmorBonus ?? 0f,
                    PortraitFileName = portraitFileName,
                    ClanName = hero.Clan?.Name?.ToString(),
                    ClanTier = hero.Clan?.Tier ?? 0,
                    ClanMembers = hero.Clan?.Heroes?.Count ?? 0,
                    FiefNames = hero.Clan?.Fiefs?.Select(f => f.Name?.ToString() ?? f.StringId).ToList() ?? new List<string>(),
                    KingdomName = hero.Clan?.Kingdom?.Name?.ToString(),
                    SpouseName = hero.Spouse?.Name?.ToString(),
                    ChildrenNames = hero.Children?.Select(child => child.Name?.ToString() ?? child.StringId).ToList() ?? new List<string>(),
                };

                // "Po bokach itemy jakie ma i jaki koń/camel/smok" (2026-08-17): one small icon
                // per filled equipment slot - weapons, armor, shield, and the mount (Horse slot
                // covers regular horse/camel and the RoT dragon/chariot/mammoth/elephant items
                // equally, since EquipHero.cs puts all of those in the same slot).
                foreach (var slot in hero.BattleEquipment.YieldFilledEquipmentSlots())
                {
                    var item = slot.element.Item;
                    if (item == null) continue;

                    string itemFileName = $"item_{hero.StringId}_{slot.index}.png";
                    string itemPath = Path.Combine(GalleryRootDir, itemFileName);
                    bool itemRendered = await ItemPortraitRenderer.RenderAsync(item, itemPath,
                        $"{card.Name} - {slot.index}");

                    if (itemRendered)
                    {
                        card.Items.Add(new ItemIcon
                        {
                            FileName = itemFileName,
                            ItemName = item.Name?.ToString() ?? item.StringId,
                            Tier = (int)item.Tier,
                            StatSummary = GetStatSummary(item),
                        });
                    }
                }

                cards.Add(card);
            }

            WriteHtml(cards);
            return cards.Count;
        }

        private static void WriteHtml(List<HeroCard> cards)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
            sb.AppendLine("<title>BLT Hero Gallery</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { background:#1a1a1a; color:#eee; font-family: sans-serif; margin:0; padding:24px; }");
            sb.AppendLine("h1 { text-align:center; }");
            sb.AppendLine(".grid { display:flex; flex-wrap:wrap; gap:16px; justify-content:center; }");
            sb.AppendLine(".card { background:#262626; border-radius:8px; padding:14px; width:760px; text-align:center; }");
            sb.AppendLine(".main-row { display:flex; gap:8px; align-items:flex-start; justify-content:center; }");
            sb.AppendLine(".item-col { display:flex; flex-direction:column; gap:6px; width:96px; }");
            sb.AppendLine(".portrait { width:150px; border-radius:6px; background:#000; flex-shrink:0; }");
            sb.AppendLine(".item-slot { background:#1a1a1a; border:1px solid #444; border-radius:4px; padding:4px; }");
            sb.AppendLine(".item-slot img { width:100%; height:64px; object-fit:contain; background:#000; border-radius:3px; display:block; }");
            sb.AppendLine(".item-slot .item-stats { font-size:10px; color:#999; margin-top:2px; }");
            sb.AppendLine(".info-col { width:130px; text-align:left; background:#1a1a1a; border:1px solid #444; border-radius:6px; padding:10px; align-self:stretch; }");
            sb.AppendLine(".info-col h3 { font-size:12px; color:#ffd700; margin:0 0 8px; text-transform:uppercase; letter-spacing:.5px; }");
            sb.AppendLine(".info-col .row { font-size:12px; color:#ccc; margin-bottom:6px; }");
            sb.AppendLine(".info-col .row .label { color:#888; display:block; font-size:10px; }");
            sb.AppendLine(".card h2 { font-size:16px; margin:10px 0 2px; }");
            sb.AppendLine(".card .class { color:#ffd700; font-size:13px; margin-bottom:6px; }");
            sb.AppendLine(".card .stats { font-size:12px; color:#ccc; text-align:center; margin-top:8px; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>BLT Hero Gallery</h1>");
            sb.AppendLine("<div class=\"grid\">");

            foreach (var c in cards)
            {
                // Paperdoll-style layout: items split into a left and right column flanking the
                // portrait, each icon labelled with the item's name, tier, and gold value.
                var leftItems = c.Items.Where((_, idx) => idx % 2 == 0).ToList();
                var rightItems = c.Items.Where((_, idx) => idx % 2 == 1).ToList();

                sb.AppendLine("<div class=\"card\">");
                sb.AppendLine("<div class=\"main-row\">");

                // Outer-left: Clan. Kept as its own column further out than the item column, so
                // widening the card (not the height) is what accommodates it.
                sb.AppendLine("<div class=\"info-col\">");
                sb.AppendLine("<h3>Clan</h3>");
                if (!string.IsNullOrEmpty(c.ClanName))
                {
                    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Name</span>{c.ClanName}</div>");
                    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Tier</span>{c.ClanTier}</div>");
                    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Members</span>{c.ClanMembers}</div>");
                    sb.AppendLine("<div class=\"row\"><span class=\"label\">Fiefs</span>"
                        + (c.FiefNames.Count > 0 ? string.Join("<br/>", c.FiefNames) : "None") + "</div>");
                }
                else
                {
                    sb.AppendLine("<div class=\"row\">No clan</div>");
                }
                sb.AppendLine("</div>");

                sb.AppendLine("<div class=\"item-col\">");
                foreach (var item in leftItems)
                {
                    sb.AppendLine("<div class=\"item-slot\">");
                    sb.AppendLine($"<img src=\"{item.FileName}\" alt=\"{item.ItemName}\" title=\"{item.ItemName}\"/>");
                    sb.AppendLine($"<div class=\"item-stats\">{item.ItemName}<br/>T{item.Tier} &middot; {item.StatSummary}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
                sb.AppendLine($"<img class=\"portrait\" src=\"{c.PortraitFileName}\" alt=\"{c.Name}\"/>");
                sb.AppendLine("<div class=\"item-col\">");
                foreach (var item in rightItems)
                {
                    sb.AppendLine("<div class=\"item-slot\">");
                    sb.AppendLine($"<img src=\"{item.FileName}\" alt=\"{item.ItemName}\" title=\"{item.ItemName}\"/>");
                    sb.AppendLine($"<div class=\"item-stats\">{item.ItemName}<br/>T{item.Tier} &middot; {item.StatSummary}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");

                // Outer-right: Kingdom + Family.
                sb.AppendLine("<div class=\"info-col\">");
                sb.AppendLine("<h3>Kingdom &amp; Family</h3>");
                sb.AppendLine($"<div class=\"row\"><span class=\"label\">Kingdom</span>{(string.IsNullOrEmpty(c.KingdomName) ? "None" : c.KingdomName)}</div>");
                sb.AppendLine($"<div class=\"row\"><span class=\"label\">Spouse</span>{(string.IsNullOrEmpty(c.SpouseName) ? "None" : c.SpouseName)}</div>");
                sb.AppendLine("<div class=\"row\"><span class=\"label\">Children</span>"
                    + (c.ChildrenNames.Count > 0 ? string.Join("<br/>", c.ChildrenNames) : "None") + "</div>");
                sb.AppendLine("</div>");

                sb.AppendLine("</div>");
                sb.AppendLine($"<h2>{c.Name}</h2>");
                sb.AppendLine($"<div class=\"class\">{c.ClassName} - Tier {c.Tier}</div>");
                sb.AppendLine("<div class=\"stats\">");
                sb.AppendLine($"Level {c.Level} &middot; {c.Kills} kills &middot; {c.Battles} battles<br/>");
                if (c.MaxHP > 0)
                {
                    sb.AppendLine($"HP {c.MaxHP:0} &middot; Dmg +{c.DamageBonusPercent:0}% &middot; Armor +{c.ArmorBonus:0}");
                }
                sb.AppendLine("</div></div>");
            }

            sb.AppendLine("</div></body></html>");
            File.WriteAllText(GalleryPath, sb.ToString());
        }
    }
}
