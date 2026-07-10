using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    public enum TreatyType { Truce, NAP, Alliance, Tribute }

    /// <summary>
    /// Base class for all treaties. Field names kept as Kingdom1Id/Kingdom2Id for
    /// save-compatibility, but they now resolve to any IFaction (Kingdom or a
    /// landed independent Clan).
    /// </summary>
    public abstract class BLTTreaty
    {
        public string Kingdom1Id { get; set; }
        public string Kingdom2Id { get; set; }
        public CampaignTime StartDate { get; set; }
        public abstract TreatyType Type { get; }

        protected BLTTreaty() { }
        protected BLTTreaty(IFaction f1, IFaction f2)
        {
            Kingdom1Id = f1?.StringId;
            Kingdom2Id = f2?.StringId;
            StartDate = CampaignTime.Now;
        }

        internal static IFaction ResolveFaction(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return (IFaction)Kingdom.All.FirstOrDefault(k => k.StringId == id)
                   ?? Clan.All.FirstOrDefault(c => c.StringId == id);
        }

        public IFaction GetFaction1() => ResolveFaction(Kingdom1Id);
        public IFaction GetFaction2() => ResolveFaction(Kingdom2Id);

        // Back-compat accessors for call sites that specifically need a Kingdom
        // (e.g. tribute gold transfer, which stays kingdom-only).
        public Kingdom GetKingdom1() => GetFaction1() as Kingdom;
        public Kingdom GetKingdom2() => GetFaction2() as Kingdom;

        public bool InvolvesBoth(IFaction f1, IFaction f2) =>
            (Kingdom1Id == f1?.StringId && Kingdom2Id == f2?.StringId) ||
            (Kingdom1Id == f2?.StringId && Kingdom2Id == f1?.StringId);

        public bool Involves(IFaction f) => Kingdom1Id == f?.StringId || Kingdom2Id == f?.StringId;

        public IFaction GetOtherFaction(IFaction f)
        {
            if (Kingdom1Id == f?.StringId) return GetFaction2();
            if (Kingdom2Id == f?.StringId) return GetFaction1();
            return null;
        }

        // Old signature kept working for existing Kingdom-only call sites.
        public Kingdom GetOtherKingdom(Kingdom k) => GetOtherFaction(k) as Kingdom;
    }

    public class BLTTruce : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Truce;
        public CampaignTime ExpirationDate { get; set; }

        public BLTTruce() { }
        public BLTTruce(IFaction f1, IFaction f2, int durationDays) : base(f1, f2)
        {
            ExpirationDate = CampaignTime.DaysFromNow(durationDays);
        }

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;
        public int DaysRemaining() => Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    public class BLTNAP : BLTTreaty
    {
        public override TreatyType Type => TreatyType.NAP;
        public BLTNAP() { }
        public BLTNAP(IFaction f1, IFaction f2) : base(f1, f2) { }
    }

    public class BLTAlliance : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Alliance;
        public BLTAlliance() { }
        public BLTAlliance(IFaction f1, IFaction f2) : base(f1, f2) { }
    }

    /// <summary>Tribute stays Kingdom-only — clan-level economy isn't part of this pass.</summary>
    public class BLTTribute : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Tribute;
        public string PayerKingdomId { get; set; }
        public int DailyAmount { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTTribute() { }
        public BLTTribute(Kingdom payer, Kingdom receiver, int dailyAmount, int durationDays) : base(payer, receiver)
        {
            PayerKingdomId = payer?.StringId;
            DailyAmount = dailyAmount;
            StartDate = CampaignTime.Now;
            ExpirationDate = StartDate + CampaignTime.Days(durationDays);
        }

        public Kingdom GetPayer() => Kingdom.All.FirstOrDefault(k => k.StringId == PayerKingdomId);
        public Kingdom GetReceiver() => GetOtherKingdom(GetPayer());
        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;
        public int DaysRemaining() => Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    public class BLTCTWProposal
    {
        public string ProposerKingdomId { get; set; }
        public string CalledKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTCTWProposal() { }
        public BLTCTWProposal(IFaction proposer, IFaction called, IFaction target, int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            CalledKingdomId = called?.StringId;
            TargetKingdomId = target?.StringId;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public IFaction GetProposer() => BLTTreaty.ResolveFaction(ProposerKingdomId);
        public IFaction GetCalled() => BLTTreaty.ResolveFaction(CalledKingdomId);
        public IFaction GetTarget() => BLTTreaty.ResolveFaction(TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;
        public int DaysRemaining() => Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// War tracking, generalized to IFaction so landed independent clans get the
    /// same enemy-list / ally-list bookkeeping kingdoms do.
    /// </summary>
    public class BLTWar
    {
        public string Attacker1Id { get; set; }
        public string Defender1Id { get; set; }
        public List<string> Attacker1AlliesIds { get; set; } = new List<string>();
        public List<string> Defender1AlliesIds { get; set; } = new List<string>();
        public CampaignTime StartDate { get; set; }

        public BLTWar() { }
        public BLTWar(IFaction attacker, IFaction defender)
        {
            Attacker1Id = attacker?.StringId;
            Defender1Id = defender?.StringId;
            StartDate = CampaignTime.Now;
        }

        public IFaction GetAttacker() => BLTTreaty.ResolveFaction(Attacker1Id);
        public IFaction GetDefender() => BLTTreaty.ResolveFaction(Defender1Id);

        public List<IFaction> GetAttackerAllies() => Attacker1AlliesIds
            .Select(BLTTreaty.ResolveFaction).Where(f => f != null).ToList();

        public List<IFaction> GetDefenderAllies() => Defender1AlliesIds
            .Select(BLTTreaty.ResolveFaction).Where(f => f != null).ToList();

        public bool IsMainParticipant(IFaction f) => f?.StringId == Attacker1Id || f?.StringId == Defender1Id;
        public bool IsAttackerSide(IFaction f) => f?.StringId == Attacker1Id || Attacker1AlliesIds.Contains(f?.StringId);
        public bool IsDefenderSide(IFaction f) => f?.StringId == Defender1Id || Defender1AlliesIds.Contains(f?.StringId);
        public bool Involves(IFaction f) => IsAttackerSide(f) || IsDefenderSide(f);

        public void AddAttackerAlly(IFaction f) { if (f != null && !Attacker1AlliesIds.Contains(f.StringId)) Attacker1AlliesIds.Add(f.StringId); }
        public void AddDefenderAlly(IFaction f) { if (f != null && !Defender1AlliesIds.Contains(f.StringId)) Defender1AlliesIds.Add(f.StringId); }
        public void RemoveAlly(IFaction f) { Attacker1AlliesIds.Remove(f?.StringId); Defender1AlliesIds.Remove(f?.StringId); }

        public List<IFaction> GetEnemies(IFaction f)
        {
            if (IsAttackerSide(f))
            {
                var enemies = new List<IFaction> { GetDefender() };
                enemies.AddRange(GetDefenderAllies());
                return enemies.Where(e => e != null).ToList();
            }
            if (IsDefenderSide(f))
            {
                var enemies = new List<IFaction> { GetAttacker() };
                enemies.AddRange(GetAttackerAllies());
                return enemies.Where(e => e != null).ToList();
            }
            return new List<IFaction>();
        }

        public IFaction GetMainOpponent(IFaction f)
        {
            if (f?.StringId == Attacker1Id) return GetDefender();
            if (f?.StringId == Defender1Id) return GetAttacker();
            if (IsAttackerSide(f)) return GetDefender();
            if (IsDefenderSide(f)) return GetAttacker();
            return null;
        }
    }
}