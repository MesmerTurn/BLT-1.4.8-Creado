using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Peace proposal - can be offered (proposer pays tribute) or demanded (proposer receives tribute).
    /// Generalized to IFaction: proposer/target may be a Kingdom, or a landed independent Clan.
    /// Custom tribute negotiation is only meaningful between two Kingdoms (see DailyTribute usage
    /// at call sites) — Clan-side peace simply carries DailyTribute == 0.
    /// </summary>
    public class BLTPeaceProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public bool IsOffer { get; set; }
        public int DailyTribute { get; set; }
        public int Duration { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTPeaceProposal() { }

        public BLTPeaceProposal(
            IFaction proposer,
            IFaction target,
            bool isOffer,
            int dailyTribute,
            int duration,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            IsOffer = isOffer;
            DailyTribute = dailyTribute;
            Duration = duration;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public IFaction GetProposer() => BLTTreaty.ResolveFaction(ProposerKingdomId);
        public IFaction GetTarget() => BLTTreaty.ResolveFaction(TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// Alliance proposal with costs that target must accept.
    /// </summary>
    public class BLTAllianceProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }
        public CampaignTime ExpirationDate { get; set; }
        public int BreakAllianceCost { get; set; }
        public int CTWCost { get; set; }

        public BLTAllianceProposal() { }

        public BLTAllianceProposal(
            IFaction proposer,
            IFaction target,
            int goldCost,
            int influenceCost,
            int daysToAccept,
            int breakAllianceCost,
            int ctwCost)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
            BreakAllianceCost = breakAllianceCost;
            CTWCost = ctwCost;
        }

        public IFaction GetProposer() => BLTTreaty.ResolveFaction(ProposerKingdomId);
        public IFaction GetTarget() => BLTTreaty.ResolveFaction(TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// Trade agreement proposal. Kept resolvable via IFaction for uniform storage/lookup,
    /// but in practice only ever created between two Kingdoms — the vanilla
    /// TradeAgreementsCampaignBehavior/TradeAgreementModel APIs are Kingdom-only,
    /// so Diplomacy.cs gates trade commands to Kingdom declarers before ever
    /// constructing one of these.
    /// </summary>
    public class BLTTradeProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTTradeProposal() { }

        public BLTTradeProposal(
            IFaction proposer,
            IFaction target,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public IFaction GetProposer() => BLTTreaty.ResolveFaction(ProposerKingdomId);
        public IFaction GetTarget() => BLTTreaty.ResolveFaction(TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// NAP proposal with costs that target must accept.
    /// </summary>
    public class BLTNAPProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTNAPProposal() { }

        public BLTNAPProposal(
            IFaction proposer,
            IFaction target,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public IFaction GetProposer() => BLTTreaty.ResolveFaction(ProposerKingdomId);
        public IFaction GetTarget() => BLTTreaty.ResolveFaction(TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }
}