using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityHeal : CompProperties_AbilityEffect
    {
        public FloatRange tendQualityRange = new FloatRange(0.4f, 0.8f);

        public CompProperties_AbilityHeal()
        {
            compClass = typeof(CompAbilityEffect_Heal);
        }
    }

    /// <summary>
    /// Field-tends every tendable injury on the target in one go.
    /// </summary>
    public class CompAbilityEffect_Heal : CompAbilityEffect
    {
        public new CompProperties_AbilityHeal Props => (CompProperties_AbilityHeal)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = target.Pawn;
            if (pawn?.health == null)
            {
                return;
            }

            int tended = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if ((hediffs[i] is Hediff_Injury || hediffs[i] is Hediff_MissingPart) && hediffs[i].TendableNow())
                {
                    hediffs[i].Tended(Props.tendQualityRange.RandomInRange, Props.tendQualityRange.TrueMax, 1);
                    tended++;
                }
            }

            if (pawn.Map == null)
            {
                return;
            }

            if (tended > 0)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "NumWoundsTended".Translate(tended), 3.65f);
            }
            FleckMaker.AttachedOverlay(pawn, FleckDefOf.FlashHollow, Vector3.zero, 1.5f);
        }

        /// <summary>
        /// Vanilla only checks psychic sensitivity when the psycast lands, after the heat and psyfocus
        /// are already spent, and then shows "Immune". VPE refused such targets while aiming; keep that,
        /// so a psychically deaf target (a mechanoid, a zeroed-out pawn) cannot waste the cast.
        /// </summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                return false;
            }

            if (Props.psychic && pawn.GetStatValue(StatDefOf.PsychicSensitivity) < float.Epsilon)
            {
                if (throwMessages)
                {
                    Messages.Message("DMST_TargetPsychicallyDeaf".Translate(pawn.Named("PAWN")), pawn,
                        MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
