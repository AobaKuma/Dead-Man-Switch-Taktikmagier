using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityRemoveHediffs : CompProperties_AbilityEffect
    {
        public List<HediffDef> hediffDefs;

        public CompProperties_AbilityRemoveHediffs()
        {
            compClass = typeof(CompAbilityEffect_RemoveHediffs);
        }
    }

    /// <summary>
    /// Removes the listed hediffs from the caster. Makes the shooting stances exclusive: quick draw
    /// drops steady aim and steady aim drops quick draw.
    /// </summary>
    public class CompAbilityEffect_RemoveHediffs : CompAbilityEffect
    {
        public new CompProperties_AbilityRemoveHediffs Props => (CompProperties_AbilityRemoveHediffs)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.health == null || Props.hediffDefs.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < Props.hediffDefs.Count; i++)
            {
                Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(Props.hediffDefs[i]);
                if (hediff != null)
                {
                    caster.health.RemoveHediff(hediff);
                }
            }
        }
    }
}
