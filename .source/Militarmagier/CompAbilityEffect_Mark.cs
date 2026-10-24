using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityMark : CompProperties_AbilityEffect
    {
        public float radius = 2.9f;

        /// <summary>Given to every hostile pawn in the radius; it carries its own duration.</summary>
        public HediffDef hediffDef;

        public CompProperties_AbilityMark()
        {
            compClass = typeof(CompAbilityEffect_Mark);
        }
    }

    /// <summary>
    /// Marks every hostile pawn around the target cell.
    ///
    /// Aimed at a cell rather than a pawn on purpose: vanilla's Verb.CanHitTargetFrom refuses a
    /// psychologically invisible hostile as a target, so a pawn-targeted mark could never be put on
    /// the cloaked magier it exists to counter. Marking an area catches them anyway.
    /// </summary>
    public class CompAbilityEffect_Mark : CompAbilityEffect
    {
        public new CompProperties_AbilityMark Props => (CompProperties_AbilityMark)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || Props.hediffDef == null)
            {
                return;
            }

            FleckMaker.Static(target.Cell, map, FleckDefOf.PsycastAreaEffect, Props.radius);

            List<Pawn> marked = GenRadial.RadialDistinctThingsAround(target.Cell, map, Props.radius, useCenter: true)
                .OfType<Pawn>()
                .Where(p => !p.Dead && p.HostileTo(caster))
                .ToList();

            for (int i = 0; i < marked.Count; i++)
            {
                // Re-marking restarts the clock rather than stacking a second mark.
                Hediff existing = marked[i].health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (existing != null)
                {
                    marked[i].health.RemoveHediff(existing);
                }
                marked[i].health.AddHediff(Props.hediffDef);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }

    public class HediffCompProperties_RevealInvisibility : HediffCompProperties
    {
        public HediffCompProperties_RevealInvisibility()
        {
            compClass = typeof(HediffComp_RevealInvisibility);
        }
    }

    /// <summary>
    /// Keeps the pawn visible for as long as the hediff lasts. Every invisibility comp on the pawn is
    /// disrupted each tick, the same thing taking damage does, so a marked pawn cannot cloak again
    /// until the mark runs out.
    /// </summary>
    public class HediffComp_RevealInvisibility : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                hediffs[i].TryGetComp<HediffComp_Invisibility>()?.DisruptInvisibility();
            }
        }
    }
}
