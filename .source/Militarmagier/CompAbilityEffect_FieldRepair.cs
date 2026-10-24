using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityFieldRepair : CompProperties_AbilityEffect
    {
        /// <summary>Hit points restored to a building, or injury severity healed on a mechanoid.</summary>
        public float amount = 40f;

        public CompProperties_AbilityFieldRepair()
        {
            compClass = typeof(CompAbilityEffect_FieldRepair);
        }
    }

    /// <summary>
    /// Field aid's mechanical counterpart: patches up a damaged building (turrets included) or the
    /// injuries of a mechanoid, constructs and DMS mechs alike. Hostile targets are refused.
    /// </summary>
    public class CompAbilityEffect_FieldRepair : CompAbilityEffect
    {
        public new CompProperties_AbilityFieldRepair Props => (CompProperties_AbilityFieldRepair)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Thing thing = target.Thing;
            if (thing == null || !thing.Spawned)
            {
                return;
            }

            if (thing is Pawn pawn)
            {
                HealMechanoid(pawn, Props.amount);
            }
            else
            {
                thing.HitPoints = Mathf.Min(thing.MaxHitPoints, thing.HitPoints + Mathf.RoundToInt(Props.amount));
                if (thing is Building building)
                {
                    thing.Map.listerBuildingsRepairable.Notify_BuildingRepaired(building);
                }
            }

            FleckMaker.ThrowMicroSparks(thing.DrawPos, thing.Map);
        }

        /// <summary>Spends <paramref name="amount"/> on the worst injuries first.</summary>
        private static void HealMechanoid(Pawn pawn, float amount)
        {
            List<Hediff_Injury> injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries);

            foreach (Hediff_Injury injury in injuries.OrderByDescending(h => h.Severity))
            {
                if (amount <= 0f)
                {
                    break;
                }
                float heal = Mathf.Min(amount, injury.Severity);
                injury.Heal(heal);
                amount -= heal;
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!NeedsRepair(target.Thing))
            {
                if (throwMessages)
                {
                    Messages.Message("DMST_NothingToRepair".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        private bool NeedsRepair(Thing thing)
        {
            if (thing == null || thing.HostileTo(parent.pawn))
            {
                return false;
            }

            if (thing is Pawn pawn)
            {
                if (!pawn.RaceProps.IsMechanoid)
                {
                    return false;
                }
                List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                pawn.health.hediffSet.GetHediffs(ref injuries);
                return injuries.Count > 0;
            }

            return thing is Building && thing.def.useHitPoints && thing.HitPoints < thing.MaxHitPoints;
        }
    }
}
