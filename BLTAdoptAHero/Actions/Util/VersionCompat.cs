using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace BLTAdoptAHero.Actions.Util
{
    // Bannerlord's native API shifted between 1.3.15 and 1.4.x in a handful of places. Rather than
    // sprinkling #if BLT_1315 through every call site, the version-divergent calls are wrapped here
    // once each - callers just use these helpers and don't need to know which engine version they're on.
    public static class VersionCompat
    {
        public static bool HasTradeAgreementCompat(this TradeAgreementsCampaignBehavior tradeBehavior, Kingdom a, Kingdom b)
        {
#if BLT_1315
            return tradeBehavior.HasTradeAgreement(a, b);
#else
            return tradeBehavior.HasTradeAgreement(a, b, out _);
#endif
        }

        public static int WarPartyLimitCompat(this Clan clan)
        {
#if BLT_1315
            return clan.CommanderLimit;
#else
            return clan.WarPartyLimit;
#endif
        }

        public static IEnumerable<MobileParty> GetPartiesToCallToArmyCompat(this Campaign campaign, MobileParty leaderParty)
        {
#if BLT_1315
            return campaign.Models.ArmyManagementCalculationModel.GetMobilePartiesToCallToArmy(leaderParty);
#else
            campaign.Models.ArmyManagementCalculationModel.CanLordCreateArmy(leaderParty, out var members);
            return (IEnumerable<MobileParty>)members ?? new List<MobileParty>();
#endif
        }
    }
}
